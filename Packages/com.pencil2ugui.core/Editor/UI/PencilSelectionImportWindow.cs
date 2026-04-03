using System;
using UnityEditor;
using UnityEngine;
using Design2Ugui.Core;

namespace Design2Ugui.UI
{
    public class PencilSelectionImportWindow : EditorWindow
    {
        private const string PenFileKey = "Design2Ugui.PenFile";
        private const string NodeIdKey = "Design2Ugui.NodeId";
        private const string OutputRootKey = "Design2Ugui.OutputRoot";
        private const string NodeExecutableKey = "Design2Ugui.NodeExecutable";

        private string penFilePath = string.Empty;
        private string nodeId = string.Empty;
        private string outputRoot = "Assets/UI";
        private string nodeExecutable = "node";

        [MenuItem("Tools/Design2Ugui/Import Pencil Selection")]
        public static void Open()
        {
            var window = GetWindow<PencilSelectionImportWindow>("Import Pencil Selection");
            window.minSize = new Vector2(540f, 180f);
            window.Show();
        }

        private void OnEnable()
        {
            penFilePath = EditorPrefs.GetString(PenFileKey, penFilePath);
            nodeId = EditorPrefs.GetString(NodeIdKey, nodeId);
            outputRoot = EditorPrefs.GetString(OutputRootKey, outputRoot);
            nodeExecutable = EditorPrefs.GetString(NodeExecutableKey, nodeExecutable);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Pencil To Unity", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select a .pen file and enter the Pencil node ID for the interface you want to restore into Unity.",
                MessageType.Info
            );

            DrawPenFileField();
            nodeId = EditorGUILayout.TextField("Selected Node ID", nodeId);
            outputRoot = EditorGUILayout.TextField("Unity Output Root", outputRoot);
            nodeExecutable = EditorGUILayout.TextField("Node Executable", nodeExecutable);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(penFilePath)))
            {
                if (GUILayout.Button("Import Selected Interface", GUILayout.Height(32f)))
                {
                    ImportSelection();
                }
            }
        }

        private void DrawPenFileField()
        {
            EditorGUILayout.BeginHorizontal();
            penFilePath = EditorGUILayout.TextField("Pencil File", penFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(80f)))
            {
                var selectedPath = EditorUtility.OpenFilePanel("Select Pencil File", "", "pen");
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    penFilePath = selectedPath;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ImportSelection()
        {
            SavePrefs();

            try
            {
                var generator = new PencilBundleGenerator();
                var bundlePath = generator.GenerateBundle(penFilePath, nodeId, outputRoot, nodeExecutable);

                var importer = new DesignBundleImporter();
                importer.ImportBundle(bundlePath);

                EditorUtility.DisplayDialog(
                    "Design2Ugui",
                    $"Imported Pencil selection '{(string.IsNullOrWhiteSpace(nodeId) ? "<first top-level node>" : nodeId)}' into Unity.",
                    "OK"
                );
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Design2Ugui", exception.Message, "OK");
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PenFileKey, penFilePath ?? string.Empty);
            EditorPrefs.SetString(NodeIdKey, nodeId ?? string.Empty);
            EditorPrefs.SetString(OutputRootKey, outputRoot ?? "Assets/UI");
            EditorPrefs.SetString(NodeExecutableKey, nodeExecutable ?? "node");
        }
    }
}
