using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// PIMLR: converts the hand-placed "City Walls" (124 x Wall_Arc_03) into spline-driven wall runs.
///  - Each straight run of walls becomes one SplineContainer + SplineInstantiate (edit the knots, walls follow).
///  - Each run gets PimlrSplineWallColliders: tall invisible barriers per segment that overlap at the corners.
///  - The old "City Walls" object is switched off (not deleted) so you can compare / roll back.
///  - Optionally moves the wall prefab + its mesh/material/textures out of Assets/ARC (Phase 2 deletes ARC).
/// Menu: PIMLR / Level / Build City Boundary (Spline Walls)
/// </summary>
public static class PimlrCityBoundaryBuilder
{
    const string OldRootName = "City Walls";
    const string NewRootName = "PIMLR_CityBoundary";
    const string WallPrefabName = "Wall_Arc_03";
    const string RelocateFolder = "Assets/_Game/Art/CityWall";

    class Wall
    {
        public Transform t;
        public Vector3 pos;
        public Vector3 fwd;   // flat forward of the wall (its long axis, local Z)
    }

    [MenuItem("PIMLR/Level/Build City Boundary (Spline Walls)")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var oldRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == OldRootName);
        if (oldRoot == null)
        {
            EditorUtility.DisplayDialog("City Boundary", $"No root object named '{OldRootName}' in {scene.name}. Open YannicksWorld first.", "OK");
            return;
        }
        if (scene.GetRootGameObjects().Any(g => g.name == NewRootName) &&
            !EditorUtility.DisplayDialog("City Boundary", $"'{NewRootName}' already exists. Build another one? (Delete the old one afterwards.)", "Build", "Cancel"))
            return;

        // ---- collect the wall instances ----
        var walls = new List<Wall>();
        GameObject prefabAsset = null;
        foreach (Transform t in oldRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == oldRoot.transform) continue;
            var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            bool isWall = (src != null && src.name == WallPrefabName && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                          || (src == null && t.name.StartsWith(WallPrefabName));
            if (!isWall) continue;
            if (src != null && prefabAsset == null) prefabAsset = src;
            Vector3 f = t.forward; f.y = 0f;
            if (f.sqrMagnitude < 0.001f) continue;
            walls.Add(new Wall { t = t, pos = t.position, fwd = f.normalized });
        }
        if (walls.Count == 0 || prefabAsset == null)
        {
            EditorUtility.DisplayDialog("City Boundary", $"Found no {WallPrefabName} prefab instances under '{OldRootName}'.", "OK");
            return;
        }

        float wallLength = MeasureWallLength(prefabAsset);

        // ---- relocate the prefab + deps out of ARC (keeps GUIDs, so nothing breaks) ----
        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        if (prefabPath.StartsWith("Assets/ARC/") &&
            EditorUtility.DisplayDialog("City Boundary",
                $"{WallPrefabName} and its mesh/material/textures live in Assets/ARC, which Phase 2 deletes.\n\nMove them to {RelocateFolder}? (GUIDs are kept, so every existing reference keeps working.)",
                "Move", "Leave in ARC"))
        {
            RelocateOutOfArc(prefabPath); // prefabAsset stays valid - MoveAsset keeps the same object/GUID
        }

        // ---- group walls into straight runs ----
        var runs = BuildRuns(walls, wallLength);

        // ---- build ----
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build City Boundary");

