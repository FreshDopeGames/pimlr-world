using System.Collections.Generic;
using JUTPS;
using JUTPS.CameraSystems;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PIMLR (post-playtest): soft controller aim assist for newer players. On by default.
///
/// While the player aims (LT) or fires on a controller and an enemy is near the crosshair:
///   - Pull: the camera eases toward the enemy's chest - stronger the closer the crosshair already is,
///     zero at the edge of the cone, so it nudges rather than snaps.
///   - Friction: right-stick look slows a little over a target, so it's easier to stay on it.
/// It never turns the camera on its own when you aren't aiming / firing, never targets through walls, and
/// is off for mouse + keyboard. Works on foot and on the bike.
///
/// The future Gameplay Settings UI only needs to set PimlrAimAssist.Enabled (and optionally Strength).
/// Nothing to set up in the scenes: it attaches itself to the gameplay camera.
/// </summary>
public class PimlrAimAssist : MonoBehaviour
{
    // ---- settings (static so a settings menu can change them from anywhere) ----
    public static bool Enabled = true;
    public static bool ControllerOnly = true;
    public static float Strength = 1.3f;  // overall multiplier: 0 = off, 1 = default soft

    public static float ConeDegrees = 7f;        // how far off the crosshair a target still counts
    public static float MaxDistance = 45f;
    public static float PullPerSecond = 3f;    // camera catch-up rate at the crosshair centre (soft)
    public static float FrictionScale = 0.7f;    // look speed over a target (1 = no friction)

    /// <summary>Read by JUInputManager to slow the right stick over a target. 1 = normal.</summary>
    public static float LookScale { get; private set; } = 1f;

    JUCameraController cam;
    JUCharacterController player;
    readonly List<JUHealth> targets = new List<JUHealth>();
    float rescan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Attach();
    }

    static void OnLoaded(Scene s, LoadSceneMode m) => Attach();

    static void Attach()
    {
        foreach (var c in FindObjectsOfType<JUCameraController>())
            if (c.GetComponent<PimlrAimAssist>() == null) c.gameObject.AddComponent<PimlrAimAssist>();
    }

    void Awake() => cam = GetComponent<JUCameraController>();

    void OnDisable() => LookScale = 1f;

    void LateUpdate()
    {
        LookScale = 1f;
        if (!Enabled || Strength <= 0f || cam == null || cam.mCamera == null) return;
        if (ControllerOnly && !PimlrInput.UsingGamepad) return;
        if (JUPauseGame.Paused || PimlrMenuNavigator.IsNavigating) return;

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            player = p != null ? p.GetComponent<JUCharacterController>() : null;
            if (player == null) return;
        }
        if (player.IsDead) return;

        // Only while aiming / shooting (FiringMode covers LT aim and RT fire in JUTPS). No assist in the car.
        if (!player.FiringMode && !player.IsAiming) return;
        if (player.IsDriving && !player.PimlrIsRidingBike) return;

        if ((rescan -= Time.unscaledDeltaTime) <= 0f) { Rescan(); rescan = 0.25f; }

        Transform camT = cam.mCamera.transform;
        JUHealth best = null; Vector3 bestPoint = default; float bestN = 1f; // 0 = dead centre, 1 = cone edge
        foreach (var h in targets)
        {
            if (h == null || !h.isActiveAndEnabled || h.IsDead) continue;
            Vector3 point = AimPoint(h);
            Vector3 to = point - camT.position;
            float dist = to.magnitude;
            if (dist > MaxDistance || dist < 1f) continue;
            float angle = Vector3.Angle(camT.forward, to);
            // Wider cone up close (a person fills more of the screen), tighter far away.
            float cone = ConeDegrees * Mathf.Lerp(1.6f, 0.8f, dist / MaxDistance);
            float n = angle / cone;
            if (n >= bestN) continue;
            if (!InSight(camT.position, point, h)) continue;
            best = h; bestPoint = point; bestN = n;
        }
        if (best == null) return;

        float closeness = 1f - bestN;                              // 0 at the cone edge, 1 on target
        LookScale = Mathf.Lerp(1f, FrictionScale, closeness * Mathf.Clamp01(Strength));

        Vector3 e = Quaternion.LookRotation(bestPoint - camT.position).eulerAngles;
        float k = PullPerSecond * Strength * closeness * closeness * Time.deltaTime; // quadratic = soft at the edge
        cam.rotytarget = Mathf.LerpAngle(cam.rotytarget, e.y, k);
        cam.rotxtarget = Mathf.LerpAngle(cam.rotxtarget, e.x, k);
    }

    void Rescan()
    {
        targets.Clear();
        foreach (var h in FindObjectsOfType<JUHealth>())
        {
            if (h.CompareTag("Player") || (player != null && h.transform.IsChildOf(player.transform))) continue;
            if (player != null && player.DriveVehicleAbility != null && player.DriveVehicleAbility.VehicleToDrive != null
                && h.transform.IsChildOf(player.DriveVehicleAbility.VehicleToDrive.transform)) continue;
            targets.Add(h);
        }
    }

    static Vector3 AimPoint(JUHealth h)
    {
        var anim = h.GetComponent<Animator>();
        if (anim != null && anim.isHuman)
        {
            var chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            if (chest != null) return chest.position;
        }
        var col = h.GetComponentInChildren<Collider>();
        return col != null ? col.bounds.center : h.transform.position + Vector3.up;
    }

    static bool InSight(Vector3 from, Vector3 to, JUHealth target)
    {
        if (!Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)) return true;
        return hit.transform.IsChildOf(target.transform) || hit.collider.GetComponentInParent<JUHealth>() == target
               || hit.collider.CompareTag("Player"); // the player's own body in front of the camera doesn't block
    }
}
