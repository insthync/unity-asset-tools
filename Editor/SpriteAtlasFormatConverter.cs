using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

public class SpriteAtlasFormatConverter : EditorWindow
{
    private List<string> _excludePathList = new List<string>();
    private List<SpriteAtlas> _textureList = new List<SpriteAtlas>();

    private Vector2 _excludeScrollPos;
    private Vector2 _textureScrollPos;

    private string _searchText;

    // Platform settings
    private bool _applyStandalone;
    private bool _overrideStandalone;
    private TextureImporterFormat _standaloneFormat = TextureImporterFormat.Automatic;

    private bool _applyAndroid;
    private bool _overrideAndroid;
    private TextureImporterFormat _androidFormat = TextureImporterFormat.ASTC_6x6;

    private bool _applyIOS;
    private bool _overrideIOS;
    private TextureImporterFormat _iosFormat = TextureImporterFormat.ASTC_6x6;

    [MenuItem("Tools/Asset Tools/Sprite Atlas Format Converter")]
    public static void ShowWindow()
    {
        var window = GetWindow<SpriteAtlasFormatConverter>("Sprite Atlas Format Converter");
        window.minSize = new Vector2(600, 760);
    }

    private void OnGUI()
    {
        GUILayout.Label("Sprite Atlas Format Converter", EditorStyles.boldLabel);

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
        GUILayout.Label("Textures To Modify", EditorStyles.boldLabel);
        // Button to add selected materials from Project tab
        if (GUILayout.Button("Add Textures From Selected Assets"))
        {
            AddSelectedTextures();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Search Textures By Name", GUILayout.Width(220));
        _searchText = GUILayout.TextField(_searchText);
        if (GUILayout.Button("Search", GUILayout.Width(60)))
        {
            Search();
        }
        EditorGUILayout.EndHorizontal();

        // -----------------------------
        // Texture List
        // -----------------------------
        _textureScrollPos = EditorGUILayout.BeginScrollView(_textureScrollPos, GUILayout.Height(240));

        for (int i = 0; i < _textureList.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _textureList[i] = (SpriteAtlas)EditorGUILayout.ObjectField(_textureList[i], typeof(SpriteAtlas), false);

            // Button to remove material from the list
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                _textureList.RemoveAt(i);
                i--; // Adjust index after removal
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        // Clear all selected materials
        if (GUILayout.Button("Clear Texture List"))
        {
            _textureList.Clear();
        }

        // -----------------------------
        // Settings
        // -----------------------------
        GUILayout.Label("Platform Settings", EditorStyles.boldLabel);

        _applyStandalone = EditorGUILayout.Toggle("Apply Standalone", _applyStandalone);
        if (_applyStandalone)
        {
            _overrideStandalone = EditorGUILayout.Toggle("Override Standalone", _overrideStandalone);
            _standaloneFormat = (TextureImporterFormat)EditorGUILayout.EnumPopup("Standalone Format", _standaloneFormat);
        }

        _applyAndroid = EditorGUILayout.Toggle("Apply Android", _applyAndroid);
        if (_applyAndroid)
        {
            _overrideAndroid = EditorGUILayout.Toggle("Override Android", _overrideAndroid);
            _androidFormat = (TextureImporterFormat)EditorGUILayout.EnumPopup("Android Format", _androidFormat);
        }

        _applyIOS = EditorGUILayout.Toggle("Apply iOS", _applyIOS);
        if (_applyIOS)
        {
            _overrideIOS = EditorGUILayout.Toggle("Override iOS", _overrideIOS);
            _iosFormat = (TextureImporterFormat)EditorGUILayout.EnumPopup("iOS Format", _iosFormat);
        }

        if (GUILayout.Button("Apply To All Textures"))
            ApplySettings();
    }

    // -------------------------------------------------------
    // Core Logic
    // -------------------------------------------------------

    private void Search()
    {
        _textureList.Clear();

        string[] guids = AssetDatabase.FindAssets("t:SpriteAtlas");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SpriteAtlas tex = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);

            if (tex == null) continue;
            if (!tex.name.ToLower().Contains(_searchText.ToLower()))
                continue;
            if (IsInExcludeFolder(path)) continue;

            _textureList.Add(tex);
        }

        _textureList = _textureList
            .OrderByDescending(t => AssetDatabase.GetAssetPath(t))
            .ToList();
    }

    private void AddSelectedTextures()
    {
        foreach (Object obj in Selection.objects)
        {
            AddTextureFromObject(obj);
        }

        _textureList = _textureList
            .Where(t => !IsInExcludeFolder(AssetDatabase.GetAssetPath(t)))
            .Distinct()
            .OrderByDescending(t => AssetDatabase.GetAssetPath(t))
            .ToList();
    }

    private void AddTextureFromObject(Object obj)
    {
        if (obj is SpriteAtlas tex)
        {
            if (!_textureList.Contains(tex))
                _textureList.Add(tex);
        }
        else
        {
            string path = AssetDatabase.GetAssetPath(obj);
            var dependencies = AssetDatabase.GetDependencies(path, true);

            foreach (var dep in dependencies)
            {
                var tex2 = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(dep);
                if (tex2 != null && !_textureList.Contains(tex2))
                    _textureList.Add(tex2);
            }
        }
    }

    private void ApplySettings()
    {
        string logs = "";

        foreach (SpriteAtlas texture in _textureList)
        {
            if (texture == null)
                continue;

            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            if (_applyStandalone)
            {
                var standaloneSettings = importer.GetPlatformTextureSettings("Standalone");
                standaloneSettings.overridden = _overrideStandalone;
                standaloneSettings.format = _standaloneFormat;
                importer.SetPlatformTextureSettings(standaloneSettings);
            }

            if (_applyAndroid)
            {
                var androidSettings = importer.GetPlatformTextureSettings("Android");
                androidSettings.overridden = _overrideAndroid;
                androidSettings.format = _androidFormat;
                importer.SetPlatformTextureSettings(androidSettings);
            }

            if (_applyIOS)
            {
                var iosSettings = importer.GetPlatformTextureSettings("iPhone");
                iosSettings.overridden = _overrideIOS;
                iosSettings.format = _iosFormat;
                importer.SetPlatformTextureSettings(iosSettings);
            }

            importer.SaveAndReimport();

            string log = $"Updated {path}";
            logs += log + "\n";
            Debug.Log(log, texture);
        }

        string logFolderPath = Path.Combine(Application.dataPath, "../Logs");
        string logFileName = $"texture-format-converter_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
        string logFullPath = Path.Combine(logFolderPath, logFileName);

        if (!Directory.Exists(logFolderPath))
        {
            Directory.CreateDirectory(logFolderPath);
        }
        File.WriteAllText(logFullPath, logs);
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