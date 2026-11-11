// PIMLR #STF (Save The Fans) — mission HUD. Every field is optional; wire what you build.
// Suggested layout (top-center, under the existing goal panel):
//   [ FAN 2/4 · KEISHA ]          [ WAVE 3/5 ]   [ 4 LEFT ]
//   [ fan "fresh" bar: gold -> red as she gets washed ]
//   [ intermission: 01:12  "Reach the fan!" / "Hop off!" ]
//   [ hype bar ]  [ "FAN HYPE READY — press B / D-pad Up" ]  [ "x1.5 HYPE" while a boost track plays ]
//
// Unreal note: a UMG widget bound to the mission subsystem's delegates (event-driven),
// rather than polling every frame like this does.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PIMLR.Missions.SaveTheFans
{
    [AddComponentMenu("PIMLR/Save The Fans/Save The Fans HUD")]
    public class SaveTheFansHUD : MonoBehaviour
    {
        // Put this component on an object that stays active (e.g. the HUD Canvas) and point
        // 'root' at a CHILD panel, otherwise hiding the root would stop this Update.
        [Tooltip("Leave empty to use SaveTheFansMission.Instance.")]
        public SaveTheFansMission mission;

        [Header("Roots")]
        [Tooltip("Whole HUD; hidden while the mission isn't running.")]
        public GameObject root;
        public GameObject combatGroup;
        public GameObject intermissionGroup;

        [Header("Combat")]
        public TMP_Text heatLabel;
        public TMP_Text waveLabel;
        public TMP_Text enemiesLabel;
        [Tooltip("Filled image: 1 = fan fresh, 0 = washed out.")]
        public Image fanFreshFill;
        public Gradient fanFreshColors = DefaultFreshGradient();

        [Header("Intermission")]
        public TMP_Text timerLabel;
        public TMP_Text instructionLabel;
        public TMP_Text distanceLabel;
        public string reachFanText = "Reach {0} before the haters do!";
        public string exitCarText = "Hop off to defend {0}!";

        [Header("Fan Hype (Music Boost)")]
        public Image hypeFill;
        public GameObject boostReadyPrompt;
        public TMP_Text boostTimerLabel;
        [Tooltip("Shown while a purchased boost track is playing (hype fills faster).")]
        public GameObject boostTrackBonusBadge;

        [Header("Banners (optional, shown briefly)")]
        public TMP_Text bannerLabel;
        [Min(0.5f)] public float bannerSeconds = 2.5f;

        float bannerUntil;
        SaveTheFansMission.State lastState = SaveTheFansMission.State.Idle;
        int lastWave = -1;

        static Gradient DefaultFreshGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.15f, 0.1f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.45f), new GradientColorKey(new Color(1f, 0.78f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        void Update()
        {
            var m = mission ? mission : SaveTheFansMission.Instance;
            bool running = m && m.IsRunning;
            SetActive(root, running);
            if (!running) { lastState = SaveTheFansMission.State.Idle; return; }

            var fan = m.CurrentFan;
            string fanName = fan ? fan.fanDisplayName : "the fan";
            var state = m.CurrentState;
            bool intermission = state == SaveTheFansMission.State.Intermission;

            SetActive(intermissionGroup, intermission);
            SetActive(combatGroup, !intermission);

            // --- banners on state changes
            if (state != lastState || m.CurrentWaveIndex != lastWave)
            {
                switch (state)
                {
                    case SaveTheFansMission.State.Wave: Banner($"WAVE {m.CurrentWaveIndex + 1}"); break;
                    case SaveTheFansMission.State.HeatSaved: Banner($"{fanName.ToUpper()} SAVED!"); break;
                    case SaveTheFansMission.State.HeatFailed: Banner($"{fanName.ToUpper()} GOT WASHED — RUN IT BACK"); break;
                    case SaveTheFansMission.State.Intermission: Banner($"NEXT FAN: {fanName.ToUpper()}"); break;
                }
                lastState = state;
                lastWave = m.CurrentWaveIndex;
            }
            if (bannerLabel) bannerLabel.gameObject.SetActive(Time.time < bannerUntil);

            // --- combat
            if (heatLabel) heatLabel.text = $"FAN {m.CurrentHeatIndex + 1}/{m.HeatCount} · {fanName.ToUpper()}";
            if (waveLabel) waveLabel.text = $"WAVE {Mathf.Max(1, m.CurrentWaveIndex + 1)}/{m.WaveCount}";
            if (enemiesLabel) enemiesLabel.text = $"{m.EnemiesRemaining} LEFT";
            if (fanFreshFill && fan)
            {
                float fresh = 1f - fan.Wash01;
                fanFreshFill.fillAmount = fresh;
                fanFreshFill.color = fanFreshColors.Evaluate(fresh);
            }

            // --- intermission
            if (intermission)
            {
                if (timerLabel)
                {
                    timerLabel.gameObject.SetActive(m.IntermissionTimed);
                    int secs = Mathf.CeilToInt(m.IntermissionTimeLeft);
                    timerLabel.text = $"{secs / 60:00}:{secs % 60:00}";
                    timerLabel.color = secs <= 10 ? new Color(1f, 0.3f, 0.2f) : Color.white;
                }
                if (instructionLabel)
                    instructionLabel.text = string.Format(m.NeedsToExitVehicle ? exitCarText : reachFanText, fanName);
                if (distanceLabel) distanceLabel.text = $"{Mathf.RoundToInt(m.DistanceToCurrentFan)} m";
            }

            // --- boost
            if (hypeFill) hypeFill.fillAmount = m.BoostActive ? m.BoostTimeLeft / Mathf.Max(0.01f, m.boostDuration) : m.Hype01;
            SetActive(boostReadyPrompt, m.BoostReady && !m.BoostActive);
            SetActive(boostTrackBonusBadge, !intermission && SaveTheFansMission.BoostTrackPlaying && !m.BoostActive && !m.BoostReady);
            if (boostTimerLabel)
            {
                boostTimerLabel.gameObject.SetActive(m.BoostActive);
                boostTimerLabel.text = $"FAN HYPE {Mathf.CeilToInt(m.BoostTimeLeft)}s";
            }
        }

        public void Banner(string text)
        {
            if (!bannerLabel) return;
            bannerLabel.text = text;
            bannerUntil = Time.time + bannerSeconds;
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go && go.activeSelf != on) go.SetActive(on);
        }
    }
}
