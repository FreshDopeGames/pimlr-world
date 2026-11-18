using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PreviewThurzV2
{
    public static string Main()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Characters/Thurz_v2/Thurz_v2.fbx");
        var scene = EditorSceneManager.NewPreviewScene();
        var outDir = Path.GetFullPath("ArtSource/Characters/Thurz/Review/meshy");
        var sets = new[] {
            new[]{ "#FFFFFF","#FFFFFF","#FFFFFF","#FFFFFF" },
            new[]{ "#4B2A8C","#1E2A44","#4B2A8C","#1A1410" },
            new[]{ "#C8202A","#D9D4C7","#E8B923","#E9D9A6" },
        };
        string[] order = { "Wolf3D_Outfit_Top","Wolf3D_Outfit_Bottom","Wolf3D_Outfit_Footwear","Wolf3D_Hair" };
        var rt = new RenderTexture(800, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var tex = new Texture2D(800, 1000, TextureFormat.RGB24, false);
        var lightGO = new GameObject("Key"); var light = lightGO.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f;
        lightGO.transform.rotation = Quaternion.Euler(35, 150, 0); SceneManager_Move(lightGO, scene);
        var fill = new GameObject("Fill"); var fl = fill.AddComponent<Light>(); fl.type = LightType.Directional; fl.intensity = 0.6f;
        fill.transform.rotation = Quaternion.Euler(20, -60, 0); SceneManager_Move(fill, scene);
        var camGO = new GameObject("Cam"); var cam = camGO.AddComponent<Camera>(); SceneManager_Move(camGO, scene);
        cam.scene = scene; cam.orthographic = true; cam.orthographicSize = 1.1f; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.55f, 0.57f); cam.targetTexture = rt; cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
        cam.transform.position = new Vector3(-0.9f, 1.05f, 3.2f); cam.transform.LookAt(new Vector3(0, 1.0f, 0));
        string log = "";
        for (int s = 0; s < sets.Length; s++)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            inst.transform.rotation = Quaternion.Euler(0, 0, 0);
            var anim = inst.GetComponent<Animator>(); if (anim) anim.enabled = false;
            for (int i = 0; i < order.Length; i++)
            {
                var t = FindDeep(inst.transform, order[i]); if (t == null) { log += "missing " + order[i] + "\n"; continue; }
                ColorUtility.TryParseHtmlString(sets[s][i], out var c);
                var r = t.GetComponent<Renderer>(); var m = new Material(r.sharedMaterial); m.color = c; r.sharedMaterial = m;
            }
            cam.Render();
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 800, 1000), 0, 0); tex.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(outDir, $"unity_tint_{s}.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(inst);
        }
        EditorSceneManager.ClosePreviewScene(scene);
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        return log + "done -> " + outDir;
    }
    static void SceneManager_Move(GameObject g, UnityEngine.SceneManagement.Scene s) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, s);
    static Transform FindDeep(Transform t, string n) { if (t.name == n) return t; foreach (Transform c in t) { var f = FindDeep(c, n); if (f) return f; } return null; }
}
