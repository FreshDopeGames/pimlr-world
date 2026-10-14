using UnityEditor;
using UnityEngine;

/// <summary>
/// PIMLR: fixes meshes that were authored inside-out (normals and triangle winding pointing INTO the surface).
/// Ready Player Me's dreadlock hair (Wolf3D_Hair) is like this - ~90% of its normals point inward - and RPM hides
/// it by marking the material double-sided. With Render Face = Front you see the inside of every strand.
///
/// Menu: PIMLR > Art > Flip Inside-Out Mesh (selected renderer, default: WaterBallonBoss hair)
///   Makes a corrected copy of the mesh (winding reversed, normals + blend-shape normals flipped, skinning and
///   blend shapes kept), saves it under Assets/_Game/Art/Fixes and assigns it to the renderer. Then set the hair
///   material's Render Face to Front. Run it again on the fixed mesh to flip back.
/// </summary>
public static class PimlrMeshInsideOutFixer
{
    const string DefaultPrefab = "Assets/_Game/Prefabs/Enemies/WaterBallonBoss.prefab";
    const string DefaultRendererName = "Renderer_Hair";
    const string OutFolder = "Assets/_Game/Art/Fixes";

    [MenuItem("PIMLR/Art/Flip Inside-Out Mesh (selected renderer, default: WaterBallonBoss hair)")]
    static void Run()
    {
        // A renderer selected in a scene or open prefab: fix that one directly.
        var go = Selection.activeGameObject;
        if (go != null && !EditorUtility.IsPersistent(go) && TryFix(go, out string msg))
        {
            EditorUtility.SetDirty(go);
            Report(msg);
            return;
        }

        // Otherwise: the selected prefab asset (or the bomber), find the hair renderer inside it.
        string path = DefaultPrefab;
        if (go != null && EditorUtility.IsPersistent(go)) path = AssetDatabase.GetAssetPath(go);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform target = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == DefaultRendererName || t.name == "Wolf3D_Hair") { target = t; break; }
            if (target == null) { Report($"No '{DefaultRendererName}' found in {path}. Select the renderer itself and run again."); return; }
            if (TryFix(target.gameObject, out string m2))
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Report(m2 + $"\nSaved into {System.IO.Path.GetFileName(path)}.");
            }
            else Report(m2);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void Report(string msg)
    {
        Debug.Log("PIMLR inside-out fix: " + msg);
        EditorUtility.DisplayDialog("PIMLR inside-out mesh fix", msg, "OK");
    }

    static bool TryFix(GameObject go, out string message)
    {
        var smr = go.GetComponent<SkinnedMeshRenderer>();
        var mf = go.GetComponent<MeshFilter>();
        Mesh src = smr ? smr.sharedMesh : (mf ? mf.sharedMesh : null);
        if (src == null) { message = $"'{go.name}' has no mesh."; return false; }

        Mesh flipped = Flip(src);
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Art")) AssetDatabase.CreateFolder("Assets/_Game", "Art");
        if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder("Assets/_Game/Art", "Fixes");
        string baseName = src.name.EndsWith("_Flipped") ? src.name.Substring(0, src.name.Length - 8) : src.name + "_Flipped";
        flipped.name = baseName;
        string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{OutFolder}/{baseName}.asset");
        AssetDatabase.CreateAsset(flipped, meshPath);
        AssetDatabase.SaveAssets();
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        if (smr) { Undo.RecordObject(smr, "Flip inside-out mesh"); smr.sharedMesh = saved; }
        else { Undo.RecordObject(mf, "Flip inside-out mesh"); mf.sharedMesh = saved; }

        message = $"'{go.name}' now uses {meshPath} (winding reversed, normals flipped). " +
                  "Set its material's Render Face to Front.";
        return true;
    }

    static Mesh Flip(Mesh src)
    {
        Mesh m = Object.Instantiate(src);   // keeps bind poses, bone weights, UVs, sub-meshes, blend shapes

        for (int s = 0; s < m.subMeshCount; s++)
        {
            int[] tri = m.GetTriangles(s);
            for (int i = 0; i + 2 < tri.Length; i += 3) { int t = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = t; }
            m.SetTriangles(tri, s, false);
        }

        Vector3[] n = m.normals;
        for (int i = 0; i < n.Length; i++) n[i] = -n[i];
        if (n.Length > 0) m.normals = n;

        // Flipping the normal flips the bitangent too; flip the tangent's sign so normal maps still read right.
        Vector4[] tg = m.tangents;
        for (int i = 0; i < tg.Length; i++) tg[i].w = -tg[i].w;
        if (tg.Length > 0) m.tangents = tg;

        // Blend shapes carry their own normal deltas - flip those as well.
        if (src.blendShapeCount > 0)
        {
            m.ClearBlendShapes();
            int vc = src.vertexCount;
            var dv = new Vector3[vc]; var dn = new Vector3[vc]; var dt = new Vector3[vc];
            for (int b = 0; b < src.blendShapeCount; b++)
            {
                string name = src.GetBlendShapeName(b);
                for (int f = 0; f < src.GetBlendShapeFrameCount(b); f++)
                {
                    src.GetBlendShapeFrameVertices(b, f, dv, dn, dt);
                    for (int i = 0; i < vc; i++) dn[i] = -dn[i];
                    m.AddBlendShapeFrame(name, src.GetBlendShapeFrameWeight(b, f), dv, dn, dt);
                }
            }
        }

        m.RecalculateBounds();
        return m;
    }
}
