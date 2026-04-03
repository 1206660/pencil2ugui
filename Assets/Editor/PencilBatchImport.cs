using System;
using System.Linq;
using Design2Ugui.Core;
using UnityEditor;
using UnityEngine;

namespace Pencil2Unity.Editor
{
    public static class PencilBatchImport
    {
        public static void ImportFromCommandLine()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                var penFilePath = GetArgument(arguments, "-penFile");
                var nodeId = GetArgument(arguments, "-nodeId");
                var outputRoot = GetArgument(arguments, "-outputRoot") ?? "Assets/UI";
                var nodeExecutable = GetArgument(arguments, "-nodeExecutable") ?? "node";

                if (string.IsNullOrWhiteSpace(penFilePath))
                {
                    throw new ArgumentException("Missing required argument: -penFile");
                }

                var generator = new PencilBundleGenerator();
                var bundlePath = generator.GenerateBundle(penFilePath, nodeId, outputRoot, nodeExecutable);

                var importer = new DesignBundleImporter();
                importer.ImportBundle(bundlePath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[PencilBatchImport] Imported node '{(string.IsNullOrWhiteSpace(nodeId) ? "<first top-level node>" : nodeId)}' " +
                    $"from '{penFilePath}' into '{outputRoot}'."
                );
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static string GetArgument(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length)
            {
                return null;
            }

            return args[index + 1];
        }
    }
}
