using System;
using System.Collections.Generic;

namespace Design2Ugui.Models
{
    [Serializable]
    public class DesignSyncDocument
    {
        public string version = "1.0.0";
        public DesignSyncProject project = new DesignSyncProject();
        public List<DesignSyncAsset> assets = new List<DesignSyncAsset>();
        public List<DesignSyncComponent> components = new List<DesignSyncComponent>();
        public List<DesignSyncScreen> screens = new List<DesignSyncScreen>();
        public List<DesignSyncReport> reports = new List<DesignSyncReport>();
    }

    [Serializable]
    public class DesignSyncProject
    {
        public string name;
        public string unityVersion;
        public string exportedAt;
        public string sourceRoot;
        public string generator = "UnityToPencilExporter";
        public string generatorVersion = "0.1.0";
    }

    [Serializable]
    public class DesignSyncAsset
    {
        public string assetKey;
        public string assetType;
        public string assetPath;
        public string assetGuid;
        public string sourceKind;
        public DesignSyncNode rootNode;
        public List<DesignSyncDependency> dependencies = new List<DesignSyncDependency>();
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public class DesignSyncDependency
    {
        public string kind;
        public string assetPath;
    }

    [Serializable]
    public class DesignSyncNode
    {
        public string id;
        public string name;
        public string unityName;
        public string semanticType;
        public string semanticRole;
        public string componentType;
        public string componentRef;
        public bool isPrefabRoot;
        public bool isComponentCandidate;
        public string repeatSignature;
        public DesignSyncBounds bounds = new DesignSyncBounds();
        public DesignSyncLayout layout = new DesignSyncLayout();
        public DesignSyncStyle style = new DesignSyncStyle();
        public DesignSyncContent content = new DesignSyncContent();
        public List<DesignSyncNode> children = new List<DesignSyncNode>();
    }

    [Serializable]
    public class DesignSyncBounds
    {
        public float x;
        public float y;
        public float width;
        public float height;
        public DesignSyncVector2 anchorMin = new DesignSyncVector2();
        public DesignSyncVector2 anchorMax = new DesignSyncVector2();
        public DesignSyncVector2 pivot = new DesignSyncVector2();
    }

    [Serializable]
    public class DesignSyncVector2
    {
        public float x;
        public float y;

        public DesignSyncVector2()
        {
        }

        public DesignSyncVector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }

    [Serializable]
    public class DesignSyncLayout
    {
        public string mode = "none";
        public float spacing;
        public DesignSyncPadding padding = new DesignSyncPadding();
        public string alignment = string.Empty;
        public bool childControlWidth;
        public bool childControlHeight;
        public bool childForceExpandWidth;
        public bool childForceExpandHeight;
        public string scrollAxis = string.Empty;
    }

    [Serializable]
    public class DesignSyncPadding
    {
        public int left;
        public int right;
        public int top;
        public int bottom;
    }

    [Serializable]
    public class DesignSyncStyle
    {
        public string styleKey = string.Empty;
        public Dictionary<string, string> tokenRefs = new Dictionary<string, string>();
        public Dictionary<string, string> fallbacks = new Dictionary<string, string>();
        public string spriteRef = string.Empty;
        public string fontRef = string.Empty;
        public string materialRef = string.Empty;
    }

    [Serializable]
    public class DesignSyncContent
    {
        public string text = string.Empty;
        public string placeholder = string.Empty;
        public string value = string.Empty;
        public string imageRef = string.Empty;
        public string iconRef = string.Empty;
        public string state = "Default";
    }

    [Serializable]
    public class DesignSyncComponent
    {
        public string componentKey;
        public string componentName;
        public string sourceAssetKey;
        public string semanticType;
        public string variant = string.Empty;
        public List<string> stateSet = new List<string>();
        public List<DesignSyncSlot> slots = new List<DesignSyncSlot>();
        public DesignSyncNode templateNode;
        public string styleKey = string.Empty;
        public int usageCount;
    }

    [Serializable]
    public class DesignSyncSlot
    {
        public string slotKey;
        public List<string> acceptedTypes = new List<string>();
    }

    [Serializable]
    public class DesignSyncScreen
    {
        public string screenKey;
        public string screenName;
        public string rootFrameName;
        public List<DesignSyncRegion> regions = new List<DesignSyncRegion>();
        public List<DesignSyncComponentInstance> componentInstances = new List<DesignSyncComponentInstance>();
        public string navigationRole = string.Empty;
        public string legacyRiskLevel = "low";
    }

    [Serializable]
    public class DesignSyncRegion
    {
        public string regionKey;
        public string name;
        public string semanticType;
    }

    [Serializable]
    public class DesignSyncComponentInstance
    {
        public string instanceKey;
        public string componentRef;
        public string name;
        public string regionKey;
        public DesignSyncBounds bounds = new DesignSyncBounds();
    }

    [Serializable]
    public class DesignSyncReport
    {
        public string reportType;
        public string targetKey;
        public string status;
        public DesignSyncMetrics metrics = new DesignSyncMetrics();
        public List<DesignSyncIssue> issues = new List<DesignSyncIssue>();
    }

    [Serializable]
    public class DesignSyncMetrics
    {
        public int assetCount;
        public int componentCount;
        public int screenCount;
        public int unnamedNodeCount;
        public int issueCount;
    }

    [Serializable]
    public class DesignSyncIssue
    {
        public string severity;
        public string code;
        public string message;
        public string nodeId;
    }

    [Serializable]
    public class DesignSyncComponentBundle
    {
        public string version = "1.0.0";
        public DesignSyncProject project = new DesignSyncProject();
        public List<DesignSyncComponent> components = new List<DesignSyncComponent>();
    }

    [Serializable]
    public class DesignSyncScreenBundle
    {
        public string version = "1.0.0";
        public DesignSyncProject project = new DesignSyncProject();
        public List<DesignSyncScreen> screens = new List<DesignSyncScreen>();
    }

    [Serializable]
    public class DesignSyncAuditBundle
    {
        public string version = "1.0.0";
        public DesignSyncProject project = new DesignSyncProject();
        public List<DesignSyncReport> reports = new List<DesignSyncReport>();
    }
}
