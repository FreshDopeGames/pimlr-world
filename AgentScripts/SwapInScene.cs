using System.Linq; using System.Text; using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine; using UnityEngine.SceneManagement;
public static class SwapInScene {
  static string P(Transform t){var s=t.name;while(t.parent){t=t.parent;s=t.name+"/"+s;}return s;}
  // mode: "inspect" | "swap" | "swapsave"
  public static string Main(string scenePath, string mode) {
    var sb = new StringBuilder();
    var cur = SceneManager.GetActiveScene();
    if (cur.path != scenePath) { if (cur.isDirty) return "active scene has unsaved changes: " + cur.path; EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single); }
    var scene = SceneManager.GetActiveScene();
    var oldMeshes = new System.Collections.Generic.HashSet<Mesh>(ThurzV3Swap.OldFbxs.SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Mesh>()));
    var newMeshes = oldMeshes;
    var anims = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
      .Where(r => r.sharedMesh && (oldMeshes.Contains(r.sharedMesh) || newMeshes.Contains(r.sharedMesh)))
      .Select(r => r.GetComponentInParent<Animator>(true)).Where(a => a).Distinct().ToList();
    foreach (var a in anims) sb.AppendLine($"Thurz model on: {P(a.transform)} active={a.gameObject.activeInHierarchy} avatar={(a.avatar ? a.avatar.name : "null")} scale={a.transform.lossyScale}");
    if (mode == "inspect") return sb.ToString();
    foreach (var a in anims) sb.AppendLine(ThurzV3Swap.Run(a.gameObject));
    if (mode == "swapsave") { EditorSceneManager.SaveScene(scene); sb.AppendLine("SAVED " + scene.path); }
    return sb.ToString();
  }
}
