using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// PIMLR #STF #CAM: shared helpers for scripted camera moves that respect buildings.
//
// Rules, in priority order (Craig, Nov 11):
//   1. Hold the cinematic height set on the first shot (the fan). Never climb to get around a building.
//   2. Move through the open space between buildings: follow the walkable route (the paths the player can take to
//      the fan / enemies), pushed to the middle of the street rather than hugging walls.
//   3. Only if no such route exists, go over the rooftops.
//   4. Only if that fails too, cut (never clip through a wall).
// Every shot and every point of every route keeps at least CameraClearance metres from level geometry, so the near
// plane never slides into a wall while the camera turns.
// Shots also keep tree canopies and bushes out of the view line (they have no colliders, so physics can't see them):
// a shot that would look through leaves first moves closer to the subject along the same angle, in front of the
// tree, before another angle is tried (Craig, Nov 12).
//
//   FindShot       camera spot around a subject with a clear view, preferring open space over walkable ground
//   FindOpenShot   same, but scored by how open each side is (the street side of a parked car)
//   Fly            moves a camera along a route planned by the rules above
//
// Used by MissionCameraDirector (Save The Fans intros) and GameExecutionManager.MoveToTargetAndBack (car reveal).
//
// Unreal: a runtime spline built from UNavigationSystemV1::FindPathToLocationSynchronously, centred with sphere
// traces left/right, driving a CineCameraActor; same clearance sweeps.
public static class PimlrCinematicCamera
{
    /// <summary>Buildings and props are on Default in YannicksWorld.</summary>
    public const int DefaultMask = 1;

    /// <summary>Minimum distance from any wall for a shot or a route point.</summary>
    public const float CameraClearance = 1.2f;
    /// <summary>Sweep radius along routes (camera body + near plane).</summary>
    const float SweepRadius = 0.9f;
    /// <summary>Last street-level try for narrow alleys (still clear of the near plane).</summary>
    const float NarrowSweepRadius = 0.5f;
    const float SampleStep = 2.5f;
    const float StreetProbe = 25f;
    const float MaxCentreShift = 12f;

    // ------------------------------------------------------------------ lens

    /// <summary>
    /// The gameplay rig moves a pivot ("Camera Pivot"); the actual lens (Main Camera) sits 3 m behind and 0.5 m to
    /// the side of it. Scripted shots and routes are planned for the lens, so while a cinematic runs the lens is put
    /// onto the pivot (local offset zeroed) and the pivot is moved. Restore() puts the rig back exactly as it was.
    /// HomePosition / HomeRotation = where the lens was, i.e. where a move back to gameplay should end.
    /// </summary>
    public sealed class LensRig
    {
        readonly Transform pivot, lens;
        readonly Vector3 pivotPos, lensLocalPos;
        readonly Quaternion pivotRot, lensLocalRot;
        public Vector3 HomePosition { get; }
        public Quaternion HomeRotation { get; }

        public LensRig(Transform pivot)
        {
            this.pivot = pivot;
            pivotPos = pivot.position;
            pivotRot = pivot.rotation;
            var main = Camera.main;
            lens = main && main.transform != pivot && main.transform.IsChildOf(pivot) ? main.transform : null;
            if (lens)
            {
                lensLocalPos = lens.localPosition;
                lensLocalRot = lens.localRotation;
                HomePosition = lens.position;
                HomeRotation = lens.rotation;
                pivot.SetPositionAndRotation(HomePosition, HomeRotation);
                lens.position = pivot.position;
                lens.rotation = pivot.rotation;
            }
            else
            {
                HomePosition = pivotPos;
                HomeRotation = pivotRot;
            }
        }

        public void Restore()
        {
            if (lens)
            {
                lens.localPosition = lensLocalPos;
                lens.localRotation = lensLocalRot;
            }
            if (pivot) pivot.SetPositionAndRotation(pivotPos, pivotRot);
        }
    }

    // ------------------------------------------------------------------ shot selection