        var root = new GameObject(NewRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create City Boundary");
        SceneManager_MoveToScene(root, scene);

        int runIndex = 0, wallTotal = 0;
        foreach (var run in runs)
        {
            var go = new GameObject($"Wall Run {runIndex++:00} ({run.count} walls)");
            go.layer = oldRoot.layer;
            Undo.RegisterCreatedObjectUndo(go, "Create Wall Run");
            go.transform.SetParent(root.transform, false);

            var container = go.AddComponent<SplineContainer>();
            var spline = container.Spline;
            spline.Clear();
            spline.Add(new BezierKnot((float3)run.start), TangentMode.Linear);
            // tiny epsilon so floating point never drops the last wall
            spline.Add(new BezierKnot((float3)(run.end + run.dir * 0.01f)), TangentMode.Linear);

            var inst = go.AddComponent<SplineInstantiate>();
            inst.Container = container;
            inst.itemsToInstantiate = new[] { new SplineInstantiate.InstantiableItem { Prefab = prefabAsset, Probability = 100f } };
            inst.InstantiateMethod = SplineInstantiate.Method.SpacingDistance;
            inst.MaxSpacing = wallLength;
            inst.MinSpacing = wallLength;
            inst.ForwardAxis = SplineComponent.AlignAxis.ZAxis;
            inst.UpAxis = SplineComponent.AlignAxis.YAxis;
            inst.UpdateInstances();

            var cols = go.AddComponent<PimlrSplineWallColliders>();
            cols.openEndExtension = wallLength * 0.5f + cols.endOverlap;
            cols.Rebuild();

            wallTotal += run.count;
        }

        Undo.RecordObject(oldRoot, "Disable old City Walls");
        oldRoot.SetActive(false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        Debug.Log($"PIMLR City Boundary: {walls.Count} walls -> {runs.Count} spline runs ({wallTotal} walls regenerated, length {wallLength:0.##} m). " +
                  $"'{OldRootName}' is now disabled - delete it once you're happy.", root);
        EditorUtility.DisplayDialog("City Boundary",
            $"Built {runs.Count} wall runs from {walls.Count} walls.\n\n'{OldRootName}' was disabled (not deleted).\n\nSelect a 'Wall Run' and use the Spline tool (Tools overlay) to move or add knots - walls and the invisible barrier follow.",
            "OK");
    }

    // Unity 2022 lacks a GameObject->Scene helper on new objects created while another scene is active; keep it explicit.
    static void SceneManager_MoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    class Run
    {
        public Vector3 start, end, dir;
        public int count;
    }

    static List<Run> BuildRuns(List<Wall> walls, float L)
    {
        var runs = new List<Run>();

        // 1) group by line direction (ignoring which way each wall faces), 5 degree tolerance
        var dirGroups = new List<List<Wall>>();
        var dirAngles = new List<float>();
        foreach (var w in walls)
        {
            float ang = Mathf.Repeat(Mathf.Atan2(w.fwd.x, w.fwd.z) * Mathf.Rad2Deg, 180f);
            int found = -1;
            for (int i = 0; i < dirAngles.Count; i++)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(ang * 2f, dirAngles[i] * 2f)) * 0.5f; // 180-periodic distance
                if (d < 5f) { found = i; break; }
            }
            if (found < 0) { dirAngles.Add(ang); dirGroups.Add(new List<Wall>()); found = dirAngles.Count - 1; }
            dirGroups[found].Add(w);
        }

