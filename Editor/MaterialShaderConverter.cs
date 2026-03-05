using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class MaterialShaderConverter : EditorWindow
{
    private class ConvertingPair
    {
        public Shader from;
        public Shader to;
    }
    private List<string> _excludePathList = new List<string>();
    private List<Material> _materialList = new List<Material>();
    private List<ConvertingPair> _shaderList = new List<ConvertingPair>();
    private Vector2 _excludePathScrollPos;
    private Vector2 _materialScrollPos;
    private Vector2 _shaderScrollPos;
    private string _searchText = string.Empty;

    [MenuItem("Tools/Asset Tools/Material Shader Converter")]
    public static void ShowWindow()
    {
        var window = GetWindow<MaterialShaderConverter>("Material Shader Converter");
        window.minSize = new Vector2(600, 760);
    }

    private void OnGUI()
    {
        GUILayout.Label("Material Shader Converter", EditorStyles.boldLabel);

        // Display the list of exclude folders
        GUILayout.Label("Excluding Folders", EditorStyles.boldLabel);
        _excludePathScrollPos = EditorGUILayout.BeginScrollView(_excludePathScrollPos, GUILayout.Height(140));
        for (int i = 0; i < _excludePathList.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _excludePathList[i] = GUILayout.TextField(_excludePathList[i]);

            // Button to remove material from the list
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                OnClickBrowseExcludeFolder(i);
            }

            // Button to remove material from the list
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                _excludePathList.RemoveAt(i);
                i--; // Adjust index after removal
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
        GUILayout.Label("Materials to Modify", EditorStyles.boldLabel);
        // Button to add selected materials from Project tab
        if (GUILayout.Button("Add Materials From Selected Assets"))
        {
            AddSelectedMaterials();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Search Materials By Shader Name", EditorStyles.boldLabel, GUILayout.Width(220));
        _searchText = GUILayout.TextField(_searchText);
        if (GUILayout.Button("Search", GUILayout.Width(60)))
        {
            Search();
        }
        EditorGUILayout.EndHorizontal();

        // -----------------------------
        // Material List
        // -----------------------------
        _materialScrollPos = EditorGUILayout.BeginScrollView(_materialScrollPos, GUILayout.Height(240));
        for (int i = 0; i < _materialList.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _materialList[i] = (Material)EditorGUILayout.ObjectField(_materialList[i], typeof(Material), false);

            // Button to remove material from the list
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                _materialList.RemoveAt(i);
                i--; // Adjust index after removal
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        // Clear all selected materials
        if (GUILayout.Button("Clear Materials List"))
        {
            _materialList.Clear();
            _shaderList.Clear();
        }

        // Fill converting shaders
        if (GUILayout.Button("Fill Converting Shaders"))
        {
            FillShaderList();
        }

        // Display the list of shaders
        GUILayout.Label("Converting Shaders", EditorStyles.boldLabel);
        _shaderScrollPos = EditorGUILayout.BeginScrollView(_shaderScrollPos, GUILayout.Height(160));
        for (int i = 0; i < _shaderList.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _shaderList[i].from = (Shader)EditorGUILayout.ObjectField(_shaderList[i].from, typeof(Shader), false);
            GUILayout.Label("To", GUILayout.Width(20));
            _shaderList[i].to = (Shader)EditorGUILayout.ObjectField(_shaderList[i].to, typeof(Shader), false);

            // Button to remove material from the list
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                _shaderList.RemoveAt(i);
                i--; // Adjust index after removal
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        // Apply shader and settings button
        if (GUILayout.Button("Convert All Materials Shaders"))
        {
            ApplyShaderToMaterials();
        }
    }

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

    private bool IsInExcludeFolder(Material material)
    {
        for (int i = 0; i < _excludePathList.Count; ++i)
        {
            string path = AssetDatabase.GetAssetPath(material);
            if (path.ToLower().Contains(_excludePathList[i].ToLower()))
                return true;
        }
        return false;
    }

    private void AddSelectedMaterials()
    {
        // Add selected materials from the Project window to the list
        Object[] selectedObjects = Selection.objects;
        HashSet<string> lookedPaths = new HashSet<string>();
        foreach (Object obj in selectedObjects)
        {
            AddSelectedMaterialsFromObject(obj, lookedPaths);
        }
        _materialList = _materialList
            .Where(mat => !IsInExcludeFolder(mat))
            .OrderByDescending(mat => AssetDatabase.GetAssetPath(mat))
            .ToList();
        FillShaderList();
    }

    private void Search()
    {
        _materialList.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.shader != null && mat.shader.name.ToLower().Contains(_searchText.ToLower()) && !mat.isVariant)
            {
                _materialList.Add(mat);
            }
        }
        _materialList = _materialList
            .Where(mat => !IsInExcludeFolder(mat))
            .OrderByDescending(mat => AssetDatabase.GetAssetPath(mat))
            .ToList();
        FillShaderList();
    }

    private void FillShaderList()
    {
        foreach (var material in _materialList)
        {
            var convertPair = new ConvertingPair()
            {
                from = material.shader,
            };
            if (_shaderList.Count(o => o.from == material.shader) == 0)
                _shaderList.Add(convertPair);
        }
        _shaderList = _shaderList.OrderBy(shader => shader.from.name).ToList();
    }

    public static List<FieldInfo> GetFieldsInheriting(System.Type type, System.Type baseType)
    {
        List<FieldInfo> result = new List<FieldInfo>();
        for (System.Type current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            foreach (var field in current.GetFields(
                BindingFlags.Instance | BindingFlags.DeclaredOnly |
                BindingFlags.Public | BindingFlags.NonPublic))
            {
                // true if field.FieldType == baseType, or derives from it, or implements it (for interfaces)
                if (baseType.IsAssignableFrom(field.FieldType))
                    result.Add(field);
            }
        }
        return result;
    }

    private void AddSelectedMaterialsFromObject(Object obj, HashSet<string> lookedPaths)
    {
        if (obj is Material material)
        {
            if (!_materialList.Contains(material) && !material.isVariant)
                _materialList.Add(material);
        }
        else
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (lookedPaths.Contains(assetPath))
                return;
            lookedPaths.Add(assetPath);

            string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);
            if (dependencies.Length == 0)
                return;
            dependencies = dependencies
                .Where(path => path.StartsWith("Assets")).ToArray();

            if (obj is ScriptableObject scriptableObject)
            {
                var type = scriptableObject.GetType();
                var fields = GetFieldsInheriting(type, typeof(AssetReference));
                foreach (var field in fields)
                {
                    var fieldVar = field.GetValue(obj) as AssetReference;
                    var guid = fieldVar.AssetGUID;
                    var aaPath = AssetDatabase.GUIDToAssetPath(guid);
                    AddSelectedMaterialsFromObject(AssetDatabase.LoadMainAssetAtPath(aaPath), lookedPaths);
                }
            }
            foreach (var dependencyPath in dependencies)
            {
                AddSelectedMaterialsFromObject(AssetDatabase.LoadMainAssetAtPath(dependencyPath), lookedPaths);
            }
        }
    }

    private void ApplyShaderToMaterials()
    {
        string logs = string.Empty;
        foreach (Material material in _materialList)
        {
            if (material == null)
                continue;

            var pair = _shaderList.Where(s => s.from == material.shader && s.to != null && s.from != s.to).FirstOrDefault();
            if (pair == null)
                continue;

            Texture texture = null;
            if (material.HasTexture("_MainTex"))
                texture = material.GetTexture("_MainTex");
            if (material.HasTexture("_BaseMap"))
                texture = material.GetTexture("_BaseMap");

            Color color = Color.white;
            if (material.HasColor("_Color"))
                color = material.GetColor("_Color");
            if (material.HasColor("_BaseColor"))
                color = material.GetColor("_BaseColor");

            Texture normalMap = null;
            if (material.HasTexture("_NormalTex"))
                normalMap = material.GetTexture("_NormalTex");
            if (material.HasTexture("_BumpMap"))
                normalMap = material.GetTexture("_BumpMap");

            float smoothness = 0f;
            if (material.HasFloat("_Smoothness"))
                smoothness = material.GetFloat("_Smoothness");

            int zWrite = 1; // On
            if (material.HasFloat("_ZWrite"))
                zWrite = (int)material.GetFloat("_ZWrite");
            if (material.HasInteger("_ZWrite"))
                zWrite = material.GetInteger("_ZWrite");

            int zTest = 4; // LessEqual
            if (material.HasFloat("_ZTest"))
                zTest = (int)material.GetFloat("_ZTest");
            if (material.HasInteger("_ZTest"))
                zTest = material.GetInteger("_ZTest");

            float cull = 2; // Back
            if (material.HasFloat("_Cull"))
                cull = material.GetFloat("_Cull");

            float alphaClip = 0;
            if (material.HasFloat("_Cutout"))
                alphaClip = material.GetFloat("_Cutout");
            if (material.HasFloat("_AlphaClip"))
                alphaClip = material.GetFloat("_AlphaClip");

            float cutoff = 0;
            if (material.HasFloat("_CutoutCutoff"))
                cutoff = material.GetFloat("_CutoutCutoff");
            if (material.HasFloat("_Cutoff"))
                cutoff = material.GetFloat("_Cutoff");

            float srcBlend = 1;
            if (material.HasFloat("_SourceBlend"))
                srcBlend = material.GetFloat("_SourceBlend");
            if (material.HasFloat("_SrcBlend"))
                srcBlend = material.GetFloat("_SrcBlend");

            float dstBlend = 0;
            if (material.HasFloat("_DestBlend"))
                dstBlend = material.GetFloat("_DestBlend");
            if (material.HasFloat("_DstBlend"))
                dstBlend = material.GetFloat("_DstBlend");

            // Omnishade's preset
            bool hasOmniShadePreset = false;
            int omniShadePreset = 0;
            if (material.HasFloat("_Preset"))
            {
                omniShadePreset = (int)material.GetFloat("_Preset");
                hasOmniShadePreset = true;
            }

            int renderQueue = material.renderQueue;

            material.shader = pair.to;

            // TODO: Find a better way to make it able to change these settings
            // Generic data conversion
            if (material.HasTexture("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasTexture("_BaseMap"))
                material.SetTexture("_BaseMap", texture);

            if (material.HasColor("_Color"))
                material.SetColor("_Color", color);
            if (material.HasColor("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasTexture("_NormalTex"))
                material.SetTexture("_NormalTex", normalMap);
            if (material.HasTexture("_BumpMap"))
                material.SetTexture("_BumpMap", normalMap);

            if (material.HasFloat("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);

            if (hasOmniShadePreset)
            {
                if (material.HasFloat("_Surface"))
                {
                    // Opaque, 0, Transparent, 1, Transparent Additive, 2, Transparent Additive Alpha, 3, Opaque Cutout, 4
                    switch (omniShadePreset)
                    {
                        case 0:
                        case 4:
                            // Opaque / Opaque Cutout
                            material.SetFloat("_Surface", 0);
                            break;
                        case 1:
                        case 2:
                        case 3:
                            // Transparent, Transparent Additive, Transparent Additive Alpha
                            material.SetFloat("_Surface", 1);
                            break;
                    }
                }

                if (material.HasFloat("_Blend"))
                {
                    // Opaque, 0, Transparent, 1, Transparent Additive, 2, Transparent Additive Alpha, 3, Opaque Cutout, 4
                    switch (omniShadePreset)
                    {
                        case 1:
                            material.SetFloat("_Blend", 0);
                            if (material.HasProperty("_SrcBlend"))
                                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            if (material.HasProperty("_DstBlend"))
                                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            if (material.HasProperty("_SrcBlendAlpha"))
                                material.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                            if (material.HasProperty("_DstBlendAlpha"))
                                material.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.Zero);
                            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            material.DisableKeyword("_ALPHAMODULATE_ON");
                            break;
                        case 2:
                            material.SetFloat("_Blend", 2);
                            if (material.HasProperty("_SrcBlend"))
                                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                            if (material.HasProperty("_DstBlend"))
                                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                            if (material.HasProperty("_SrcBlendAlpha"))
                                material.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                            if (material.HasProperty("_DstBlendAlpha"))
                                material.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                            material.DisableKeyword("_ALPHAMODULATE_ON");
                            break;
                        case 3:
                            material.SetFloat("_Blend", 2);
                            if (material.HasProperty("_SrcBlend"))
                                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                            if (material.HasProperty("_DstBlend"))
                                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                            if (material.HasProperty("_SrcBlendAlpha"))
                                material.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                            if (material.HasProperty("_DstBlendAlpha"))
                                material.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                            material.DisableKeyword("_ALPHAMODULATE_ON");
                            break;
                    }
                }
            }

            if (material.HasFloat("_Cutout"))
                material.SetFloat("_Cutout", alphaClip);
            if (material.HasFloat("_AlphaClip"))
                material.SetFloat("_AlphaClip", alphaClip);

            if (material.HasFloat("_CutoutCutoff"))
                material.SetFloat("_CutoutCutoff", cutoff);
            if (material.HasFloat("_Cutoff"))
                material.SetFloat("_Cutoff", cutoff);

            if (material.HasFloat("_SourceBlend"))
                material.SetFloat("_SourceBlend", srcBlend);
            if (material.HasFloat("_SrcBlend"))
                material.SetFloat("_SrcBlend", srcBlend);

            if (material.HasFloat("_DestBlend"))
                material.SetFloat("_DestBlend", dstBlend);
            if (material.HasFloat("_DstBlend"))
                material.SetFloat("_DstBlend", dstBlend);

            if (material.HasFloat("_ZWrite"))
                material.SetFloat("_ZWrite", zWrite);

            if (material.HasFloat("_ZTest"))
                material.SetFloat("_ZTest", zTest);

            if (material.HasFloat("_Cull"))
                material.SetFloat("_Cull", cull);

            // Other
            if (material.HasFloat("_Fog"))
                material.SetFloat("_Fog", 0f);

            if (material.HasFloat("_ShadowsEnabled"))
                material.SetFloat("_ShadowsEnabled", 0f);

            material.renderQueue = renderQueue;

            string path = AssetDatabase.GetAssetPath(material);
            string log = $"Convert {path}'s shader to {pair.to.name} (from {pair.from.name})";
            logs += $"{log}\n";
            Debug.Log(log, material);
        }

        string logFolderPath = Path.Combine(Application.dataPath, "../Logs");
        string logFileName = $"material-shader-converter_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
        string logFullPath = Path.Combine(logFolderPath, logFileName);

        if (!Directory.Exists(logFolderPath))
        {
            Directory.CreateDirectory(logFolderPath);
        }
        File.WriteAllText(logFullPath, logs);
    }
}
