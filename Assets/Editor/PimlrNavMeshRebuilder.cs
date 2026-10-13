using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// PIMLR: replaces YannicksWorld's legacy whole-scene NavMesh bake (every static mesh, rooftops included,
/// 3.7 MB in Assets/IDEA/PIMLR/YannicksWorld/NavMesh.asset) with one NavMeshSurface that only covers
/// street level inside the city boundary, built from physics colliders.
/// Also removes the 8 stray, never-baked NavMeshSurface components sitting on street prefab pieces.
/// Menu: PIMLR / Level / Rebuild Street NavMesh
/// </summary>
public static class PimlrNavMeshRebuilder
{
    const string SurfaceName = "PIMLR_NavMesh";
    const float HeightAboveGround = 5f;   // agents only need street level; this cuts rooftops/upper floors out
    const float DepthBelowGround = 2f;
    const float MinRegionArea = 8f;       // drops tiny islands on car roofs, benches, planters

    [MenuItem("PIMLR/Level/Rebuild Street NavMesh")]
    public static void Rebuild()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!EditorUtility.DisplayDialog("Street NavMesh",
                $"Rebuild the NavMesh for '{scene.name}'?\n\n" +
                "- Removes stray NavMeshSurface components on street pieces\n" +
                "- Clears the old whole-scene bake\n" +
                $"- Bakes one surface: inside the city walls, street level only (+{HeightAboveGround} m)\n\n" +
                "Save the scene afterwards.", "Rebuild", "Cancel"))
            return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild Street NavMesh");

        // 1) stray surfaces
        int removed = 0;
        foreach (var s in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s.gameObject.scene != scene || s.gameObject.name == SurfaceName) continue;
            Debug.Log($"PIMLR NavMesh: removing stray NavMeshSurface on '{s.gameObject.name}'.", s.gameObject);
            Undo.DestroyObjectImmediate(s);
            removed++;
        }

        // 2) where is the playable area?
        if (!TryGetCityBounds(scene, out Bounds xz))
        {
            EditorUtility.DisplayDialog("Street NavMesh",
                "Couldn't find the city boundary. Run PIMLR > Level > Build City Boundary first (or keep 'City Walls' enabled).", "OK");
            return;
        }
        float groundY = EstimateGroundY(xz);

        // 3) the surface
        var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == SurfaceName);
        GameObject go = existing ? existing : new GameObject(SurfaceName);
        if (!existing) Undo.RegisterCreatedObjectUndo(go, "Create NavMesh Surface");
        go.transform.SetPositionAndRotation(new Vector3(xz.center.x, groundY, xz.center.z), Quaternion.identity);
        go.transform.localScale = Vector3.one;

        var surface = go.GetComponent<NavMeshSurface>();
        if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(go); // (no ?? - Unity's fake-null)
        Undo.RecordObject(surface, "Configure NavMesh Surface");
        surface.agentTypeID = 0; // Humanoid - what every enemy agent uses
        surface.collectObjects = CollectObjects.Volume;
        surface.size = new Vector3(xz.size.x, HeightAboveGround + DepthBelowGround, xz.size.z);
        surface.center = new Vector3(0f, (HeightAboveGround - DepthBelowGround) * 0.5f, 0f);
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1 << 0; // Default only: ignores Player, Vehicle, Item, Bullet, Triggers, etc.
        surface.minRegionArea = MinRegionArea;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;

        // 4) drop the legacy scene bake, then bake the surface (asset saved next to the scene)
        UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);

        NavMeshAssetManager.instance.StartBakingSurfaces(new Object[] { surface });

        Debug.Log($"PIMLR NavMesh: removed {removed} stray surface(s); baking '{SurfaceName}' over {xz.size.x:0}x{xz.size.z:0} m at ground Y {groundY:0.##}. " +
                  "The old Assets/IDEA/PIMLR/YannicksWorld/NavMesh.asset is no longer referenced - delete it once enemies path correctly.", go);
        Selection.activeGameObject = go;
    }

    static bool TryGetCityBounds(UnityEngine.SceneManagement.Scene scene, out Bounds b)
    {
        b = default;
        bool has = false;
        var roots = scene.GetRootGameObjects();

        // New spline boundary: use its barrier colliders (computed from transforms, works while inactive too).
        var boundary = roots.FirstOrDefault(g => g.name == "PIMLR_CityBoundary");
        if (boundary)
        {
            foreach (var box in boundary.GetComponentsInChildren<BoxCollider>(true))
                Encapsulate(ref b, ref has, box.transform, box.center, box.size);
        }
        // Fallback: the hand-placed walls.
        if (!has)
        {
            var walls = roots.FirstOrDefault(g => g.name == "City Walls");
            if (walls)
                foreach (var box in walls.GetComponentsInChildren<BoxCollider>(true))
                    Encapsulate(ref b, ref has, box.transform, box.center, box.size);
        }
        return has;
    }

    static void Encapsulate(ref Bounds b, ref bool has, Transform t, Vector3 center, Vector3 size)
    {
        Vector3 e = size * 0.5f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            Vector3 w = t.TransformPoint(corner);
            if (!has) { b = new Bounds(w, Vector3.zero); has = true; } else b.Encapsulate(w);
        }
    }

    // Street height from what's under a grid of points inside the city (the streets).
    static float EstimateGroundY(Bounds xz)
    {
        var hits = new List<float>();
        for (int ix = 1; ix < 8; ix++)
        for (int iz = 1; iz < 8; iz++)
        {
            var p = new Vector3(Mathf.Lerp(xz.min.x, xz.max.x, ix / 8f), xz.max.y + 200f, Mathf.Lerp(xz.min.z, xz.max.z, iz / 8f));
            // top surface at each sample point: open street or a rooftop
            if (Physics.Raycast(p, Vector3.down, out var hit, 600f, 1 << 0, QueryTriggerInteraction.Ignore))
                hits.Add(hit.point.y);
        }
        if (hits.Count == 0) return 0f;
        hits.Sort();
        return hits[hits.Count / 4]; // lower quartile = street level (rooftops sit above it)
    }
}
