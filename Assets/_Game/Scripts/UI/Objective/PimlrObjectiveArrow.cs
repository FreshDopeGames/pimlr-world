using System.Collections.Generic;
using UnityEngine;
using PIMLR.Missions.SaveTheFans;

/// <summary>
/// PIMLR #ARROW: a chunky yellow 3D arrow floating over the player's head (or over their vehicle) that points at
/// the current objective. Self-bootstrapping, one per game, no scene/prefab edits.
///
/// Built-in objectives (checked every frame, first match wins):
///   1. SetOverride(target) - anything else that wants the arrow (future missions, tutorials).
///   2. Save The Fans - while travelling to the next fan (mission state Intermission).
///   3. Story ending  - Zone Boss 2, player driving, police car alive.
///
/// Look/tuning: Resources/PIMLR/ObjectiveArrow.asset (PimlrObjectiveArrowSettings).
/// Model: Blender (ArtSource/PIMLR_ObjectiveArrow.blend -> _Game/Models/ObjectiveArrow/PIMLR_ObjectiveArrow.fbx),
/// assigned in the settings asset. The generated mesh below is only a fallback.
/// </summary>
[DefaultExecutionOrder(9000)] // after cameras/vehicles have moved
public class PimlrObjectiveArrow : MonoBehaviour
{
    public static PimlrObjectiveArrow Instance { get; private set; }

    // ------------------------------------------------------------------ public API
    static Transform overrideTarget; static float overrideHideDistance;
    /// <summary>Point the arrow at this until ClearOverride (takes priority over the built-in objectives).</summary>
    public static void SetOverride(Transform target, float hideWithinMeters = 4f) { overrideTarget = target; overrideHideDistance = hideWithinMeters; }
    public static void ClearOverride(Transform target = null) { if (!target || target == overrideTarget) overrideTarget = null; }

