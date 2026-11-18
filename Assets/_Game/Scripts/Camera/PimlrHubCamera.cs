// PIMLR #CAM (Nov 16): scripted moves of the Living Room's gameplay camera (SceneStaticEU), shared by the
// Select Your Artist carousel (PimlrArtistSelect) and the Leaderboards fly-out (PimlrLeaderboardCamera).
//
// Same single-camera rule as MissionCameraDirector: the JUTPS camera controller is switched off, the real Main Camera
// is moved, then everything is put back exactly as it was (pose, lens offset, field of view, controller).
// Moves use unscaled time, so they also run while a menu has paused the game.
//
//   if (PimlrHubCamera.Take()) { yield return PimlrHubCamera.Fly(pos, rot, fov, seconds); ... PimlrHubCamera.Release(); }
//
// Unreal note: SetViewTargetWithBlend to a CameraActor placed at each menu "station" (VTBlend_EaseInOut), and back to
// the player pawn on close; or one CineCameraActor driven along a spline by a Timeline.

using System.Collections;
using UnityEngine;

public static class PimlrHubCamera
{
    static Transform pivot;
    static Behaviour controller;
    static bool controllerWasOn;
    static PimlrCinematicCamera.LensRig lens;
    static Camera cam;
    static float homeFov;

    /// <summary>True while a script owns the camera.</summary>
    public static bool Held { get; private set; }
    public static Vector3 HomePosition => lens != null ? lens.HomePosition : Vector3.zero;
    public static Quaternion HomeRotation => lens != null ? lens.HomeRotation : Quaternion.identity;
    public static float HomeFov => homeFov;
    public static Vector3 Position => pivot ? pivot.position : Vector3.zero;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Forget();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // The rig belongs to the scene: a new scene never inherits a hold.
    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        if (m == UnityEngine.SceneManagement.LoadSceneMode.Single) Forget();
    }

    static void Forget() { Held = false; pivot = null; controller = null; lens = null; cam = null; }

    /// <summary>Take the camera away from the JUTPS controller. False if the scene has no Main Camera.</summary>
    public static bool Take()
    {
        if (Held && pivot) return true;
        cam = Camera.main;
        if (!cam) return false;
        controller = cam.GetComponentInParent<JUTPS.CameraSystems.TPSCameraController>();
        // The controller moves a pivot; the lens (Main Camera) hangs off it with a shoulder offset.
        pivot = cam.transform.parent && controller && cam.transform.parent.IsChildOf(controller.transform) ? cam.transform.parent : cam.transform;
        controllerWasOn = controller && controller.enabled;
        if (controller) controller.enabled = false;
        lens = new PimlrCinematicCamera.LensRig(pivot);
        homeFov = cam.fieldOfView;
        Held = true;
        return true;
    }

    /// <summary>Cut to a pose (fov 0 = keep the current field of view).</summary>
    public static void Set(Vector3 position, Quaternion rotation, float fov = 0f)
    {
        if (!Held || !pivot) return;
        pivot.SetPositionAndRotation(position, rotation);
        if (cam && fov > 0f) cam.fieldOfView = fov;
    }

    /// <summary>Hand the camera back: pose, lens offset, field of view and controller exactly as they were.</summary>
    public static void Release()
    {
        if (!Held) return;
        if (cam) cam.fieldOfView = homeFov;
        if (lens != null) lens.Restore();
        if (controller && controllerWasOn) controller.enabled = true;
        Forget();
    }

    /// <summary>
    /// Ease the camera to a pose. control = optional point the path bends toward (quadratic Bezier), e.g. the middle
    /// of a window the camera should pass through. toFov 0 = keep the field of view.
    /// </summary>
    public static IEnumerator Fly(Vector3 toPos, Quaternion toRot, float toFov, float seconds, Vector3? control = null)
    {
        if (!Held || !pivot) yield break;
        Vector3 fromPos = pivot.position;
        Quaternion fromRot = pivot.rotation;
        float fromFov = cam ? cam.fieldOfView : 60f;
        if (toFov <= 0f) toFov = fromFov;
        float t = 0f;
        seconds = Mathf.Max(0.01f, seconds);
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            if (!Held || !pivot) yield break;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
            Vector3 p = control.HasValue
                ? (1f - k) * (1f - k) * fromPos + 2f * k * (1f - k) * control.Value + k * k * toPos
                : Vector3.Lerp(fromPos, toPos, k);
            pivot.SetPositionAndRotation(p, Quaternion.Slerp(fromRot, toRot, k));
            if (cam) cam.fieldOfView = Mathf.Lerp(fromFov, toFov, k);
            yield return null;
        }
        if (Held && pivot)
        {
            pivot.SetPositionAndRotation(toPos, toRot);
            if (cam) cam.fieldOfView = toFov;
        }
    }
}
