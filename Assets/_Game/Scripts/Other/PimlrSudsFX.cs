using UnityEngine;

/// <summary>
/// PIMLR (post-playtest): makes the water gun's soap-suds impact fall and squish instead of floating up.
/// Added at runtime to each impact FX spawned by a Bullet with "Pimlr Suds Physics" ticked (WaterGunBullet).
///   - Stays put: detached from whatever was hit (JUTPS parents impact FX to the target), world-space bubbles.
///   - Gravity: the Epic Toon FX "BubbleBlastBigUnderwater" bubbles float upward (gravity -0.1, it's an
///     underwater effect). This gives them real gravity.
///   - Ground: a cheap collision plane at the floor under the hit, so suds land and settle instead of falling
///     through the street (no per-particle world raycasts - fine for WebGL).
///   - Squash: each bubble pops out wide and flat, springs tall, wobbles back to round, then flattens as it
///     settles at the end of its life. Applied on top of the effect's own size-over-lifetime.
/// Tune the static values below (they apply to every impact).
/// </summary>
public class PimlrSudsFX : MonoBehaviour
{
    public static float Gravity = 0.8f;            // 1 = full Physics.gravity
    public static bool LandOnGround = true;
    public static float GroundBounce = 0.15f;
    public static float GroundDampen = 0.7f;      // speed lost on landing
    public static float GroundSearch = 12f;       // metres to look down for the floor
    public static bool Squash = true;
    public static float SquashAmount = 0.35f;     // 0 = none, 0.35 = 35% flatter at the start
    public static float SettleFlatten = 0.25f;    // how flat they get as they settle at the end of their life
    // Child systems left alone (the flash isn't a bubble).
    public static string[] Skip = { "Glow" };

    void Awake()
    {
        // Stay where the hit happened. JUTPS parents impact FX to whatever was hit, so on a running enemy the whole
        // effect (and its landing plane) was dragged along. Detach it and simulate the bubbles in world space.
        transform.SetParent(null, true);

        Transform ground = LandOnGround ? MakeGroundPlane() : null;

        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (Skipped(ps.name)) continue;

            var main = ps.main;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(Gravity);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            if (ground != null)
            {
                var col = ps.collision;
                col.enabled = true;
                col.type = ParticleSystemCollisionType.Planes;
                col.SetPlane(0, ground);
                col.bounce = new ParticleSystem.MinMaxCurve(GroundBounce);
                col.dampen = new ParticleSystem.MinMaxCurve(GroundDampen);
                col.lifetimeLoss = new ParticleSystem.MinMaxCurve(0f);
                col.radiusScale = 0.5f;
                col.sendCollisionMessages = false;
            }

            if (Squash) ApplySquash(ps);
        }
    }

    static bool Skipped(string n)
    {
        foreach (var s in Skip) if (n.IndexOf(s, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    Transform MakeGroundPlane()
    {
        // Start a little above the hit so a hit ON the floor still finds the floor.
        Vector3 from = transform.position + Vector3.up * 0.5f;
        // Nearest STATIC surface below: skip anything with a Rigidbody (the enemy that was hit, cars, props).
        RaycastHit hit = default;
        bool found = false;
        float best = float.MaxValue;
        foreach (var h in Physics.RaycastAll(from, Vector3.down, GroundSearch, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.attachedRigidbody != null || h.distance >= best) continue;
            best = h.distance; hit = h; found = true;
        }
        if (!found) return null;

        var plane = new GameObject("SudsGround").transform;
        plane.SetParent(transform, true);           // destroyed with the FX
        plane.position = hit.point;
        plane.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal); // plane's up = collision normal
        return plane;
    }

    // Separate-axes size over lifetime = the effect's own size curve x a squash-and-stretch wobble.
    static void ApplySquash(ParticleSystem ps)
    {
        var sol = ps.sizeOverLifetime;
        bool had = sol.enabled;
        ParticleSystem.MinMaxCurve baseX = had ? sol.x : new ParticleSystem.MinMaxCurve(1f);
        ParticleSystem.MinMaxCurve baseY = had ? (sol.separateAxes ? sol.y : sol.x) : new ParticleSystem.MinMaxCurve(1f);

        const int N = 16;
        var kx = new Keyframe[N];
        var ky = new Keyframe[N];
        float s = SquashAmount, f = SettleFlatten;
        for (int i = 0; i < N; i++)
        {
            float t = i / (float)(N - 1);
            // Damped spring: starts squashed (y low), overshoots tall, settles round by ~45% of its life.
            float wobble = -s * Mathf.Exp(-7f * t) * Mathf.Cos(t * 18f);
            // Ease into a flat "puddle" over the last 30% of its life.
            float settle = -f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));
            float y = 1f + wobble + settle;
            float x = 1f / Mathf.Sqrt(Mathf.Max(0.2f, y)); // keep the area roughly constant: flatter = wider

            ky[i] = new Keyframe(t, Mathf.Max(0f, Eval(baseY, t) * y));
            kx[i] = new Keyframe(t, Mathf.Max(0f, Eval(baseX, t) * x));
        }

        sol.enabled = true;
        sol.separateAxes = true;
        sol.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(kx));
        sol.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(ky));
        sol.z = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(kx));
    }

    static float Eval(ParticleSystem.MinMaxCurve c, float t)
    {
        switch (c.mode)
        {
            case ParticleSystemCurveMode.Constant: return c.constant;
            case ParticleSystemCurveMode.Curve: return c.curveMultiplier * c.curve.Evaluate(t);
            case ParticleSystemCurveMode.TwoConstants: return (c.constantMin + c.constantMax) * 0.5f;
            case ParticleSystemCurveMode.TwoCurves:
                return c.curveMultiplier * (c.curveMin.Evaluate(t) + c.curveMax.Evaluate(t)) * 0.5f;
        }
        return 1f;
    }
}