    /// <summary>Current objective the arrow is pointing at (null = hidden).</summary>
    public Transform CurrentTarget { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; overrideTarget = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance) return;
        var go = new GameObject("PIMLR_ObjectiveArrow");
        DontDestroyOnLoad(go);
        go.AddComponent<PimlrObjectiveArrow>();
    }

    // ------------------------------------------------------------------ state
    PimlrObjectiveArrowSettings s;
    Transform visual;          // bob/scale lives here; root carries position+rotation
    Transform player;
    JUTPS.ActionScripts.DriveVehicles driver;
    float shown;               // 0..1 pop-in
    Quaternion facing = Quaternion.identity;
    bool facingInit;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        s = PimlrObjectiveArrowSettings.Load();
        BuildVisual();
        visual.gameObject.SetActive(false);
    }

    void BuildVisual()
    {
        visual = new GameObject("Arrow").transform;
        visual.SetParent(transform, false);
        int layer = LayerMask.NameToLayer("Ignore Raycast");
        if (layer >= 0) visual.gameObject.layer = layer;

        var fill = s.customMesh ? s.customMesh : ArrowMeshBuilder.Build(0f);
        AddPart("Fill", fill, s.fillMaterial, layer);
        if (s.outlineMaterial)
        {
            Mesh shell = s.customMesh ? s.customOutlineMesh : ArrowMeshBuilder.Build(s.outlineThickness);
            if (shell) AddPart("Outline", shell, s.outlineMaterial, layer);
        }
    }

    void AddPart(string name, Mesh mesh, Material mat, int layer)
    {
        var go = new GameObject(name);
        if (layer >= 0) go.layer = layer;
        go.transform.SetParent(visual, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // ------------------------------------------------------------------ per frame
    void LateUpdate()
    {
        ResolvePlayer();
        Transform anchor = ResolveAnchor(out bool inVehicle);
        Transform target = PickTarget(out float hideWithin);

        bool want = target && anchor && !Blocked();
        if (want)
        {
            Vector3 a = anchor.position, t = target.position; a.y = t.y = 0f;
            if ((t - a).sqrMagnitude < hideWithin * hideWithin) want = false;
        }
        CurrentTarget = want ? target : null;

        shown = Mathf.MoveTowards(shown, want ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, s.popTime));
        bool visible = shown > 0.001f && anchor;
        if (visual.gameObject.activeSelf != visible) visual.gameObject.SetActive(visible);
        if (!visible) { facingInit = false; return; }

        // Position: over the head / over the car roof.
        Vector3 pos = anchor.position + Vector3.up * (inVehicle ? s.heightInVehicle : s.heightOnFoot);
        transform.position = pos;

        // Aim: full yaw, gentle pitch so it dips toward targets below and rises toward ones above.
        if (target)
        {
            Vector3 d = target.position - pos;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                float pitch = -Mathf.Atan2(d.y + s.targetAimHeight, flat.magnitude) * Mathf.Rad2Deg; // aim at the target's middle, not its feet
                pitch = Mathf.Clamp(pitch, -s.maxPitch, s.maxPitch);
                Quaternion want2 = Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(pitch, 0f, 0f);
                facing = facingInit ? Quaternion.Slerp(facing, want2, 1f - Mathf.Exp(-s.turnSpeed * Time.unscaledDeltaTime)) : want2;
                facingInit = true;
            }
        }
        // Readability: roll the flat face toward the camera around the pointing axis (direction is unchanged),
        // so a low third-person camera doesn't see the arrow edge-on.
        Quaternion rot = facing;
        var cam = Camera.main;
        if (cam && s.faceCamera > 0f)
        {
            Vector3 fwd = facing * Vector3.forward;
            Vector3 upWant = Vector3.ProjectOnPlane(cam.transform.position - pos, fwd);
            if (upWant.sqrMagnitude > 0.0001f)
            {
                Vector3 up = Vector3.Slerp(facing * Vector3.up, upWant.normalized, s.faceCamera);
                rot = Quaternion.LookRotation(fwd, up);
            }
        }
        transform.rotation = rot;

        // Life: bob + pulse + pop-in (ease-out-back).
        float time = Time.unscaledTime;
        float pop = EaseOutBack(shown);
        float pulse = 1f + Mathf.Sin(time * s.pulseSpeed) * s.pulseAmount;
        float size = (inVehicle ? s.sizeInVehicle : s.sizeOnFoot) * pop * pulse;
        visual.localScale = Vector3.one * size;
        visual.localPosition = Vector3.up * (Mathf.Sin(time * s.bobSpeed) * s.bobHeight);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        x = Mathf.Clamp01(x);
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    bool Blocked()
    {
        if (PimlrInterstitial.Playing) return true;                               // loading / zone card / fade
        var cam = MissionCameraDirector.Shared;
        if (cam && cam.IsPlaying) return true;                                    // fan intro / portal cutscene
        return false;
    }

    // ------------------------------------------------------------------ who / where
    void ResolvePlayer()
    {
        if (player && player.gameObject.activeInHierarchy) return;
        var p = GameObject.FindGameObjectWithTag("Player");
        player = p ? p.transform : null;
        driver = p ? p.GetComponent<JUTPS.ActionScripts.DriveVehicles>() : null;
    }

    Transform ResolveAnchor(out bool inVehicle)
    {
        inVehicle = PimlrInput.PlayerInVehicle;
        if (inVehicle && driver)
        {
            var v = driver.IsDriving ? driver.VehicleToDrive : driver.VehicleToShootWithDrive;
            if (v) return v.transform;
        }
        return player;
    }

    Transform PickTarget(out float hideWithin)
    {
        hideWithin = 4f;

        // 1. explicit override
        if (overrideTarget && overrideTarget.gameObject.activeInHierarchy) { hideWithin = overrideHideDistance; return overrideTarget; }

        // 2. Save The Fans: travelling to the next fan
        var stf = SaveTheFansMission.Instance;
        if (stf && stf.isActiveAndEnabled && stf.CurrentState == SaveTheFansMission.State.Intermission && stf.CurrentFan)
        {
            hideWithin = stf.CurrentFan.arrivalRadius + s.extraHideMargin;
            return stf.CurrentFan.transform;
        }

        // 3. Story ending: in the car, chasing the police cruiser
        var gem = GameExecutionManager.Instance;
        if (gem && gem.currentZoneMode == Zone.ZoneBoss2 && PimlrInput.PlayerInVehicle && gem.AiCar && gem.AiCar.activeInHierarchy)
        {
            var hp = gem.AiCar.GetComponentInChildren<JUTPS.JUHealth>();
            if (!hp || !hp.IsDead)
            {
                hideWithin = s.policeHideDistance;
                return gem.AiCar.transform;
            }
        }
        return null;
    }
}

