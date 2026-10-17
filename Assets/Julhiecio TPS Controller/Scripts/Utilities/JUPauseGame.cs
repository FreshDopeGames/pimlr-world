using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using JUTPS.InputEvents;
using JUTPSEditor.JUHeader;
namespace JUTPS
{
    [AddComponentMenu("JU TPS/Utilities/JU Pause Game")]
    public class JUPauseGame : MonoBehaviour
    {
        public static JUPauseGame instance;
        public static bool Paused;
        
        [JUHeader("Pause Input")]
        public MultipleActionEvent PauseInputs;

        [JUHeader("On Pause Events")]
        public UnityEvent OnPause;
        public UnityEvent OnUnpause;
        private FX.JUSlowmotion SlowmotionInstance;
        private static bool slowmotionWasEnabled;
        void Start()
        {
            instance = this;
            SlowmotionInstance = FindObjectOfType<FX.JUSlowmotion>();
            PauseInputs.OnButtonsDown.AddListener(Pause);
        }
        private void OnEnable() { PauseInputs.Enable(); }
        private void OnDisable() { PauseInputs.Disable(); }
        
        [JUHeader("PIMLR: Resume Ramp")]
        [Tooltip("Real seconds to ease game speed from Resume Start Speed back to full speed after unpausing. 0 = instant (stock JUTPS).")]
        public float ResumeRampSeconds = 1.5f;
        [Tooltip("Game speed the moment play resumes (0-1).")]
        [Range(0.05f, 1f)] public float ResumeStartSpeed = 0.25f;

        /// <summary>PIMLR: true while game speed is easing back up after an unpause.</summary>
        public static bool IsResuming { get; private set; }
        private Coroutine resumeRamp;

        public static void Pause()
        {
            //Update state
            Paused = !Paused;

            // PIMLR (post-playtest): pausing (again) cancels a resume ramp that's still running.
            if (instance != null && instance.resumeRamp != null)
            {
                instance.StopCoroutine(instance.resumeRamp);
                instance.resumeRamp = null;
                IsResuming = false;
            }

            //Update time scale
            // PIMLR (post-playtest): unpausing eases game speed back up instead of snapping straight to full speed,
            // and briefly ignores gameplay buttons so pressing Resume with A doesn't also jump.
            bool ramp = !Paused && instance != null && instance.ResumeRampSeconds > 0f && instance.isActiveAndEnabled
                        && !CinematicOwnsTime();
            Time.timeScale = Paused ? 0 : (ramp ? instance.ResumeStartSpeed : 1);
            if (!Paused) PimlrInput.BlockGameplayInputFor(PimlrInput.UnpauseButtonPause, blockMovement: false);

            //Trigger events
            if (Paused) { instance.OnPause.Invoke(); } else { instance.OnUnpause.Invoke(); }

            //Disable / Enable Slowmotion
            // PIMLR: restore the slow-motion helper to how the scene had it, instead of force-enabling it on every
            // unpause. YannicksWorld ships it disabled; force-enabling it made it reset timeScale to 1 every frame
            // (breaking the death slow-mo) and log to the console every frame.
            if (instance.SlowmotionInstance != null)
            {
                if (Paused) { slowmotionWasEnabled = instance.SlowmotionInstance.EnableSlowmotion; instance.SlowmotionInstance.EnableSlowmotion = false; }
                else if (!ramp) instance.SlowmotionInstance.EnableSlowmotion = slowmotionWasEnabled;
                // with a ramp, it's restored when the ramp ends (it would otherwise snap timeScale to 1)
            }

            if (ramp) instance.resumeRamp = instance.StartCoroutine(instance.ResumeRamp());
        }

        // The WASHED / HATER FREE! sequences drive timeScale themselves; never fight them.
        private static bool CinematicOwnsTime() => PimlrDeathSequence.IsPlaying || PimlrVictorySequence.IsPlaying;

        private IEnumerator ResumeRamp()
        {
            IsResuming = true;
            float t = 0f;
            while (t < ResumeRampSeconds)
            {
                if (Paused) break;                                   // Pause() already stopped us; belt and braces
                if (CinematicOwnsTime()) { Finish(false); yield break; } // a cinematic started: hand time over
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / ResumeRampSeconds);
                float eased = k * k * (3f - 2f * k);                 // smoothstep: gentle start and finish
                Time.timeScale = Mathf.Lerp(ResumeStartSpeed, 1f, eased);
                yield return null;
            }
            Finish(!Paused);
        }

        private void Finish(bool setFullSpeed)
        {
            if (setFullSpeed) Time.timeScale = 1f;
            if (SlowmotionInstance != null && !Paused) SlowmotionInstance.EnableSlowmotion = slowmotionWasEnabled;
            IsResuming = false;
            resumeRamp = null;
        }

        private void OnDestroy()
        {
            // Leaving the scene mid-ramp must not carry slow time into the next one.
            if (IsResuming) { Time.timeScale = 1f; IsResuming = false; }
            if (instance == this) instance = null;
        }
    }
}