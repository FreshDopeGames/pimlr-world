using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PIMLR: fixes Z-fighting baked into a model - triangles lying on exactly the same surface as other triangles
/// (crosswalk / lane markings modelled flush on the road in New Gen Urban's StreetSet.fbx, used by
/// Street_WideCrossing). The GPU can't decide which is in front, so they flicker.
///
/// Menu: PIMLR > Art > Fix Z-Fighting On Selected Prefab (default: Street_WideCrossing)
///   1. Finds triangles that sit on top of a LARGER triangle (same plane within 2 cm, centre inside it).
///   2. Copies the mesh and lifts those smaller "decal" triangles 1 cm along their normal (own vertices,
///      so nothing else moves).
///   3. Saves the copy under Assets/_Game/Art/Fixes and points the prefab's MeshFilter at it - every placed
///      instance updates. The MeshCollider keeps the original mesh, so walking / NavMesh are unchanged.
/// Safe to run again (it always starts from the original mesh). Undo = set the MeshFilter back to the FBX mesh.
/// </summary>
public static class PimlrZFightFixer
{
    const string DefaultPrefab = "Assets/Reversed Interactive/New Gen Urban/Prefabs/Premade Plots/Street_WideCrossing.prefab";
    const string OutFolder = "Assets/_Game/Art/Fixes";
    const float PlaneTolerance = 0.02f;   // metres: "same surface"
    const float Lift = 0.01f;             // metres: how far the upper layer is raised
    const float NormalDot = 0.98f;

    [MenuItem("PIMLR/Art/Fix Z-Fighting On Selected Prefab (default: Street_WideCrossing)")]
    static void Run()
    {
        string path = DefaultPrefab;
        var sel = Selection.activeObject;
        if (sel != null && PrefabUtility.IsPartOfPrefabAsset(sel)) path = AssetDatabase.GetAssetPath(sel);

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int fixedFilters = 0, lifted = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(mf.gameObject) != null) continue; // nested props (bins, lights): leave alone
                Mesh source = OriginalMesh(mf.sharedMesh);   // (mesh data is readable from editor scripts)
                if (source == null) continue;

                Mesh fixedMesh = BuildFixed(source, out int count);
                if (count == 0) continue;
                lifted += count;

