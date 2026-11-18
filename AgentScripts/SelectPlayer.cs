using System.Linq; using UnityEditor; using UnityEngine.SceneManagement;
public static class SelectPlayer { public static string Main() {
  var p = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
  Selection.activeGameObject = p; return p ? "selected Player" : "no Player"; } }
