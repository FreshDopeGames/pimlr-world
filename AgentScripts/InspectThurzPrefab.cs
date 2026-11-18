using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class InspectThurzPrefab
{
    public static string Main(string path)
    {
        var sb = new StringBuilder();
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            void Walk(Transform t, int d)
            {
                if (d > 4 && t.GetComponents<Component>().Length <= 1) return;
                var comps = t.GetComponents<Component>().Where(c => c && !(c is Transform)).Select(c => c.GetType().Name);
                if (d <= 3 || comps.Any())
                    sb.AppendLine(new string(' ', d * 2) + t.name + (comps.Any() ? "  [" + string.Join(",", comps) + "]" : ""));
                if (d < 3 || t.name.Contains("Armature") || t.name == "Hips" || t.GetComponents<Component>().Length > 1)
                    foreach (Transform c in t) Walk(c, d + 1);
            }
            Walk(root.transform, 0);
            var anim = root.GetComponentInChildren<Animator>(true);
            sb.AppendLine($"Animator on {anim?.name} avatar={anim?.avatar?.name} ctrl={anim?.runtimeAnimatorController?.name} applyRoot={anim?.applyRootMotion}");
            var smr = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in smr) sb.AppendLine($"SMR {r.name} parent={r.transform.parent.name} root={r.rootBone?.name} mats={string.Join("|", r.sharedMaterials.Select(m => m ? m.name + "/" + m.shader.name : "null"))}");
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb); var it = so.GetIterator(); var refs = new StringBuilder();
                while (it.NextVisible(true))
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is Transform tr && tr.IsChildOf(root.transform))
                        refs.Append($" {it.propertyPath}->{tr.name};");
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is Renderer rr && rr.transform.IsChildOf(root.transform))
                        refs.Append($" {it.propertyPath}->R:{rr.name};");
                }
                if (refs.Length > 0) sb.AppendLine($"MB {mb.GetType().Name}@{mb.name}:{refs}");
            }
            var col = root.GetComponent<CapsuleCollider>(); if (col) sb.AppendLine($"Capsule h={col.height} r={col.radius} c={col.center}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return sb.ToString();
    }
}
