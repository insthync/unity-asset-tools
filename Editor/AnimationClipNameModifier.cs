using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Insthync.AssetTools
{
    public class AnimationClipNameModifier : EditorWindow
    {
        private List<string> _selectedFiles = new List<string>();
        private Vector2 _scrollPosition;

        [MenuItem("Tools/Asset Tools/Animation Clip Name Modifier")]
        public static void ShowWindow()
        {
            GetWindow<AnimationClipNameModifier>("Animation Clip Name Modifier");
        }

        private void OnGUI()
        {
            GUILayout.Label("Animation Clip Name Modifier", EditorStyles.boldLabel);

            if (GUILayout.Button("Select Model Folder"))
            {
                string folderPath = EditorUtility.OpenFolderPanel("Select Model Folder", "", "");
                if (!string.IsNullOrEmpty(folderPath))
                {
                    _selectedFiles.Clear();
                    string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        if (file.ToLower().EndsWith(".fbx"))
                        {
                            _selectedFiles.Add(file);
                        }
                    }
                }
            }

            if (_selectedFiles.Count > 0)
            {
                GUILayout.Label("Selected Files:", EditorStyles.boldLabel);
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
                foreach (var file in _selectedFiles)
                {
                    GUILayout.Label(file);
                }
                GUILayout.EndScrollView();
            }

            if (GUILayout.Button("Modify Animation Clip Names"))
            {
                ModifyAnimationClipNames();
            }
        }

        private void ModifyAnimationClipNames()
        {
            foreach (string file in _selectedFiles)
            {
                string relativePath = "Assets" + file.Substring(Application.dataPath.Length);

                ModelImporter modelImporter = AssetImporter.GetAtPath(relativePath) as ModelImporter;
                if (modelImporter != null)
                {
                    // Get the model file name without extension
                    string modelFileName = Path.GetFileNameWithoutExtension(file);

                    // Get all clips from the model importer
                    ModelImporterClipAnimation[] clipAnimations = modelImporter.clipAnimations;
                    if (clipAnimations.Length == 0)
                    {
                        clipAnimations = modelImporter.defaultClipAnimations;
                    }

                    // Modify each clip name
                    for (int i = 0; i < clipAnimations.Length; i++)
                    {
                        if (clipAnimations[i].name.ToLower().StartsWith("take "))
                            clipAnimations[i].name = modelFileName + (i > 0 ? i.ToString("D3") : string.Empty);
                    }

                    modelImporter.clipAnimations = clipAnimations;

                    // Reimport the asset to apply changes
                    AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"Modified animation clip names for {relativePath}");
                }
                else
                {
                    Debug.LogError($"Failed to load the model importer for {relativePath}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
