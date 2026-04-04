using System.Collections.Generic;
using System.Text.RegularExpressions;
using Design2Ugui.Models;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Design2Ugui.Core
{
    public class UguiConverter
    {
        private readonly Sprite whiteSprite;

        public UguiConverter()
        {
            whiteSprite = CreateWhiteSprite();
        }

        private Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.zero);
        }

        public GameObject Convert(UguiNode node, Transform parent = null, IDictionary<string, GameObject> prefabMap = null)
        {
            GameObject go;
            if (node.componentType == UguiComponentType.PrefabInstance
                && prefabMap != null
                && !string.IsNullOrEmpty(node.prefabKey)
                && prefabMap.TryGetValue(node.prefabKey, out var prefab))
            {
                go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                go.name = SanitizeGameObjectName(node.name);
            }
            else
            {
                go = new GameObject(SanitizeGameObjectName(node.name));
            }

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = go.AddComponent<RectTransform>();
            }

            ApplyRectTransform(rectTransform, node.rectTransform);

            var childParent = go.transform;
            var scrollContentParent = default(Transform);
            switch (node.componentType)
            {
                case UguiComponentType.PrefabInstance:
                    return go;
                case UguiComponentType.Canvas:
                    go.AddComponent<Canvas>();
                    go.AddComponent<CanvasScaler>();
                    go.AddComponent<GraphicRaycaster>();
                    break;
                case UguiComponentType.Image:
                    AddImage(go, node.componentData);
                    break;
                case UguiComponentType.Text:
                    var text = go.AddComponent<TextMeshProUGUI>();
                    text.text = node.componentData.text;
                    text.fontSize = node.componentData.fontSize;
                    text.color = node.componentData.color;
                    text.alignment = node.componentData.textAlign;
                    text.characterSpacing = node.componentData.letterSpacing;
                    break;
                case UguiComponentType.Panel:
                    AddOptionalBackground(go, node.componentData);
                    ApplyLayout(go, node.layout);
                    break;
                case UguiComponentType.ScrollView:
                    AddOptionalBackground(go, node.componentData);
                    scrollContentParent = AddScrollView(go, node.layout);
                    childParent = scrollContentParent;
                    break;
                case UguiComponentType.Button:
                    var buttonImage = AddOptionalBackground(go, node.componentData);
                    var button = go.AddComponent<Button>();
                    if (buttonImage != null)
                    {
                        button.targetGraphic = buttonImage;
                    }

                    ApplyLayout(go, node.layout);
                    break;
                case UguiComponentType.Toggle:
                    var toggleImage = AddOptionalBackground(go, node.componentData);
                    var toggle = go.AddComponent<Toggle>();
                    if (toggleImage != null)
                    {
                        toggle.targetGraphic = toggleImage;
                    }

                    ApplyLayout(go, node.layout);
                    break;
                case UguiComponentType.InputField:
                    AddOptionalBackground(go, node.componentData);
                    ApplyLayout(go, node.layout);
                    break;
                case UguiComponentType.HorizontalLayout:
                case UguiComponentType.VerticalLayout:
                    AddOptionalBackground(go, node.componentData);
                    ApplyLayout(go, node.layout);
                    break;
            }

            foreach (var child in node.children)
            {
                Convert(child, childParent, prefabMap);
            }

            if (node.componentType == UguiComponentType.ScrollView
                && node.children.Count == 0
                && scrollContentParent != null
                && prefabMap != null
                && !string.IsNullOrEmpty(node.itemTemplateKey)
                && prefabMap.TryGetValue(node.itemTemplateKey, out var itemPrefab))
            {
                var sampleItem = PrefabUtility.InstantiatePrefab(itemPrefab) as GameObject;
                if (sampleItem != null)
                {
                    sampleItem.name = "SampleItem";
                    sampleItem.transform.SetParent(scrollContentParent, false);
                }
            }

            PostProcess(go, node);

            return go;
        }

        private void ApplyRectTransform(RectTransform rt, RectTransformData data)
        {
            rt.anchorMin = data.anchorMin;
            rt.anchorMax = data.anchorMax;
            rt.pivot = data.pivot;
            rt.anchoredPosition = data.anchoredPosition;
            rt.sizeDelta = data.sizeDelta;
        }

        private Image AddImage(GameObject go, ComponentData data)
        {
            var image = go.AddComponent<Image>();
            image.sprite = data.sprite ?? whiteSprite;
            image.color = data.color;
            image.type = Image.Type.Simple;
            return image;
        }

        private Image AddOptionalBackground(GameObject go, ComponentData data)
        {
            if (!HasVisibleGraphic(data))
            {
                return null;
            }

            return AddImage(go, data);
        }

        private bool HasVisibleGraphic(ComponentData data)
        {
            return data.sprite != null
                || !string.IsNullOrEmpty(data.imageRef)
                || data.color.a > 0.001f;
        }

        private void ApplyLayout(GameObject go, LayoutData layout)
        {
            if (layout == null || string.IsNullOrEmpty(layout.direction))
            {
                return;
            }

            HorizontalOrVerticalLayoutGroup group = layout.direction == "vertical"
                ? go.AddComponent<VerticalLayoutGroup>()
                : go.AddComponent<HorizontalLayoutGroup>();

            group.spacing = layout.gap;
            group.padding = new RectOffset(
                layout.padding?.left ?? 0,
                layout.padding?.right ?? 0,
                layout.padding?.top ?? 0,
                layout.padding?.bottom ?? 0
            );
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = ResolveAlignment(layout.justifyContent, layout.alignItems);

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private TextAnchor ResolveAlignment(string justifyContent, string alignItems)
        {
            bool centerX = alignItems == "center";
            bool endX = alignItems == "end";
            bool centerY = justifyContent == "center";
            bool endY = justifyContent == "end";

            if (centerX && centerY) return TextAnchor.MiddleCenter;
            if (endX && centerY) return TextAnchor.MiddleRight;
            if (centerX && endY) return TextAnchor.LowerCenter;
            if (endX && endY) return TextAnchor.LowerRight;
            if (centerX) return TextAnchor.UpperCenter;
            if (endX) return TextAnchor.UpperRight;
            if (centerY) return TextAnchor.MiddleLeft;
            if (endY) return TextAnchor.LowerLeft;
            return TextAnchor.UpperLeft;
        }

        private Transform AddScrollView(GameObject go, LayoutData contentLayout)
        {
            var scrollRect = go.AddComponent<ScrollRect>();

            var viewport = new GameObject("Viewport");
            viewport.name = SanitizeGameObjectName(viewport.name);
            viewport.transform.SetParent(go.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewport.AddComponent<Image>();
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.name = SanitizeGameObjectName(content.name);
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);

            ApplyLayout(content, contentLayout);

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            return content.transform;
        }

        private void PostProcess(GameObject go, UguiNode node)
        {
            if (node.componentType == UguiComponentType.Toggle)
            {
                ConfigureToggle(go, node);
                return;
            }

            if (node.componentType == UguiComponentType.InputField)
            {
                ConfigureInputField(go, node);
                return;
            }

            if (node.semanticType == "Dialog")
            {
                ConfigureDialog(go);
            }
        }

        private void ConfigureToggle(GameObject go, UguiNode node)
        {
            var toggle = go.GetComponent<Toggle>();
            if (toggle == null)
            {
                return;
            }

            var graphics = go.GetComponentsInChildren<Graphic>(true);
            Graphic candidateGraphic = null;
            foreach (var graphic in graphics)
            {
                if (graphic.gameObject == go)
                {
                    continue;
                }

                var lowerName = graphic.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("check") || lowerName.Contains("dot") || lowerName.Contains("icon"))
                {
                    candidateGraphic = graphic;
                    break;
                }

                candidateGraphic ??= graphic;
            }

            if (candidateGraphic != null)
            {
                toggle.graphic = candidateGraphic;
                candidateGraphic.gameObject.SetActive(true);
            }

            if (!string.IsNullOrWhiteSpace(node.name) && node.name.ToLowerInvariant().Contains("radio"))
            {
                var parent = go.transform.parent;
                if (parent != null)
                {
                    var group = parent.GetComponent<ToggleGroup>();
                    if (group == null)
                    {
                        group = parent.gameObject.AddComponent<ToggleGroup>();
                    }

                    toggle.group = group;
                }
            }
        }

        private void ConfigureInputField(GameObject go, UguiNode node)
        {
            var inputField = go.GetComponent<TMP_InputField>();
            if (inputField == null)
            {
                inputField = go.AddComponent<TMP_InputField>();
            }

            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            TextMeshProUGUI placeholder = null;
            TextMeshProUGUI textComponent = null;
            foreach (var text in texts)
            {
                if (text.gameObject == go)
                {
                    continue;
                }

                var lowerName = text.gameObject.name.ToLowerInvariant();
                var looksLikePlaceholder = lowerName.Contains("placeholder")
                    || lowerName.Contains("value")
                    || text.color.a > 0f && text.color.grayscale > 0.35f && text.color.grayscale < 0.8f;

                if (looksLikePlaceholder && placeholder == null)
                {
                    placeholder = text;
                    continue;
                }

                textComponent ??= text;
            }

            if (textComponent == null && placeholder != null)
            {
                textComponent = CreateInputTextChild(go.transform, placeholder);
            }

            if (textComponent == null)
            {
                textComponent = CreateInputTextChild(go.transform, null);
            }

            inputField.textComponent = textComponent;
            if (placeholder != null)
            {
                inputField.placeholder = placeholder;
                if (string.IsNullOrEmpty(inputField.text))
                {
                    inputField.text = string.Empty;
                }
            }

            if (node.name != null && node.name.ToLowerInvariant().Contains("textarea"))
            {
                inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            }
        }

        private TextMeshProUGUI CreateInputTextChild(Transform parent, TextMeshProUGUI source)
        {
            var textGo = new GameObject("InputText");
            textGo.transform.SetParent(parent, false);
            var rect = textGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.fontSize = source != null ? source.fontSize : 14f;
            text.color = source != null ? source.color : Color.black;
            text.alignment = source != null ? source.alignment : TextAlignmentOptions.Left;
            return text;
        }

        private void ConfigureDialog(GameObject go)
        {
            if (go.GetComponent<CanvasGroup>() == null)
            {
                go.AddComponent<CanvasGroup>();
            }
        }

        private string SanitizeGameObjectName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "Node";
            }

            var normalized = Regex.Replace(raw, @"[^\p{L}\p{N}_]+", "_");
            normalized = Regex.Replace(normalized, @"_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(normalized) ? "Node" : normalized;
        }
    }
}
