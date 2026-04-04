using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Design2Ugui.Models;
using Newtonsoft.Json;

namespace Design2Ugui.Core
{
    public class UnitySemanticNormalizer
    {
        public NormalizationResult Normalize(string scanFilePath, string outputRoot = null)
        {
            if (string.IsNullOrWhiteSpace(scanFilePath))
            {
                throw new ArgumentException("Scan file path is required.", nameof(scanFilePath));
            }

            if (!File.Exists(scanFilePath))
            {
                throw new FileNotFoundException("Scan file not found.", scanFilePath);
            }

            var json = File.ReadAllText(scanFilePath);
            var document = JsonConvert.DeserializeObject<DesignSyncDocument>(json);
            if (document == null)
            {
                throw new InvalidOperationException("Failed to deserialize unity scan document.");
            }

            var resolvedOutputRoot = ResolveOutputRoot(outputRoot);
            var componentDirectory = Path.Combine(resolvedOutputRoot, "component-library");
            var screenDirectory = Path.Combine(resolvedOutputRoot, "screen-compositions");
            var reportDirectory = Path.Combine(resolvedOutputRoot, "reports");

            Directory.CreateDirectory(componentDirectory);
            Directory.CreateDirectory(screenDirectory);
            Directory.CreateDirectory(reportDirectory);

            var context = BuildContext(document);
            var components = BuildComponents(document, context);
            var screens = BuildScreens(document, context);
            var reports = BuildReports(document, components, screens, context);

            var componentBundle = new DesignSyncComponentBundle
            {
                version = document.version,
                project = document.project,
                components = components
            };
            var screenBundle = new DesignSyncScreenBundle
            {
                version = document.version,
                project = document.project,
                screens = screens
            };
            var auditBundle = new DesignSyncAuditBundle
            {
                version = document.version,
                project = document.project,
                reports = reports
            };

            var componentPath = Path.Combine(componentDirectory, "components.bundle.json");
            var screenPath = Path.Combine(screenDirectory, "screens.bundle.json");
            var reportPath = Path.Combine(reportDirectory, "audit-report.json");

            File.WriteAllText(componentPath, JsonConvert.SerializeObject(componentBundle, Formatting.Indented));
            File.WriteAllText(screenPath, JsonConvert.SerializeObject(screenBundle, Formatting.Indented));
            File.WriteAllText(reportPath, JsonConvert.SerializeObject(auditBundle, Formatting.Indented));

            return new NormalizationResult
            {
                ComponentBundlePath = componentPath,
                ScreenBundlePath = screenPath,
                AuditBundlePath = reportPath
            };
        }

        private static NormalizationContext BuildContext(DesignSyncDocument document)
        {
            var context = new NormalizationContext();

            foreach (var asset in document.assets)
            {
                if (asset.rootNode == null)
                {
                    continue;
                }

                context.AssetByKey[asset.assetKey] = asset;
                context.RootSignatureByAssetKey[asset.assetKey] = asset.rootNode.repeatSignature ?? string.Empty;
            }

            return context;
        }

