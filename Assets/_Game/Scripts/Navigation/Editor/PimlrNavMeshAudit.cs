// PIMLR #NAV: NavMesh audit and fixes for YannicksWorld (Unity 6.6, AI Navigation 2.x).
//
// Menu: PIMLR > Navigation >
//   1. Audit NavMesh (report only)       - changes nothing. Writes NavMeshAudit.md in the project folder (next to
//                                          Assets) and draws the findings in the Scene view.
//   2. Fix: Bake Static Props Instead Of Carving
//   3. Fix: Rebake With Recommended Settings
//   4. Fix: Block Unreachable Islands              - NavMesh nobody can walk to (building interiors, ground outside
//                                          the level boundary) is covered by Not Walkable NavMeshModifierVolumes
//                                          under PIMLR_NavMesh_IslandBlockers. Walls stay in the bake; a volume is only
//                                          placed where it stays 0.3 m clear of walkable NavMesh. Rebake afterwards.
//   Show / Hide Audit Markers
//
// What the audit looks for
//   - Gaps: ground the player can stand on (flat, open headroom, at street level) with no NavMesh under it, each
//     tagged with the most likely cause:
//       * "hidden by NavMeshObstacle": the NavMeshSurface has Ignore NavMesh Obstacle on, so any GameObject with a
//         NavMeshObstacle (even a non-carving one) is left out of the bake. If that object is a sidewalk plot or a
//         street piece, its ground is never baked.
//       * "layer not in bake": the ground collider is on a layer the surface doesn't collect.
//       * "outside bake volume": above or below the surface's box.
//       * "rebake": none of the above; usually a stale bake (geometry moved since) or a patch smaller than
//         Min Region Area.
//   - Islands: separate pieces of NavMesh that can't be walked to from where the player starts (the Story start
//     in YannicksWorld). Enemies spawned there can't reach the player and vice versa.
//   - Runtime carving: every NavMeshObstacle with Carve on. Each one cuts a hole (its size + the agent radius) at
//     runtime and costs CPU every time it moves or is enabled. Thousands of static props carving is both a source
//     of runtime gaps (a row of bollards seals off a sidewalk) and a WebGL cost.
//
// Fix 2 (Undo-able): static obstacles (no moving Rigidbody above them) stop carving and are baked into the NavMesh
// instead, by turning off the surface's Ignore NavMesh Obstacle. Where a static obstacle has no collider of its
// own on the bake layers, a Not Walkable NavMeshModifierVolume of the same size is added so it still blocks.
// Moving things (cars with physics) keep carving.
// Fix 3: Min Region Area 8 -> 2 (keeps small legit patches between props), then bakes the surface the same way
// its Inspector Bake button does. Save the scene afterwards.
//
// Unreal: Nav Mesh Bounds Volume + RecastNavMesh settings; static props are "Can Ever Affect Navigation" meshes,
// moving ones use Dynamic Obstacle / Nav Modifier components (runtime generation = Dynamic Modifiers Only).

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class PimlrNavMeshAudit
{
    const string Menu = "PIMLR/Navigation/";
    const float Step = 1f;              // gap scan grid (metres)
    const float MinPieceArea = 4f;      // ignore ground pieces smaller than this (planters, bins, kerb stones)
    const int MinGapCells = 3;          // report gap regions of at least this many m²
    const float MinIslandArea = 10f;    // report islands at least this big
    const int NotWalkableArea = 1;

    static readonly string[] NonGroundLayers =
    {
        "TransparentFX", "Ignore Raycast", "Player", "UI", "EnemyTracker", "wheels", "carBody", "RCC",
        "RCC_WheelCollider", "RCC_DetachablePart", "RCC_Trailer", "RCC_Crasher", "Item", "Minimap", "Bullet", "Missile"
    };

    // ------------------------------------------------------------------ results (for the Scene view overlay)
    class GapRegion { public Vector3 centre; public int cells; public string cause; public List<string> objects = new List<string>(); }
    class Island
    {
        public Vector3 centre; public float area; public int root;
        public List<int> tris = new List<int>();           // first index of each triangle in lastTri.indices
        public string surfaces = "", layers = ""; public float meanY;
    }
    static NavMeshTriangulation lastTri;
    static List<int> mainTris = new List<int>();

    static List<Vector3> gapCells = new List<Vector3>();
    static List<GapRegion> gapRegions = new List<GapRegion>();
    static List<Island> islands = new List<Island>();
    static bool showMarkers;

    // ================================================================== 1. audit
    [MenuItem(Menu + "1. Audit NavMesh (report only)", false, 1)]
    public static void Audit()
    {
        var surface = FindSurface();
        if (!surface) return;

        var sb = new StringBuilder();
        sb.AppendLine($"# NavMesh audit — {EditorSceneManager.GetActiveScene().name}");
        sb.AppendLine($"_{System.DateTime.Now:yyyy-MM-dd HH:mm}_ · PIMLR > Navigation > 1. Audit NavMesh");
        sb.AppendLine();

        // ---- settings
        var bs = NavMesh.GetSettingsByID(surface.agentTypeID);
        Bounds vol = SurfaceBounds(surface);
        sb.AppendLine("## Bake settings");
        sb.AppendLine($"- Surface `{surface.name}`: collect {surface.collectObjects}, layers {LayerNames(surface.layerMask)}, " +
                      $"geometry {surface.useGeometry}, ignore NavMeshObstacle **{surface.ignoreNavMeshObstacle}**, " +
                      $"min region area {surface.minRegionArea}, voxel {(surface.overrideVoxelSize ? surface.voxelSize.ToString("0.###") : "auto")}");
        sb.AppendLine($"- Volume: centre {vol.center:F1}, size {vol.size:F1} (y {vol.min.y:0.0} to {vol.max.y:0.0})");
        sb.AppendLine($"- Agent: radius {bs.agentRadius}, height {bs.agentHeight}, step {bs.agentClimb}, slope {bs.agentSlope}°");
        sb.AppendLine($"- NavMesh data: {(surface.navMeshData ? AssetDatabase.GetAssetPath(surface.navMeshData) : "**none (not baked)**")}");
        sb.AppendLine();

        // ---- obstacles
        var obstacles = Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include);
        var carving = obstacles.Where(o => o.carving).ToList();
        var staticCarving = carving.Where(o => !IsMoving(o)).ToList();
        int noCollider = staticCarving.Count(o => !HasBakeCollider(o, surface.layerMask));
        sb.AppendLine("## Runtime carving");
        sb.AppendLine($"- NavMeshObstacles in the scene: {obstacles.Length}; carving: **{carving.Count}** (static: {staticCarving.Count}, moving: {carving.Count - staticCarving.Count}).");
        sb.AppendLine($"- Static carving obstacles without a collider on the bake layers: {noCollider} (Fix 2 gives these a Not Walkable volume).");
        sb.AppendLine();
        sb.AppendLine("| Carving obstacles under | Count |");
        sb.AppendLine("| --- | --- |");
        foreach (var g in carving.GroupBy(o => TopName(o.transform)).OrderByDescending(g => g.Count()).Take(20))
            sb.AppendLine($"| {g.Key} | {g.Count()} |");
        sb.AppendLine();

        // ---- gaps
        ScanGaps(surface, vol, bs);
        sb.AppendLine("## Gaps (standable street-level ground with no NavMesh)");
        sb.AppendLine($"- {gapCells.Count} m² of gaps in {gapRegions.Count} regions of {MinGapCells} m² or more. Edit-mode bake only; runtime carving adds more.");
        foreach (var c in gapRegions.GroupBy(r => r.cause).OrderByDescending(g => g.Sum(r => r.cells)))
            sb.AppendLine($"  - {c.Key}: {c.Sum(r => r.cells)} m² in {c.Count()} regions");
        sb.AppendLine();
        sb.AppendLine("| # | Centre (x, z) | m² | Likely cause | Ground objects |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        int n = 0;
        foreach (var r in gapRegions.OrderByDescending(r => r.cells).Take(40))
            sb.AppendLine($"| {++n} | {r.centre.x:0}, {r.centre.z:0} | {r.cells} | {r.cause} | {string.Join(", ", r.objects.Take(3))} |");
        sb.AppendLine();

        // ---- islands
        Vector3 start = PlayerStart();
        FindIslands(start, out float mainArea, out float totalArea);
        sb.AppendLine("## Islands (NavMesh you can't walk to from the player start)");
        sb.AppendLine($"- Player start {start:F0}. Walkable from there: {mainArea:0} m² of {totalArea:0} m² total.");
        sb.AppendLine();
        sb.AppendLine($"- Unreachable islands of {MinIslandArea} m² or more: {islands.Count}, {islands.Sum(i => i.area):0} m² in total.");
        sb.AppendLine();
        sb.AppendLine("| # | Centre (x, z) | m² | Height (y) | Standing on | Layer |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
        n = 0;
        foreach (var i in islands.OrderByDescending(i => i.area).Take(40))
            sb.AppendLine($"| {++n} | {i.centre.x:0}, {i.centre.z:0} | {i.area:0} | {i.meanY:0.0} | {i.surfaces} | {i.layers} |");
        sb.AppendLine();

        // ---- mission points: can the player (and enemies) walk to them?
        sb.AppendLine("## Mission points (walkable from the player start?)");
        sb.AppendLine("| Point | Position | Status |");
        sb.AppendLine("| --- | --- | --- |");
        int bad = 0;
        foreach (var (label, pos) in MissionPoints())
        {
            string status = Reach(start, pos);
            if (status != "ok") bad++;
            sb.AppendLine($"| {label} | {pos.x:0}, {pos.y:0.0}, {pos.z:0} | {status} |");
        }
        sb.AppendLine($"- {bad} point(s) not reachable.");
        sb.AppendLine();

        if (carving.Count > 0 && carving.Count <= 30)
        {
            sb.AppendLine("## Remaining carving obstacles");
            foreach (var o in carving)
                sb.AppendLine($"- `{HierarchyPath(o.transform)}` ({(IsMoving(o) ? "moving" : "static")}, shape {o.shape})");
            sb.AppendLine();
        }
        sb.AppendLine("Scene view: red = gaps, orange = unreachable islands (PIMLR > Navigation > Show / Hide Audit Markers).");

        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "NavMeshAudit.md"));
        File.WriteAllText(path, sb.ToString());

        showMarkers = true;
        SceneView.duringSceneGui -= DrawMarkers;
        SceneView.duringSceneGui += DrawMarkers;
        SceneView.RepaintAll();

        string summary = $"PIMLR NavMesh audit: {gapCells.Count} m² of gaps in {gapRegions.Count} regions, {islands.Count} unreachable islands " +
                         $"({islands.Sum(i => i.area):0} m²), {carving.Count} carving obstacles ({staticCarving.Count} static). Report: {path}";
        Debug.Log(summary);
        EditorUtility.DisplayDialog("PIMLR NavMesh audit", summary.Replace("PIMLR NavMesh audit: ", "") , "OK");
        EditorUtility.RevealInFinder(path);
    }

    static void ScanGaps(NavMeshSurface surface, Bounds vol, NavMeshBuildSettings bs)
    {
        gapCells.Clear();
        gapRegions.Clear();
        int groundMask = ~LayerMask.GetMask(NonGroundLayers);
        float r = bs.agentRadius;
        float h = bs.agentHeight;
        float slopeCos = Mathf.Cos(bs.agentSlope * Mathf.Deg2Rad);

        int nx = Mathf.CeilToInt(vol.size.x / Step), nz = Mathf.CeilToInt(vol.size.z / Step);
        var gapGrid = new string[nx, nz];          // cause per gap cell
        var objGrid = new string[nx, nz];
        var hits = new RaycastHit[8];
        var blockers = Object.FindObjectsByType<NavMeshModifierVolume>(FindObjectsInactive.Exclude).Where(v => v.area == NotWalkableArea).ToList();

        for (int ix = 0; ix < nx; ix++)
        {
            if (ix % 20 == 0 && EditorUtility.DisplayCancelableProgressBar("PIMLR NavMesh audit", "Scanning for gaps…", ix / (float)nx)) break;
            for (int iz = 0; iz < nz; iz++)
            {
                float x = vol.min.x + (ix + 0.5f) * Step, z = vol.min.z + (iz + 0.5f) * Step;
                var from = new Vector3(x, vol.max.y + 20f, z);
                int count = Physics.RaycastNonAlloc(from, Vector3.down, hits, vol.size.y + 60f, groundMask, QueryTriggerInteraction.Ignore);
                if (count == 0) continue;

                // Street level = the lowest upward-facing hit in this column.
                RaycastHit ground = default;
                bool found = false;
                for (int k = 0; k < count; k++)
                {
                    if (hits[k].normal.y < slopeCos) continue;
                    if (!found || hits[k].point.y < ground.point.y) { ground = hits[k]; found = true; }
                }
                if (!found) continue;
                if (ground.collider.attachedRigidbody && !ground.collider.attachedRigidbody.isKinematic) continue; // cars, props with physics

                Vector3 p = ground.point;
                Bounds b = ground.collider.bounds;
                if (b.size.x * b.size.z < MinPieceArea) continue;                          // prop top, not ground

                // Standable: room for the agent (the eroded edge next to walls is expected, so use the full radius).
                Vector3 c0 = p + Vector3.up * (r + 0.05f), c1 = p + Vector3.up * Mathf.Max(r + 0.05f, h - r);
                if (Physics.CheckCapsule(c0, c1, r, groundMask, QueryTriggerInteraction.Ignore)) continue;

                if (NavMesh.SamplePosition(p, out _, 0.6f, NavMesh.AllAreas)) continue;
                if (InBlocker(blockers, p)) continue; // blocked on purpose (Fix 4 / designer volumes)

                gapGrid[ix, iz] = Cause(ground.collider, surface, vol, p);
                objGrid[ix, iz] = TopName(ground.collider.transform);
            }
        }
        EditorUtility.ClearProgressBar();

        // Flood-fill gap cells into regions.
        var seen = new bool[nx, nz];
        var queue = new Queue<Vector2Int>();
        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            if (gapGrid[ix, iz] == null || seen[ix, iz]) continue;
            var cells = new List<Vector2Int>();
            seen[ix, iz] = true;
            queue.Enqueue(new Vector2Int(ix, iz));
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                cells.Add(c);
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int ax = c.x + dx, az = c.y + dz;
                    if (ax < 0 || az < 0 || ax >= nx || az >= nz || seen[ax, az] || gapGrid[ax, az] == null) continue;
                    seen[ax, az] = true;
                    queue.Enqueue(new Vector2Int(ax, az));
                }
            }
            foreach (var c in cells) gapCells.Add(new Vector3(vol.min.x + (c.x + 0.5f) * Step, vol.center.y, vol.min.z + (c.y + 0.5f) * Step));
            if (cells.Count < MinGapCells) continue;

            var region = new GapRegion { cells = cells.Count };
            Vector3 sum = Vector3.zero;
            foreach (var c in cells) sum += new Vector3(vol.min.x + (c.x + 0.5f) * Step, 0f, vol.min.z + (c.y + 0.5f) * Step);
            region.centre = sum / cells.Count;
            region.cause = cells.GroupBy(c => gapGrid[c.x, c.y]).OrderByDescending(g => g.Count()).First().Key;
            region.objects = cells.Select(c => objGrid[c.x, c.y]).GroupBy(o => o).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();
            gapRegions.Add(region);
        }
    }

    static bool InBlocker(List<NavMeshModifierVolume> vols, Vector3 p)
    {
        foreach (var v in vols)
        {
            Vector3 l = v.transform.InverseTransformPoint(p) - v.center;
            Vector3 h = v.size * 0.5f;
            const float erosion = 1f; // the agent radius around a blocked area loses NavMesh too
            if (Mathf.Abs(l.x) <= h.x + erosion && Mathf.Abs(l.y) <= h.y && Mathf.Abs(l.z) <= h.z + erosion) return true;
        }
        return false;
    }

    static string Cause(Collider col, NavMeshSurface surface, Bounds vol, Vector3 p)
    {
        if (surface.ignoreNavMeshObstacle && col.gameObject.GetComponent<NavMeshObstacle>()) return "hidden by NavMeshObstacle";
        if (((1 << col.gameObject.layer) & surface.layerMask.value) == 0) return $"layer '{LayerMask.LayerToName(col.gameObject.layer)}' not in bake";
        if (p.y < vol.min.y || p.y > vol.max.y) return "outside bake volume";
        return "rebake (stale or below min region area)";
    }

    static void FindIslands(Vector3 start, out float mainArea, out float totalArea)
    {
        islands.Clear();
        var tri = NavMesh.CalculateTriangulation();
        int vcount = tri.vertices.Length;
        // Weld vertices that sit on tile borders.
        var key = new Dictionary<Vector3Int, int>();
        var weld = new int[vcount];
        for (int i = 0; i < vcount; i++)
        {
            var v = tri.vertices[i];
            var k = new Vector3Int(Mathf.RoundToInt(v.x * 20f), Mathf.RoundToInt(v.y * 20f), Mathf.RoundToInt(v.z * 20f));
            if (!key.TryGetValue(k, out int w)) key[k] = w = i;
            weld[i] = w;
        }
        var parent = new int[vcount];
        for (int i = 0; i < vcount; i++) parent[i] = i;
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }

        var idx = tri.indices;
        for (int t = 0; t < idx.Length; t += 3)
        {
            Union(weld[idx[t]], weld[idx[t + 1]]);
            Union(weld[idx[t]], weld[idx[t + 2]]);
        }

        var area = new Dictionary<int, float>();
        var centre = new Dictionary<int, Vector3>();
        var trisOf = new Dictionary<int, List<int>>();
        totalArea = 0f;
        for (int t = 0; t < idx.Length; t += 3)
        {
            Vector3 a = tri.vertices[idx[t]], b = tri.vertices[idx[t + 1]], c = tri.vertices[idx[t + 2]];
            float ar = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            int root = Find(weld[idx[t]]);
            area.TryGetValue(root, out float sum); area[root] = sum + ar;
            centre.TryGetValue(root, out Vector3 cs); centre[root] = cs + (a + b + c) / 3f * ar;
            if (!trisOf.TryGetValue(root, out var list)) trisOf[root] = list = new List<int>();
            list.Add(t);
            totalArea += ar;
        }
        lastTri = tri;

        // The island under the player start.
        int main = -1;
        if (NavMesh.SamplePosition(start, out var hit, 10f, NavMesh.AllAreas))
        {
            float best = float.MaxValue;
            for (int i = 0; i < vcount; i++)
            {
                float d = (tri.vertices[i] - hit.position).sqrMagnitude;
                if (d < best) { best = d; main = Find(weld[i]); }
            }
        }
        if (main < 0 && area.Count > 0) main = area.OrderByDescending(kv => kv.Value).First().Key;
        mainArea = main >= 0 && area.ContainsKey(main) ? area[main] : 0f;

        mainTris = main >= 0 && trisOf.ContainsKey(main) ? trisOf[main] : new List<int>();
        foreach (var kv in area)
            if (kv.Key != main && kv.Value >= MinIslandArea)
            {
                var isl = new Island { area = kv.Value, centre = centre[kv.Key] / kv.Value, root = kv.Key, tris = trisOf[kv.Key] };
                Describe(isl);
                islands.Add(isl);
            }
    }

    /// What an island is standing on: the colliders right under a sample of its triangles.
    static void Describe(Island isl)
    {
        var names = new Dictionary<string, int>();
        var layers = new HashSet<string>();
        float ySum = 0f; int n = 0;
        int stride = Mathf.Max(1, isl.tris.Count / 40);
        for (int i = 0; i < isl.tris.Count; i += stride)
        {
            Vector3 c = Centroid(isl.tris[i]);
            ySum += c.y; n++;
            var col = ColliderUnder(c);
            if (!col) continue;
            string nm = TopName(col.transform) + (col.transform.root != col.transform ? "/" + col.name : "");
            names.TryGetValue(nm, out int k); names[nm] = k + 1;
            layers.Add(LayerMask.LayerToName(col.gameObject.layer));
        }
        isl.meanY = n > 0 ? ySum / n : 0f;
        isl.surfaces = string.Join(", ", names.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key));
        isl.layers = string.Join(", ", layers);
    }

    static Vector3 Centroid(int t) =>
        (lastTri.vertices[lastTri.indices[t]] + lastTri.vertices[lastTri.indices[t + 1]] + lastTri.vertices[lastTri.indices[t + 2]]) / 3f;

    static Collider ColliderUnder(Vector3 p) =>
        Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, out var hit, 1.2f, ~LayerMask.GetMask(NonGroundLayers), QueryTriggerInteraction.Ignore) ? hit.collider : null;

    // ================================================================== 2. fix: bake static props
    [MenuItem(Menu + "2. Fix: Bake Static Props Instead Of Carving", false, 20)]
    public static void FixCarving()
    {
        var surface = FindSurface();
        if (!surface) return;
        var carving = Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include).Where(o => o.carving && !IsMoving(o)).ToList();
        if (!EditorUtility.DisplayDialog("PIMLR NavMesh",
                $"{carving.Count} static NavMeshObstacles will stop carving at runtime and be baked into the NavMesh instead " +
                "(the surface's Ignore NavMesh Obstacle goes off). Obstacles without a collider get a Not Walkable volume of the same size. " +
                "Moving cars keep carving.\n\nUndo-able. Rebake afterwards (step 3).", "Do it", "Cancel")) return;

        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("PIMLR: bake static props instead of carving");
        Undo.RecordObject(surface, "surface");
        surface.ignoreNavMeshObstacle = false;

        int reverted = 0, set = 0, volumes = 0;
        try
        {
            for (int i = 0; i < carving.Count; i++)
            {
                if (i % 100 == 0) EditorUtility.DisplayProgressBar("PIMLR NavMesh", "Converting obstacles…", i / (float)carving.Count);
                var o = carving[i];
                var source = PrefabUtility.GetCorrespondingObjectFromSource(o);
                if (source && !source.carving)
                {
                    // The prefab itself doesn't carve: drop the scene override (keeps the scene file small).
                    var so = new SerializedObject(o);
                    var prop = so.FindProperty("m_Carve");
                    if (prop != null && prop.prefabOverride) { PrefabUtility.RevertPropertyOverride(prop, InteractionMode.UserAction); reverted++; }
                    else { Undo.RecordObject(o, "carve"); o.carving = false; set++; }
                }
                else { Undo.RecordObject(o, "carve"); o.carving = false; set++; }

                if (!HasBakeCollider(o, surface.layerMask) && !o.GetComponent<NavMeshModifierVolume>())
                {
                    var v = Undo.AddComponent<NavMeshModifierVolume>(o.gameObject);
                    v.area = NotWalkableArea;
                    if (o.shape == NavMeshObstacleShape.Box) { v.center = o.center; v.size = o.size; }
                    else { v.center = o.center; v.size = new Vector3(o.radius * 2f, o.height, o.radius * 2f); }
                    volumes++;
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);

        string msg = $"Carving off on {reverted + set} static obstacles ({reverted} by reverting the scene override, {set} set directly); " +
                     $"{volumes} Not Walkable volumes added; surface now bakes obstacle objects. Next: 3. Rebake.";
        Debug.Log("PIMLR NavMesh: " + msg);
        EditorUtility.DisplayDialog("PIMLR NavMesh", msg, "OK");
    }

    // ================================================================== 3. fix: rebake
    [MenuItem(Menu + "3. Fix: Rebake With Recommended Settings", false, 21)]
    public static void Rebake()
    {
        var surface = FindSurface();
        if (!surface) return;
        if (!EditorUtility.DisplayDialog("PIMLR NavMesh",
                $"Rebake '{surface.name}'.\n\nMin Region Area {surface.minRegionArea} -> 2 (keeps small walkable patches between props). " +
                "Other settings unchanged. Save the scene when the bake finishes, then run the audit again.", "Bake", "Cancel")) return;

        Undo.RecordObject(surface, "PIMLR: NavMesh settings");
        surface.minRegionArea = 2f;
        EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
        NavMeshAssetManager.instance.StartBakingSurfaces(new Object[] { surface });
        Debug.Log("PIMLR NavMesh: bake started (progress bar bottom-right). Save the scene when it's done, then run 1. Audit NavMesh.");
    }

    // ================================================================== 4. fix: block unreachable islands
    // (An earlier version set Ignore From Build on the colliders under the islands. On these plots one collider is
    //  both the floor and the building walls, so that removed the walls too and opened the interiors to the street.
    //  Not Walkable volumes leave every collider in the bake.)
    [MenuItem(Menu + "4. Fix: Block Unreachable Islands", false, 22)]
    public static void BlockIslands()
    {
        var surface = FindSurface();
        if (!surface) return;
        Vector3 start = PlayerStart();
        FindIslands(start, out float mainArea, out _);
        if (islands.Count == 0) { EditorUtility.DisplayDialog("PIMLR NavMesh", "No unreachable islands. Nothing to do.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("PIMLR NavMesh",
                $"{islands.Count} unreachable islands ({islands.Sum(i => i.area):0} m²) will be covered by Not Walkable volumes under " +
                "PIMLR_NavMesh_IslandBlockers. Volumes stay 0.3 m clear of the walkable NavMesh. Undo-able. Rebake afterwards (step 3).", "Do it", "Cancel")) return;

        int added = PlaceBlockers(out int uncovered);
        EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
        string msg = $"{added} Not Walkable volumes added ({uncovered} island triangles touch walkable NavMesh and were left). Next: 3. Rebake, save, then 1. Audit.";
        Debug.Log("PIMLR NavMesh: " + msg);
        EditorUtility.DisplayDialog("PIMLR NavMesh", msg, "OK");
    }

    /// Uses the islands from the last FindIslands call. Returns the number of volumes created.
    public static int PlaceBlockers(out int uncovered)
    {
        // Walkable (main island) vertices in a 4 m hash, to keep volumes clear of them.
        var grid = new Dictionary<Vector2Int, List<Vector3>>();
        foreach (int t in mainTris)
            for (int k = 0; k < 3; k++)
            {
                var v = lastTri.vertices[lastTri.indices[t + k]];
                var key = new Vector2Int(Mathf.FloorToInt(v.x / 4f), Mathf.FloorToInt(v.z / 4f));
                if (!grid.TryGetValue(key, out var l)) grid[key] = l = new List<Vector3>();
                l.Add(v);
            }

        bool Clashes(Bounds b)
        {
            b.Expand(new Vector3(0.6f, 0f, 0.6f));
            int x0 = Mathf.FloorToInt(b.min.x / 4f), x1 = Mathf.FloorToInt(b.max.x / 4f);
            int z0 = Mathf.FloorToInt(b.min.z / 4f), z1 = Mathf.FloorToInt(b.max.z / 4f);
            for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                if (grid.TryGetValue(new Vector2Int(x, z), out var l))
                    foreach (var v in l)
                        if (v.x > b.min.x && v.x < b.max.x && v.z > b.min.z && v.z < b.max.z && v.y > b.min.y - 1f && v.y < b.max.y + 1f)
                            return true;
            return false;
        }

        Bounds BoundsOf(IEnumerable<int> tris)
        {
            bool first = true; Bounds b = default;
            foreach (int t in tris)
                for (int k = 0; k < 3; k++)
                {
                    var v = lastTri.vertices[lastTri.indices[t + k]];
                    if (first) { b = new Bounds(v, Vector3.zero); first = false; } else b.Encapsulate(v);
                }
            return b;
        }

        var root = GameObject.Find("PIMLR_NavMesh_IslandBlockers");
        if (!root)
        {
            root = new GameObject("PIMLR_NavMesh_IslandBlockers");
            Undo.RegisterCreatedObjectUndo(root, "PIMLR: NavMesh island blockers");
        }
        int n = root.transform.childCount, added = 0;
        uncovered = 0;
        foreach (var isl in islands)
        {
            var tris = isl.tris;
            var boxes = new List<Bounds>();
            var whole = BoundsOf(tris);
            if (!Clashes(whole)) boxes.Add(whole);
            else
                foreach (float cell in new[] { 6f, 2f, 1f })
                {
                    var rest = new List<int>();
                    foreach (var g in tris.GroupBy(t => { var c = Centroid(t); return new Vector2Int(Mathf.FloorToInt(c.x / cell), Mathf.FloorToInt(c.z / cell)); }))
                    {
                        var cb = BoundsOf(g);
                        if (!Clashes(cb)) boxes.Add(cb); else rest.AddRange(g);
                    }
                    tris = rest;
                    if (tris.Count == 0) break;
                }
            if (!ReferenceEquals(tris, isl.tris)) uncovered += tris.Count; // left after tiling

            foreach (var b in boxes)
            {
                var go = new GameObject("Blocker_" + (++n));
                Undo.RegisterCreatedObjectUndo(go, "PIMLR: NavMesh island blocker");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(b.center.x, b.min.y + 1f, b.center.z);
                var v = go.AddComponent<NavMeshModifierVolume>();
                v.area = NotWalkableArea;
                v.center = Vector3.zero;
                v.size = new Vector3(Mathf.Max(0.5f, b.size.x), b.size.y + 4f, Mathf.Max(0.5f, b.size.z));
                added++;
            }
        }
        return added;
    }

    /// Fans, their enemy spawn points, and anything named like a spawn point in the open scene.
    static IEnumerable<(string, Vector3)> MissionPoints()
    {
        foreach (var fan in Object.FindObjectsByType<PIMLR.Missions.SaveTheFans.FanHeat>(FindObjectsInactive.Include))
        {
            yield return ($"Fan {fan.fanDisplayName}", fan.transform.position);
            if (fan.spawnPoints == null) continue;
            int i = 0;
            foreach (var sp in fan.spawnPoints)
                if (sp) yield return ($"{fan.fanDisplayName} spawn {++i}", sp.position);
        }
    }

    static string Reach(Vector3 start, Vector3 p)
    {
        if (!NavMesh.SamplePosition(start, out var a, 6f, NavMesh.AllAreas)) return "player start off NavMesh";
        if (!NavMesh.SamplePosition(p, out var b, 2f, NavMesh.AllAreas)) return "**no NavMesh within 2 m**";
        var path = new NavMeshPath();
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        return path.status == NavMeshPathStatus.PathComplete ? "ok" : $"**{path.status}**";
    }

    static string HierarchyPath(Transform t)
    {
        string s = t.name;
        while (t.parent) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    // ================================================================== markers
    [MenuItem(Menu + "Show ∕ Hide Audit Markers", false, 40)]
    static void ToggleMarkers()
    {
        showMarkers = !showMarkers;
        SceneView.duringSceneGui -= DrawMarkers;
        if (showMarkers) SceneView.duringSceneGui += DrawMarkers;
        SceneView.RepaintAll();
    }

    static void DrawMarkers(SceneView view)
    {
        if (!showMarkers) return;
        var camPos = view.camera.transform.position;
        Handles.color = new Color(1f, 0.15f, 0.1f, 0.9f);
        foreach (var c in gapCells)
        {
            if ((c - camPos).sqrMagnitude > 150f * 150f) continue;
            Vector3 p = new Vector3(c.x, GroundY(c), c.z) + Vector3.up * 0.1f;
            Handles.DrawSolidDisc(p, Vector3.up, 0.35f);
        }
        int i = 0;
        foreach (var r in gapRegions.OrderByDescending(r => r.cells).Take(40))
        {
            i++;
            Vector3 p = new Vector3(r.centre.x, GroundY(r.centre), r.centre.z) + Vector3.up * 2f;
            if ((p - camPos).sqrMagnitude > 250f * 250f) continue;
            Handles.Label(p, $"GAP {i}: {r.cells} m²\n{r.cause}");
        }
        Handles.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        foreach (var isl in islands)
        {
            Handles.DrawWireDisc(isl.centre + Vector3.up * 0.2f, Vector3.up, Mathf.Sqrt(isl.area / Mathf.PI));
            if ((isl.centre - camPos).sqrMagnitude < 250f * 250f)
                Handles.Label(isl.centre + Vector3.up * 2f, $"UNREACHABLE {isl.area:0} m²");
        }
    }

    static readonly Dictionary<Vector2Int, float> groundCache = new Dictionary<Vector2Int, float>();
    static float GroundY(Vector3 c)
    {
        var k = new Vector2Int(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.z));
        if (groundCache.TryGetValue(k, out float y)) return y;
        y = Physics.Raycast(new Vector3(c.x, c.y + 50f, c.z), Vector3.down, out var hit, 120f, ~LayerMask.GetMask(NonGroundLayers), QueryTriggerInteraction.Ignore) ? hit.point.y : c.y;
        groundCache[k] = y;
        return y;
    }

    // ================================================================== helpers
    static NavMeshSurface FindSurface()
    {
        var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Exclude);
        if (surfaces.Length == 0)
        {
            EditorUtility.DisplayDialog("PIMLR NavMesh", "No NavMeshSurface in the open scene. Open YannicksWorld first.", "OK");
            return null;
        }
        if (surfaces.Length > 1) Debug.LogWarning($"PIMLR NavMesh: {surfaces.Length} NavMeshSurfaces found; using the largest.");
        return surfaces.OrderByDescending(s => s.size.x * s.size.z).First();
    }

    static Bounds SurfaceBounds(NavMeshSurface s)
    {
        if (s.collectObjects != CollectObjects.Volume)
        {
            // All / Children: use every collider on the bake layers.
            var all = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude).Where(c => ((1 << c.gameObject.layer) & s.layerMask.value) != 0).ToList();
            if (all.Count == 0) return new Bounds(s.transform.position, Vector3.one * 100f);
            var b = all[0].bounds;
            foreach (var c in all) b.Encapsulate(c.bounds);
            return b;
        }
        return new Bounds(s.transform.TransformPoint(s.center), Vector3.Scale(s.size, s.transform.lossyScale));
    }

    static Vector3 PlayerStart()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        return p ? p.transform.position : Vector3.zero;
    }

    static bool IsMoving(NavMeshObstacle o)
    {
        var rb = o.GetComponentInParent<Rigidbody>();
        return rb && !rb.isKinematic;
    }

    static bool HasBakeCollider(NavMeshObstacle o, LayerMask mask)
    {
        foreach (var c in o.GetComponents<Collider>())
            if (!c.isTrigger && c.enabled && ((1 << c.gameObject.layer) & mask.value) != 0) return true;
        return false;
    }

    static string TopName(Transform t)
    {
        // Name of the outermost prefab instance (or the root), without "(12)".
        var root = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
        string n = root ? root.name : t.root.name;
        int i = n.IndexOf(" (");
        return i > 0 ? n.Substring(0, i) : n;
    }

    static string LayerNames(LayerMask m)
    {
        var names = new List<string>();
        for (int i = 0; i < 32; i++) if ((m.value & (1 << i)) != 0) names.Add(LayerMask.LayerToName(i));
        return string.Join(", ", names);
    }
}
