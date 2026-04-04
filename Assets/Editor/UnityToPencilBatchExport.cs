using System;
using Design2Ugui.Core;
using UnityEditor;
using UnityEngine;

namespace Pencil2Unity.Editor
{
    public static class UnityToPencilBatchExport
    {
        [MenuItem("Tools/Design2Ugui/Export Unity To Pencil (.pen)")]
        public static void ExportPenFromMenu()
        {
            try
            {
                const string defaultRoot = "Assets/UI";
                var inputRoot = SelectProjectFolder(defaultRoot, "Select Unity UI Root Folder");
                if (string.IsNullOrWhiteSpace(inputRoot))
                {
                    return;
                }

                var outputPath = EditorUtility.SaveFilePanel(
                    "Export Unity UI To Pencil",
                    "Temp/DesignSync/output",
                    "app-ui",
                    "pen"
                );

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    return;
                }

                var result = RunPipeline(inputRoot, outputPath);

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "Design2Ugui",
                    $"Input Root:\n{inputRoot}\n\nPencil file written to:\n{result.PenPath}\n\nScan:\n{result.ScanPath}\n\nBundles:\n{result.ComponentBundlePath}\n{result.ScreenBundlePath}\n{result.AuditBundlePath}",
                    "OK"
                );
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Design2Ugui", exception.Message, "OK");
            }
        }

        [MenuItem("Tools/Design2Ugui/Export Unity Scan")]
        public static void ExportScanFromMenu()
        {
            try
            {
                const string defaultRoot = "Assets/UI";
                var inputRoot = SelectProjectFolder(defaultRoot, "Select Unity UI Root Folder");
                if (string.IsNullOrWhiteSpace(inputRoot))
                {
                    return;
                }

                var outputPath = EditorUtility.SaveFilePanel(
                    "Export Unity UI Scan",
                    "Temp/DesignSync/unity-scan",
                    "unity-scan",
                    "json"
                );

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    return;
                }

                var exporter = new UnityScanExporter();
                var scanPath = exporter.ExportScan(inputRoot, outputPath);

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Design2Ugui", $"Input Root:\n{inputRoot}\n\nUnity scan exported to:\n{scanPath}", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Design2Ugui", exception.Message, "OK");
            }
        }

        [MenuItem("Tools/Design2Ugui/Build Unity Design Bundles")]
        public static void BuildBundlesFromMenu()
        {
            try
            {
                var scanPath = EditorUtility.OpenFilePanel(
                    "Select Unity Scan JSON",
                    "Temp/DesignSync/unity-scan",
                    "json"
                );

                if (string.IsNullOrWhiteSpace(scanPath))
                {
                    return;
                }

                var outputDirectory = EditorUtility.OpenFolderPanel(
                    "Select Design Sync Output Folder",
                    "Temp/DesignSync",
                    string.Empty
                );

                if (string.IsNullOrWhiteSpace(outputDirectory))
                {
                    return;
                }

                var normalizer = new UnitySemanticNormalizer();
                var result = normalizer.Normalize(scanPath, outputDirectory);

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "Design2Ugui",
                    $"Bundles generated:\n{result.ComponentBundlePath}\n{result.ScreenBundlePath}\n{result.AuditBundlePath}",
                    "OK"
                );
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Design2Ugui", exception.Message, "OK");
            }
        }

        [MenuItem("Tools/Design2Ugui/Write Pencil File From Bundles")]
        public static void WritePenFileFromMenu()
        {
            try
            {
                var componentBundlePath = EditorUtility.OpenFilePanel(
                    "Select Components Bundle",
                    "Temp/DesignSync/component-library",
                    "json"
                );

                if (string.IsNullOrWhiteSpace(componentBundlePath))
                {
                    return;
                }

                var screenBundlePath = EditorUtility.OpenFilePanel(
                    "Select Screens Bundle",
                    "Temp/DesignSync/screen-compositions",
                    "json"
                );

                if (string.IsNullOrWhiteSpace(screenBundlePath))
                {
                    return;
                }

                var auditBundlePath = EditorUtility.OpenFilePanel(
                    "Select Audit Bundle",
                    "Temp/DesignSync/reports",
                    "json"
                );

                if (string.IsNullOrWhiteSpace(auditBundlePath))
                {
                    return;
                }

                var outputPath = EditorUtility.SaveFilePanel(
                    "Write Pencil File",
                    "Temp/DesignSync/output",
                    "app-ui",
                    "pen"
                );

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    return;
                }

                var generator = new UnityPenFileGenerator();
                var penPath = generator.GeneratePenFile(componentBundlePath, screenBundlePath, auditBundlePath, outputPath);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Design2Ugui", $"Pencil file written to:\n{penPath}", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Design2Ugui", exception.Message, "OK");
            }
        }

        public static void ExportScan()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                var inputRoot = GetArgument(arguments, "-inputRoot") ?? "Assets/UI";
                var outputPath = GetArgument(arguments, "-outputPath");

                var exporter = new UnityScanExporter();
                var scanPath = exporter.ExportScan(inputRoot, outputPath);

                AssetDatabase.Refresh();
                Debug.Log($"[UnityToPencilBatchExport] Exported Unity scan from '{inputRoot}' to '{scanPath}'.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        public static void BuildBundles()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                var scanPath = GetArgument(arguments, "-scanPath");
                var outputRoot = GetArgument(arguments, "-outputRoot");

                if (string.IsNullOrWhiteSpace(scanPath))
                {
                    throw new ArgumentException("Missing required argument: -scanPath");
                }

                var normalizer = new UnitySemanticNormalizer();
                var result = normalizer.Normalize(scanPath, outputRoot);

                AssetDatabase.Refresh();
                Debug.Log(
                    $"[UnityToPencilBatchExport] Generated bundles:\n{result.ComponentBundlePath}\n{result.ScreenBundlePath}\n{result.AuditBundlePath}"
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

        public static void WritePen()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                var componentBundlePath = GetArgument(arguments, "-components");
                var screenBundlePath = GetArgument(arguments, "-screens");
                var auditBundlePath = GetArgument(arguments, "-audit");
                var outputPenPath = GetArgument(arguments, "-out");
                var nodeExecutable = GetArgument(arguments, "-nodeExecutable") ?? "node";

                var generator = new UnityPenFileGenerator();
                var penPath = generator.GeneratePenFile(
                    componentBundlePath,
                    screenBundlePath,
                    auditBundlePath,
                    outputPenPath,
                    nodeExecutable
                );

                AssetDatabase.Refresh();
                Debug.Log($"[UnityToPencilBatchExport] Wrote Pencil file to '{penPath}'.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        public static void ExportPen()
        {
            try
            {
                var arguments = Environment.GetCommandLineArgs();
                var inputRoot = GetArgument(arguments, "-inputRoot") ?? "Assets/UI";
                var outputPenPath = GetArgument(arguments, "-outputPenPath");
                var nodeExecutable = GetArgument(arguments, "-nodeExecutable") ?? "node";

                if (string.IsNullOrWhiteSpace(outputPenPath))
                {
                    throw new ArgumentException("Missing required argument: -outputPenPath");
                }

                var result = RunPipeline(inputRoot, outputPenPath, nodeExecutable);

                AssetDatabase.Refresh();
                Debug.Log(
                    $"[UnityToPencilBatchExport] Exported Pencil file to '{result.PenPath}'.\n" +
                    $"Scan: {result.ScanPath}\n" +
                    $"Components: {result.ComponentBundlePath}\n" +
                    $"Screens: {result.ScreenBundlePath}\n" +
                    $"Audit: {result.AuditBundlePath}"
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

        private static PipelineResult RunPipeline(string inputRoot, string outputPenPath, string nodeExecutable = "node")
        {
            var outputRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(outputPenPath) ?? "Temp/DesignSync/output",
                ".."
            ));
            var scanPath = System.IO.Path.Combine(outputRoot, "unity-scan", "unity-scan.json");

            var exporter = new UnityScanExporter();
            var resolvedScanPath = exporter.ExportScan(inputRoot, scanPath);

            var normalizer = new UnitySemanticNormalizer();
            var normalizationResult = normalizer.Normalize(resolvedScanPath, outputRoot);

            var generator = new UnityPenFileGenerator();
            var penPath = generator.GeneratePenFile(
                normalizationResult.ComponentBundlePath,
                normalizationResult.ScreenBundlePath,
                normalizationResult.AuditBundlePath,
                outputPenPath,
                nodeExecutable
            );

            return new PipelineResult
            {
                ScanPath = resolvedScanPath,
                ComponentBundlePath = normalizationResult.ComponentBundlePath,
                ScreenBundlePath = normalizationResult.ScreenBundlePath,
                AuditBundlePath = normalizationResult.AuditBundlePath,
                PenPath = penPath
            };
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

        private static string SelectProjectFolder(string defaultAssetPath, string title)
        {
            var projectRoot = System.IO.Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("Unable to resolve Unity project root.");
            }

            var initialFolder = System.IO.Path.Combine(projectRoot, defaultAssetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var selectedFolder = EditorUtility.OpenFolderPanel(title, initialFolder, string.Empty);
            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                return null;
            }

            var normalizedProjectRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            var normalizedSelectedFolder = selectedFolder.Replace('\\', '/');
            if (!normalizedSelectedFolder.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Selected folder must be inside the current Unity project.");
            }

            var relativePath = normalizedSelectedFolder.Substring(normalizedProjectRoot.Length).TrimStart('/');
            return string.IsNullOrWhiteSpace(relativePath) ? "Assets" : relativePath;
        }

        private sealed class PipelineResult
        {
            public string ScanPath;
            public string ComponentBundlePath;
            public string ScreenBundlePath;
            public string AuditBundlePath;
            public string PenPath;
        }
    }
}
