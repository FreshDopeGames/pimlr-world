using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PIMLR: swap the Ready Player Me body on an enemy/NPC prefab for another downloaded RPM avatar, keeping
/// everything JUTPS hangs off the skeleton (weapon positions, IK targets, ragdoll, health bar, AI...).
/// Why the Inspector couldn't do it: the boss prefabs were built from the same .glb as Big Boss, and a skinned
/// mesh can't just be dragged in - its bones, bind pose and materials all have to come across together.
///
/// What it does, by bone/renderer NAME (all RPM avatars share the same rig names):
///   1. copies the new avatar's bone pose onto the target skeleton (so the new mesh's bind pose fits),
///   2. points every Renderer_* at the new avatar's mesh + materials (adds Hair/Beard etc. if the old one lacked them),
///   3. hides Renderer_* pieces the new avatar doesn't have.
/// Menu: PIMLR / Characters / Swap RPM Avatar...
/// </summary>
public class PimlrAvatarSwapper : EditorWindow
{
    const string DefaultTarget = "Assets/_Game/Prefabs/WaterBallonBoss.prefab";
    const string DefaultSourceGuidPath = "Assets/Ready Player Me/Avatars/64e634111dfe4c3936791db2/2fac66e374c947c41bc74325c6e3d934/64e634111dfe4c3936791db2.prefab";

    GameObject targetPrefab;
    GameObject sourceAvatar;

    [MenuItem("PIMLR/Characters/Swap RPM Avatar...")]
    static void Open()
    {
        var w = GetWindow<PimlrAvatarSwapper>(true, "Swap RPM Avatar");
        w.minSize = new Vector2(460, 170);
        w.targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTarget);
        w.sourceAvatar = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSourceGuidPath);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Target = the character prefab to re-skin (e.g. WaterBallonBoss).\nSource = a downloaded RPM avatar prefab (Assets/Ready Player Me/Avatars/<id>/.../<id>.prefab) or its .glb.", MessageType.Info);
        targetPrefab = (GameObject)EditorGUILayout.ObjectField("Target prefab", targetPrefab, typeof(GameObject), false);
        sourceAvatar = (GameObject)EditorGUILayout.ObjectField("Source RPM avatar", sourceAvatar, typeof(GameObject), false);
        GUI.enabled = targetPrefab && sourceAvatar;
        if (GUILayout.Button("Swap", GUILayout.Height(30)))
            Swap(targetPrefab, sourceAvatar);
        GUI.enabled = true;
    }

    public static void Swap(GameObject targetAsset, GameObject sourceAsset)
    {
        string targetPath = AssetDatabase.GetAssetPath(targetAsset);
        if (string.IsNullOrEmpty(targetPath)) { Debug.LogError("Target must be a prefab asset."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(targetPath);
        GameObject src = (GameObject)Object.Instantiate(sourceAsset);
        src.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var srcRenderers = src.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var dstRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                                   .Where(r => r.name.StartsWith("Renderer_") || r.name.StartsWith("Wolf3D")).ToList();
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

            // 2) renderers
            Transform rendererParent = dstRenderers[0].transform.parent;
            var matched = new HashSet<SkinnedMeshRenderer>();
            int swapped = 0, added = 0;
            foreach (var s in srcRenderers)
            {
                var d = dstRenderers.FirstOrDefault(r => r.name == s.name);
                if (d == null)
                {
                    var go = new GameObject(s.name);
                    go.layer = dstRenderers[0].gameObject.layer;
                    go.transform.SetParent(rendererParent, false);
                    go.transform.localPosition = s.transform.localPosition;
                    go.transform.localRotation = s.transform.localRotation;
                    go.transform.localScale = s.transform.localScale;
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

            PrefabUtility.SaveAsPrefabAsset(root, targetPath);
            Debug.Log($"Swap RPM Avatar: '{targetAsset.name}' now uses '{sourceAsset.name}'. Posed {posed} bones, swapped {swapped} renderer(s), added {added}, hid {hidden}.", targetAsset);
            EditorUtility.DisplayDialog("Swap RPM Avatar",
                $"Done. {swapped} swapped, {added} added, {hidden} hidden.\n\nOpen the prefab to check it. Scene instances with their own renderer overrides keep those overrides - revert them if the old look still shows.",
                "OK");
        }
        finally
        {
            Object.DestroyImmediate(src);
            PrefabUtility.UnloadPrefabContents(root);
        }
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