                if (!AssetDatabase.IsValidFolder("Assets/_Game/Art")) AssetDatabase.CreateFolder("Assets/_Game", "Art");
                if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder("Assets/_Game/Art", "Fixes");
                string meshPath = $"{OutFolder}/{source.name}_NoZFight.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(fixedMesh, meshPath);
                mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                fixedFilters++;
            }

            if (fixedFilters > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            string msg = fixedFilters > 0
                ? $"Lifted {lifted} overlapping triangles on {fixedFilters} mesh(es) in {Path.GetFileName(path)}. Every placed instance now uses the fixed mesh."
                : $"No coplanar overlaps found in {Path.GetFileName(path)} - the flicker is coming from something else.";
            Debug.Log("PIMLR Z-fight fix: " + msg);
            EditorUtility.DisplayDialog("PIMLR Z-fighting fix", msg, "OK");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>If the filter already points at one of our fixed copies, start again from the FBX mesh.</summary>
    static Mesh OriginalMesh(Mesh m)
    {
        if (m == null) return null;
        string p = AssetDatabase.GetAssetPath(m);
        if (!p.StartsWith(OutFolder)) return m;
        string want = m.name.Replace("_NoZFight", "");
        foreach (string guid in AssetDatabase.FindAssets(want + " t:Mesh"))
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                if (o is Mesh mm && mm.name == want && !AssetDatabase.GetAssetPath(mm).StartsWith(OutFolder)) return mm;
        return m;
    }

    struct Tri { public int sub, a, b, c; public Vector3 n, centre; public float area; }

    static Mesh BuildFixed(Mesh src, out int liftedCount)
    {
        Vector3[] v = src.vertices;
        var tris = new List<Tri>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] idx = src.GetTriangles(s);
            for (int i = 0; i + 2 < idx.Length; i += 3)
            {
                Vector3 p0 = v[idx[i]], p1 = v[idx[i + 1]], p2 = v[idx[i + 2]];
                Vector3 cr = Vector3.Cross(p1 - p0, p2 - p0);
                float area = cr.magnitude * 0.5f;
                if (area < 1e-6f) continue;
                tris.Add(new Tri { sub = s, a = idx[i], b = idx[i + 1], c = idx[i + 2], n = cr.normalized, centre = (p0 + p1 + p2) / 3f, area = area });
            }
        }

        // Spatial hash on the two axes across the dominant normal so the overlap test stays fast.
        Vector3 avgN = Vector3.zero; foreach (var t in tris) avgN += t.n * t.area;
        int up = Mathf.Abs(avgN.x) > Mathf.Abs(avgN.y) ? (Mathf.Abs(avgN.x) > Mathf.Abs(avgN.z) ? 0 : 2) : (Mathf.Abs(avgN.y) > Mathf.Abs(avgN.z) ? 1 : 2);
        int ax = up == 0 ? 1 : 0, az = up == 2 ? 1 : 2;
        const float cell = 1f;
        var grid = new Dictionary<long, List<int>>();
        for (int i = 0; i < tris.Count; i++)
        {
            Vector3 p0 = v[tris[i].a], p1 = v[tris[i].b], p2 = v[tris[i].c];
            int x0 = Mathf.FloorToInt(Mathf.Min(p0[ax], Mathf.Min(p1[ax], p2[ax])) / cell), x1 = Mathf.FloorToInt(Mathf.Max(p0[ax], Mathf.Max(p1[ax], p2[ax])) / cell);
            int z0 = Mathf.FloorToInt(Mathf.Min(p0[az], Mathf.Min(p1[az], p2[az])) / cell), z1 = Mathf.FloorToInt(Mathf.Max(p0[az], Mathf.Max(p1[az], p2[az])) / cell);
            for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
            {
                long key = ((long)x << 32) ^ (uint)z;
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                list.Add(i);
            }
        }

        var lift = new bool[tris.Count];
        for (int i = 0; i < tris.Count; i++)
        {
            Tri t = tris[i];
            long key = ((long)Mathf.FloorToInt(t.centre[ax] / cell) << 32) ^ (uint)Mathf.FloorToInt(t.centre[az] / cell);
            if (!grid.TryGetValue(key, out var cands)) continue;
            foreach (int j in cands)
            {
                if (j == i) continue;
                Tri u = tris[j];
                // t is the upper "decal" layer if it's smaller (or equal size but later), on u's plane, inside u.
                if (t.area > u.area * 1.0001f || (Mathf.Approximately(t.area, u.area) && i < j)) continue;
                if (Vector3.Dot(t.n, u.n) < NormalDot) continue;
                Vector3 u0 = v[u.a];
                if (Mathf.Abs(Vector3.Dot(t.centre - u0, u.n)) > PlaneTolerance) continue;
                if (!InsideTriangle(t.centre, u0, v[u.b], v[u.c], u.n)) continue;
                lift[i] = true; break;
            }
        }

        // New mesh: lifted triangles get their own vertex copies, raised along their normal.
        var verts = new List<Vector3>(v);
        var normals = new List<Vector3>(src.normals);
        var tangents = new List<Vector4>(src.tangents);
        var uvLists = new List<List<Vector2>>();
        for (int ch = 0; ch < 4; ch++) { var l = new List<Vector2>(); src.GetUVs(ch, l); uvLists.Add(l); }
        var colors = new List<Color>(src.colors);
        var subs = new List<List<int>>();
        for (int s = 0; s < src.subMeshCount; s++) subs.Add(new List<int>());

        liftedCount = 0;
        for (int i = 0; i < tris.Count; i++)
        {
            Tri t = tris[i];
            if (!lift[i]) { subs[t.sub].Add(t.a); subs[t.sub].Add(t.b); subs[t.sub].Add(t.c); continue; }
            liftedCount++;
            foreach (int oi in new[] { t.a, t.b, t.c })
            {
                int ni = verts.Count;
                verts.Add(v[oi] + t.n * Lift);
                if (normals.Count > 0) normals.Add(normals[oi]);
                if (tangents.Count > 0) tangents.Add(tangents[oi]);
                foreach (var l in uvLists) if (l.Count > 0) l.Add(l[oi]);
                if (colors.Count > 0) colors.Add(colors[oi]);
                subs[t.sub].Add(ni);
            }
        }

        var m = new Mesh { name = src.name + "_NoZFight" };
        if (verts.Count > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts);
        if (normals.Count == verts.Count) m.SetNormals(normals);
        if (tangents.Count == verts.Count) m.SetTangents(tangents);
        for (int ch = 0; ch < 4; ch++) if (uvLists[ch].Count == verts.Count) m.SetUVs(ch, uvLists[ch]);
        if (colors.Count == verts.Count) m.SetColors(colors);
        m.subMeshCount = subs.Count;
        for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
        m.RecalculateBounds();
        if (normals.Count != verts.Count) m.RecalculateNormals();
        return m;
    }

    static bool InsideTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, Vector3 n)
    {
        // Same-side test on the triangle's plane, with a small inward margin so shared edges don't count.
        bool S(Vector3 x, Vector3 y) => Vector3.Dot(Vector3.Cross(y - x, p - x), n) > 1e-5f;
        return S(a, b) && S(b, c) && S(c, a);
    }
}
