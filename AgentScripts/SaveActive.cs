using UnityEditor.SceneManagement; using UnityEngine.SceneManagement;
public static class SaveActive { public static string Main(string expect) { var s = SceneManager.GetActiveScene(); if (s.path != expect) return "active is " + s.path; EditorSceneManager.SaveScene(s); return "SAVED " + s.path; } }
