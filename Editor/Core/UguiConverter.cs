using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using System.Collections.Generic;
using Design2Ugui.Models;

namespace Design2Ugui.Core
{
    public class UguiConverter
    {
        private Sprite whiteSprite;

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
            if (node.componentType == UguiComponentType.PrefabInstance && prefabMap != null && !string.IsNullOrEmpty(node.prefabKey) && prefabMap.TryGetValue(node.prefabKey, out var prefab))
            {
                go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                go.name = node.name;
            }
            else
            {
                go = new GameObject(node.name);
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
                    AddScrollView(go);
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
                case UguiComponentType.HorizontalLayout:
                case UguiComponentType.VerticalLayout:
                    AddOptionalBackground(go, node.componentData);
                    ApplyLayout(go, node.layout);
                    break;
            }

            foreach (var child in node.children)
            {
                Convert(child, go.transform, prefabMap);
            }

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

        private void AddScrollView(GameObject go)
        {
            var scrollRect = go.AddComponent<ScrollRect>();
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(go.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewport.AddComponent<Image>();
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
        }
    }
}
