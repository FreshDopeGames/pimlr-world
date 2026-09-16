#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LocalAvatarLoader))]
public sealed class LocalAvatarLoaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh GLB avatars from Assets/Avatars"))
        {
            Refresh((LocalAvatarLoader)target);
        }

        EditorGUILayout.HelpBox(
            "GLB files are enumerated in the editor and stored as direct asset references for WebGL/player builds.",
            MessageType.Info);
    }

    private static void Refresh(LocalAvatarLoader loader)
    {
        var serializedObject = new SerializedObject(loader);
        var avatars = serializedObject.FindProperty("avatars");
        avatars.ClearArray();

        var paths = Directory.GetFiles("Assets/Avatars", "*.glb", SearchOption.TopDirectoryOnly)
            .Select(path => path.Replace('\\', '/'))
            .OrderBy(path => path, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in paths)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            if (asset == null)
            {
                Debug.LogWarning($"Could not load GLB main GameObject at '{path}'. Reimport the asset and try again.", loader);
                continue;
            }

            var index = avatars.arraySize;
            avatars.InsertArrayElementAtIndex(index);
            var definition = avatars.GetArrayElementAtIndex(index);
            definition.FindPropertyRelative("name").stringValue = Path.GetFileNameWithoutExtension(path);
            definition.FindPropertyRelative("asset").objectReferenceValue = asset;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(loader);
        Debug.Log($"Configured {paths.Count} local GLB avatar(s) on '{loader.name}'.", loader);
    }
}
#endif