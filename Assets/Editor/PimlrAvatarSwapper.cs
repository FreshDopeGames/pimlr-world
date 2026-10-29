using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// PIMLR: swap the Ready Player Me body on a character for another avatar, keeping everything JUTPS hangs off
/// the skeleton (weapon positions, IK targets, ragdoll, health bar, AI, Animator avatar...).
/// Why the Inspector couldn't do it: a skinned mesh can't just be dragged in - its bones, bind pose and
/// materials all have to come across together.
///
/// What it does, by bone/renderer NAME (all RPM rigs share the same bone names):
///   1. copies the new avatar's bone pose onto the target skeleton (so the new mesh's bind pose fits),
///   2. points every body renderer at the new avatar's mesh + materials (adds pieces the old one lacked),
///   3. hides body pieces the new avatar doesn't have.
/// Renderer names are matched ignoring the "Renderer_" / "Wolf3D_" prefix, so an FBX export
/// (Wolf3D_Body, EyeLeft...) lines up with a renamed rig (Renderer_Body, Renderer_EyeLeft...).
///
/// Target: a prefab asset (edited and saved) OR a GameObject in the open scene (edited in place, undoable).
/// Source: an RPM avatar prefab/.glb, or an FBX exported from one (e.g. Assets/_Game/Prefabs/Players/Thurz.fbx).
/// Menu: PIMLR / Characters / Swap RPM Avatar...
/// </summary>
public class PimlrAvatarSwapper : EditorWindow
{
    const string DefaultTarget = "Assets/_Game/Prefabs/WaterBallonBoss.prefab";
    const string DefaultSourceGuidPath = "Assets/Ready Player Me/Avatars/64e634111dfe4c3936791db2/2fac66e374c947c41bc74325c6e3d934/64e634111dfe4c3936791db2.prefab";

    GameObject targetObject;
    GameObject sourceAvatar;

