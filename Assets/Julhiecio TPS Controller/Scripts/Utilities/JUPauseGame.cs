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
        
        public static void Pause()
        {
            //Update state
            Paused = !Paused;
            //Update time scale
            Time.timeScale = Paused ? 0 : 1;
            //Trigger events
            if (Paused) { instance.OnPause.Invoke(); } else { instance.OnUnpause.Invoke(); }

            //Disable / Enable Slowmotion
            // PIMLR: restore the slow-motion helper to how the scene had it, instead of force-enabling it on every
            // unpause. YannicksWorld ships it disabled; force-enabling it made it reset timeScale to 1 every frame
            // (breaking the death slow-mo) and log to the console every frame.
            if (instance.SlowmotionInstance != null)
            {
                if (Paused) { slowmotionWasEnabled = instance.SlowmotionInstance.EnableSlowmotion; instance.SlowmotionInstance.EnableSlowmotion = false; }
                else instance.SlowmotionInstance.EnableSlowmotion = slowmotionWasEnabled;
            }
        }
    }
}