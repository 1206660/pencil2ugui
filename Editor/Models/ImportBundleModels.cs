using System;
using System.Collections.Generic;

namespace Design2Ugui.Models
{
    [Serializable]
    public class DesignImportBundle
    {
        public string version;
        public string outputRoot;
        public BundleSource source;
        public List<BundleAsset> assets = new List<BundleAsset>();
        public List<BundleFont> fonts = new List<BundleFont>();
        public List<BundleComponent> components = new List<BundleComponent>();
        public BundleScreen screen;
    }

    [Serializable]
    public class BundleSource
    {
        public string penFile;
        public string nodeId;
        public string screenName;
    }

    [Serializable]
    public class BundleAsset
    {
        public string key;
        public string kind;
        public string sourcePath;
        public string targetPath;
    }

    [Serializable]
    public class BundleFont
    {
        public string family;
        public List<string> weights = new List<string>();
    }

    [Serializable]
    public class BundleComponent
    {
        public string key;
        public string name;
        public string prefabPath;
        public UguiNode rootNode;
    }

    [Serializable]
    public class BundleScreen
    {
        public string name;
        public string prefabPath;
        public UguiNode rootNode;
    }
}
