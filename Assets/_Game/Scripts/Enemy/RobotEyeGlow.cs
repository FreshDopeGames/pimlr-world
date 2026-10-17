using System.Collections.Generic;
using JUTPS;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// PIMLR: glowing red robot eyes. Flicker on when the robot steps out of its portal, pulse while it hunts,
/// glitch while it's being shocked to death, then sputter out.
///
/// Swaps the material on the enemy's eye meshes (Renderer_EyeLeft / Renderer_EyeRight) for the shared unlit
/// eye material and drives its colour with MaterialPropertyBlocks (no per-enemy material copies). A soft
/// additive halo sprite on each eye bone fakes bloom, so the glow reads with post-processing off.
/// Added automatically by PimlrRobotFX.Attach. Look & feel: Resources/PimlrRobotFX.asset.
/// </summary>
[DisallowMultipleComponent]
public class RobotEyeGlow : MonoBehaviour
{
    [Tooltip("Eye meshes. Filled automatically with child renderers named like Renderer_EyeLeft / Renderer_EyeRight.")]
    public Renderer[] eyeRenderers;
    [Tooltip("Where the halos sit. Filled automatically from the humanoid LeftEye / RightEye bones.")]
    public Transform[] eyeAnchors;

    enum State { Off, Booting, On, Malfunction, PoweringDown }

    static MaterialPropertyBlock mpb;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    PimlrRobotFXSettings s;
    State state;
    float stateStart, stateLength, nextFlicker, flickerValue = 1f, level;
    bool initialised;
    int[] eyeSlotCounts;
    Transform[] halos;
    MeshRenderer[] haloRenderers;
    Color color;
    float brightness;

    /// <summary>Current glow level (0 = dark, 1 = normal, >1 = overload spike).</summary>
    public float Level => level;
    public bool IsOn => state != State.Off;

    void Awake() { Init(); }

    void OnEnable()
    {
        Init();
        var health = GetComponent<JUHealth>();
        if (health && health.IsDead) { TurnOff(); return; }
        SetState(s.bootUpTime > 0f ? State.Booting : State.On, s.bootUpTime);
    }

    void OnDisable() { HideHalos(); }