    /// <summary>
    /// A camera spot around focus with a clear view, at least CameraClearance from walls. Angles near preferredDir win,
    /// then open space and being above walkable ground. reachableFrom (usually the player): strongly prefer spots the
    /// player could walk to from there, so the shot sits on the player's side of buildings and fences and the move to
    /// it can follow the streets. Pulls in closer if nothing fits at full distance.
    /// </summary>
    public static Vector3 FindShot(Vector3 focus, Vector3 preferredDir, float distance, float height, int mask = DefaultMask, Vector3? reachableFrom = null)
    {
        Vector3 fwd = Flat(preferredDir);
        bool haveStart = false;
        NavMeshHit startHit = default;
        if (reachableFrom.HasValue) haveStart = NavMesh.SamplePosition(reachableFrom.Value, out startHit, 6f, NavMesh.AllAreas);
        var reachPath = haveStart ? new NavMeshPath() : null;
        // Full clearance at shrinking distances first; only then accept a tighter spot.
        foreach (var (scale, clearance) in new[] { (1f, CameraClearance), (0.8f, CameraClearance), (0.6f, CameraClearance), (0.45f, CameraClearance),
                                                  (1f, 0.9f), (0.7f, 0.9f), (0.5f, 0.9f) })
        {
            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                if (angle > 180f) angle -= 360f;
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * fwd;
                // PIMLR #CAM: leaves in the way -> step in toward the subject on this same angle until the view is clear.
                // If the canopy hangs over the subject, closer never clears it: then drop under the leaves instead.
                Vector3 p = Vector3.zero;
                float pulledIn = -1f, dropped = 0f;
                foreach (float hgt in height > UnderCanopyHeight ? new[] { height, UnderCanopyHeight } : new[] { height })
                {
                    for (float d = distance * scale; d >= MinFoliagePullIn - 0.01f; d -= 0.75f)
                    {
                        Vector3 q = focus + dir * d + Vector3.up * hgt;
                        if (!ShotValid(focus, q, mask, clearance)) break;      // a wall: closer won't help on this angle
                        if (!FoliageBlocks(focus, q)) { p = q; pulledIn = distance * scale - d; break; }
                    }
                    if (pulledIn >= 0f) { dropped = height - hgt; break; }
                }
                if (pulledIn < 0f) continue;

                float open = Mathf.Min(Openness(p, mask), 8f);
                // Craig: leaves in the way -> move closer rather than swing round, so the first 2 m of pulling in cost
                // very little; past that the shot gets steep (camera stays at cinematic height), so it costs more.
                float score = open - Mathf.Abs(angle) / 30f + (OverWalkable(p) ? 2f : 0f)
                              - pulledIn * 0.1f - Mathf.Max(0f, pulledIn - 2f) * 0.8f - dropped * 0.5f;
                if (haveStart && NavMesh.SamplePosition(p, out var gp, 8f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(startHit.position, gp.position, NavMesh.AllAreas, reachPath) &&
                    reachPath.status == NavMeshPathStatus.PathComplete)
                    score += 12f; // reachable on foot from the player: beats any amount of openness or angle
                if (score > bestScore) { bestScore = score; best = p; }
            }
            if (bestScore > float.MinValue) return best;
        }
        return PulledIn(focus, fwd, distance, height, mask);
    }

