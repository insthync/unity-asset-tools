using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Insthync.AssetTools
{
    public class TextRaycastDisabler : EditorWindow
    {
        private List<string> _excludePathList = new List<string>();
        private List<GameObject> _prefabList = new List<GameObject>();

        private Vector2 _excludeScrollPos;
        private Vector2 _prefabScrollPos;

        private string _searchText = string.Empty;

        [MenuItem("Tools/Asset Tools/Text Raycast Disabler")]
        public static void ShowWindow()
        {
            var window = GetWindow<TextRaycastDisabler>("Text Raycast Disabler");
            window.minSize = new Vector2(600, 760);
        }

        private void OnGUI()
        {
            GUILayout.Label("Text Raycast Disabler", EditorStyles.boldLabel);

            // -----------------------------
            // Exclude Folders
            // -----------------------------
            GUILayout.Label("Excluding Folders", EditorStyles.boldLabel);
            _excludeScrollPos = EditorGUILayout.BeginScrollView(_excludeScrollPos, GUILayout.Height(140));

            for (int i = 0; i < _excludePathList.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _excludePathList[i] = GUILayout.TextField(_excludePathList[i]);

                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    OnClickBrowseExcludeFolder(i);
                }

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    _excludePathList.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add"))
            {
                _excludePathList.Add(string.Empty);
            }
            if (GUILayout.Button("Clear"))
            {
                _excludePathList.Clear();
            }
            EditorGUILayout.EndHorizontal();

            // -----------------------------
            // Search
            // -----------------------------
            GUILayout.Label("Prefabs To Modify", EditorStyles.boldLabel);
            // Button to add selected materials from Project tab
            if (GUILayout.Button("Add Prefabs From Selected Assets"))
            {
                AddSelectedPrefabs();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Search Prefabs By Name", GUILayout.Width(220));
            _searchText = GUILayout.TextField(_searchText);
            if (GUILayout.Button("Search", GUILayout.Width(60)))
            {
                Search();
            }
            EditorGUILayout.EndHorizontal();

            // -----------------------------
            // Prefab List
            // -----------------------------
            _prefabScrollPos = EditorGUILayout.BeginScrollView(_prefabScrollPos, GUILayout.Height(240));

            for (int i = 0; i < _prefabList.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _prefabList[i] = (GameObject)EditorGUILayout.ObjectField(_prefabList[i], typeof(GameObject), false);

                // Button to remove material from the list
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    _prefabList.RemoveAt(i);
                    i--; // Adjust index after removal
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // Clear all selected materials
            if (GUILayout.Button("Clear Prefab List"))
            {
                _prefabList.Clear();
            }

            // -----------------------------
            // Settings
            // -----------------------------
            GUILayout.Label("Platform Settings", EditorStyles.boldLabel);

            if (GUILayout.Button("Disable All Text Raycasts"))
                ApplySettings();
        }

        // -------------------------------------------------------
        // Core Logic
        // -------------------------------------------------------

        private void Search()
        {
            _prefabList.Clear();

            string[] guids = AssetDatabase.FindAssets("t:Prefab");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject tex = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (tex == null) continue;
                if (!tex.name.ToLower().Contains(_searchText.ToLower()))
                    continue;
                if (IsInExcludeFolder(path)) continue;

                _prefabList.Add(tex);
            }

            _prefabList = _prefabList
                .OrderByDescending(t => AssetDatabase.GetAssetPath(t))
                .ToList();
        }

        private void AddSelectedPrefabs()
        {
            foreach (Object obj in Selection.objects)
            {
                AddPrefabFromObject(obj);
            }

            _prefabList = _prefabList
                .Where(t => !IsInExcludeFolder(AssetDatabase.GetAssetPath(t)))
                .Distinct()
                .OrderByDescending(t => AssetDatabase.GetAssetPath(t))
                .ToList();
        }

        private void AddPrefabFromObject(Object obj)
        {
            if (obj is GameObject prefab)
            {
                if (!_prefabList.Contains(prefab))
                    _prefabList.Add(prefab);
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(obj);
                var dependencies = AssetDatabase.GetDependencies(path, true);

                foreach (var dep in dependencies)
                {
                    var prefab2 = AssetDatabase.LoadAssetAtPath<GameObject>(dep);
                    if (prefab2 != null && !_prefabList.Contains(prefab2))
                        _prefabList.Add(prefab2);
                }
            }
        }

        private void ApplySettings()
        {
            string logs = "";

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (GameObject prefab in _prefabList)
                {
                    if (prefab == null)
                        continue;

                    string path = AssetDatabase.GetAssetPath(prefab);

                    bool hasChanges = false;

                    GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);

                    try
                    {
                        var textComponents = prefabRoot.GetComponentsInChildren<Text>(true);
                        foreach (var component in textComponents)
                        {
                            if (component.raycastTarget)
                            {
                                component.raycastTarget = false;
                                EditorUtility.SetDirty(component);
                                hasChanges = true;
                            }
                        }

                        var tmpTextComponents = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
                        foreach (var component in tmpTextComponents)
                        {
                            if (component.raycastTarget)
                            {
                                component.raycastTarget = false;
                                EditorUtility.SetDirty(component);
                                hasChanges = true;
                            }
                        }

                        if (hasChanges)
                        {
                            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);

                            string log = $"Updated {path}";
                            logs += log + "\n";
                            Debug.Log(log, prefab);
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string logFolderPath = Path.Combine(Application.dataPath, "../Logs");
            string logFileName = $"text-raycast-disabler_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            string logFullPath = Path.Combine(logFolderPath, logFileName);

            if (!Directory.Exists(logFolderPath))
                Directory.CreateDirectory(logFolderPath);

            File.WriteAllText(logFullPath, logs);

            Debug.Log($"Finished. Log saved to: {logFullPath}");
        }

        // -------------------------------------------------------
        // Helpers
        // -------------------------------------------------------

        private void OnClickBrowseExcludeFolder(int index)
        {
            // Select folder
            string absolutePath = EditorUtility.OpenFolderPanel("Select Excluding Folder", Application.dataPath, "");

            // Ensure it's inside the project
            if (!string.IsNullOrEmpty(absolutePath) && absolutePath.StartsWith(Application.dataPath))
            {
                // Convert to Assets-relative path
                string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
                _excludePathList[index] = relativePath;
            }
            else
            {
                Debug.LogWarning("Please select a folder inside the Assets directory.");
            }
        }

        private bool IsInExcludeFolder(string assetPath)
        {
            foreach (var exclude in _excludePathList)
            {
                if (!string.IsNullOrEmpty(exclude) &&
                    assetPath.ToLower().Contains(exclude.ToLower()))
                    return true;
            }
            return false;
        }
    }
}