        private static List<DesignSyncComponent> BuildComponents(DesignSyncDocument document, NormalizationContext context)
        {
            var components = new List<DesignSyncComponent>();

            foreach (var asset in document.assets.Where(IsComponentAsset))
            {
                if (asset.rootNode == null)
                {
                    continue;
                }

                var component = new DesignSyncComponent
                {
                    componentKey = asset.assetKey,
                    componentName = asset.rootNode.name,
                    sourceAssetKey = asset.assetKey,
                    semanticType = asset.rootNode.semanticType,
                    variant = ExtractVariant(asset.rootNode.name),
                    stateSet = BuildStateSet(asset.rootNode),
                    slots = BuildSlots(asset.rootNode),
                    templateNode = asset.rootNode,
                    styleKey = asset.rootNode.style?.styleKey ?? string.Empty,
                    usageCount = 0
                };

                components.Add(component);
                context.ComponentByKey[component.componentKey] = component;
                context.ComponentByName[component.componentName] = component.componentKey;
                context.ComponentBySignature[asset.rootNode.repeatSignature ?? string.Empty] = component.componentKey;
            }

            return components
                .OrderBy(component => component.componentName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<DesignSyncScreen> BuildScreens(DesignSyncDocument document, NormalizationContext context)
        {
            var screens = new List<DesignSyncScreen>();

            foreach (var asset in document.assets.Where(asset => asset.assetType == "screen"))
            {
                if (asset.rootNode == null)
                {
                    continue;
                }

                var screen = new DesignSyncScreen
                {
                    screenKey = asset.assetKey,
                    screenName = asset.rootNode.name,
                    rootFrameName = asset.rootNode.name,
                    navigationRole = "main",
                    legacyRiskLevel = asset.assetPath.IndexOf("/Legacy/", StringComparison.OrdinalIgnoreCase) >= 0 ? "high" : "low"
                };

                foreach (var child in asset.rootNode.children)
                {
                    screen.regions.Add(new DesignSyncRegion
                    {
                        regionKey = $"{screen.screenKey}.{NormalizeKey(child.name)}",
                        name = child.name,
                        semanticType = child.semanticType
                    });

                    CollectComponentInstances(child, screen, context, child.name);
                }

                screens.Add(screen);
            }

            foreach (var screen in screens)
            {
                screen.componentInstances = screen.componentInstances
                    .OrderBy(instance => instance.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return screens
                .OrderBy(screen => screen.screenName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void CollectComponentInstances(
            DesignSyncNode node,
            DesignSyncScreen screen,
            NormalizationContext context,
            string regionName)
        {
            if (node.isComponentCandidate && IsReferenceSemantic(node.semanticType))
            {
                var componentRef = ResolveComponentReference(node, context);
                if (string.IsNullOrWhiteSpace(componentRef))
                {
                    context.NormalizationIssues.Add(new DesignSyncIssue
                    {
                        severity = "warning",
                        code = "unresolved-component-ref",
                        message = $"Node '{node.name}' could not be resolved to a component.",
                        nodeId = node.id
                    });
                }
                else if (context.ComponentByKey.TryGetValue(componentRef, out var component))
                {
                    component.usageCount += 1;
                }

                screen.componentInstances.Add(new DesignSyncComponentInstance
                {
                    instanceKey = node.id,
                    componentRef = componentRef ?? string.Empty,
                    name = node.name,
                    regionKey = $"{screen.screenKey}.{NormalizeKey(regionName)}",
                    bounds = node.bounds
                });

                return;
            }

            foreach (var child in node.children)
            {
                CollectComponentInstances(child, screen, context, regionName);
            }
        }

        private static List<DesignSyncReport> BuildReports(
            DesignSyncDocument document,
            List<DesignSyncComponent> components,
            List<DesignSyncScreen> screens,
            NormalizationContext context)
        {
            var reports = new List<DesignSyncReport>();
            reports.AddRange(document.reports ?? new List<DesignSyncReport>());

            var unresolvedCount = context.NormalizationIssues.Count(issue => issue.code == "unresolved-component-ref");
            reports.Add(new DesignSyncReport
            {
                reportType = "normalization-summary",
                targetKey = "project",
                status = unresolvedCount > 0 ? "warning" : "pass",
                metrics = new DesignSyncMetrics
                {
                    assetCount = document.assets.Count,
                    componentCount = components.Count,
                    screenCount = screens.Count,
                    unnamedNodeCount = context.NormalizationIssues.Count(issue => issue.code == "default-name"),
                    issueCount = context.NormalizationIssues.Count
                },
                issues = context.NormalizationIssues
                    .OrderBy(issue => issue.code, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            });

            return reports;
        }

        private static bool IsComponentAsset(DesignSyncAsset asset)
        {
            return asset.assetType == "component" || asset.assetType == "pattern";
        }

        private static string ResolveComponentReference(DesignSyncNode node, NormalizationContext context)
        {
            if (!string.IsNullOrWhiteSpace(node.componentRef) && context.ComponentByKey.ContainsKey(node.componentRef))
            {
                return node.componentRef;
            }

            if (!string.IsNullOrWhiteSpace(node.name) && context.ComponentByName.TryGetValue(node.name, out var byName))
            {
                return byName;
            }

            if (!string.IsNullOrWhiteSpace(node.repeatSignature) &&
                context.ComponentBySignature.TryGetValue(node.repeatSignature, out var bySignature))
            {
                return bySignature;
            }

            return string.Empty;
        }

        private static bool IsReferenceSemantic(string semanticType)
        {
            return semanticType == "Button"
                   || semanticType == "IconButton"
                   || semanticType == "Toggle"
                   || semanticType == "InputField"
                   || semanticType == "Dialog"
                   || semanticType == "Card"
                   || semanticType == "Table"
                   || semanticType == "TableRow"
                   || semanticType == "ScrollView";
        }

        private static string ExtractVariant(string componentName)
        {
            if (string.IsNullOrWhiteSpace(componentName))
            {
                return string.Empty;
            }

            var semanticPrefixes = new[]
            {
                "Button", "IconButton", "Toggle", "InputField", "Dialog", "Card", "Table", "TableRow", "ScrollView"
            };

            foreach (var prefix in semanticPrefixes)
            {
                if (componentName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return componentName.Substring(prefix.Length);
                }
            }

            return string.Empty;
        }

        private static List<string> BuildStateSet(DesignSyncNode rootNode)
        {
            if (rootNode.semanticType == "Button" || rootNode.semanticType == "IconButton")
            {
                return new List<string> { "Default", "Hover", "Pressed", "Disabled" };
            }

            if (rootNode.semanticType == "Toggle")
            {
                return new List<string> { "Default", "Checked", "Disabled" };
            }

            if (rootNode.semanticType == "InputField")
            {
                return new List<string> { "Default", "Focused", "Disabled", "Error" };
            }

            return new List<string> { "Default" };
        }

        private static List<DesignSyncSlot> BuildSlots(DesignSyncNode rootNode)
        {
            var slots = new List<DesignSyncSlot>();
            var hasTextChild = rootNode.children.Any(child => child.semanticType == "Text");
            var hasImageChild = rootNode.children.Any(child => child.semanticType == "Image");

            if (hasTextChild)
            {
                slots.Add(new DesignSyncSlot
                {
                    slotKey = "label",
                    acceptedTypes = new List<string> { "Text" }
                });
            }

            if (hasImageChild)
            {
                slots.Add(new DesignSyncSlot
                {
                    slotKey = "icon",
                    acceptedTypes = new List<string> { "Image" }
                });
            }

            return slots;
        }

        private static string ResolveOutputRoot(string outputRoot)
        {
            var candidate = string.IsNullOrWhiteSpace(outputRoot)
                ? Path.Combine("Temp", "DesignSync")
                : outputRoot;
            return Path.GetFullPath(candidate);
        }

        private static string NormalizeKey(string value)
        {
            return string.Join("-", (value ?? string.Empty)
                .Split(new[] { ' ', '/', '\\', '.', '_' }, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant();
        }

        private sealed class NormalizationContext
        {
            public Dictionary<string, DesignSyncAsset> AssetByKey { get; } = new Dictionary<string, DesignSyncAsset>();
            public Dictionary<string, string> RootSignatureByAssetKey { get; } = new Dictionary<string, string>();
            public Dictionary<string, DesignSyncComponent> ComponentByKey { get; } = new Dictionary<string, DesignSyncComponent>();
            public Dictionary<string, string> ComponentByName { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> ComponentBySignature { get; } = new Dictionary<string, string>();
            public List<DesignSyncIssue> NormalizationIssues { get; } = new List<DesignSyncIssue>();
        }
    }

    public class NormalizationResult
    {
        public string ComponentBundlePath;
        public string ScreenBundlePath;
        public string AuditBundlePath;
    }
}
