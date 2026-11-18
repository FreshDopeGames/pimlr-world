using System.Linq; using UnityEngine; using UnityEngine.SceneManagement;
public static class RefreshSMR { public static string Main() {
  var p = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Player"); int n = 0;
  foreach (var r in p.GetComponentsInChildren<SkinnedMeshRenderer>(false)) {
    var m = r.sharedMesh; var b = r.bones; r.enabled = false; r.sharedMesh = null; r.sharedMesh = m; r.bones = b; r.enabled = true; n++; }
  UnityEditor.SceneView.RepaintAll(); return "refreshed " + n; } }
