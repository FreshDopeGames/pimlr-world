using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PIMLR: makes AI path AROUND cars instead of into them.
/// The city NavMesh is baked from Default-layer colliders only, so the walkable area runs straight under every
/// vehicle (they're on the Vehicle layer / are rigidbodies). Enemies planned paths through parked cars, hit them and
/// got stuck. This gives every vehicle a carving NavMeshObstacle sized to its body, so a parked car cuts its own
/// footprint out of the NavMesh. "Carve only stationary" means a car only carves once it has stopped, so moving
/// traffic costs nothing.
/// Installed automatically on every scene load by PimlrInputRuntime; vehicles enabled later are handled too.
/// </summary>
public static class PimlrVehicleNavObstacles
{
    public static float Margin = 0.25f; // extra clearance around the body, metres

    public static void InstallAll()
    {
        var roots = new HashSet<GameObject>();
        foreach (var v in Object.FindObjectsOfType<JUTPS.VehicleSystem.Vehicle>(true)) roots.Add(v.gameObject);
        foreach (var v in Object.FindObjectsOfType<RCC_CarControllerV3>(true)) roots.Add(v.gameObject);
        foreach (var go in roots) Install(go);
    }

    public static void Install(GameObject vehicle)
    {
        if (!vehicle || vehicle.GetComponent<NavMeshObstacle>()) return;
        if (!TryLocalBounds(vehicle.transform, out Bounds b)) return;

        var obstacle = vehicle.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = b.center;
        obstacle.size = b.size + new Vector3(Margin * 2f, 0f, Margin * 2f);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.carvingTimeToStationary = 0.5f;
        obstacle.carvingMoveThreshold = 0.2f;
    }

    /// <summary>The vehicle's solid colliders as one box in the vehicle's own (local) space, so a car parked at an
    /// angle gets a tight rotated box rather than an inflated world-aligned one.</summary>
    static bool TryLocalBounds(Transform root, out Bounds result)
    {
        result = default;
        bool any = false;
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger || col is WheelCollider) continue;

            Bounds local; Transform t = col.transform;
            if (col is BoxCollider box) local = new Bounds(box.center, box.size);
            else if (col is MeshCollider mc && mc.sharedMesh) local = mc.sharedMesh.bounds;
            else if (col is CapsuleCollider cap) local = new Bounds(cap.center, Vector3.one * cap.radius * 2f + Vector3.up * cap.height);
            else if (col is SphereCollider sph) local = new Bounds(sph.center, Vector3.one * sph.radius * 2f);
            else continue;

            Vector3 e = local.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 p = root.InverseTransformPoint(t.TransformPoint(corner));
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                else result.Encapsulate(p);
            }
        }
        // Ignore absurd results (e.g. a huge trigger-less helper collider) rather than carving half the street.
        if (any && (result.size.x > 30f || result.size.z > 30f)) return false;
        return any;
    }
}
