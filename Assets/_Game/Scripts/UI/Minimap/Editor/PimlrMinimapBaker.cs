// PIMLR #MAP: bakes a flat 2D top-down map of the open scene for PimlrMinimap2D.
// PIMLR > Minimap > Bake 2D Map (Open Scene)  ->  Assets/_Game/Textures/Minimap/<Scene>_Minimap.png
//                                               + Assets/_Game/Resources/PIMLR/Minimap/<Scene>.asset
// One ray straight down per texel, classified by what it hits:
//   road (material "Road")  /  sidewalk & plazas (paths, pavement, concrete, "Ground")  /  park (grass, parks)
//   / building (anything taller than 2.5 m, shaded slightly by height, with a light outline) / nothing = background.
// Rays start 400 m up, so a building is drawn by its roof footprint (the old live minimap camera sat 70 m up,
// under the skyscraper roofs, and showed their ground floors as open plazas).
// Props, cars and benches are looked through (the ray continues to the surface under them). Tree leaves have no
// colliders, so trees never cover streets. Re-run after changing the level layout.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class PimlrMinimapBaker
{
    public static float MetresPerPixel = 0.4f;
    const float Margin = 20f;
    const float BuildingHeight = 2.5f;

    // Contrast order (dark -> light): background, buildings, road, plaza, sidewalk. Edit these to restyle, then re-bake.
    static readonly Color32 Background = new Color32(12, 13, 18, 255);
    static readonly Color32 Road = new Color32(92, 98, 116, 255);
    static readonly Color32 Sidewalk = new Color32(160, 166, 182, 255);
    static readonly Color32 Plaza = new Color32(128, 134, 150, 255);
    static readonly Color32 Park = new Color32(56, 112, 70, 255);
    static readonly Color32 BuildingLow = new Color32(26, 29, 40, 255);
    static readonly Color32 BuildingHigh = new Color32(36, 40, 56, 255);
    static readonly Color32 BuildingEdge = new Color32(200, 160, 40, 255); // PIMLR yellow outline

    enum Kind : byte { None, Road, Sidewalk, Plaza, Park, Building, Prop }

    [MenuItem("PIMLR/Minimap/Bake 2D Map (Open Scene)")]
    public static void BakeMenu()
    {
        string msg = Bake();
        EditorUtility.DisplayDialog("PIMLR Minimap", msg, "OK");
    }

    public static string Bake()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length == 0) return "No NavMesh in this scene - the map area is taken from the NavMesh.";
        var b = new Bounds(tri.vertices[0], Vector3.zero);
        foreach (var v in tri.vertices) b.Encapsulate(v);
        float minX = b.min.x - Margin, minZ = b.min.z - Margin;
        int w = Mathf.CeilToInt((b.size.x + Margin * 2f) / MetresPerPixel / 4f) * 4;
        int h = Mathf.CeilToInt((b.size.z + Margin * 2f) / MetresPerPixel / 4f) * 4;
        float top = 400f;

        var kinds = new Kind[w * h];
        var heights = new float[w * h];
        var matCache = new Dictionary<Material, Kind>();
        try
        {
            for (int y = 0; y < h; y++)
            {
                if (y % 64 == 0 && EditorUtility.DisplayCancelableProgressBar("PIMLR Minimap", $"Baking {scene.name} ({w}x{h})", (float)y / h))
                    return "Cancelled.";
                for (int x = 0; x < w; x++)
                {
                    var origin = new Vector3(minX + (x + 0.5f) * MetresPerPixel, top, minZ + (y + 0.5f) * MetresPerPixel);
                    Classify(origin, matCache, out kinds[y * w + x], out heights[y * w + x]);
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        // Paint
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++)
        {
            switch (kinds[i])
            {
                case Kind.Road: px[i] = Road; break;
                case Kind.Sidewalk: px[i] = Sidewalk; break;
                case Kind.Plaza: px[i] = Plaza; break;
                case Kind.Park: px[i] = Park; break;
                case Kind.Building: px[i] = Color32.Lerp(BuildingLow, BuildingHigh, Mathf.InverseLerp(5f, 150f, heights[i])); break;
                default: px[i] = Background; break;
            }
        }
        // Building outlines (a building texel next to a non-building texel)
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                if (kinds[i] != Kind.Building) continue;
                if (kinds[i - 1] != Kind.Building || kinds[i + 1] != Kind.Building || kinds[i - w] != Kind.Building || kinds[i + w] != Kind.Building)
                    px[i] = Color32.Lerp(px[i], BuildingEdge, 0.35f);
            }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        string texDir = "Assets/_Game/Textures/Minimap";
        Directory.CreateDirectory(texDir);
        string texPath = $"{texDir}/{scene.name}_Minimap.png";
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
        imp.textureType = TextureImporterType.Default;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.maxTextureSize = 4096;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();

        string resDir = "Assets/_Game/Resources/PIMLR/Minimap";
        Directory.CreateDirectory(resDir);
        AssetDatabase.Refresh();
        string assetPath = $"{resDir}/{scene.name}.asset";
        var mapAsset = AssetDatabase.LoadAssetAtPath<PimlrMinimapMap>(assetPath);
        if (!mapAsset) { mapAsset = ScriptableObject.CreateInstance<PimlrMinimapMap>(); AssetDatabase.CreateAsset(mapAsset, assetPath); }
        mapAsset.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        mapAsset.worldMin = new Vector2(minX, minZ);
        mapAsset.worldSize = new Vector2(w * MetresPerPixel, h * MetresPerPixel);
        mapAsset.metresPerPixel = MetresPerPixel;
        EditorUtility.SetDirty(mapAsset);
        AssetDatabase.SaveAssets();
        return $"Baked {scene.name}: {w}x{h} px at {MetresPerPixel} m/px, covering {mapAsset.worldSize.x:F0} x {mapAsset.worldSize.y:F0} m.\n{texPath}\n{assetPath}";
    }

    static void Classify(Vector3 origin, Dictionary<Material, Kind> cache, out Kind kind, out float height)
    {
        kind = Kind.None; height = 0f;
        Vector3 from = origin;
        for (int step = 0; step < 4; step++) // look through up to 3 props
        {
            if (!Physics.Raycast(from, Vector3.down, out var hit, 600f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (step > 0) kind = Kind.Plaza; // something low stood on nothing we know: draw it as ground
                return;
            }
            height = hit.point.y;
            if (hit.point.y > BuildingHeight)
            {
                // tall: a building, unless it's a prop sitting on something (lamp posts, signs - thin, rare)
                kind = Kind.Building;
                return;
            }
            var k = SurfaceKind(hit, cache);
            if (k != Kind.Prop) { kind = k; return; }
            from = hit.point + Vector3.down * 0.02f; // continue under the prop
        }
        kind = Kind.Plaza;
    }

    static Kind SurfaceKind(RaycastHit hit, Dictionary<Material, Kind> cache)
    {
        var mat = MaterialAt(hit);
        if (!mat) return Kind.Prop;
        if (cache.TryGetValue(mat, out var k)) return k;
        string n = mat.name.ToLowerInvariant();
        if (n.Contains("road") && !n.Contains("prop")) k = Kind.Road;
        else if (n.Contains("asphalt") || n.Contains("street")) k = Kind.Road;
        else if (n.Contains("path") || n.Contains("pavement") || n.Contains("sidewalk") || n.Contains("concrete") || n.Contains("tile")) k = Kind.Sidewalk;
        else if (n == "ground" || n.Contains("ground")) k = Kind.Plaza;
        else if (n.Contains("grass") || n.Contains("park") || n.Contains("lawn")) k = Kind.Park;
        else k = Kind.Prop;
        cache[mat] = k;
        return k;
    }

    static Material MaterialAt(RaycastHit hit)
    {
        var r = hit.collider.GetComponent<Renderer>();
        if (!r) return null;
        var mats = r.sharedMaterials;
        if (mats.Length == 0) return null;
        if (mats.Length > 1 && hit.collider is MeshCollider mc && mc.sharedMesh)
        {
            var m = mc.sharedMesh;
            int idx = hit.triangleIndex * 3;
            for (int s = 0; s < m.subMeshCount && s < mats.Length; s++)
            {
                var d = m.GetSubMesh(s);
                if (idx >= d.indexStart && idx < d.indexStart + d.indexCount) return mats[s];
            }
        }
        return mats[0];
    }
}
