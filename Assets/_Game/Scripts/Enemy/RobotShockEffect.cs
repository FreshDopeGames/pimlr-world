using System.Collections.Generic;
using JUTPS;
using JUTPS.PhysicsScripts;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// PIMLR: robot enemies short-circuit when they die. While JU's ragdoll takes over, jagged electric arcs jump
/// between their bones, sparks spray, limbs twitch, a blue light flickers and the eyes (RobotEyeGlow) glitch,
/// then power down.
///
/// Triggers on JUHealth.IsDead, then waits a moment for AdvancedRagdollController to switch the ragdoll on.
/// (Not on "ragdoll switched on" alone - JU also ragdolls living characters on big physics hits.)
/// Bones are cached while the enemy is alive: JU unparents the Hips from the root when it ragdolls.
/// Added automatically by PimlrRobotFX.Attach. Look & feel: Resources/PimlrRobotFX.asset.
/// WebGL-safe: LineRenderers + a Shuriken particle system, no VFX Graph.
/// </summary>
[DisallowMultipleComponent]
public class RobotShockEffect : MonoBehaviour
{
    struct Arc
    {
        public LineRenderer lr;
        public Transform a, b;
        public Vector3 offA, offB, strayDir;
        public bool stray;
        public float endTime;
    }

    static readonly HumanBodyBones[] ArcBones =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
        HumanBodyBones.Neck, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
    };

    static int activeLights, activeShocks;
    static float lastZapSoundTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { activeLights = 0; activeShocks = 0; lastZapSoundTime = 0f; }

    PimlrRobotFXSettings s;
    JUHealth health;
    AdvancedRagdollController ragdoll;
    RobotEyeGlow eyes;

    readonly List<Transform> bones = new List<Transform>();
    Rigidbody[] awakeBodies;
    Rigidbody[] bodies;

    Arc[] arcs;
    Vector3[] points;
    Transform fxRoot;
    ParticleSystem sparkPS;
    Light shockLight;
    AudioSource zapAudio;

    bool built, shocking, done, ownsLight, countsAsActive;
    float deadSince = -1f, shockStart, nextZap, nextZapSound;

    public bool IsShocking => shocking;

    void Awake()
    {
        s = PimlrRobotFX.Settings;
        health = GetComponent<JUHealth>();
        ragdoll = GetComponent<AdvancedRagdollController>();
        CollectBones();
    }

    void OnDisable()
    {
        if (shocking) EndShock(false);
        ReleaseLight();
        HideAllArcs();
    }

    void OnDestroy()
    {
        ReleaseLight();
        ReleaseActive();
    }

    /// <summary>Back to "alive" (runs by itself if the enemy is resurrected).</summary>
    public void ResetEffect()
    {
        if (shocking) EndShock(false);
        done = false;
        deadSince = -1f;
        HideAllArcs();
        if (sparkPS) sparkPS.Clear(true);
        if (!eyes) eyes = GetComponent<RobotEyeGlow>();
        if (eyes) eyes.ResetGlow();
    }

    /// <summary>Start the shock now. Normally automatic on death.</summary>
    [ContextMenu("Test Shock (Play Mode)")]
    public void TriggerShock()
    {
        if (shocking || !isActiveAndEnabled) return;
        done = true; // one shock per death
        if (!eyes) eyes = GetComponent<RobotEyeGlow>();

        if (bones.Count < 2 || activeShocks >= s.maxConcurrentShocks)
        {
            // No skeleton, or a whole wave died at once: skip the sparks, still kill the eyes.
            if (eyes) eyes.PowerDown(s.eyePowerDownTime);
            return;
        }

        Build();
        bodies = ragdoll && ragdoll.RagdollBones != null && ragdoll.RagdollBones.Length > 0 ? ragdoll.RagdollBones : awakeBodies;
        shocking = true;
        shockStart = nextZap = nextZapSound = Time.time;
        activeShocks++;
        countsAsActive = true;

        if (s.flickerLight && shockLight && activeLights < s.maxConcurrentLights)
        {
            activeLights++;
            ownsLight = true;
        }

        if (eyes) eyes.Malfunction();
    }

    // ---------------------------------------------------------------- loop

    void Update()
    {
        if (!shocking)
        {
            if (!health) return;
            if (!health.IsDead)
            {
                if (done) ResetEffect(); // resurrected (or the context-menu test finished)
                deadSince = -1f;
                return;
            }
            if (done) return;

            // JU switches the ragdoll on the frame after death; start then so the twitches land on live bodies.
            if (deadSince < 0f) deadSince = Time.time;
            if ((ragdoll && ragdoll.RagdollEnabled) || Time.time - deadSince > 0.25f) TriggerShock();
            return;
        }

        float now = Time.time;
        float t = (now - shockStart) / Mathf.Max(0.05f, s.shockDuration);
        if (t >= 1f)
        {
            EndShock(true);
            return;
        }

        float k = Mathf.Clamp01(s.shockStrength.Evaluate(t));
        if (now >= nextZap)
        {
            if (k > 0.02f) Zap(k);
            nextZap = now + Random.Range(s.zapInterval.x, s.zapInterval.y) / Mathf.Max(k, 0.25f);
        }
    }

    // Ragdoll bones move in physics; draw after everything has settled for the frame.
    void LateUpdate()
    {
        if (!built) return;
        UpdateArcs(Time.time);

        if (shockLight && shockLight.enabled)
        {
            shockLight.intensity = Mathf.MoveTowards(shockLight.intensity, 0f, s.lightIntensity * 12f * Time.deltaTime);
            if (shockLight.intensity <= 0.001f) shockLight.enabled = false;
        }
    }

    void EndShock(bool powerDownEyes)
    {
        shocking = false;
        ReleaseLight();
        ReleaseActive();
        if (powerDownEyes && eyes) eyes.PowerDown(s.eyePowerDownTime);
    }

    void ReleaseLight()
    {
        if (ownsLight)
        {
            ownsLight = false;
            activeLights = Mathf.Max(0, activeLights - 1);
        }
        if (shockLight)
        {
            shockLight.intensity = 0f;
            shockLight.enabled = false;
        }
    }

    void ReleaseActive()
    {
        if (!countsAsActive) return;
        countsAsActive = false;
        activeShocks = Mathf.Max(0, activeShocks - 1);
    }

    // ---------------------------------------------------------------- zap

    void Zap(float k)
    {
        int ia = Random.Range(0, bones.Count);
        Transform a = bones[ia];
        if (!a) return;
        Vector3 pa = a.position;

        if (arcs != null)
        {
            Transform b = Random.value < s.strayArcChance ? null : PickNeighbour(ia, pa);
            SpawnArc(a, b, k);
        }

        if (sparkPS) EmitSparks(pa, k);
        if (s.twitch) Twitch(k);

        if (ownsLight && shockLight)
        {
            shockLight.transform.position = pa;
            shockLight.intensity = Mathf.Max(shockLight.intensity, s.lightIntensity * Random.Range(0.6f, 1f) * k);
            shockLight.enabled = true;
        }

        PlayZapSound(pa, k);
    }

    Transform PickNeighbour(int ia, Vector3 pa)
    {
        float maxSqr = s.maxArcLength * s.maxArcLength;
        for (int tries = 0; tries < 6; tries++)
        {
            int ib = Random.Range(0, bones.Count);
            if (ib == ia || !bones[ib]) continue;
            float d = (bones[ib].position - pa).sqrMagnitude;
            if (d > 0.0025f && d < maxSqr) return bones[ib];
        }
        return null;
    }

    void SpawnArc(Transform a, Transform b, float k)
    {
        int slot = 0;
        float oldest = float.MaxValue;
        for (int i = 0; i < arcs.Length; i++)
        {
            if (!arcs[i].lr.enabled) { slot = i; break; }
            if (arcs[i].endTime < oldest) { oldest = arcs[i].endTime; slot = i; }
        }

        ref Arc arc = ref arcs[slot];
        arc.a = a;
        arc.b = b;
        arc.stray = b == null;
        arc.offA = Random.insideUnitSphere * 0.04f;
        arc.offB = Random.insideUnitSphere * 0.04f;
        arc.strayDir = Random.onUnitSphere * Random.Range(0.15f, 0.4f);
        arc.endTime = Time.time + Random.Range(s.arcLifetime.x, s.arcLifetime.y);
        arc.lr.widthMultiplier = Random.Range(s.arcWidth.x, s.arcWidth.y) * Mathf.Lerp(0.5f, 1f, k);
        arc.lr.enabled = true;
        DrawBolt(ref arc);
    }

    void UpdateArcs(float now)
    {
        if (arcs == null) return;
        for (int i = 0; i < arcs.Length; i++)
        {
            if (!arcs[i].lr.enabled) continue;
            if (now >= arcs[i].endTime || !arcs[i].a)
            {
                arcs[i].lr.enabled = false;
                continue;
            }
            DrawBolt(ref arcs[i]); // re-jitter every frame = flicker
        }
    }

    void DrawBolt(ref Arc arc)
    {
        Vector3 A = arc.a.position + arc.offA;
        Vector3 B = arc.stray || !arc.b ? A + arc.strayDir : arc.b.position + arc.offB;

        float len = Vector3.Distance(A, B);
        float jitter = s.arcJitter * Mathf.Clamp(len / 0.3f, 0.5f, 2f);
        int seg = points.Length - 1;

        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg;
            Vector3 p = Vector3.LerpUnclamped(A, B, t);
            if (i > 0)
            {
                // Pinned at both ends bone-to-bone, loose at the tip for stray arcs.
                float env = arc.stray ? t : Mathf.Sin(t * Mathf.PI);
                if (i < seg || arc.stray) p += Random.insideUnitSphere * (jitter * env);
            }
            points[i] = p;
        }
        arc.lr.SetPositions(points);
    }

    void EmitSparks(Vector3 pos, float k)
    {
        int count = Mathf.RoundToInt(Random.Range(s.sparksPerZap.x, s.sparksPerZap.y + 1) * Mathf.Lerp(0.4f, 1f, k));
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            ep.position = pos;
            ep.velocity = Random.onUnitSphere * Random.Range(s.sparkSpeed.x, s.sparkSpeed.y) + Vector3.up * 0.8f;
            sparkPS.Emit(ep, 1);
        }
    }

    void Twitch(float k)
    {
        if (bodies == null || bodies.Length == 0) return;
        Rigidbody rb = bodies[Random.Range(0, bodies.Length)];
        if (!rb || rb.isKinematic) return;
        rb.AddForce(Random.onUnitSphere * (s.twitchVelocity * k), ForceMode.VelocityChange);
        rb.AddTorque(Random.onUnitSphere * (s.twitchSpin * k), ForceMode.VelocityChange);
    }

    void PlayZapSound(Vector3 pos, float k)
    {
        if (!zapAudio) return;
        float now = Time.time;
        if (now < nextZapSound || now - lastZapSoundTime < 0.05f || Random.value > s.zapSoundChance) return;
        AudioClip clip = s.zapClips[Random.Range(0, s.zapClips.Length)];
        if (!clip) return;

        nextZapSound = now + Random.Range(0.12f, 0.3f);
        lastZapSoundTime = now;
        float crowd = 1f / (1f + 0.5f * Mathf.Max(0, activeShocks - 1)); // a wave dying doesn't become a wall of noise
        zapAudio.transform.position = pos;
        zapAudio.pitch = Random.Range(s.zapPitch.x, s.zapPitch.y);
        zapAudio.PlayOneShot(clip, s.zapVolume * Mathf.Lerp(0.5f, 1f, k) * crowd);
    }

    void HideAllArcs()
    {
        if (arcs == null) return;
        for (int i = 0; i < arcs.Length; i++)
            if (arcs[i].lr) arcs[i].lr.enabled = false;
    }

    // ---------------------------------------------------------------- setup

    void CollectBones()
    {
        bones.Clear();
        Animator anim = GetComponent<Animator>();
        if (!anim) anim = GetComponentInChildren<Animator>(true);
        if (anim && anim.isHuman)
            foreach (var hb in ArcBones)
            {
                Transform t = anim.GetBoneTransform(hb);
                if (t && !bones.Contains(t)) bones.Add(t);
            }

        if (bones.Count < 2)
        {
            var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr)
                foreach (var b in smr.bones)
                    if (b && !bones.Contains(b)) bones.Add(b);
        }

        var list = new List<Rigidbody>();
        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
            if (rb.transform != transform) list.Add(rb);
        awakeBodies = list.ToArray();
    }

    void Build()
    {
        if (built) return;
        built = true;

        fxRoot = new GameObject("RobotShockFX").transform;
        fxRoot.SetParent(transform, false);

        // Arcs
        Material arcMat = PimlrRobotFX.ArcMaterial;
        if (arcMat)
        {
            points = new Vector3[s.arcSegments + 1];
            arcs = new Arc[s.maxArcs];
            var taper = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.25f));
            for (int i = 0; i < arcs.Length; i++)
            {
                var go = new GameObject("Arc" + i);
                go.transform.SetParent(fxRoot, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = points.Length;
                lr.numCapVertices = 0;
                lr.numCornerVertices = 0;
                lr.alignment = LineAlignment.View;
                lr.textureMode = LineTextureMode.Stretch;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.lightProbeUsage = LightProbeUsage.Off;
                lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                lr.widthCurve = taper;
                lr.startColor = lr.endColor = Color.white;
                lr.sharedMaterial = arcMat;
                lr.enabled = false;
                arcs[i].lr = lr;
            }
        }

        // Sparks
        Material glowMat = PimlrRobotFX.GlowMaterial;
        if (s.sparks && glowMat)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(fxRoot, false);
            sparkPS = go.AddComponent<ParticleSystem>();
            sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = sparkPS.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.6f, 0.85f, 1f));
            main.gravityModifier = 1f;
            main.maxParticles = 150;

            var emission = sparkPS.emission;
            emission.enabled = false; // we Emit() by hand
            var shape = sparkPS.shape;
            shape.enabled = false;

            var sol = sparkPS.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 2f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = glowMat;

            sparkPS.Play(); // loops with zero emission - just simulates what we Emit()
        }

        // Light
        if (s.flickerLight)
        {
            var go = new GameObject("ShockLight");
            go.transform.SetParent(fxRoot, false);
            shockLight = go.AddComponent<Light>();
            shockLight.type = LightType.Point;
            shockLight.shadows = LightShadows.None;
            shockLight.color = s.lightColor;
            shockLight.range = s.lightRange;
            shockLight.intensity = 0f;
            shockLight.enabled = false;
        }

        // Sound
        if (s.zapClips != null && s.zapClips.Length > 0)
        {
            var go = new GameObject("ShockAudio");
            go.transform.SetParent(fxRoot, false);
            zapAudio = go.AddComponent<AudioSource>();
            zapAudio.playOnAwake = false;
            zapAudio.spatialBlend = 1f;
            zapAudio.rolloffMode = AudioRolloffMode.Linear;
            zapAudio.minDistance = 3f;
            zapAudio.maxDistance = s.zapSoundMaxDistance;
            zapAudio.dopplerLevel = 0f;
        }
    }
}
