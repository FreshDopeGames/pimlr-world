using UnityEngine; using UnityEditor.SceneManagement;
public static class InspectAnim {
  public static string Main(string scene) {
    var s = EditorSceneManager.GetActiveScene(); if (s.path != scene) { if (s.isDirty) return "active scene dirty"; EditorSceneManager.OpenScene(scene); }
    var p = Object.FindAnyObjectByType<PimlrProneVisualOffset>(); if (!p) return "none";
    var a = p.GetComponent<Animator>(); var rb = p.GetComponent<Rigidbody>();
    return $"{p.name} updateMode={a.updateMode} cull={a.cullingMode} applyRootMotion={a.applyRootMotion} rb={(rb? rb.interpolation.ToString():"none")} lift={p.proneLift}";
  }
}
