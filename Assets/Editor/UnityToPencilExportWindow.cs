using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pencil2Unity.Editor
{
    public sealed class UnityToPencilExportWindow : EditorWindow
    {
        private const string DefaultInputRoot = "Assets/Art/UIPanel/gonghui";

        private string inputRoot = DefaultInputRoot;
        private string outputDirectory = string.Empty;
        private string outputFileName = "gonghui.pen";
        private Vector2 scrollPosition;

        [MenuItem("Tools/Design2Ugui/Export Unity To Pencil (.pen)")]
        public static void OpenWindow()
        {
            var window = GetWindow<UnityToPencilExportWindow>("Unity To Pencil");
            window.minSize = new Vector2(720f, 220f);
            window.Show();
        }

        private static string ProjectRoot
        {
            get
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    throw new InvalidOperationException("Unable to resolve Unity project root.");
                }

                return projectRoot;
            }
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(inputRoot))
            {
                inputRoot = DefaultInputRoot;
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = GetDefaultOutputDirectory();
            }

            if (string.IsNullOrWhiteSpace(outputFileName))
            {
                outputFileName = "gonghui.pen";
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Unity To Pencil Export", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Defaults are shown directly here. Use Change only when you need a different folder.", MessageType.Info);

            DrawInputRootSection();
            EditorGUILayout.Space(8f);
            DrawOutputDirectorySection();
            EditorGUILayout.Space(8f);
            DrawOutputFileSection();
            EditorGUILayout.Space(16f);
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawInputRootSection()
        {
            EditorGUILayout.LabelField("UI Root", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(inputRoot, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Change", GUILayout.Width(96f)))
                {
                    var selectedPath = UnityToPencilBatchExport.SelectProjectFolder(DefaultInputRoot, "Select Unity UI Root Folder");
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        inputRoot = selectedPath;
                        if (string.Equals(outputFileName, "gonghui.pen", StringComparison.OrdinalIgnoreCase)
                            || string.IsNullOrWhiteSpace(outputFileName))
                        {
                            outputFileName = $"{Path.GetFileName(inputRoot.TrimEnd('/', '\\'))}.pen";
                        }
                    }
                }
            }
        }

        private void DrawOutputDirectorySection()
        {
            EditorGUILayout.LabelField("Output Directory", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(outputDirectory, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Change", GUILayout.Width(96f)))
                {
                    var selectedDirectory = EditorUtility.OpenFolderPanel("Select Pencil Export Directory", outputDirectory, string.Empty);
                    if (!string.IsNullOrWhiteSpace(selectedDirectory))
                    {
                        outputDirectory = Path.GetFullPath(selectedDirectory);
                    }
                }
            }
        }

        private void DrawOutputFileSection()
        {
            EditorGUILayout.LabelField("Output File", EditorStyles.boldLabel);
            outputFileName = EditorGUILayout.TextField(outputFileName);
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Export", GUILayout.Width(128f), GUILayout.Height(30f)))
                {
                    Export();
                }
            }
        }

        private void Export()
        {
            try
            {
                if (!AssetDatabase.IsValidFolder(inputRoot))
                {
                    throw new DirectoryNotFoundException($"Unity folder not found: {inputRoot}");
                }

                if (string.IsNullOrWhiteSpace(outputDirectory))
                {
                    throw new InvalidOperationException("Output directory is required.");
                }

                if (string.IsNullOrWhiteSpace(outputFileName))
                {
                    throw new InvalidOperationException("Output file name is required.");
                }

                Directory.CreateDirectory(outputDirectory);
                var normalizedFileName = outputFileName.EndsWith(".pen", StringComparison.OrdinalIgnoreCase)
                    ? outputFileName
                    : $"{outputFileName}.pen";
                var outputPenPath = Path.Combine(outputDirectory, normalizedFileName);

                var result = UnityToPencilBatchExport.RunPipeline(inputRoot, outputPenPath);

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

        private static string GetDefaultOutputDirectory()
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot, "Temp", "PencilBundles"));
        }
    }
}