        for (int g = 0; g < dirGroups.Count; g++)
        {
            float a = dirAngles[g] * Mathf.Deg2Rad;
            Vector3 axis = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 perp = new Vector3(axis.z, 0f, -axis.x);

            // 2) split into parallel lines by perpendicular offset (1.5 m tolerance)
            var byOffset = dirGroups[g].OrderBy(w => Vector3.Dot(w.pos, perp)).ToList();
            var lines = new List<List<Wall>>();
            foreach (var w in byOffset)
            {
                float off = Vector3.Dot(w.pos, perp);
                if (lines.Count == 0 || Mathf.Abs(off - lines[^1].Average(x => Vector3.Dot(x.pos, perp))) > 1.5f)
                    lines.Add(new List<Wall>());
                lines[^1].Add(w);
            }

            // 3) split each line into contiguous runs (a gap bigger than ~1.5 walls starts a new run)
            foreach (var line in lines)
            {
                var sorted = line.OrderBy(w => Vector3.Dot(w.pos, axis)).ToList();
                var current = new List<Wall> { sorted[0] };
                for (int i = 1; i < sorted.Count; i++)
                {
                    float gap = Vector3.Dot(sorted[i].pos - sorted[i - 1].pos, axis);
                    if (gap > L * 1.5f) { runs.Add(MakeRun(current, axis, L)); current = new List<Wall>(); }
                    current.Add(sorted[i]);
                }
                runs.Add(MakeRun(current, axis, L));
            }
        }
        return runs;
    }

    static Run MakeRun(List<Wall> run, Vector3 axis, float L)
    {
        // Keep the walls facing the way most of them faced originally.
        int along = run.Count(w => Vector3.Dot(w.fwd, axis) >= 0f);
        Vector3 dir = along >= run.Count - along ? axis : -axis;

        var sorted = run.OrderBy(w => Vector3.Dot(w.pos, dir)).ToList();
        Vector3 first = sorted[0].pos, last = sorted[^1].pos;

        // Re-space evenly at exactly one wall length, keeping the run centred where it was.
        float span = Vector3.Dot(last - first, dir);
        int count = Mathf.Max(1, Mathf.RoundToInt(span / L) + 1);
        float newSpan = (count - 1) * L;
        Vector3 centre = (first + last) * 0.5f;
        float y = run.Average(w => w.pos.y);
        Vector3 start = centre - dir * (newSpan * 0.5f); start.y = y;
        Vector3 end = centre + dir * (newSpan * 0.5f); end.y = y;

        return new Run { start = start, end = end, dir = dir, count = count };
    }

    /// <summary>
    /// Length of one wall along its forward (Z) axis, measured from what it actually renders. (The first build
    /// read 0 from the collider - SplineInstantiate then clamped spacing to 0.1 m and stacked thousands of
    /// walls on top of each other, which is the z-fighting / crawling texture.)
    /// </summary>
    public static float MeasureWallLength(GameObject prefab)
    {
        var temp = (GameObject)Object.Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        try
        {
            bool any = false; Bounds b = default;
            foreach (var r in temp.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            float len = any ? b.size.z : 0f;
            if (len < 1f)
            {
                var box = temp.GetComponentInChildren<BoxCollider>(true);
                if (box) len = Mathf.Abs(box.size.z * box.transform.lossyScale.z);
            }
            return len >= 1f ? len : 15.57f;
        }
        finally { Object.DestroyImmediate(temp); }
    }

    [MenuItem("PIMLR/Level/Fix City Wall Spacing")]
    public static void FixSpacing()
    {
        int fixedRuns = 0;
        foreach (var inst in Object.FindObjectsByType<SplineInstantiate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var items = inst.itemsToInstantiate;
            if (items == null || items.Length == 0 || items[0].Prefab == null) continue;
            float len = MeasureWallLength(items[0].Prefab);
            // Snap a straight 2-knot run to a whole number of walls so the last wall lands on the end knot.
            var container = inst.Container;
            if (container != null && container.Spline != null && container.Spline.Count == 2)
            {
                var spline = container.Spline;
                Vector3 a = (Vector3)spline[0].Position, b = (Vector3)spline[1].Position;
                float span = Vector3.Distance(a, b);
                int count = Mathf.Max(1, Mathf.RoundToInt(span / len));
                Vector3 dir = span > 0.01f ? (b - a) / span : Vector3.forward;
                Vector3 mid = (a + b) * 0.5f;
                float half = (count * len) * 0.5f - len * 0.5f;      // centres of the first / last wall
                Undo.RecordObject(container, "Fix wall spacing");
                spline.SetKnot(0, new BezierKnot((float3)(mid - dir * half)));
                spline.SetKnot(1, new BezierKnot((float3)(mid + dir * (half + 0.01f))));
            }

            Undo.RecordObject(inst, "Fix wall spacing");
            inst.MaxSpacing = len;
            inst.MinSpacing = len;
            inst.UpdateInstances();
            EditorUtility.SetDirty(inst);
            var cols = inst.GetComponent<PimlrSplineWallColliders>();
            if (cols) { cols.openEndExtension = len * 0.5f + cols.endOverlap; cols.Rebuild(); }
            fixedRuns++;
            Debug.Log($"PIMLR City Boundary: '{inst.name}' spacing set to {len:0.##} m.", inst);
        }
        if (fixedRuns > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("City Wall Spacing", fixedRuns > 0
            ? $"Fixed {fixedRuns} wall run(s). Save the scene."
            : "No SplineInstantiate wall runs found in the open scene.", "OK");
    }

    static void RelocateOutOfArc(string prefabPath)
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Art")) AssetDatabase.CreateFolder("Assets/_Game", "Art");
        if (!AssetDatabase.IsValidFolder(RelocateFolder)) AssetDatabase.CreateFolder("Assets/_Game/Art", "CityWall");

        var deps = AssetDatabase.GetDependencies(prefabPath, true)
            .Where(p => p.StartsWith("Assets/ARC/") && !p.EndsWith(".cs"))
            .ToList();

        int moved = 0;
        foreach (var src in deps)
        {
            string dst = AssetDatabase.GenerateUniqueAssetPath($"{RelocateFolder}/{Path.GetFileName(src)}");
            string err = AssetDatabase.MoveAsset(src, dst);
            if (string.IsNullOrEmpty(err)) moved++;
            else Debug.LogWarning($"PIMLR City Boundary: couldn't move {src}: {err}");
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"PIMLR City Boundary: moved {moved} wall asset(s) out of ARC into {RelocateFolder}: {string.Join(", ", deps.Select(Path.GetFileName))}");
    }
}