/// <summary>PIMLR #ARROW: procedural low-poly arrow (points +Z, flat on XZ). inflate > 0 builds the outline shell.</summary>
public static class ArrowMeshBuilder
{
    // Outline (counter-clockwise seen from above), metres. Head and shaft are both convex, so each is a fan.
    static readonly Vector2[] Head = { new Vector2(0f, 0.55f), new Vector2(-0.40f, 0.02f), new Vector2(0.40f, 0.02f) };
    static readonly Vector2[] Shaft = { new Vector2(-0.16f, 0.06f), new Vector2(-0.16f, -0.50f), new Vector2(0.16f, -0.50f), new Vector2(0.16f, 0.06f) };
    const float Thickness = 0.16f;

    public static Mesh Build(float inflate)
    {
        var verts = new List<Vector3>(); var tris = new List<int>();
        AddPrism(Inflate(Head, inflate), Thickness + inflate * 2f, verts, tris);
        AddPrism(Inflate(Shaft, inflate), Thickness * 0.8f + inflate * 2f, verts, tris);
        var m = new Mesh { name = inflate > 0f ? "PIMLR_ObjectiveArrow_Outline" : "PIMLR_ObjectiveArrow" };
        m.SetVertices(verts); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Vector2[] Inflate(Vector2[] poly, float d)
    {
        if (d <= 0f) return poly;
        var o = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 prev = poly[(i + poly.Length - 1) % poly.Length], cur = poly[i], next = poly[(i + 1) % poly.Length];
            Vector2 n1 = Outward(cur - prev), n2 = Outward(next - cur);
            Vector2 bis = (n1 + n2).normalized;
            float miter = d / Mathf.Max(0.35f, Vector2.Dot(bis, n1));
            o[i] = cur + bis * miter;
        }
        return o;
    }

    static Vector2 Outward(Vector2 edge) => new Vector2(edge.y, -edge.x).normalized; // CCW polygon -> right-hand normal points out

    static void AddPrism(Vector2[] p, float h, List<Vector3> v, List<int> t)
    {
        float y0 = -h * 0.5f, y1 = h * 0.5f;
        int n = p.Length;
        // top (+Y) and bottom (-Y) caps, separate verts for flat shading
        int top = v.Count; foreach (var q in p) v.Add(new Vector3(q.x, y1, q.y));
        int bot = v.Count; foreach (var q in p) v.Add(new Vector3(q.x, y0, q.y));
        for (int i = 1; i < n - 1; i++)
        {
            t.Add(top); t.Add(top + i + 1); t.Add(top + i);     // CCW from above -> clockwise for Unity front face seen from +Y
            t.Add(bot); t.Add(bot + i); t.Add(bot + i + 1);
        }
        // sides
        for (int i = 0; i < n; i++)
        {
            Vector2 a = p[i], b = p[(i + 1) % n];
            int s0 = v.Count;
            v.Add(new Vector3(a.x, y0, a.y)); v.Add(new Vector3(b.x, y0, b.y));
            v.Add(new Vector3(b.x, y1, b.y)); v.Add(new Vector3(a.x, y1, a.y));
            t.Add(s0); t.Add(s0 + 2); t.Add(s0 + 1);
            t.Add(s0); t.Add(s0 + 3); t.Add(s0 + 2);
        }
    }
}
