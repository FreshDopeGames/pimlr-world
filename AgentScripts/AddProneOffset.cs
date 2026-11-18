using System.Linq; using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine; using UnityEngine.SceneManagement;
public static class AddProneOffset { public static string Main(string scenePath) {
  var cur = SceneManager.GetActiveScene(); if (cur.path != scenePath) { if (cur.isDirty) return "dirty: " + cur.path; EditorSceneManager.OpenScene(scenePath); }
  var s = SceneManager.GetActiveScene(); string log = "";
  foreach (var lg in s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LODGroup>(true)))
  { var a = lg.GetComponent<Animator>(); if (!a) continue;
    if (!lg.GetComponent<PimlrProneVisualOffset>()) { lg.gameObject.AddComponent<PimlrProneVisualOffset>(); log += "added on " + lg.name + "\n"; } else log += "already on " + lg.name + "\n";
    foreach (var r in lg.GetComponentsInChildren<SkinnedMeshRenderer>(false)) { var m = r.sharedMesh; var b = r.bones; r.enabled = false; r.sharedMesh = null; r.sharedMesh = m; r.bones = b; r.enabled = true; } }
  EditorSceneManager.MarkSceneDirty(s); EditorSceneManager.SaveScene(s); return log + "saved " + s.path; } }
