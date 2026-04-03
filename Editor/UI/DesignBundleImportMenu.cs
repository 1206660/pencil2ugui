using UnityEditor;
using UnityEngine;
using Design2Ugui.Core;

namespace Design2Ugui.UI
{
    public static class DesignBundleImportMenu
    {
        [MenuItem("Tools/Design2Ugui/Import Bundle")]
        public static void ImportBundle()
        {
            var bundlePath = EditorUtility.OpenFilePanel("Select Design Import Bundle", "", "json");
            if (string.IsNullOrEmpty(bundlePath))
            {
                return;
            }

            try
            {
                var importer = new DesignBundleImporter();
                importer.ImportBundle(bundlePath);
                EditorUtility.DisplayDialog("Design2Ugui", "Bundle import complete.", "OK");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Design2Ugui", exception.Message, "OK");
            }
        }
    }
}
