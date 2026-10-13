using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// PIMLR: invisible boundary colliders that follow a wall spline. One tall box per spline segment (knot to knot),
/// a little longer than the segment so corners overlap and there are no seams to squeeze through.
/// Rebuilds automatically when you edit the spline in the Scene view. The wall meshes themselves come from the
/// SplineInstantiate component on the same object.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
public class PimlrSplineWallColliders : MonoBehaviour
{
    public const string ColliderPrefix = "Boundary Collider";

    [Tooltip("How tall the invisible barrier is (much taller than the walls so nothing can be jumped or launched over).")]
    public float height = 40f;
    [Tooltip("Barrier thickness. Thicker = harder for fast vehicles to tunnel through.")]
    public float thickness = 4f;
    [Tooltip("How far the barrier starts below the knot height.")]
    public float depthBelow = 8f;
    [Tooltip("Extra length added to BOTH ends of every segment so neighbouring segments overlap at corners.")]
    public float endOverlap = 3f;
    [Tooltip("Barrier length beyond the first/last knot of an open spline (covers the half wall that hangs past the end knot).")]
    public float openEndExtension = 8f;

    SplineContainer container;
    bool dirty;

    void OnEnable()
    {
        container = GetComponent<SplineContainer>();
        Spline.Changed += OnSplineChanged;
        if (transform.Find(ColliderPrefix + " 0") == null) Rebuild();
    }

    void OnDisable() => Spline.Changed -= OnSplineChanged;

    void OnValidate() => dirty = true;

    void OnSplineChanged(Spline spline, int knot, SplineModification mod)
    {
        if (!container) return;
        foreach (var s in container.Splines)
            if (s == spline) { dirty = true; return; }
    }

    void Update()
    {
        if (!dirty || Application.isPlaying) return;
        dirty = false;
        Rebuild();
    }

    [ContextMenu("Rebuild Boundary Colliders")]
    public void Rebuild()
    {
        if (!container) container = GetComponent<SplineContainer>();
        if (!container) return;

        var old = new List<GameObject>();
        foreach (Transform child in transform)
            if (child.name.StartsWith(ColliderPrefix)) old.Add(child.gameObject);
        foreach (var go in old)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        int n = 0;
        foreach (var spline in container.Splines)
        {
            int count = spline.Count;
            if (count < 2) continue;
            int segments = spline.Closed ? count : count - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = container.transform.TransformPoint((Vector3)spline[i].Position);
                Vector3 b = container.transform.TransformPoint((Vector3)spline[(i + 1) % count].Position);
                Vector3 flat = b - a; flat.y = 0f;
                if (flat.sqrMagnitude < 0.01f) continue;
                Vector3 dir = flat.normalized;

                float startExt = (!spline.Closed && i == 0) ? openEndExtension : endOverlap;
                float endExt = (!spline.Closed && i == segments - 1) ? openEndExtension : endOverlap;
                a -= dir * startExt;
                b += dir * endExt;

                var go = new GameObject($"{ColliderPrefix} {n++}");
                go.layer = gameObject.layer;
                go.isStatic = true;
                go.transform.SetParent(transform, false);
                float baseY = Mathf.Min(a.y, b.y) - depthBelow;
                Vector3 mid = (a + b) * 0.5f;
                go.transform.position = new Vector3(mid.x, baseY + height * 0.5f, mid.z);
                go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                var box = go.AddComponent<BoxCollider>();
                Vector3 ls = go.transform.lossyScale;
                box.size = new Vector3(thickness / Mathf.Max(0.0001f, ls.x), height / Mathf.Max(0.0001f, ls.y),
                                       Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z)) / Mathf.Max(0.0001f, ls.z));
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
        foreach (Transform child in transform)
        {
            if (!child.name.StartsWith(ColliderPrefix)) continue;
            var box = child.GetComponent<BoxCollider>();
            if (!box) continue;
            Gizmos.matrix = child.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
