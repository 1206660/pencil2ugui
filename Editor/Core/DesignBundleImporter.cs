using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Design2Ugui.Models;

namespace Design2Ugui.Core
{
    public class DesignBundleImporter
    {
        private readonly UguiConverter converter = new UguiConverter();
        private readonly PrefabCreator prefabCreator = new PrefabCreator();

        public void ImportBundle(string bundlePath)
        {
            var json = File.ReadAllText(bundlePath);
            var bundle = JsonConvert.DeserializeObject<DesignImportBundle>(json);
            if (bundle == null || bundle.screen == null || bundle.screen.rootNode == null)
            {
                throw new InvalidOperationException("Invalid import bundle.");
            }

            var spriteMap = ImportSprites(bundle.assets);
            var prefabMap = BuildComponentPrefabs(bundle.components, spriteMap);
            AssignSprites(bundle.screen.rootNode, spriteMap);

            var screenObject = converter.Convert(bundle.screen.rootNode, null, prefabMap);
            prefabCreator.SaveAsPrefab(screenObject, bundle.screen.prefabPath);
        }

        private Dictionary<string, Sprite> ImportSprites(List<BundleAsset> assets)
        {
            var spriteMap = new Dictionary<string, Sprite>();
            foreach (var asset in assets ?? new List<BundleAsset>())
            {
                var targetFullPath = Path.GetFullPath(asset.targetPath);
                var targetDirectory = Path.GetDirectoryName(targetFullPath);
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Copy(asset.sourcePath, targetFullPath, true);
                AssetDatabase.ImportAsset(asset.targetPath, ImportAssetOptions.ForceSynchronousImport);

                var importer = AssetImporter.GetAtPath(asset.targetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset.targetPath);
                if (sprite != null)
                {
                    spriteMap[asset.key] = sprite;
                }
            }

            return spriteMap;
        }

        private Dictionary<string, GameObject> BuildComponentPrefabs(List<BundleComponent> components, Dictionary<string, Sprite> spriteMap)
        {
            var prefabMap = new Dictionary<string, GameObject>();
            var pending = new List<BundleComponent>(components ?? new List<BundleComponent>());

            while (pending.Count > 0)
            {
                var progressed = false;
                for (var index = pending.Count - 1; index >= 0; index -= 1)
                {
                    var component = pending[index];
                    if (!CanBuild(component.rootNode, prefabMap))
                    {
                        continue;
                    }

                    AssignSprites(component.rootNode, spriteMap);
                    var componentObject = converter.Convert(component.rootNode, null, prefabMap);
                    var prefab = prefabCreator.SaveAsPrefab(componentObject, component.prefabPath);
                    prefabMap[component.key] = prefab;
                    pending.RemoveAt(index);
                    progressed = true;
                }

                if (!progressed)
                {
                    throw new InvalidOperationException("Component dependency graph contains unresolved prefab references.");
                }
            }

            return prefabMap;
        }

        private bool CanBuild(UguiNode node, Dictionary<string, GameObject> prefabMap)
        {
            if (node == null)
            {
                return true;
            }

            if (node.componentType == UguiComponentType.PrefabInstance)
            {
                return !string.IsNullOrEmpty(node.prefabKey) && prefabMap.ContainsKey(node.prefabKey);
            }

            foreach (var child in node.children)
            {
                if (!CanBuild(child, prefabMap))
                {
                    return false;
                }
            }

            return true;
        }

        private void AssignSprites(UguiNode node, Dictionary<string, Sprite> spriteMap)
        {
            if (node?.componentData?.imageRef != null && spriteMap.TryGetValue(node.componentData.imageRef, out var sprite))
            {
                node.componentData.sprite = sprite;
            }

            foreach (var child in node?.children ?? new List<UguiNode>())
            {
                AssignSprites(child, spriteMap);
            }
        }
    }
}
