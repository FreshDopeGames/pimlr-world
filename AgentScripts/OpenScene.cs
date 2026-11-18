using UnityEditor.SceneManagement; using UnityEngine.SceneManagement;
public static class OpenScene { public static string Main(string path) { if (SceneManager.GetActiveScene().isDirty) return "dirty, not switching"; EditorSceneManager.OpenScene(path, OpenSceneMode.Single); return "opened " + path; } }
