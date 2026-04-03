using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Design2Ugui.Models
{
    public class UguiNode
    {
        public string sourceId;
        public string name;
        public string prefabKey;
        public UguiComponentType componentType;
        public RectTransformData rectTransform;
        public ComponentData componentData;
        public LayoutData layout;
        public List<UguiNode> children = new List<UguiNode>();
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum UguiComponentType
    {
        Canvas,
        Panel,
        Image,
        Text,
        Button,
        ScrollView,
        HorizontalLayout,
        VerticalLayout,
        GridLayout,
        PrefabInstance
    }

    public class RectTransformData
    {
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
    }

    public class ComponentData
    {
        public Color color = Color.white;
        public string text;
        public float fontSize;
        public string fontFamily;
        public int fontWeight;
        public TextAlignmentOptions textAlign = TextAlignmentOptions.Left;
        public float letterSpacing;
        public string imageRef;
        public Sprite sprite;
        public bool isScrollable;
    }

    public class LayoutData
    {
        public string direction;
        public float gap;
        public PaddingData padding;
        public string justifyContent;
        public string alignItems;
    }

    public class PaddingData
    {
        public int top;
        public int right;
        public int bottom;
        public int left;
    }
}
