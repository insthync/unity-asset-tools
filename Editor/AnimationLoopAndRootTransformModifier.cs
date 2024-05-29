using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Insthync.AssetTools
{
    public class AnimationLoopAndRootTransformModifier : EditorWindow
    {
        public enum RootRotBakeUpon
        {
            Original,
            BodyOrienation,
        }

        public enum RootYBakeUpon
        {
            Original,
            CenterOfMass,
            Feet,
        }

        public enum RootXZBakeUpon
        {
            Original,
            CenterOfMass,
        }

        private List<string> _selectedFiles = new List<string>();
        private Vector2 _scrollPosition;

        private bool _loopTime;
        private bool _loopPose;
        private float _cycleOffset = 0f;

        private bool _lockRootRotation = false;
        private RootRotBakeUpon _rootRotBakeUpon = RootRotBakeUpon.BodyOrienation;
        private bool _keepOriginalOrientation = false;
        private float _rotationOffset = 0f;

        // Y
        private bool _lockRootHeightY = false;
        private RootYBakeUpon _rootYBakeUpon = RootYBakeUpon.Original;
        private bool _keepOriginalPositionY = false;
        private bool _heightFromFeet = false;
        private float _heightOffset = 0f;

        // XZ
        private bool _lockRootPositionXZ = false;
        private RootXZBakeUpon _rootXZBakeUpon = RootXZBakeUpon.CenterOfMass;
        private bool _keepOriginalPositionXZ = false;

        // Mirror
        private bool _mirror = false;

        // Additive reference pose
        private bool _hasAdditiveReferencePose = false;
        private float _additiveReferencePoseFrame = 0f;

        // Mask
        private ClipAnimationMaskType _maskType = ClipAnimationMaskType.None;
        private AvatarMask _maskSource;

        [MenuItem("Tools/Asset Tools/Animation Loop And Root Transform Modifier")]
        public static void ShowWindow()
        {
            GetWindow<AnimationLoopAndRootTransformModifier>("Animation Loop And Root Transform Modifier");
        }

        private void OnGUI()
        {
            GUILayout.Label("Animation Loop And Root Transform Modifier", EditorStyles.boldLabel);

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

            _loopTime = EditorGUILayout.Toggle("Loop Time", _loopTime);
            if (!_loopTime)
                GUI.enabled = false;
            _loopPose = EditorGUILayout.Toggle("Loop Pose", _loopPose);
            _cycleOffset = EditorGUILayout.FloatField("Cycle Offset", _cycleOffset);
            GUI.enabled = true;

            _lockRootRotation = EditorGUILayout.Toggle("Root Rot -> Bake Into Pose", _lockRootRotation);
            _rootRotBakeUpon = (RootRotBakeUpon)EditorGUILayout.EnumPopup("Root Rot -> Baked Upon", _rootRotBakeUpon);
            switch (_rootRotBakeUpon)
            {
                case RootRotBakeUpon.Original:
                    _keepOriginalOrientation = true;
                    break;
                case RootRotBakeUpon.BodyOrienation:
                    _keepOriginalOrientation = false;
                    break;
            }
            _rotationOffset = EditorGUILayout.FloatField("Root Rot -> Offset", _rotationOffset);

            // Y
            _lockRootHeightY = EditorGUILayout.Toggle("Root Y -> Bake Into Pose", _lockRootHeightY);
            _rootYBakeUpon = (RootYBakeUpon)EditorGUILayout.EnumPopup("Root Y -> Baked Upon", _rootYBakeUpon);
            switch (_rootYBakeUpon)
            {
                case RootYBakeUpon.Original:
                    _keepOriginalPositionY = true;
                    _heightFromFeet = false;
                    break;
                case RootYBakeUpon.CenterOfMass:
                    _keepOriginalPositionY = false;
                    _heightFromFeet = false;
                    break;
                case RootYBakeUpon.Feet:
                    _keepOriginalPositionY = false;
                    _heightFromFeet = true;
                    break;
            }
            _heightOffset = EditorGUILayout.FloatField("Root Y -> Offset", _heightOffset);

            // XZ
            _lockRootPositionXZ = EditorGUILayout.Toggle("Root XZ -> Bake Into Pose", _lockRootPositionXZ);
            _rootXZBakeUpon = (RootXZBakeUpon)EditorGUILayout.EnumPopup("Root XZ -> Baked Upon", _rootXZBakeUpon);
            switch (_rootXZBakeUpon)
            {
                case RootXZBakeUpon.Original:
                    _keepOriginalPositionXZ = true;
                    break;
                case RootXZBakeUpon.CenterOfMass:
                    _keepOriginalPositionXZ = false;
                    break;
            }

            // Mirror
            _mirror = EditorGUILayout.Toggle("Mirror", _mirror);

            // Additive reference pose
            _hasAdditiveReferencePose = EditorGUILayout.Toggle("Has Additive Reference Pose", _hasAdditiveReferencePose);
            if (!_hasAdditiveReferencePose)
                GUI.enabled = false;
            _additiveReferencePoseFrame = EditorGUILayout.FloatField("Additive Reference Pose", _additiveReferencePoseFrame);
            GUI.enabled = true;

            // Mask
            _maskType = (ClipAnimationMaskType)EditorGUILayout.EnumPopup("Mask Type", _maskType);
            switch (_maskType)
            {
                case ClipAnimationMaskType.CopyFromOther:
                    _maskSource = (AvatarMask)EditorGUILayout.ObjectField("Mask Source", _maskSource, typeof(AvatarMask), false);
                    break;
            }

            if (GUILayout.Button("Modify Animation Loop And Root Transforms"))
            {
                ModifyAnimationLoopAndRootTransforms();
            }
        }

        private void ModifyAnimationLoopAndRootTransforms()
        {
            foreach (string file in _selectedFiles)
            {
                string relativePath = "Assets" + file.Substring(Application.dataPath.Length);

                ModelImporter modelImporter = AssetImporter.GetAtPath(relativePath) as ModelImporter;
                if (modelImporter != null)
                {
                    // Get all clips from the model importer
                    ModelImporterClipAnimation[] clipAnimations = modelImporter.clipAnimations;
                    if (clipAnimations.Length == 0)
                    {
                        clipAnimations = modelImporter.defaultClipAnimations;
                    }

                    // Modify each clip name
                    for (int i = 0; i < clipAnimations.Length; i++)
                    {
                        clipAnimations[i].loopTime = _loopTime;
                        clipAnimations[i].loopPose = _loopPose;
                        clipAnimations[i].cycleOffset = _cycleOffset;

                        // Rotation
                        clipAnimations[i].lockRootRotation = _lockRootRotation;
                        clipAnimations[i].rotationOffset = _rotationOffset;
                        clipAnimations[i].keepOriginalOrientation = _keepOriginalOrientation;

                        // Y
                        clipAnimations[i].lockRootHeightY = _lockRootHeightY;
                        clipAnimations[i].heightOffset = _heightOffset;
                        clipAnimations[i].keepOriginalPositionY = _keepOriginalPositionY;
                        clipAnimations[i].heightFromFeet = _heightFromFeet;

                        // XZ
                        clipAnimations[i].lockRootPositionXZ = _lockRootPositionXZ;
                        clipAnimations[i].keepOriginalPositionXZ = _keepOriginalPositionXZ;

                        // Mirror
                        clipAnimations[i].mirror = _mirror;

                        // Additive reference pose
                        clipAnimations[i].hasAdditiveReferencePose = _hasAdditiveReferencePose;
                        if (clipAnimations[i].hasAdditiveReferencePose)
                            clipAnimations[i].additiveReferencePoseFrame = _additiveReferencePoseFrame;

                        // Mask
                        clipAnimations[i].maskType = _maskType;
                        clipAnimations[i].maskSource = _maskSource;
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
