using System.IO;
using UnityEditor;
using UnityEngine;

namespace Design2Ugui.Core
{
    public class PrefabCreator
    {
        private const string DEFAULT_PREFAB_DIR = "Assets/Prefabs";

        public GameObject SaveAsPrefab(GameObject go, string pathOrName)
        {
            var assetPath = ResolvePrefabPath(pathOrName);
            var directory = Path.GetDirectoryName(assetPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, assetPath);
            Object.DestroyImmediate(go);
            AssetDatabase.Refresh();
            return prefab;
        }

        private string ResolvePrefabPath(string pathOrName)
        {
            if (pathOrName.EndsWith(".prefab"))
            {
                return pathOrName;
            }

            return $"{DEFAULT_PREFAB_DIR}/{pathOrName}.prefab";
        }
    }
}
