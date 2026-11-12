// PIMLR #STF (Save The Fans) — heat intro fly-over: Fan -> first enemy's portal -> player.
//
// Drives the REAL gameplay camera, the same way GameExecutionManager.MoveToTargetAndBack
// already does for the police-car reveal: switch tpsCameraController off, move cameraObj,
// fly back to the pose it started from, switch the controller back on. The player is frozen
// for the duration, so that starting pose is still correct at the end and nothing snaps.
//
// (The first draft used a second camera and disabled the main one. That makes Camera.main
// return null for the duration, which other PIMLR/JUTPS scripts read every frame, and a
// second URP base camera costs a full extra render on WebGL. One camera avoids both.)
//
// Shots are pulled in automatically if a building is between the subject and the camera, and every move
// between shots is routed by PimlrCinematicCamera: straight when clear, over the rooftops when not.
// The HUD is faded with a CanvasGroup rather than SetActive(false): UI_GoalPanel.OnDisable
// resets the active music boost, so the HUD must never be deactivated mid-mission.
//
// Unreal note: SetViewTargetWithBlend between CineCameraActors, or a Level Sequence per heat
// whose camera bindings are retargeted at runtime to the fan / first enemy / player.

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PIMLR.Missions.SaveTheFans
{
    [AddComponentMenu("PIMLR/Save The Fans/Mission Camera Director")]
    public class MissionCameraDirector : MonoBehaviour
    {
        [Header("Camera rig (leave empty: taken from GameExecutionManager)")]
        [Tooltip("The object the JUTPS camera controller moves (GameExecutionManager.cameraObj).")]
        public Transform cameraTransform;
        [Tooltip("The JUTPS camera controller to pause (GameExecutionManager.tpsCameraController).")]
        public Behaviour cameraController;

        [Header("Timing")]
        [Min(0.1f)] public float travelTime = 1.4f;
        [Min(0f)] public float holdOnFan = 1.6f;
        [Tooltip("Long enough to watch the portal open and the enemy step out (PimlrSpawnFX step-through delay is 0.65 s).")]
        [Min(0f)] public float holdOnEnemy = 2.2f;
        [Tooltip("Front-on hero shot of the player before handing control back. Off = fly straight back behind the player.")]
        public bool showPlayerShot = true;
        [Min(0f)] public float holdOnPlayer = 0.8f;
        [Min(0.1f)] public float returnTime = 1.2f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Framing")]
        [Tooltip("How far back from the subject the camera sits.")]
        [Min(1f)] public float shotDistance = 7f;
        [Min(0f)] public float shotHeight = 3f;
        [Tooltip("Slow push-in during holds, in metres.")]
        [Min(0f)] public float holdPushIn = 1f;
        [Tooltip("Layers that block the camera. Buildings are on Default in YannicksWorld.")]
        public LayerMask occlusionMask = 1;

        [Header("While playing")]
        public bool freezePlayer = true;
        [Tooltip("A / Cross on a controller, Space or Enter on keyboard.")]
        public bool skippable = true;
        [Tooltip("Fade out the 'JUTPS Interface' HUD during the fly-over.")]
        public bool fadeHud = true;
        [Tooltip("Optional letterbox bars / 'FAN IN DANGER' title card.")]
        public GameObject showDuringCinematic;

        public bool IsPlaying { get; private set; }

        bool skipRequested;
        Vector3 homePosition;
        Quaternion homeRotation;
        PimlrCinematicCamera.LensRig lensRig; // PIMLR #STF: shots are planned for the lens, not the pivot 3 m in front of it
        bool controllerWasEnabled;
        CanvasGroup hudGroup;
        float hudAlphaBefore = 1f;

        void Update()
        {
            if (!IsPlaying || !skippable) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) ||
                (pad != null && pad.buttonSouth.wasPressedThisFrame))
                skipRequested = true;
        }

        /// Full intro. 'onEnemyShotReached' is invoked when the camera arrives at the enemy spot,
        /// so the mission can open the first enemy's portal on camera.
        public IEnumerator PlayHeatIntro(FanHeat fan, Vector3 enemySpot, System.Action onEnemyShotReached, Transform player)
        {
            if (!Begin()) yield break;
            reachFrom = player ? player.position : (Vector3?)null; // PIMLR #STF

            Vector3 fanFocus = fan ? fan.FocusPoint : enemySpot;

            if (fan)
                yield return FlyTo(fanFocus, fan.transform.forward, travelTime, holdOnFan, null, fan.PlayGreeting); // PIMLR #STF: wave as the camera arrives

            if (!skipRequested)
            {
                Vector3 enemyFocus = enemySpot + Vector3.up * 1.5f;
                // Camera sits on the fan's side of the portal, so the enemy steps out toward the lens.
                Vector3 towardFan = fan ? fanFocus - enemyFocus : Vector3.forward;
                yield return FlyTo(enemyFocus, towardFan, travelTime, holdOnEnemy, null, onEnemyShotReached);
            }

            if (showPlayerShot && player && !skipRequested)
                yield return FlyTo(player.position + Vector3.up * 1.6f, player.forward, travelTime, holdOnPlayer, null, null);

            yield return ReturnHome();
            End();
        }

        /// PIMLR (Story first-enemy cutscene): fly to where an enemy is about to arrive, invoke onShotReached
        /// (open the portal there), hold while it steps out, then fly straight back behind the player.
        /// onShotReached is NOT called if the move is skipped or the cutscene can't start; callers check IsPlaying.
        public IEnumerator PlayEnemyArrival(Vector3 spot, Transform player, System.Action onShotReached)
        {
            if (!Begin()) yield break;
            reachFrom = player ? player.position : (Vector3?)null;
            Vector3 focus = spot + Vector3.up * 1.5f;
            // Camera on the player's side of the portal, so the enemy steps out toward the lens.
            Vector3 towardPlayer = player ? player.position - spot : Vector3.forward;
            yield return FlyTo(focus, towardPlayer, travelTime, holdOnEnemy, null, onShotReached);
            yield return ReturnHome();
            End();
        }

        /// PIMLR: the scene's director (Save The Fans' one in YannicksWorld), or a runtime one with default settings.
        public static MissionCameraDirector Shared
        {
            get
            {
                if (shared) return shared;
                var m = SaveTheFansMission.Instance;
                if (m && m.cameraDirector) return shared = m.cameraDirector;
                shared = FindFirstObjectByType<MissionCameraDirector>();
                if (!shared) shared = new GameObject("PIMLR_CameraDirector").AddComponent<MissionCameraDirector>();
                return shared;
            }
        }
        static MissionCameraDirector shared;

        /// Short version for retries: just a quick look at the fan.
        public IEnumerator PlayQuickFanShot(FanHeat fan)
        {
            if (!fan || !Begin()) yield break;
            var pc = PlayerControlLock.ResolvePlayer();                // PIMLR #STF
            reachFrom = pc ? pc.transform.position : (Vector3?)null;
            yield return FlyTo(fan.FocusPoint, fan.transform.forward, travelTime * 0.7f, holdOnFan * 0.5f, null, fan.PlayGreeting); // PIMLR #STF
            yield return ReturnHome();
            End();
        }

        // ---------------------------------------------------------------------

        bool ResolveRig()
        {
            var gem = GameExecutionManager.Instance;
            if (!cameraTransform && gem && gem.cameraObj) cameraTransform = gem.cameraObj.transform;
            if (!cameraController && gem && gem.tpsCameraController) cameraController = gem.tpsCameraController;
            if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
            if (!cameraController && cameraTransform)
                cameraController = cameraTransform.GetComponentInParent<JUTPS.CameraSystems.TPSCameraController>();

            if (!cameraTransform)
            {
                Debug.LogWarning("[SaveTheFans] No gameplay camera found (GameExecutionManager.cameraObj / Camera.main). Skipping the fly-over.", this);
                return false;
            }
            return true;
        }

        bool Begin()
        {
            if (IsPlaying || !ResolveRig()) return false;
            IsPlaying = true;
            skipRequested = false;

            if (freezePlayer) PlayerControlLock.Lock();

            lensRig = new PimlrCinematicCamera.LensRig(cameraTransform);
            homePosition = lensRig.HomePosition;
            homeRotation = lensRig.HomeRotation;
            controllerWasEnabled = cameraController && cameraController.enabled;
            if (cameraController) cameraController.enabled = false;

            if (fadeHud) SetHudVisible(false);
            if (showDuringCinematic) showDuringCinematic.SetActive(true);
            return true;
        }

        void End()
        {
            if (!IsPlaying) return;
            if (lensRig != null) { lensRig.Restore(); lensRig = null; }
            else if (cameraTransform) cameraTransform.SetPositionAndRotation(homePosition, homeRotation);
            if (cameraController && controllerWasEnabled) cameraController.enabled = true;

            if (fadeHud) SetHudVisible(true);
            if (showDuringCinematic) showDuringCinematic.SetActive(false);
            if (freezePlayer) PlayerControlLock.Unlock();

            // The press that skipped the fly-over shouldn't also jump or fire.
            if (skipRequested) PimlrInput.BlockGameplayInputFor(0.3f, false);
            IsPlaying = false;
        }

        void OnDisable()
        {
            if (IsPlaying) End(); // never leave the camera controller off or the player frozen
        }

        void SetHudVisible(bool visible)
        {
            if (!hudGroup)
            {
                var sms = SceneManagerScript.Instance;
                GameObject hud = sms && sms.uiManager ? sms.uiManager.GetMenu("JUTPS Interface") : null;
                if (!hud) return;
                hudGroup = hud.GetComponent<CanvasGroup>();
                if (!hudGroup) hudGroup = hud.AddComponent<CanvasGroup>();
            }
            if (!visible) hudAlphaBefore = hudGroup.alpha;
            hudGroup.alpha = visible ? hudAlphaBefore : 0f;
        }

        IEnumerator FlyTo(Vector3 focus, Vector3 subjectForward, float travel, float hold, Vector3? lookAtOverride, System.Action onArrive)
        {
            Vector3 lookAt = lookAtOverride ?? focus;
            Vector3 shotPos = ComputeShotPosition(focus, subjectForward);
            Quaternion shotRot = LookRotationSafe(lookAt - shotPos);

            yield return Move(shotPos, shotRot, travel, lookAt);
            if (skipRequested) yield break;

            onArrive?.Invoke();

            // Hold with a gentle push-in.
            Vector3 pushTarget = Vector3.MoveTowards(shotPos, focus, holdPushIn);
            float t = 0f;
            while (t < hold && !skipRequested)
            {
                t += Time.deltaTime;
                float k = hold <= 0f ? 1f : t / hold;
                Vector3 p = Vector3.Lerp(shotPos, pushTarget, k);
                cameraTransform.SetPositionAndRotation(p, LookRotationSafe(lookAt - p));
                yield return null;
            }
        }

        IEnumerator ReturnHome()
        {
            // PIMLR #STF: back to the player along a route that goes over buildings, never through them.
            // A skip still flies (quickly) rather than cutting through walls.
            float dur = skipRequested ? 0.35f : returnTime;
            yield return PimlrCinematicCamera.Fly(cameraTransform, homePosition, homeRotation, dur, ease, null, occlusionMask);
        }

        IEnumerator Move(Vector3 toPos, Quaternion toRot, float duration, Vector3? lookAt = null)
        {
            // PIMLR #STF: was a straight lerp with a small arc, which cut through buildings between fan, enemy and
            // player. Now (PimlrCinematicCamera rules): straight if clear; else down the middle of the streets at the
            // cinematic height set on the fan, keeping the next subject framed; else over the roofs; else a cut.
            yield return PimlrCinematicCamera.Fly(cameraTransform, toPos, toRot, duration, ease, () => skipRequested, occlusionMask, lookAt);
        }

        // PIMLR #STF: shots favour spots on the player's walkable side, so moves follow the player's routes.
        Vector3? reachFrom;

        Vector3 ComputeShotPosition(Vector3 focus, Vector3 subjectForward)
        {
            return PimlrCinematicCamera.FindShot(focus, subjectForward, shotDistance, shotHeight, occlusionMask, reachFrom);
        }

        static Quaternion LookRotationSafe(Vector3 dir)
        {
            return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
        }
    }
}
