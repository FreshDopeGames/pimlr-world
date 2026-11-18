using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InspectSceneThurz
{
    static string P(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
    public static string Main()
    {
        var sb = new StringBuilder();
        var fbxPath = "Assets/_Game/Prefabs/Players/Thurz.fbx";
        var oldMeshes = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Mesh>().ToList();
        var scene = SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        foreach (var r in all.Where(r => r.sharedMesh && oldMeshes.Contains(r.sharedMesh)))
        {
            var anim = r.GetComponentInParent<Animator>(true);
            sb.AppendLine($"SMR {P(r.transform)} mesh={r.sharedMesh.name} root={(r.rootBone ? P(r.rootBone) : "null")} animator={(anim ? P(anim.transform) + " avatar=" + (anim.avatar ? anim.avatar.name : "null") : "none")} active={r.gameObject.activeInHierarchy}");
        }
        // list components on the animator object + changeColor users
        foreach (var a in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Animator>(true)).Where(a => a.avatar && a.avatar.name.Contains("Thurz")))
        {
            sb.AppendLine($"ANIM {P(a.transform)} comps=" + string.Join(",", a.GetComponents<Component>().Select(c => c ? c.GetType().Name : "missing")));
            var hips = a.GetBoneTransform(HumanBodyBones.Hips); sb.AppendLine($"  hips={(hips ? P(hips) : "null")} isHuman={a.isHuman}");
            foreach (Transform c in hips) { }
            var cap = a.GetComponent<CapsuleCollider>(); if (cap) sb.AppendLine($"  capsule h={cap.height} c={cap.center}");
            // children attached under bones that are NOT bones (weapons, props)
            var boneNames = new System.Collections.Generic.HashSet<string>(AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<GameObject>().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.name));
            foreach (var t in hips.GetComponentsInChildren<Transform>(true))
                if (!boneNames.Contains(t.name) && t.parent && boneNames.Contains(t.parent.name)) sb.AppendLine($"  attached: {P(t)}");
        }
        foreach (var mb in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(m => m && (m.GetType().Name == "changeColor" || m.GetType().Name == "Customization_Applier" || m.GetType().BaseType?.Name == "changeColor")))
        {
            var so = new SerializedObject(mb); var rs = so.FindProperty("renderers"); var d = so.FindProperty("defaultColorIndices");
            var names = rs == null ? "" : string.Join(",", Enumerable.Range(0, rs.arraySize).Select(i => rs.GetArrayElementAtIndex(i).objectReferenceValue ? rs.GetArrayElementAtIndex(i).objectReferenceValue.name : "null"));
            var defs = d == null ? "n/a" : string.Join(",", Enumerable.Range(0, d.arraySize).Select(i => d.GetArrayElementAtIndex(i).intValue));
            sb.AppendLine($"CC {mb.GetType().Name}@{P(mb.transform)} renderers=[{names}] defaults=[{defs}]");
        }
        return sb.ToString();
    }
}
