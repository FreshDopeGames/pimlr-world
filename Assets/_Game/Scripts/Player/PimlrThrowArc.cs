// PIMLR #GREN: the player's water-balloon trajectory arrow. A flat, narrow ribbon that follows the predicted flight
// path from the hand for the first part of the arc (arrowLengthFraction, default half), ending in an arrow head, in
// semi-transparent blue with a white border (the same fill + outline idea as the objective arrow, PimlrObjectiveArrow).
// Rebuilt every frame while the player aims (LT), so it bends and stretches as the left stick changes the distance.
// Materials: Assets/_Game/Materials/ThrowArc/ (assigned on Resources/PIMLR/PlayerGrenades.asset).
//
// Unreal note: a Spline Mesh (or a procedural mesh) driven by UGameplayStatics::PredictProjectilePath, with a
// translucent unlit material and a second spline mesh for the border.

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PimlrThrowArc : MonoBehaviour
{
    const int PathSamples = 40;

    Mesh fillMesh, outlineMesh;
    MeshRenderer fillRenderer, outlineRenderer;

    readonly List<Vector3> path = new List<Vector3>(PathSamples + 1);
    readonly List<float> pathLen = new List<float>(PathSamples + 1);
    readonly List<Vector3> verts = new List<Vector3>(512);
    readonly List<int> tris = new List<int>(1024);
    readonly List<Vector2> loop = new List<Vector2>(256);
    Vector3 side;

    public static PimlrThrowArc Create(Material fill, Material outline)
    {
        var go = new GameObject("PIMLR Throw Arc");
        var arc = go.AddComponent<PimlrThrowArc>();
        arc.fillMesh = arc.AddPart("Fill", fill, out arc.fillRenderer);
        arc.outlineMesh = arc.AddPart("Outline", outline, out arc.outlineRenderer);
        arc.Hide();
        return arc;
    }

    Mesh AddPart(string name, Material mat, out MeshRenderer mr)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = "PIMLR_ThrowArc_" + name };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return mesh;
    }

    public void Hide()
    {
        if (fillRenderer) fillRenderer.enabled = false;
        if (outlineRenderer) outlineRenderer.enabled = false;
    }

    void OnDestroy()
    {
        if (fillMesh) Destroy(fillMesh);
        if (outlineMesh) Destroy(outlineMesh);
    }

    /// <summary>Draws the arrow along p(t) = p0 + v t + g t²/2 for t in [0, flightTime * s.arrowLengthFraction].</summary>
    public void Show(Vector3 p0, Vector3 v, Vector3 g, float flightTime, PimlrPlayerGrenadeSettings s)
    {
        if (!s || flightTime <= 0f) { Hide(); return; }

        Vector3 flat = new Vector3(v.x, 0f, v.z);
        if (flat.sqrMagnitude < 0.0001f) { Hide(); return; }
        side = Vector3.Cross(Vector3.up, flat.normalized); // the ribbon lies flat across the direction of travel

        // Sample the path and its running length.
        path.Clear(); pathLen.Clear();
        float tEnd = flightTime * s.arrowLengthFraction;
        float len = 0f;
        for (int i = 0; i <= PathSamples; i++)
        {
            float t = tEnd * i / PathSamples;
            Vector3 p = p0 + v * t + 0.5f * g * t * t;
            if (i > 0) len += Vector3.Distance(path[i - 1], p);
            path.Add(p); pathLen.Add(len);
        }
        if (len < 0.3f) { Hide(); return; }

        float halfW = s.arrowWidth * 0.5f;
        float headHalf = Mathf.Max(s.arrowHeadWidth * 0.5f, halfW * 1.2f);
        float headLen = Mathf.Min(s.arrowHeadLength, len * 0.45f);
        float shaftEnd = len - headLen;

        // ---- fill: shaft strip, then head strip (both follow the curve)
        verts.Clear(); tris.Clear();
        int prev = -1;
        for (int i = 0; i <= PathSamples && pathLen[i] < shaftEnd; i++) prev = Row(pathLen[i], halfW, prev);
        prev = Row(shaftEnd, halfW, prev);
        prev = -1;
        const int headRows = 8;
        for (int r = 0; r <= headRows; r++)
        {
            float k = r / (float)headRows;
            prev = Row(shaftEnd + headLen * k, headHalf * (1f - k), prev);
        }
        Apply(fillMesh);

        // ---- outline: a band of arrowOutlineWidth just outside the arrow's edge
        if (s.arrowOutlineWidth > 0f && outlineRenderer && outlineRenderer.sharedMaterial)
        {
            loop.Clear();
            const float step = 0.25f;
            Edge(new Vector2(0f, -halfW), new Vector2(shaftEnd, -halfW), step);
            Edge(new Vector2(shaftEnd, -halfW), new Vector2(shaftEnd, -headHalf), step);
            Edge(new Vector2(shaftEnd, -headHalf), new Vector2(len, 0f), step);
            Edge(new Vector2(len, 0f), new Vector2(shaftEnd, headHalf), step);
            Edge(new Vector2(shaftEnd, headHalf), new Vector2(shaftEnd, halfW), step);
            Edge(new Vector2(shaftEnd, halfW), new Vector2(0f, halfW), step);
            Edge(new Vector2(0f, halfW), new Vector2(0f, -halfW), step);
            BuildBand(s.arrowOutlineWidth);
            Apply(outlineMesh);
            outlineRenderer.enabled = true;
        }
        else if (outlineRenderer) outlineRenderer.enabled = false;

        fillRenderer.enabled = true;
    }

    // One cross-section of the fill at arc length sAlong; joins it to the previous one.
    int Row(float sAlong, float half, int prevRow)
    {
        int a = verts.Count;
        verts.Add(Map(sAlong, -half));
        verts.Add(Map(sAlong, half));
        if (prevRow >= 0)
        {
            tris.Add(prevRow); tris.Add(prevRow + 1); tris.Add(a + 1);
            tris.Add(prevRow); tris.Add(a + 1); tris.Add(a);
        }
        return a;
    }

    // Adds points from a (inclusive) toward b (exclusive) at most `step` apart: the closed loop never repeats a point.
    void Edge(Vector2 a, Vector2 b, float step)
    {
        float d = Vector2.Distance(a, b);
        if (d < 0.0001f) return;
        int n = Mathf.Max(1, Mathf.CeilToInt(d / step));
        for (int i = 0; i < n; i++) loop.Add(Vector2.Lerp(a, b, i / (float)n));
    }

    // The loop runs counter-clockwise in (along, across) space, so (dy, -dx) of each edge points outward.
    void BuildBand(float width)
    {
        verts.Clear(); tris.Clear();
        int n = loop.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = loop[i];
            Vector2 d0 = (p - loop[(i - 1 + n) % n]).normalized;
            Vector2 d1 = (loop[(i + 1) % n] - p).normalized;
            Vector2 n0 = new Vector2(d0.y, -d0.x), n1 = new Vector2(d1.y, -d1.x);
            Vector2 m = (n0 + n1); m = m.sqrMagnitude > 0.0001f ? m.normalized : n1;
            float scale = Mathf.Min(width / Mathf.Max(0.2f, Vector2.Dot(m, n1)), width * 3f); // mitred, capped at sharp tips
            Vector2 o = p + m * scale;
            verts.Add(Map(p.x, p.y));
            verts.Add(Map(o.x, o.y));
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 2, b = ((i + 1) % n) * 2;
            tris.Add(a); tris.Add(a + 1); tris.Add(b + 1);
            tris.Add(a); tris.Add(b + 1); tris.Add(b);
        }
    }

    // (arc length along the path, sideways offset) -> world position. Extrapolates past either end along the tangent.
    Vector3 Map(float sAlong, float across)
    {
        int last = path.Count - 1;
        Vector3 p;
        if (sAlong <= 0f) p = path[0] + (path[1] - path[0]).normalized * sAlong;
        else if (sAlong >= pathLen[last]) p = path[last] + (path[last] - path[last - 1]).normalized * (sAlong - pathLen[last]);
        else
        {
            int i = 1;
            while (i < last && pathLen[i] < sAlong) i++;
            float seg = pathLen[i] - pathLen[i - 1];
            p = Vector3.Lerp(path[i - 1], path[i], seg > 0f ? (sAlong - pathLen[i - 1]) / seg : 0f);
        }
        return p + side * across;
    }

    void Apply(Mesh m)
    {
        m.Clear();
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
    }
}
