using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Design2Ugui.Models;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UguiInputField = UnityEngine.UI.InputField;
using UguiText = UnityEngine.UI.Text;

namespace Design2Ugui.Core
{
    public class UnityScanExporter
    {
        private static readonly Regex DefaultNamePattern = new Regex(
            @"^(GameObject|New Game Object|Image(?: \(\d+\))?|Text(?: \(TMP\))?(?: \(\d+\))?|RawImage(?: \(\d+\))?|Button(?: \(\d+\))?|Toggle(?: \(\d+\))?)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        public string ExportScan(string inputRoot = "Assets/UI", string outputPath = null)
        {
            if (string.IsNullOrWhiteSpace(inputRoot))
            {
                throw new ArgumentException("Input root is required.", nameof(inputRoot));
            }

            if (!AssetDatabase.IsValidFolder(inputRoot))
            {
                throw new DirectoryNotFoundException($"Unity folder not found: {inputRoot}");
            }

            var resolvedOutputPath = ResolveOutputPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutputPath) ?? Directory.GetCurrentDirectory());

            var document = BuildDocument(inputRoot);
            var json = JsonConvert.SerializeObject(document, Formatting.Indented);
            File.WriteAllText(resolvedOutputPath, json);
            return resolvedOutputPath;
        }

        private DesignSyncDocument BuildDocument(string inputRoot)
        {
            var document = new DesignSyncDocument
            {
                project = new DesignSyncProject
                {
                    name = Application.productName,
                    unityVersion = Application.unityVersion,
                    exportedAt = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
                    sourceRoot = inputRoot
                }
            };

            var globalIssues = new List<DesignSyncIssue>();

            foreach (var prefabPath in AssetDatabase.FindAssets("t:Prefab", new[] { inputRoot })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var asset = BuildPrefabAsset(prefabPath, globalIssues);
                if (asset != null)
                {
                    document.assets.Add(asset);
                }
            }

            foreach (var scenePath in AssetDatabase.FindAssets("t:Scene", new[] { inputRoot })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var asset = BuildSceneAsset(scenePath, globalIssues);
                if (asset != null)
                {
                    document.assets.Add(asset);
                }
            }

            document.reports.Add(BuildSummaryReport(document.assets, globalIssues));
            return document;
        }

