// PIMLR World — swaps the in-scene Thurz player model to Thurz v3 (quad body + 606K head/hair, LOD0/LOD1).
// Keeps the scene's Player object, renderer names (PlayerPrefs color keys), components and anything parented to bones.
// Menu: Tools > PIMLR > Swap Player Model to Thurz v3 (select the Player root first, or it finds the Animator using the old Thurz meshes).
// Fully Undo-able (Ctrl+Z). Does NOT save the scene.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ThurzV3Swap
{
    public const string NewFbx = "Assets/_Game/Characters/Thurz_v4/Thurz_v4.fbx"; // v4 (Nov 18): single atlas, T-pose Meshy quad remesh
    public static readonly string[] OldFbxs = { "Assets/_Game/Prefabs/Players/Thurz.fbx", "Assets/_Game/Characters/Thurz_v2/Thurz_v2.fbx", "Assets/_Game/Characters/Thurz_v3/Thurz_v3.fbx", "Assets/_Game/Characters/Thurz_v4/Thurz_v4.fbx" };
    public const string AvatarDir = "Assets/_Game/Characters/Thurz_v4";
    public const float Lod0ScreenHeight = 0.30f;  // LOD1 (≈22k tris) below 30% of screen height
    // scene renderer suffix -> new FBX renderer name
    static readonly Dictionary<string, string> PartMap = new Dictionary<string, string> {
        {"Outfit_Top","Wolf3D_Outfit_Top"},{"Outfit_Bottom","Wolf3D_Outfit_Bottom"},{"Outfit_Footwear","Wolf3D_Outfit_Footwear"},
        {"Hair","Wolf3D_Hair"},{"Head","Wolf3D_Head"},{"Body","Wolf3D_Body"} };
    static readonly string[] HideParts = { "Teeth", "EyeLeft", "EyeRight" };
    // PimlrPalette indices: renderers order in changeColor = Top, Hair, Bottom, Footwear
    public static readonly int[] DefaultColors = { 9, 3, 6, 9 }; // Purple jacket, Black locs, Navy jeans, Purple shoe accents

    [MenuItem("Tools/PIMLR/Swap Player Model to Thurz v3")]
    static void MenuSwap() { Debug.Log(Run(Selection.activeGameObject)); }

    static string Key(string n) { foreach (var k in PartMap.Keys.Concat(HideParts)) if (n.EndsWith(k)) return k; return null; }

    public static string Run(GameObject playerRoot = null)
    {
        var log = new StringBuilder();
        var newModel = AssetDatabase.LoadAssetAtPath<GameObject>(NewFbx);
        var oldMeshes = new HashSet<Mesh>(OldFbxs.SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Mesh>()));
        if (!newModel) return "Thurz v4 assets missing";

        Animator anim = playerRoot ? playerRoot.GetComponentInParent<Animator>(true) ?? playerRoot.GetComponentInChildren<Animator>(true) : null;
        if (!anim)
        {
            var smr = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                .FirstOrDefault(r => r.sharedMesh && oldMeshes.Contains(r.sharedMesh));
            if (smr) anim = smr.GetComponentInParent<Animator>(true);
        }
        if (!anim) return "No player Animator using a Thurz model found in the active scene.";
        var root = anim.transform;
        log.AppendLine("Player: " + root.name);

        // temp instance of the new model to read its rest pose
        var src = (GameObject)Object.Instantiate(newModel);
        src.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            src.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); src.transform.localScale = Vector3.one;
            var srcArm = src.transform.Find("Armature"); var srcHips = srcArm ? srcArm.Find("Hips") : null;
            var dstHips = FindDeep(root, "Hips"); var dstArm = dstHips ? dstHips.parent : null;
            if (!srcHips || !dstHips) return "Hips not found (src " + (srcHips != null) + ", dst " + (dstHips != null) + ")";

            Undo.SetCurrentGroupName("Swap Player Model to Thurz v3");
            int group = Undo.GetCurrentGroup();

            // 1) skeleton rest pose: Armature node + every bone by name
            if (dstArm != root)
            {
                Undo.RecordObject(dstArm, "arm"); dstArm.localPosition = srcArm.localPosition; dstArm.localRotation = srcArm.localRotation; dstArm.localScale = srcArm.localScale;
            }
            var dstBones = dstHips.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            int moved = 0; var missing = new List<string>();
            foreach (var s in srcHips.GetComponentsInChildren<Transform>(true))
            {
                if (!dstBones.TryGetValue(s.name, out var d)) { missing.Add(s.name); continue; }
                Undo.RecordObject(d, "bone"); d.localPosition = s.localPosition; d.localRotation = s.localRotation; d.localScale = s.localScale; moved++;
            }
            log.AppendLine($"bones set to v3 rest pose: {moved}" + (missing.Count > 0 ? "  missing in scene: " + string.Join(",", missing) : ""));

            // 2) renderers
            var srcR = src.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r => r.name);
            var lod0 = new List<Renderer>(); var lod1 = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(x => !x.name.EndsWith("_LOD1")).ToList())
            {
                var k = Key(r.name); if (k == null) continue;
                if (HideParts.Contains(k)) { Undo.RecordObject(r.gameObject, "hide"); r.gameObject.SetActive(false); log.AppendLine("hidden " + r.name); continue; }
                if (!srcR.TryGetValue(PartMap[k], out var s)) { log.AppendLine("no v3 part for " + r.name); continue; }
                Undo.RecordObject(r, "smr"); Undo.RecordObject(r.transform, "smrT");
                                r.transform.localPosition = s.transform.localPosition; r.transform.localRotation = s.transform.localRotation; r.transform.localScale = s.transform.localScale;
                r.sharedMesh = s.sharedMesh;
                r.bones = s.bones.Select(b => b && dstBones.TryGetValue(b.name, out var d) ? d : null).ToArray();
                r.rootBone = s.rootBone && dstBones.TryGetValue(s.rootBone.name, out var rb) ? rb : dstHips;
                r.sharedMaterials = s.sharedMaterials;
                r.localBounds = s.localBounds;
                int nulls = r.bones.Count(b => b == null);
                log.AppendLine($"{r.name} <- {s.sharedMesh.name} ({s.sharedMesh.triangles.Length / 3} tris){(nulls > 0 ? "  NULL BONES " + nulls : "")}");
                lod0.Add(r);
                // distance LOD twin: "<scene renderer>_LOD1" next to it
                if (srcR.TryGetValue(PartMap[k] + "_LOD1", out var s1))
                {
                    var t1 = r.transform.parent.Find(r.name + "_LOD1");
                    SkinnedMeshRenderer r1;
                    if (t1 == null)
                    {
                        var go = new GameObject(r.name + "_LOD1"); Undo.RegisterCreatedObjectUndo(go, "lod1");
                        go.transform.SetParent(r.transform.parent, false); go.layer = r.gameObject.layer; go.tag = r.gameObject.tag;
                        r1 = go.AddComponent<SkinnedMeshRenderer>();
                    }
                    else { r1 = t1.GetComponent<SkinnedMeshRenderer>(); Undo.RecordObject(r1, "smr1"); Undo.RecordObject(t1, "smr1T"); }
                    r1.transform.localPosition = s1.transform.localPosition; r1.transform.localRotation = s1.transform.localRotation; r1.transform.localScale = s1.transform.localScale;
                    r1.sharedMesh = s1.sharedMesh;
                    r1.bones = s1.bones.Select(b => b && dstBones.TryGetValue(b.name, out var d) ? d : null).ToArray();
                    r1.rootBone = r.rootBone; r1.sharedMaterials = s1.sharedMaterials; r1.localBounds = s1.localBounds;
                    r1.shadowCastingMode = r.shadowCastingMode; r1.receiveShadows = r.receiveShadows; r1.updateWhenOffscreen = r.updateWhenOffscreen; r1.quality = r.quality;
                    lod1.Add(r1);
                }
            }
            // LOD Group on the player root
            var lg = root.GetComponent<LODGroup>(); if (!lg) lg = Undo.AddComponent<LODGroup>(root.gameObject); else Undo.RecordObject(lg, "lod");
            lg.SetLODs(new[] { new LOD(Lod0ScreenHeight, lod0.ToArray()), new LOD(0.0005f, lod1.ToArray()) });
            lg.RecalculateBounds();
            log.AppendLine($"LODGroup: LOD0 {lod0.Count} renderers (> {Lod0ScreenHeight:P0} screen height), LOD1 {lod1.Count} renderers");
            if (!root.GetComponent<PimlrProneVisualOffset>()) { Undo.AddComponent<PimlrProneVisualOffset>(root.gameObject); log.AppendLine("added PimlrProneVisualOffset (prone floor clearance)"); }

            // 3) humanoid avatar built from a bare copy of the ACTIVE hierarchy at the v3 rest pose
            //    (skips inactive leftovers, e.g. a disabled second "Armature", which would make bone names ambiguous)
            log.Append(BuildAndAssignAvatar(anim));

            // 4) default colors for the picker
            foreach (var mb in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (!mb) continue; var so = new SerializedObject(mb); var p = so.FindProperty("defaultColorIndices");
                if (p == null || !p.isArray) continue;
                Undo.RecordObject(mb, "defaults");
                p.arraySize = DefaultColors.Length; for (int i = 0; i < DefaultColors.Length; i++) p.GetArrayElementAtIndex(i).intValue = DefaultColors[i];
                so.ApplyModifiedProperties(); log.AppendLine($"defaults [9,3,6,9] -> {mb.GetType().Name}@{mb.name}");
            }
            // GPU skinning buffers are sized for the old mesh until the renderer re-initializes
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            { var m = r.sharedMesh; var b = r.bones; r.enabled = false; r.sharedMesh = null; r.sharedMesh = m; r.bones = b; r.enabled = true; }
            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        finally { Object.DestroyImmediate(src); }
        log.AppendLine("Done. Scene NOT saved — review, Play-test, then File > Save (or Ctrl+Z to undo).");
        return log.ToString();
    }

    public static string BuildAndAssignAvatar(Animator anim)
    {
        var root = anim.transform;
        var srcAvatar = AssetDatabase.LoadAllAssetsAtPath(NewFbx).OfType<Avatar>().First();
        var hd = srcAvatar.humanDescription;
        var bare = new GameObject(root.name); bare.hideFlags = HideFlags.HideAndDontSave;
        var sk = new List<SkeletonBone>();
        void Copy(Transform s, Transform parent)
        {
            if (!s.gameObject.activeSelf) return;
            var d = parent == null ? bare.transform : new GameObject(s.name).transform;
            if (parent != null) { d.hideFlags = HideFlags.HideAndDontSave; d.SetParent(parent, false); }
            bool isRoot = parent == null; // avatar space = Player's local space (ignore scene placement and the 2x display scale)
            var lp = isRoot ? Vector3.zero : s.localPosition; var lr = isRoot ? Quaternion.identity : s.localRotation; var ls = isRoot ? Vector3.one : s.localScale;
            d.localPosition = lp; d.localRotation = lr; d.localScale = ls;
            sk.Add(new SkeletonBone { name = s.name, position = lp, rotation = lr, scale = ls });
            foreach (Transform c in s) Copy(c, d);
        }
        try
        {
            Copy(root, null);
            hd.skeleton = sk.ToArray();
            var av = AvatarBuilder.BuildHumanAvatar(bare, hd);
            av.name = "Thurz_v4_" + SceneManager.GetActiveScene().name + "_Avatar";
            if (!av.isValid) return "avatar build FAILED (see console)\n";
            var path = $"{AvatarDir}/{av.name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Avatar>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(av, path); AssetDatabase.SaveAssets();
            Undo.RecordObject(anim, "avatar"); anim.avatar = av;
            EditorUtility.SetDirty(anim);
            return $"Animator avatar -> {path} (valid={av.isValid}, human={av.isHuman})\n";
        }
        finally { Object.DestroyImmediate(bare); }
    }

    [MenuItem("Tools/PIMLR/Rebuild Thurz v3 Avatar for Player")]
    static void MenuAvatar()
    {
        var a = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<Animator>(true) : null;
        if (!a) a = SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.GetComponent<Animator>()).FirstOrDefault(x => x && x.name == "Player");
        Debug.Log(a ? BuildAndAssignAvatar(a) : "Select the Player first.");
    }

    static Transform FindDeep(Transform t, string n) { if (t.name == n) return t; foreach (Transform c in t) { var f = FindDeep(c, n); if (f) return f; } return null; }
}