    /// <summary>
    /// Shot on the most open side of the subject (street rather than wall). sideAxis: if set, the subject's flanks
    /// (perpendicular to it, e.g. a car's sides) are preferred.
    /// </summary>
    public static Vector3 FindOpenShot(Vector3 focus, float distance, float height, Vector3? sideAxis = null, int mask = DefaultMask)
    {
        Vector3 axis = sideAxis.HasValue ? Flat(sideAxis.Value) : Vector3.zero;
        foreach (float scale in new[] { 1f, 0.8f, 0.6f })
        {
            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * 22.5f, Vector3.up) * Vector3.forward;
                Vector3 p = focus + dir * distance * scale + Vector3.up * height;
                if (!ShotValid(focus, p, mask)) continue;
                // PIMLR #CAM: no shots through leaves; step in on this side if a tree is between.
                for (float d = distance * scale - 0.75f; FoliageBlocks(focus, p) && d >= MinFoliagePullIn; d -= 0.75f)
                {
                    Vector3 q = focus + dir * d + Vector3.up * height;
                    if (!ShotValid(focus, q, mask)) break;
                    p = q;
                }
                if (FoliageBlocks(focus, p)) continue;

                // How far you can see past the subject on this side (street = long, wall = short).
                Vector3 probeFrom = focus + dir * 2.5f;
                float open = Physics.SphereCast(probeFrom, 0.35f, dir, out var hit, 30f, mask, QueryTriggerInteraction.Ignore) ? hit.distance : 30f;
                float score = open + Mathf.Min(Openness(p, mask), 8f);
                if (sideAxis.HasValue) score *= Mathf.Lerp(0.55f, 1f, 1f - Mathf.Abs(Vector3.Dot(dir, axis)));
                if (score > bestScore) { bestScore = score; best = p; }
            }
            if (bestScore > float.MinValue) return best;
        }
        return PulledIn(focus, sideAxis.HasValue ? Vector3.Cross(Vector3.up, axis) : Vector3.forward, distance, height, mask);
    }

    static bool ShotValid(Vector3 focus, Vector3 p, int mask, float clearance = CameraClearance) =>
        !Physics.CheckSphere(p, clearance, mask, QueryTriggerInteraction.Ignore) && LineClear(focus, p, mask);

    static Vector3 PulledIn(Vector3 focus, Vector3 fwd, float distance, float height, int mask)
    {
        Vector3 dir = (Flat(fwd) * distance + Vector3.up * height).normalized;
        float len = new Vector2(distance, height).magnitude;
        if (Physics.SphereCast(focus, 0.3f, dir, out var hit, len, mask, QueryTriggerInteraction.Ignore))
            return focus + dir * Mathf.Max(1.5f, hit.distance - CameraClearance);
        return focus + dir * len;
    }

    /// <summary>Distance to the nearest wall around p (8 horizontal probes, capped).</summary>
    static float Openness(Vector3 p, int mask)
    {
        float nearest = 20f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 d = Quaternion.AngleAxis(i * 45f, Vector3.up) * Vector3.forward;
            if (Physics.Raycast(p, d, out var hit, 20f, mask, QueryTriggerInteraction.Ignore)) nearest = Mathf.Min(nearest, hit.distance);
        }
        return nearest;
    }

    static bool OverWalkable(Vector3 p) => NavMesh.SamplePosition(p, out var hit, 8f, NavMesh.AllAreas) && FlatDist(hit.position, p) < 1.5f;

    // ------------------------------------------------------------------ paths

    public enum RouteKind { Straight, Street, Rooftop, Cut }

    public class Path
    {
        public readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> cumulative = new List<float>();
        public RouteKind kind;
        public bool isCut => kind == RouteKind.Cut;
        public float Length { get; private set; }

        internal void Finish()
        {
            cumulative.Clear();
            Length = 0f;
            cumulative.Add(0f);
            for (int i = 1; i < points.Count; i++)
            {
                Length += Vector3.Distance(points[i - 1], points[i]);
                cumulative.Add(Length);
            }
        }

        public Vector3 Evaluate(float k)
        {
            if (points.Count == 0) return Vector3.zero;
            if (isCut) return k < 0.5f ? points[0] : points[points.Count - 1];
            if (points.Count == 1 || Length <= 0.0001f) return points[points.Count - 1];
            float d = Mathf.Clamp01(k) * Length;
            for (int i = 1; i < points.Count; i++)
            {
                if (d <= cumulative[i])
                {
                    float seg = cumulative[i] - cumulative[i - 1];
                    return Vector3.Lerp(points[i - 1], points[i], seg > 0f ? (d - cumulative[i - 1]) / seg : 1f);
                }
            }
            return points[points.Count - 1];
        }

        /// <summary>Direction of travel at k (flat), for framing during the move.</summary>
        public Vector3 Tangent(float k)
        {
            Vector3 a = Evaluate(Mathf.Max(0f, k - 0.02f)), b = Evaluate(Mathf.Min(1f, k + 0.02f));
            return Flat(b - a);
        }
    }

    /// <summary>A route from 'from' to 'to' following the rules at the top of this file.</summary>
    public static Path Plan(Vector3 from, Vector3 to, int mask = DefaultMask)
    {
        // The gameplay camera (start/end of a move) may sit closer to a wall than CameraClearance (it has its own
        // collision). Segments touching an endpoint are swept with whatever room that endpoint actually has.
        endRadiusFrom = FreeRadius(from, mask);
        endRadiusTo = FreeRadius(to, mask);

        // 1. Clear straight line (with clearance): take it.
        if (SegmentClear(from, to, mask, Mathf.Min(SweepRadius, endRadiusFrom, endRadiusTo)))
        {
            var p = new Path { kind = RouteKind.Straight };
            p.points.Add(from); p.points.Add(to);
            p.Finish();
            return p;
        }

        // 2. Down the middle of the streets at the established height. Fallbacks, still at street level: no
        //    smoothing, then the plain walk path, then a tighter clearance for narrow alleys.
        var street = StreetRoute(from, to, mask, centre: true, smooth: true, SweepRadius)
                  ?? StreetRoute(from, to, mask, centre: true, smooth: false, SweepRadius)
                  ?? StreetRoute(from, to, mask, centre: false, smooth: false, SweepRadius)
                  ?? StreetRoute(from, to, mask, centre: false, smooth: false, NarrowSweepRadius);
        if (street != null) return street;

        // 3. Over the rooftops.
        var crane = CraneRoute(from, to, mask);
        if (crane != null) return crane;

        // 4. Cut.
        var cut = new Path { kind = RouteKind.Cut };
        cut.points.Add(from); cut.points.Add(to);
        cut.Finish();
        return cut;
    }

    static Path StreetRoute(Vector3 from, Vector3 to, int mask, bool centre, bool smooth, float sweep)
    {
        if (!NavMesh.SamplePosition(from, out var ga, 30f, NavMesh.AllAreas)) return null;
        if (!NavMesh.SamplePosition(to, out var gb, 30f, NavMesh.AllAreas)) return null;
        var nav = new NavMeshPath();
        if (!NavMesh.CalculatePath(ga.position, gb.position, NavMesh.AllAreas, nav) || nav.status == NavMeshPathStatus.PathInvalid) return null;

        // Heights above the ground: cruise at the cinematic height (the higher of the two shots; the gameplay
        // camera is the low one), easing up from the start over the first quarter and down to the end over the last.
        float hFrom = Mathf.Max(2.5f, from.y - ga.position.y);
        float hTo = Mathf.Max(2.5f, to.y - gb.position.y);
        float cruise = Mathf.Max(hFrom, hTo);

        // Densify the walk path (ground level). A partial path (the walkable area is split by a low fence or kerb
        // the camera flies over anyway) is finished with a straight hop to the target; the clearance check decides.
        var cornerList = new List<Vector3>(nav.corners);
        if (cornerList.Count == 0) return null;
        if (nav.status == NavMeshPathStatus.PathPartial && FlatDist(cornerList[cornerList.Count - 1], gb.position) > 0.5f)
            cornerList.Add(gb.position);
        var ground = new List<Vector3>();
        var c = cornerList.ToArray();
        for (int i = 0; i < c.Length - 1; i++)
        {
            float len = Vector3.Distance(c[i], c[i + 1]);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / SampleStep));
            for (int j = 0; j < n; j++) ground.Add(Vector3.Lerp(c[i], c[i + 1], j / (float)n));
        }
        ground.Add(c[c.Length - 1]);
        if (ground.Count < 2) return null;

        float total = 0f;
        var dist = new float[ground.Count];
        for (int i = 1; i < ground.Count; i++) { total += Vector3.Distance(ground[i - 1], ground[i]); dist[i] = total; }

        // Lift to camera height and push each point to the middle of the open space across the walk direction.
        var pts = new List<Vector3>(ground.Count);
        for (int i = 0; i < ground.Count; i++)
        {
            float k = total > 0f ? dist[i] / total : 1f;
            float h = k < 0.25f ? Mathf.Lerp(hFrom, cruise, Mathf.SmoothStep(0f, 1f, k / 0.25f))
                    : k > 0.75f ? Mathf.Lerp(cruise, hTo, Mathf.SmoothStep(0f, 1f, (k - 0.75f) / 0.25f))
                    : cruise;
            Vector3 p = ground[i] + Vector3.up * h;
            Vector3 dir = Flat(ground[Mathf.Min(i + 1, ground.Count - 1)] - ground[Mathf.Max(i - 1, 0)]);
            pts.Add(centre ? Centre(p, dir, mask) : p);
        }

        // Smooth (moving average, twice), keeping the first and last few points tied to the endpoints.
        for (int pass = 0; smooth && pass < 2; pass++)
        {
            var s = new List<Vector3>(pts);
            for (int i = 1; i < pts.Count - 1; i++) s[i] = (pts[i - 1] + pts[i] * 2f + pts[i + 1]) * 0.25f;
            pts = s;
        }

        var path = new Path { kind = RouteKind.Street };
        path.points.Add(from);
        // Skip route points right next to the endpoints so the camera doesn't double back to the walk path.
        for (int i = 1; i < pts.Count - 1; i++)
        {
            if (Vector3.Distance(pts[i], from) < 2f || Vector3.Distance(pts[i], to) < 2f) continue;
            path.points.Add(pts[i]);
        }
        path.points.Add(to);

        // Shortcut where a direct hop is clear and stays in open space (keeps the move from zig-zagging).
        Simplify(path.points, mask, sweep);

        if (!PolylineClear(path.points, mask, sweep)) return null;
        path.Finish();
        return path;
    }

    /// <summary>
    /// Shift p sideways (perpendicular to dir) to the roomiest spot across the street at its height: lateral offsets
    /// up to MaxCentreShift are scored by their distance to the nearest geometry in every direction, so fire escapes,
    /// awnings and signs sticking out at camera height count, not just the building faces.
    /// </summary>
    static Vector3 Centre(Vector3 p, Vector3 dir, int mask)
    {
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 best = p;
        float bestScore = Physics.CheckSphere(p, CameraClearance, mask, QueryTriggerInteraction.Ignore) ? float.MinValue : Mathf.Min(Openness(p, mask), 8f);
        for (float off = -MaxCentreShift; off <= MaxCentreShift; off += 1f)
        {
            if (Mathf.Approximately(off, 0f)) continue;
            Vector3 q = p + side * off;
            if (Physics.CheckSphere(q, CameraClearance, mask, QueryTriggerInteraction.Ignore)) continue;
            // Don't jump across a wall to a parallel street.
            if (!LineClear(p, q, mask)) continue;
            float score = Mathf.Min(Openness(q, mask), 8f) - Mathf.Abs(off) * 0.05f;
            if (score > bestScore) { bestScore = score; best = q; }
        }
        return best;
    }

    static void Simplify(List<Vector3> pts, int mask, float sweep)
    {
        int i = 0;
        while (i < pts.Count - 2)
        {
            // Drop pts[i+1] if i -> i+2 is clear and the drop is small (keeps the centred line).
            Vector3 a = pts[i], b = pts[i + 1], c = pts[i + 2];
            float dev = DistanceToSegment(b, a, c);
            float r = sweep;
            if (i == 0) r = Mathf.Min(r, endRadiusFrom);
            if (i + 2 == pts.Count - 1) r = Mathf.Min(r, endRadiusTo);
            if (dev < 1.5f && SegmentClear(a, c, mask, r)) pts.RemoveAt(i + 1);
            else i++;
        }
    }

    static Path CraneRoute(Vector3 from, Vector3 to, int mask)
    {
        float roof = RoofHeightAlong(from, to, mask);
        float cruise = Mathf.Max(roof + 4f, Mathf.Max(from.y, to.y) + 2f);
        for (int attempt = 0; attempt < 4; attempt++, cruise += 8f)
        {
            Vector3 up = new Vector3(from.x, cruise, from.z);
            Vector3 over = new Vector3(to.x, cruise, to.z);
            if (!SegmentClear(from, up, mask, Mathf.Min(SweepRadius, endRadiusFrom)) || !SegmentClear(up, over, mask) ||
                !SegmentClear(over, to, mask, Mathf.Min(SweepRadius, endRadiusTo))) continue;
            var path = new Path { kind = RouteKind.Rooftop };
            path.points.Add(from); path.points.Add(up); path.points.Add(over); path.points.Add(to);
            path.Finish();
            return path;
        }
        return null;
    }

    // ------------------------------------------------------------------ flying

    /// <summary>
    /// Moves cam to toPos/toRot along a planned route. duration is the minimum; long routes take longer (capped at
    /// 2.5x) so the move doesn't whip. skip() returning true ends the move early. lookAt: keep this point framed
    /// mid-move (the next subject); without it the camera faces along the route mid-move.
    /// </summary>
    public static IEnumerator Fly(Transform cam, Vector3 toPos, Quaternion toRot, float duration,
        AnimationCurve ease = null, Func<bool> skip = null, int mask = DefaultMask, Vector3? lookAt = null)
    {
        Vector3 fromPos = cam.position;
        Quaternion fromRot = cam.rotation;
        Path path = Plan(fromPos, toPos, mask);
        LastRoute = path.kind;

        if (path.isCut)
        {
            cam.SetPositionAndRotation(toPos, toRot);
            yield break;
        }

        float straight = Vector3.Distance(fromPos, toPos);
        float dur = duration;
        if (straight > 0.01f && path.Length > straight * 1.2f)
            dur = Mathf.Min(duration * 2.5f, duration * path.Length / straight);

        bool winding = path.kind != RouteKind.Straight;
        float t = 0f;
        while (t < dur)
        {
            if (skip != null && skip()) yield break;
            t += Time.deltaTime;
            float lin = Mathf.Clamp01(t / dur);
            float k = ease != null ? ease.Evaluate(lin) : Mathf.SmoothStep(0f, 1f, lin);
            Vector3 p = path.Evaluate(k);

            Quaternion rot = Quaternion.Slerp(fromRot, toRot, k);
            if (winding)
            {
                // Mid-route, look where we're going (or at the next subject) instead of sweeping across walls.
                Vector3 look = lookAt.HasValue ? lookAt.Value - p : path.Tangent(k);
                if (look.sqrMagnitude > 0.01f)
                {
                    Quaternion travel = Quaternion.LookRotation(look.normalized, Vector3.up);
                    float w = Mathf.Sin(k * Mathf.PI); // 0 at both ends so the start and end framing are exact
                    rot = Quaternion.Slerp(rot, travel, w * 0.85f);
                }
            }
            cam.SetPositionAndRotation(p, rot);
            yield return null;
        }
        cam.SetPositionAndRotation(toPos, toRot);
    }

    /// <summary>Which rule the last Fly used (for testing).</summary>
    public static RouteKind LastRoute { get; private set; }

    // ------------------------------------------------------------------ geometry helpers

    // ------------------------------------------------------------------ foliage (PIMLR #CAM)
    // Trees and bushes have no colliders on their leaves (only a thin trunk capsule), so Physics can't see them.
    // Each one is modelled as an upright cylinder: trees = the canopy only (from ~30% of their height, measured on
    // "Tree 1": leaves start 2.1 m up a 6.7 m tree), bushes = the whole bush. Built once per scene from the renderers.

    /// <summary>Material names (lower case, "contains") that mark leaves. TreeUse = the street trees, Leaves = bushes.</summary>
    public static readonly string[] FoliageMaterialKeywords = { "treeuse", "leaves", "leaf", "foliage" };
    const float MinFoliagePullIn = 2.5f;     // never pull a shot closer than this to escape leaves
    const float UnderCanopyHeight = 0.4f;    // camera height above the subject focus (~1.9 m above ground: under street-tree leaves, which start ~2.1 m up)
    const float FoliageRadiusScale = 0.85f;  // canopy edges are sparse
    const float TreeCanopyStart = 0.3f;      // fraction of tree height where the leaves begin

    struct Foliage { public Vector2 xz; public float radius, bottom, top; }
    static List<Foliage> foliage;
    static UnityEngine.SceneManagement.Scene foliageScene;

    static List<Foliage> FoliageCache()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (foliage != null && foliageScene == scene) return foliage;
        foliageScene = scene;
        foliage = new List<Foliage>();
        var done = new HashSet<Transform>();
        foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
        {
            if (!r.enabled || !IsFoliage(r)) continue;
            var lod = r.GetComponentInParent<LODGroup>();
            Transform key = lod ? lod.transform : r.transform;
            if (!done.Add(key)) continue;                                // one cylinder per tree, not per LOD
            Bounds b = r.bounds;
            if (lod) foreach (var lr in lod.GetComponentsInChildren<MeshRenderer>()) if (IsFoliage(lr)) b.Encapsulate(lr.bounds);
            float h = b.size.y;
            bool tree = h > 3f;
            foliage.Add(new Foliage
            {
                xz = new Vector2(b.center.x, b.center.z),
                radius = Mathf.Max(b.extents.x, b.extents.z) * FoliageRadiusScale,
                bottom = b.min.y + (tree ? h * TreeCanopyStart : 0f),
                top = b.max.y,
            });
        }
        return foliage;
    }

    static bool IsFoliage(Renderer r)
    {
        foreach (var m in r.sharedMaterials)
        {
            if (!m) continue;
            string n = m.name.ToLowerInvariant();
            foreach (var k in FoliageMaterialKeywords) if (n.Contains(k)) return true;
        }
        return false;
    }

    /// <summary>True if the straight view from a to b passes through (or starts inside) a tree canopy or a bush.</summary>
    public static bool FoliageBlocks(Vector3 a, Vector3 b)
    {
        var list = FoliageCache();
        if (list.Count == 0) return false;
        Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z);
        float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x), minZ = Mathf.Min(a.z, b.z), maxZ = Mathf.Max(a.z, b.z);
        float len = Vector3.Distance(a, b);
        int steps = Mathf.Max(2, Mathf.CeilToInt(len / 0.4f));
        foreach (var f in list)
        {
            if (f.xz.x + f.radius < minX || f.xz.x - f.radius > maxX || f.xz.y + f.radius < minZ || f.xz.y - f.radius > maxZ) continue;
            // The subject's own end is skipped (0.6 m) so a fan standing at a bush's edge isn't "blocked" by it.
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                if (t * len < 0.6f) continue;
                Vector3 p = Vector3.Lerp(a, b, t);
                if (p.y < f.bottom || p.y > f.top) continue;
                if ((new Vector2(p.x, p.z) - f.xz).sqrMagnitude <= f.radius * f.radius) return true;
            }
        }
        return false;
    }

    public static bool LineClear(Vector3 a, Vector3 b, int mask = DefaultMask)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return true;
        return !Physics.SphereCast(a, 0.3f, d / len, out _, len, mask, QueryTriggerInteraction.Ignore);
    }

    static bool SegmentClear(Vector3 a, Vector3 b, int mask, float radius = SweepRadius)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return true;
        // Both directions: a sweep that starts inside a collider doesn't report it.
        return !Physics.SphereCast(a, radius, d / len, out _, len, mask, QueryTriggerInteraction.Ignore)
            && !Physics.SphereCast(b, radius, -d / len, out _, len, mask, QueryTriggerInteraction.Ignore);
    }

    static float endRadiusFrom = SweepRadius, endRadiusTo = SweepRadius;

    /// <summary>Largest sphere (up to SweepRadius) that fits at p.</summary>
    static float FreeRadius(Vector3 p, int mask)
    {
        for (float r = SweepRadius; r > 0.1f; r -= 0.1f)
            if (!Physics.CheckSphere(p, r, mask, QueryTriggerInteraction.Ignore)) return r;
        return 0.05f;
    }

    static bool PolylineClear(List<Vector3> pts, int mask, float radius = SweepRadius)
    {
        for (int i = 1; i < pts.Count; i++)
        {
            float r = radius;
            if (i == 1) r = Mathf.Min(r, endRadiusFrom);
            if (i == pts.Count - 1) r = Mathf.Min(r, endRadiusTo);
            if (!SegmentClear(pts[i - 1], pts[i], mask, r)) return false;
        }
        return true;
    }

    static float RoofHeightAlong(Vector3 from, Vector3 to, int mask)
    {
        float top = Mathf.Max(from.y, to.y);
        float startY = top + 200f;
        const int samples = 24;
        for (int i = 0; i <= samples; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, i / (float)samples);
            for (int j = -1; j <= 1; j++)
            {
                Vector3 side = Vector3.Cross(Vector3.up, Flat(to - from)) * (j * 1.5f);
                if (Physics.Raycast(new Vector3(p.x, startY, p.z) + side, Vector3.down, out var hit, 400f, mask, QueryTriggerInteraction.Ignore))
                    top = Mathf.Max(top, hit.point.y);
            }
        }
        return top;
    }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }

    static float FlatDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