    void Init()
    {
        if (initialised) return;
        initialised = true;
        s = PimlrRobotFX.Settings;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        color = s.eyeColor;
        brightness = s.eyeBrightness;

        if (eyeRenderers == null || eyeRenderers.Length == 0) eyeRenderers = FindEyeRenderers();
        eyeSlotCounts = new int[eyeRenderers.Length];
        for (int i = 0; i < eyeRenderers.Length; i++)
        {
            var r = eyeRenderers[i];
            if (!r) continue;
            var mats = r.sharedMaterials;
            if (s.eyeMaterial)
            {
                for (int m = 0; m < mats.Length; m++) mats[m] = s.eyeMaterial;
                r.sharedMaterials = mats;
            }
            r.shadowCastingMode = ShadowCastingMode.Off;
            eyeSlotCounts[i] = mats.Length;
        }

        if (eyeAnchors == null || eyeAnchors.Length == 0) eyeAnchors = FindEyeAnchors();
        if (s.eyeHalo) BuildHalos();
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Back to the alive state (boot-up flicker).</summary>
    public void ResetGlow()
    {
        if (!enabled) { enabled = true; return; } // OnEnable boots the eyes
        SetState(s.bootUpTime > 0f ? State.Booting : State.On, s.bootUpTime);
    }

    /// <summary>Glitchy flicker until PowerDown() is called.</summary>
    public void Malfunction() { if (enabled) SetState(State.Malfunction, 0f); }

    /// <summary>Fade the eyes out with a few dying sputters.</summary>
    public void PowerDown(float fadeTime) { if (enabled) SetState(State.PoweringDown, Mathf.Max(0.01f, fadeTime)); }

    /// <summary>Instantly dead eyes.</summary>
    public void TurnOff()
    {
        Init();
        SetState(State.Off, 0f);
        level = 0f;
        ApplyEyes();
        HideHalos();
    }

    /// <summary>Per-robot colour, e.g. orange eyes on a boss: GetComponent&lt;RobotEyeGlow&gt;().SetColor(...).</summary>
    public void SetColor(Color newColor, float newBrightness = -1f)
    {
        Init();
        color = newColor;
        if (newBrightness > 0f) brightness = newBrightness;
    }

    // ---------------------------------------------------------------- loop

    void Update()
    {
        float now = Time.time;
        float t = stateLength > 0f ? Mathf.Clamp01((now - stateStart) / stateLength) : 1f;

        switch (state)
        {
            case State.Booting:
                if (now >= nextFlicker)
                {
                    flickerValue = Random.value < t * 1.3f ? 1f : 0.03f; // more "on" as the boot finishes
                    nextFlicker = now + Random.Range(0.03f, 0.08f);
                }
                level = flickerValue;
                if (t >= 1f) SetState(State.On, 0f);
                break;

            case State.On:
                level = 1f - s.pulseAmount * (0.5f + 0.5f * Mathf.Sin(now * s.pulseSpeed * Mathf.PI * 2f));
                break;

            case State.Malfunction:
                if (now >= nextFlicker)
                {
                    flickerValue = Random.value < s.malfunctionDropChance
                        ? Random.Range(0f, 0.15f)
                        : Random.Range(0.8f, 1.7f); // overload spikes
                    nextFlicker = now + Random.Range(0.5f, 1.5f) / Mathf.Max(1f, s.malfunctionHz);
                }
                level = flickerValue;
                break;

            case State.PoweringDown:
                if (now >= nextFlicker)
                {
                    flickerValue = Random.value < 0.25f ? 0.2f : 1f; // sputter
                    nextFlicker = now + Random.Range(0.04f, 0.1f);
                }
                level = (1f - t) * (1f - t) * flickerValue;
                if (t >= 1f)
                {
                    TurnOff();
                    enabled = false; // dead robot: stop ticking
                    return;
                }
                break;

            default:
                level = 0f;
                break;
        }

        // Every frame on purpose: FrostTint clears property blocks when an enemy thaws.
        ApplyEyes();
    }

    void LateUpdate()
    {
        if (halos == null) return;
        Camera cam = PimlrRobotFX.MainCamera;
        if (!cam || level < 0.01f) { HideHalos(); return; }

        Vector3 camPos = cam.transform.position;
        if ((camPos - transform.position).sqrMagnitude > s.haloMaxDistance * s.haloMaxDistance) { HideHalos(); return; }

        Quaternion camRot = cam.transform.rotation;
        float scaleFix = Mathf.Abs(transform.lossyScale.x) > 0.0001f ? transform.lossyScale.x : 1f;
        float size = s.haloSize * Mathf.Lerp(0.7f, 1.15f, Mathf.Clamp01(level));
        Color hc = color * (s.haloIntensity * level);
        hc.a = 1f;

        for (int i = 0; i < halos.Length; i++)
        {
            Transform anchor = eyeAnchors[i];
            if (!anchor || !halos[i]) { if (haloRenderers[i]) haloRenderers[i].enabled = false; continue; }

            // Nudge towards the camera so the face doesn't cut the halo in half.
            Vector3 p = anchor.position;
            Vector3 toCam = camPos - p;
            float dist = toCam.magnitude;
            if (dist > 0.001f) p += toCam / dist * Mathf.Min(s.haloSize * 0.6f, dist * 0.5f);

            halos[i].SetPositionAndRotation(p, camRot);
            halos[i].localScale = Vector3.one * (size / scaleFix);

            haloRenderers[i].GetPropertyBlock(mpb, 0);
            mpb.SetColor(BaseColorId, hc);
            haloRenderers[i].SetPropertyBlock(mpb, 0);
            haloRenderers[i].enabled = true;
        }
    }

    // ---------------------------------------------------------------- internals

    void SetState(State newState, float length)
    {
        state = newState;
        stateStart = Time.time;
        stateLength = length;
        nextFlicker = 0f;
        flickerValue = newState == State.Booting ? 0f : 1f;
    }

    void ApplyEyes()
    {
        if (eyeRenderers == null) return;
        Color c = color * (brightness * level);
        c.a = 1f;
        for (int i = 0; i < eyeRenderers.Length; i++)
        {
            var r = eyeRenderers[i];
            if (!r) continue;
            for (int m = 0; m < eyeSlotCounts[i]; m++)
            {
                r.GetPropertyBlock(mpb, m);
                mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(mpb, m);
            }
        }
    }

    void HideHalos()
    {
        if (haloRenderers == null) return;
        foreach (var r in haloRenderers) if (r) r.enabled = false;
    }

    Renderer[] FindEyeRenderers()
    {
        var found = new List<Renderer>();
        if (!string.IsNullOrEmpty(s.eyeRendererName))
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.name.IndexOf(s.eyeRendererName, System.StringComparison.OrdinalIgnoreCase) >= 0) found.Add(r);
        return found.ToArray();
    }

    Transform[] FindEyeAnchors()
    {
        var found = new List<Transform>();
        Animator anim = GetComponent<Animator>();
        if (anim && anim.isHuman)
        {
            Transform l = anim.GetBoneTransform(HumanBodyBones.LeftEye);
            Transform r = anim.GetBoneTransform(HumanBodyBones.RightEye);
            if (l) found.Add(l);
            if (r) found.Add(r);
        }
        if (found.Count == 0)
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "LeftEye" || t.name == "RightEye") found.Add(t);
        return found.ToArray();
    }

    void BuildHalos()
    {
        Material mat = PimlrRobotFX.GlowMaterial;
        if (!mat || eyeAnchors.Length == 0) return;

        halos = new Transform[eyeAnchors.Length];
        haloRenderers = new MeshRenderer[eyeAnchors.Length];
        for (int i = 0; i < eyeAnchors.Length; i++)
        {
            var go = new GameObject("RobotEyeHalo");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = PimlrRobotFX.QuadMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.enabled = false;
            halos[i] = go.transform;
            haloRenderers[i] = mr;
        }
    }
}