    [MenuItem("PIMLR/Characters/Swap RPM Avatar...")]
    static void Open()
    {
        var w = GetWindow<PimlrAvatarSwapper>(true, "Swap RPM Avatar");
        w.minSize = new Vector2(460, 190);
        // Prefill with whatever is selected: a scene character or a prefab asset.
        var sel = Selection.activeGameObject;
        w.targetObject = sel != null ? sel : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTarget);
        w.sourceAvatar = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSourceGuidPath);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Target = the character to re-skin: a prefab asset, or a character in the open scene (e.g. SceneStaticEU > Player).\n" +
            "Source = an RPM avatar prefab/.glb, or an FBX exported from one (e.g. _Game/Prefabs/Players/Thurz.fbx).",
            MessageType.Info);
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target", targetObject, typeof(GameObject), true);
        sourceAvatar = (GameObject)EditorGUILayout.ObjectField("Source avatar", sourceAvatar, typeof(GameObject), false);
        GUI.enabled = targetObject && sourceAvatar;
        if (GUILayout.Button("Swap", GUILayout.Height(30)))
            Swap(targetObject, sourceAvatar);
        GUI.enabled = true;
    }

    public static void Swap(GameObject target, GameObject sourceAsset)
    {
        bool isAsset = EditorUtility.IsPersistent(target);
        string targetPath = isAsset ? AssetDatabase.GetAssetPath(target) : null;
        if (isAsset && string.IsNullOrEmpty(targetPath)) { Debug.LogError("Swap RPM Avatar: target asset has no path."); return; }

        GameObject root;
        if (isAsset)
        {
            root = PrefabUtility.LoadPrefabContents(targetPath);
        }
        else
        {
            root = target;
            Undo.RegisterFullObjectHierarchyUndo(root, "Swap RPM Avatar");
        }

        GameObject src = (GameObject)Object.Instantiate(sourceAsset);
        src.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var srcRenderers = src.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var dstRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                                   .Where(IsBodyRenderer).ToList();
            if (srcRenderers.Length == 0 || dstRenderers.Count == 0)
            {
                EditorUtility.DisplayDialog("Swap RPM Avatar", "Couldn't find RPM skinned renderers on the target or the source.", "OK");
                return;
            }

            // Target skeleton: everything under the old body's root bone's top-most bone ancestor ("Armature"/"Hips").
            Transform dstRigRoot = FindRigRoot(dstRenderers[0]);
            Transform srcRigRoot = FindRigRoot(srcRenderers[0]);
            var dstBones = Index(dstRigRoot);
            var srcBones = Index(srcRigRoot);

            // 1) bone pose (so bind poses line up). Extra JUTPS children under bones are untouched.
            int posed = 0;
            foreach (var kv in srcBones)
            {
                if (!dstBones.TryGetValue(kv.Key, out var d)) continue;
                d.localPosition = kv.Value.localPosition;
                d.localRotation = kv.Value.localRotation;
                d.localScale = kv.Value.localScale;
                posed++;
            }

            // 2) renderers - matched by name with the Renderer_/Wolf3D_ prefix ignored
            bool usesRendererPrefix = dstRenderers.Any(r => r.name.StartsWith("Renderer_"));
            Transform rendererParent = dstRenderers[0].transform.parent;
            var matched = new HashSet<SkinnedMeshRenderer>();
            int swapped = 0, added = 0;
            foreach (var s in srcRenderers)
            {
                string key = Key(s.name);
                var d = dstRenderers.FirstOrDefault(r => Key(r.name) == key && !matched.Contains(r));
                if (d == null)
                {
                    var go = new GameObject(usesRendererPrefix ? "Renderer_" + key : s.name);
                    go.layer = dstRenderers[0].gameObject.layer;
                    go.transform.SetParent(rendererParent, false);
                    go.transform.localPosition = s.transform.localPosition;
                    go.transform.localRotation = s.transform.localRotation;
                    go.transform.localScale = s.transform.localScale;
                    if (!isAsset) Undo.RegisterCreatedObjectUndo(go, "Swap RPM Avatar");
                    d = go.AddComponent<SkinnedMeshRenderer>();
                    d.shadowCastingMode = dstRenderers[0].shadowCastingMode;
                    d.updateWhenOffscreen = dstRenderers[0].updateWhenOffscreen;
                    added++;
                }
                else swapped++;

                d.gameObject.SetActive(true);
                d.enabled = true;
                d.sharedMesh = s.sharedMesh;
                d.sharedMaterials = s.sharedMaterials;
                d.bones = s.bones.Select(b => b && dstBones.TryGetValue(b.name, out var t) ? t : null).ToArray();
                d.rootBone = s.rootBone && dstBones.TryGetValue(s.rootBone.name, out var rb) ? rb : dstRenderers[0].rootBone;
                d.localBounds = s.localBounds;
                if (s.sharedMesh)
                    for (int i = 0; i < s.sharedMesh.blendShapeCount; i++) d.SetBlendShapeWeight(i, s.GetBlendShapeWeight(i));
                matched.Add(d);

                int missing = d.bones.Count(b => b == null);
                if (missing > 0) Debug.LogWarning($"Swap RPM Avatar: {s.name} has {missing} bone(s) not found on the target rig.");
            }

            // 3) leftovers from the old avatar
            int hidden = 0;
            foreach (var d in dstRenderers)
                if (!matched.Contains(d)) { d.gameObject.SetActive(false); hidden++; }

            if (isAsset)
            {
                PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            }
            else
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                EditorSceneManager.MarkSceneDirty(root.scene);
                // Keep the white, tintable Fit Check materials on the clothing pieces (the source avatar brings textured ones).
                PimlrFitCheckMaterials.Restore(true);
            }

            Debug.Log($"Swap RPM Avatar: '{target.name}' now uses '{sourceAsset.name}'. Posed {posed} bones, swapped {swapped} renderer(s), added {added}, hid {hidden}.", target);
            EditorUtility.DisplayDialog("Swap RPM Avatar",
                $"Done. {swapped} swapped, {added} added, {hidden} hidden.\n\n" +
                (isAsset ? "Open the prefab to check it. Scene instances with their own renderer overrides keep those overrides - revert them if the old look still shows."
                         : "The scene is now modified - save it (Ctrl+S). Edit > Undo reverts the swap."),
                "OK");
        }
        finally
        {
            Object.DestroyImmediate(src);
            if (isAsset) PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool IsBodyRenderer(SkinnedMeshRenderer r) =>
        r.name.StartsWith("Renderer_") || r.name.StartsWith("Wolf3D") || r.name == "EyeLeft" || r.name == "EyeRight";

    // "Renderer_Outfit_Top" / "Wolf3D_Outfit_Top" -> "Outfit_Top"; "Renderer_EyeLeft" / "EyeLeft" -> "EyeLeft"
    static string Key(string n)
    {
        if (n.StartsWith("Renderer_")) return n.Substring("Renderer_".Length);
        if (n.StartsWith("Wolf3D_")) return n.Substring("Wolf3D_".Length);
        return n;
    }

    static Transform FindRigRoot(SkinnedMeshRenderer r)
    {
        Transform t = r.rootBone ? r.rootBone : (r.bones.Length > 0 ? r.bones[0] : r.transform);
        // climb to the highest ancestor that is still part of the rig (stop below the renderer's own parent)
        while (t.parent && t.parent != r.transform.parent && t.parent.GetComponent<Animator>() == null) t = t.parent;
        return t;
    }

    static Dictionary<string, Transform> Index(Transform rigRoot)
    {
        var map = new Dictionary<string, Transform>();
        foreach (var t in rigRoot.GetComponentsInChildren<Transform>(true))
            if (!map.ContainsKey(t.name)) map[t.name] = t;
        return map;
    }
}