        private DesignSyncAsset BuildPrefabAsset(string prefabPath, IList<DesignSyncIssue> globalIssues)
        {
            GameObject prefab;
            try
            {
                prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            catch (Exception exception)
            {
                globalIssues.Add(new DesignSyncIssue
                {
                    severity = "warning",
                    code = "prefab-load-failed",
                    message = $"Failed to load prefab '{prefabPath}': {exception.Message}",
                    nodeId = BuildAssetKey(prefabPath, DetermineAssetType(prefabPath))
                });
                return null;
            }

            if (prefab == null)
            {
                globalIssues.Add(new DesignSyncIssue
                {
                    severity = "warning",
                    code = "prefab-load-null",
                    message = $"Prefab '{prefabPath}' could not be loaded and was skipped.",
                    nodeId = BuildAssetKey(prefabPath, DetermineAssetType(prefabPath))
                });
                return null;
            }

            try
            {
                var context = new ScanContext(prefabPath, true);
                var asset = CreateAssetShell(prefabPath, "prefab");
                asset.rootNode = ConvertNode(prefab, asset.assetKey, context, true);
                asset.dependencies = context.Dependencies
                    .OrderBy(item => item.kind, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.assetPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                asset.tags.AddRange(BuildTags(asset.assetType, asset.rootNode?.semanticType));
                foreach (var issue in context.Issues)
                {
                    globalIssues.Add(issue);
                }

                return asset;
            }
            finally
            {
                try
                {
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
                catch
                {
                    // Ignore unload failures for broken prefab contents that were partially loaded.
                }
            }
        }

        private DesignSyncAsset BuildSceneAsset(string scenePath, IList<DesignSyncIssue> globalIssues)
        {
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                var roots = scene.GetRootGameObjects()
                    .Where(IsUiRelevantRoot)
                    .OrderBy(go => go.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (roots.Count == 0)
                {
                    return null;
                }

                var sceneName = Path.GetFileNameWithoutExtension(scenePath);
                var context = new ScanContext(scenePath, false);
                var asset = CreateAssetShell(scenePath, "scene");
                asset.rootNode = new DesignSyncNode
                {
                    id = $"{asset.assetKey}.root",
                    name = BuildStableName(sceneName),
                    unityName = sceneName,
                    semanticType = "ScreenRoot",
                    semanticRole = "screen",
                    componentType = "Canvas"
                };

                foreach (var root in roots)
                {
                    asset.rootNode.children.Add(ConvertNode(root, asset.assetKey, context, true));
                }

                asset.dependencies = context.Dependencies
                    .OrderBy(item => item.kind, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.assetPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                asset.tags.AddRange(BuildTags(asset.assetType, asset.rootNode.semanticType));
                foreach (var issue in context.Issues)
                {
                    globalIssues.Add(issue);
                }

                return asset;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        private DesignSyncAsset CreateAssetShell(string assetPath, string sourceKind)
        {
            var assetType = DetermineAssetType(assetPath);
            return new DesignSyncAsset
            {
                assetKey = BuildAssetKey(assetPath, assetType),
                assetType = assetType,
                assetPath = assetPath,
                assetGuid = AssetDatabase.AssetPathToGUID(assetPath),
                sourceKind = sourceKind
            };
        }

        private DesignSyncNode ConvertNode(GameObject gameObject, string assetKey, ScanContext context, bool isRootNode)
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            var componentType = DetermineComponentType(gameObject);
            var semanticType = DetermineSemanticType(gameObject, componentType, isRootNode);
            var unityPath = GetHierarchyPath(gameObject.transform);
            var nodeId = $"{assetKey}.{NormalizeKey(unityPath)}";

            var node = new DesignSyncNode
            {
                id = nodeId,
                name = BuildStableName(gameObject.name),
                unityName = gameObject.name,
                semanticType = semanticType,
                semanticRole = DetermineSemanticRole(semanticType, isRootNode),
                componentType = componentType,
                componentRef = string.Empty,
                isPrefabRoot = context.IsPrefabAsset && isRootNode,
                isComponentCandidate = IsComponentCandidate(gameObject, semanticType, componentType, isRootNode),
                repeatSignature = BuildRepeatSignature(gameObject, semanticType, componentType),
                bounds = BuildBounds(rectTransform),
                layout = BuildLayout(gameObject),
                style = BuildStyle(gameObject, context),
                content = BuildContent(gameObject, context)
            };

            if (DefaultNamePattern.IsMatch(gameObject.name))
            {
                context.Issues.Add(new DesignSyncIssue
                {
                    severity = "warning",
                    code = "default-name",
                    message = $"Node '{gameObject.name}' uses a default Unity name.",
                    nodeId = nodeId
                });
            }

            for (var index = 0; index < gameObject.transform.childCount; index++)
            {
                var child = gameObject.transform.GetChild(index);
                if (!IsUiRelevantNode(child.gameObject))
                {
                    continue;
                }

                node.children.Add(ConvertNode(child.gameObject, assetKey, context, false));
            }

            return node;
        }

        private static bool IsUiRelevantRoot(GameObject gameObject)
        {
            return gameObject.GetComponent<Canvas>() != null
                   || gameObject.GetComponent<RectTransform>() != null
                   || gameObject.GetComponentsInChildren<RectTransform>(true).Length > 0;
        }

        private static bool IsUiRelevantNode(GameObject gameObject)
        {
            return gameObject.GetComponent<RectTransform>() != null
                   || gameObject.GetComponent<Graphic>() != null
                   || gameObject.GetComponent<LayoutGroup>() != null
                   || gameObject.GetComponent<TMP_InputField>() != null
                   || gameObject.GetComponent<TMP_Text>() != null;
        }

        private static string DetermineAssetType(string assetPath)
        {
            if (assetPath.IndexOf("/Components/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "component";
            }

            if (assetPath.IndexOf("/Patterns/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "pattern";
            }

            if (assetPath.IndexOf("/Screens/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "screen";
            }

            if (assetPath.IndexOf("/Legacy/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "legacy";
            }

            return "screen";
        }

        private static string BuildAssetKey(string assetPath, string assetType)
        {
            var stem = Path.ChangeExtension(assetPath, null)?.Replace('\\', '/') ?? assetPath.Replace('\\', '/');
            if (stem.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                stem = stem.Substring("Assets/".Length);
            }

            return $"{assetType}.{NormalizeKey(stem)}";
        }

        private static IEnumerable<string> BuildTags(string assetType, string semanticType)
        {
            yield return "ui";
            yield return assetType;
            if (!string.IsNullOrWhiteSpace(semanticType))
            {
                yield return semanticType.ToLowerInvariant();
            }
        }

        private static string DetermineComponentType(GameObject gameObject)
        {
            if (gameObject.GetComponent<TMP_InputField>() != null)
            {
                return "InputField";
            }

            if (gameObject.GetComponent<UguiInputField>() != null)
            {
                return "InputField";
            }

            if (gameObject.GetComponent<Toggle>() != null)
            {
                return "Toggle";
            }

            if (gameObject.GetComponent<Button>() != null)
            {
                return "Button";
            }

            if (gameObject.GetComponent<ScrollRect>() != null)
            {
                return "ScrollView";
            }

            if (gameObject.GetComponent<VerticalLayoutGroup>() != null)
            {
                return "VerticalLayout";
            }

            if (gameObject.GetComponent<HorizontalLayoutGroup>() != null)
            {
                return "HorizontalLayout";
            }

            if (gameObject.GetComponent<GridLayoutGroup>() != null)
            {
                return "GridLayout";
            }

            if (gameObject.GetComponent<TMP_Text>() != null)
            {
                return "Text";
            }

            if (gameObject.GetComponent<UguiText>() != null)
            {
                return "Text";
            }

            if (gameObject.GetComponent<Image>() != null || gameObject.GetComponent<RawImage>() != null)
            {
                return "Image";
            }

            if (gameObject.GetComponent<Canvas>() != null)
            {
                return "Canvas";
            }

            return "Panel";
        }

        private static string DetermineSemanticType(GameObject gameObject, string componentType, bool isRootNode)
        {
            var normalizedName = gameObject.name.Replace(" ", string.Empty);
            if (isRootNode && normalizedName.IndexOf("Screen", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "ScreenRoot";
            }

            if (componentType == "Button")
            {
                return normalizedName.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0 ? "IconButton" : "Button";
            }

            if (componentType == "Toggle")
            {
                return "Toggle";
            }

            if (componentType == "InputField")
            {
                return "InputField";
            }

            if (componentType == "ScrollView")
            {
                return normalizedName.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0 ? "Table" : "ScrollView";
            }

            if (normalizedName.IndexOf("Dialog", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedName.IndexOf("Modal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Dialog";
            }

            if (normalizedName.IndexOf("Card", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Card";
            }

            if (normalizedName.IndexOf("Row", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "TableRow";
            }

            if (normalizedName.IndexOf("Cell", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "TableCell";
            }

            return componentType switch
            {
                "Text" => "Text",
                "Image" => "Image",
                _ => "Container"
            };
        }

        private static string DetermineSemanticRole(string semanticType, bool isRootNode)
        {
            if (isRootNode)
            {
                return "root";
            }

            return semanticType switch
            {
                "Dialog" => "overlay",
                "Button" => "action",
                "IconButton" => "action",
                "Toggle" => "input",
                "InputField" => "input",
                "Table" => "content",
                "TableRow" => "item",
                "TableCell" => "item",
                _ => "content"
            };
        }

        private static bool IsComponentCandidate(GameObject gameObject, string semanticType, string componentType, bool isRootNode)
        {
            if (PrefabUtility.GetPrefabAssetType(gameObject) != PrefabAssetType.NotAPrefab
                || PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject))
            {
                return true;
            }

            if (isRootNode)
            {
                return componentType != "Canvas";
            }

            return semanticType == "Button"
                   || semanticType == "IconButton"
                   || semanticType == "Toggle"
                   || semanticType == "InputField"
                   || semanticType == "Dialog"
                   || semanticType == "Card"
                   || semanticType == "TableRow";
        }

        private static string BuildRepeatSignature(GameObject gameObject, string semanticType, string componentType)
        {
            var childTypes = new List<string>();
            for (var index = 0; index < gameObject.transform.childCount; index++)
            {
                childTypes.Add(DetermineComponentType(gameObject.transform.GetChild(index).gameObject));
            }

            return $"{semanticType}|{componentType}|{string.Join(",", childTypes)}";
        }

        private static DesignSyncBounds BuildBounds(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return new DesignSyncBounds();
            }

            return new DesignSyncBounds
            {
                x = rectTransform.anchoredPosition.x,
                y = rectTransform.anchoredPosition.y,
                width = rectTransform.rect.width,
                height = rectTransform.rect.height,
                anchorMin = new DesignSyncVector2(rectTransform.anchorMin.x, rectTransform.anchorMin.y),
                anchorMax = new DesignSyncVector2(rectTransform.anchorMax.x, rectTransform.anchorMax.y),
                pivot = new DesignSyncVector2(rectTransform.pivot.x, rectTransform.pivot.y)
            };
        }

        private static DesignSyncLayout BuildLayout(GameObject gameObject)
        {
            var layout = new DesignSyncLayout();

            if (gameObject.TryGetComponent<VerticalLayoutGroup>(out var verticalLayout))
            {
                layout.mode = "vertical";
                PopulateLayoutGroup(layout, verticalLayout);
            }
            else if (gameObject.TryGetComponent<HorizontalLayoutGroup>(out var horizontalLayout))
            {
                layout.mode = "horizontal";
                PopulateLayoutGroup(layout, horizontalLayout);
            }
            else if (gameObject.TryGetComponent<GridLayoutGroup>(out var gridLayout))
            {
                layout.mode = "grid";
                layout.spacing = gridLayout.spacing.x;
                layout.padding.left = gridLayout.padding.left;
                layout.padding.right = gridLayout.padding.right;
                layout.padding.top = gridLayout.padding.top;
                layout.padding.bottom = gridLayout.padding.bottom;
            }

            if (gameObject.TryGetComponent<ScrollRect>(out var scrollRect))
            {
                layout.scrollAxis = scrollRect.vertical && !scrollRect.horizontal ? "vertical" :
                    scrollRect.horizontal && !scrollRect.vertical ? "horizontal" :
                    scrollRect.horizontal || scrollRect.vertical ? "both" : string.Empty;
            }

            return layout;
        }

        private static void PopulateLayoutGroup(DesignSyncLayout layout, HorizontalOrVerticalLayoutGroup group)
        {
            layout.spacing = group.spacing;
            layout.padding.left = group.padding.left;
            layout.padding.right = group.padding.right;
            layout.padding.top = group.padding.top;
            layout.padding.bottom = group.padding.bottom;
            layout.childControlWidth = group.childControlWidth;
            layout.childControlHeight = group.childControlHeight;
            layout.childForceExpandWidth = group.childForceExpandWidth;
            layout.childForceExpandHeight = group.childForceExpandHeight;
            layout.alignment = group.childAlignment.ToString();
        }

        private static DesignSyncStyle BuildStyle(GameObject gameObject, ScanContext context)
        {
            var style = new DesignSyncStyle();

            if (gameObject.TryGetComponent<Graphic>(out var graphic))
            {
                style.fallbacks["fill"] = ColorUtility.ToHtmlStringRGBA(graphic.color);
                RegisterDependency(context, "material", graphic.material);
            }

            if (gameObject.TryGetComponent<Image>(out var image))
            {
                style.spriteRef = RegisterDependency(context, "sprite", image.sprite);
                RegisterDependency(context, "material", image.material);
            }
            else if (gameObject.TryGetComponent<RawImage>(out var rawImage))
            {
                RegisterDependency(context, "texture", rawImage.texture);
                RegisterDependency(context, "material", rawImage.material);
            }

            if (gameObject.TryGetComponent<TMP_Text>(out var text))
            {
                style.fontRef = RegisterDependency(context, "font", text.font);
                style.fallbacks["textColor"] = ColorUtility.ToHtmlStringRGBA(text.color);
                style.fallbacks["fontSize"] = text.fontSize.ToString(CultureInfo.InvariantCulture);
                RegisterDependency(context, "material", text.fontMaterial);
            }
            else if (gameObject.TryGetComponent<UguiText>(out var uguiText))
            {
                style.fontRef = RegisterDependency(context, "font", uguiText.font);
                style.fallbacks["textColor"] = ColorUtility.ToHtmlStringRGBA(uguiText.color);
                style.fallbacks["fontSize"] = uguiText.fontSize.ToString(CultureInfo.InvariantCulture);
                style.fallbacks["fontStyle"] = uguiText.fontStyle.ToString();
                RegisterDependency(context, "material", uguiText.material);
            }

            return style;
        }

        private static DesignSyncContent BuildContent(GameObject gameObject, ScanContext context)
        {
            var content = new DesignSyncContent();

            if (gameObject.TryGetComponent<TMP_Text>(out var text))
            {
                content.text = text.text ?? string.Empty;
                content.value = text.text ?? string.Empty;
            }
            else if (gameObject.TryGetComponent<UguiText>(out var uguiText))
            {
                content.text = uguiText.text ?? string.Empty;
                content.value = uguiText.text ?? string.Empty;
            }

            if (gameObject.TryGetComponent<TMP_InputField>(out var inputField))
            {
                content.placeholder = (inputField.placeholder as TMP_Text)?.text ?? string.Empty;
                content.value = inputField.text ?? string.Empty;
                RegisterDependency(context, "font", inputField.textComponent?.font);
            }
            else if (gameObject.TryGetComponent<UguiInputField>(out var uguiInputField))
            {
                content.placeholder = (uguiInputField.placeholder as UguiText)?.text ?? string.Empty;
                content.value = uguiInputField.text ?? string.Empty;
                RegisterDependency(context, "font", uguiInputField.textComponent?.font);
            }

            if (gameObject.TryGetComponent<Image>(out var image))
            {
                content.imageRef = RegisterDependency(context, "sprite", image.sprite);
            }

            return content;
        }

        private static string RegisterDependency(ScanContext context, string kind, UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            context.Dependencies.Add(new DesignSyncDependency
            {
                kind = kind,
                assetPath = assetPath
            });
            return assetPath;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var parts = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }

        private static string BuildStableName(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "Node";
            }

            var parts = Regex.Split(source, @"[^A-Za-z0-9]+")
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1))
                .ToArray();

            return parts.Length == 0 ? "Node" : string.Concat(parts);
        }

        private static string NormalizeKey(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "node";
            }

            var normalized = source.Replace('\\', '/');
            normalized = Regex.Replace(normalized, @"[^A-Za-z0-9/]+", "-");
            normalized = normalized.Replace("/", ".");
            normalized = Regex.Replace(normalized, @"-+", "-").Trim('-', '.');
            return normalized.ToLowerInvariant();
        }

        private static string ResolveOutputPath(string outputPath)
        {
            var candidate = string.IsNullOrWhiteSpace(outputPath)
                ? Path.Combine("Temp", "DesignSync", "unity-scan", "unity-scan.json")
                : outputPath;

            return Path.GetFullPath(candidate);
        }

        private static DesignSyncReport BuildSummaryReport(IEnumerable<DesignSyncAsset> assets, IList<DesignSyncIssue> issues)
        {
            var assetList = assets.ToList();
            return new DesignSyncReport
            {
                reportType = "scan-summary",
                targetKey = "project",
                status = issues.Any(issue => issue.severity == "error") ? "fail" : issues.Count > 0 ? "warning" : "pass",
                metrics = new DesignSyncMetrics
                {
                    assetCount = assetList.Count,
                    componentCount = assetList.Count(asset => asset.assetType == "component" || asset.assetType == "pattern"),
                    screenCount = assetList.Count(asset => asset.assetType == "screen"),
                    unnamedNodeCount = issues.Count(issue => issue.code == "default-name"),
                    issueCount = issues.Count
                },
                issues = issues.ToList()
            };
        }

        private sealed class ScanContext
        {
            public ScanContext(string assetPath, bool isPrefabAsset)
            {
                AssetPath = assetPath;
                IsPrefabAsset = isPrefabAsset;
            }

            public string AssetPath { get; }

            public bool IsPrefabAsset { get; }

            public HashSet<DesignSyncDependency> Dependencies { get; } = new HashSet<DesignSyncDependency>(new DependencyComparer());

            public List<DesignSyncIssue> Issues { get; } = new List<DesignSyncIssue>();
        }

        private sealed class DependencyComparer : IEqualityComparer<DesignSyncDependency>
        {
            public bool Equals(DesignSyncDependency x, DesignSyncDependency y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                return string.Equals(x.kind, y.kind, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(x.assetPath, y.assetPath, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(DesignSyncDependency obj)
            {
                unchecked
                {
                    var hash = 17;
                    hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(obj.kind ?? string.Empty);
                    hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(obj.assetPath ?? string.Empty);
                    return hash;
                }
            }
        }
    }
}
