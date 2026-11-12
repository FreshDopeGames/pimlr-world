// PIMLR #STF (Save The Fans) — mission HUD. Every field is optional; wire what you build.
// Suggested layout (top-center, under the existing goal panel):
//   [ FAN 2/4 · KEISHA ]          [ WAVE 3/5 ]   [ 4 LEFT ]
//   [ threat bar: red enemy icon -> closing in on -> green fan icon, thin yellow track underneath ]
//   [ "KEISHA WASHED IN 0:24" countdown ]
//   [ intermission: 01:12  "Reach the fan!" / "Hop off!" ]
//   [ hype bar ]  [ "FAN HYPE READY — press B / D-pad Up" ]  [ "x1.5 HYPE" while a boost track plays ]
//
// No HUD in the scene? SaveTheFansMission calls EnsureExists() when the mission starts, which builds this layout
// from plain Unity UI shapes (placeholder colours: red enemy, green fan, yellow track). Replace the Images'
// sprites with your own icons later; the script only moves and colours them.
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

        // PIMLR #STF: threat bar. The enemy icon slides from the left end toward the fan icon on the right as the
        // heat's enemies wash the fan; the red fill on the thin yellow track follows it. Reaching the fan = washed.
        [Header("Threat bar (enemy closing in on the fan)")]
        [Tooltip("The full-width track the enemy icon travels along (thin yellow bar).")]
        public RectTransform threatTrack;
        [Tooltip("Fill on the track from the left end up to the enemy icon. Its right anchor is driven.")]
        public RectTransform threatFill;
        [Tooltip("Enemy icon. Its horizontal anchor is driven from 0 (fan safe) to 1 (fan washed).")]
        public RectTransform threatEnemyIcon;
        public RectTransform threatFanIcon;
        [Tooltip("\"JOSE WASHED IN 0:24\" / \"JOSE IS SAFE\".")]
        public TMP_Text threatCountdownLabel;
        [Tooltip("Smoothing for the enemy icon's movement (higher = snappier).")]
        public float threatIconSharpness = 6f;
        [Tooltip("Status turns red (IN DANGER) when the fan would be washed in under this many seconds at the current pace...")]
        public float threatUrgentSeconds = 10f;
        [Tooltip("...or when the fan's health is at or under this share.")]
        [Range(0f, 1f)] public float threatLowHealth = 0.3f;
        [Tooltip("Seconds without a hit before the status calms back down.")]
        public float threatCalmAfter = 2.5f;

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
        float threatShown;
        int threatTier; float threatTierSince; // 0 calm, 1 under attack, 2 in danger (PIMLR #STF)

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

            // PIMLR #STF: the Fan intro flyover (Cinematic) is clean - no wave meter / wave counter until the wave starts.
            bool cinematic = state == SaveTheFansMission.State.Cinematic;
            SetActive(intermissionGroup, intermission);
            SetActive(combatGroup, !intermission && !cinematic);
            if (waveLabel) SetActive(waveLabel.gameObject, !cinematic);
            if (enemiesLabel) SetActive(enemiesLabel.gameObject, !cinematic);

            // --- banners on state changes
            if (state != lastState || m.CurrentWaveIndex != lastWave)
            {
                switch (state)
                {
                    case SaveTheFansMission.State.Wave: Banner($"WAVE {m.CurrentWaveIndex + 1}"); break;
                    // HeatSaved: the mission plays the letterbox "<FAN> SAVED!" banner instead (PimlrVictorySequence).
                    case SaveTheFansMission.State.HeatFailed: Banner($"{fanName.ToUpper()} GOT WASHED — RUN IT BACK"); break;
                    case SaveTheFansMission.State.Intermission: Banner($"NEXT FAN: {fanName.ToUpper()}"); break;
                }
                if (state == SaveTheFansMission.State.Intermission || state == SaveTheFansMission.State.Cinematic) { threatShown = 0f; threatTier = 0; }
                lastState = state;
                lastWave = m.CurrentWaveIndex;
            }
            if (bannerLabel) bannerLabel.gameObject.SetActive(Time.time < bannerUntil);

            // --- combat
            // PIMLR #STF: no "FAN 2/3 · NAME" header - the fan's name already sits over their health bar.
            if (heatLabel) SetActive(heatLabel.gameObject, false);
            if (waveLabel) waveLabel.text = $"WAVE {Mathf.Max(1, m.CurrentWaveIndex + 1)}/{m.WaveCount}";
            if (enemiesLabel) enemiesLabel.text = $"{m.EnemiesRemaining} LEFT";
            if (fanFreshFill && fan)
            {
                float fresh = 1f - fan.Wash01;
                fanFreshFill.fillAmount = fresh;
                fanFreshFill.color = fanFreshColors.Evaluate(fresh);
            }
            if (fan && !intermission) UpdateThreat(fan, fanName, state);

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

        // PIMLR #STF
        void UpdateThreat(FanHeat fan, string fanName, SaveTheFansMission.State state)
        {
            float target = fan.Wash01;
            threatShown = Mathf.Lerp(threatShown, target, 1f - Mathf.Exp(-threatIconSharpness * Time.deltaTime));
            if (Mathf.Abs(threatShown - target) < 0.002f) threatShown = target;

            if (threatEnemyIcon)
            {
                threatEnemyIcon.anchorMin = new Vector2(threatShown, threatEnemyIcon.anchorMin.y);
                threatEnemyIcon.anchorMax = new Vector2(threatShown, threatEnemyIcon.anchorMax.y);
            }
            if (threatFill) threatFill.anchorMax = new Vector2(threatShown, threatFill.anchorMax.y);

            if (!threatCountdownLabel) return;
            string who = fanName.ToUpper();
            if (state == SaveTheFansMission.State.HeatSaved) { threatCountdownLabel.text = $"{who} IS SAFE"; threatCountdownLabel.color = new Color(0.4f, 1f, 0.5f); return; }
            if (state == SaveTheFansMission.State.HeatFailed || fan.IsWashedOut) { threatCountdownLabel.text = $"{who} GOT WASHED"; threatCountdownLabel.color = new Color(1f, 0.3f, 0.2f); return; }

            // PIMLR #STF: a status, not a clock. The old "WASHED IN m:ss" divided health by a 1.5 s average of
            // bursty hits, so it jumped from 0:20 to 5:08 and back (and even counted up). Tiers rise at once and
            // only fall after they've held for a moment, so the line doesn't flicker between hits.
            bool underAttack = fan.SecondsSinceWashed < threatCalmAfter;
            bool danger = fan.Health01 <= threatLowHealth || (underAttack && fan.TimeToWashOut <= threatUrgentSeconds);
            int tier = danger ? 2 : underAttack ? 1 : 0;
            if (tier > threatTier || Time.time - threatTierSince >= 1.5f)
            {
                if (tier != threatTier) { threatTier = tier; threatTierSince = Time.time; }
            }

            switch (threatTier)
            {
                case 2:
                    threatCountdownLabel.text = $"{who} IS IN DANGER!";
                    threatCountdownLabel.color = new Color(1f, 0.3f, 0.2f);
                    break;
                case 1:
                    threatCountdownLabel.text = $"{who} IS UNDER ATTACK";
                    threatCountdownLabel.color = new Color(1f, 0.85f, 0.25f);
                    break;
                default:
                    threatCountdownLabel.text = $"PROTECT {who}";
                    threatCountdownLabel.color = Color.white;
                    break;
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

        // ------------------------------------------------------------------ runtime build (PIMLR #STF)

        /// <summary>Called by the mission at start: builds a placeholder HUD if the scene doesn't have one.</summary>
        public static SaveTheFansHUD EnsureExists(SaveTheFansMission owner)
        {
            var existing = FindAnyObjectByType<SaveTheFansHUD>(FindObjectsInactive.Include);
            if (existing) return existing;
            return BuildRuntime(owner);
        }

        static readonly Color EnemyRed = new Color(0.9f, 0.18f, 0.15f);
        static readonly Color FanGreen = new Color(0.2f, 0.8f, 0.3f);
        static readonly Color TrackYellow = new Color(1f, 0.82f, 0.15f);

        public static SaveTheFansHUD BuildRuntime(SaveTheFansMission owner)
        {
            var canvasGo = new GameObject("SaveTheFans_HUD (runtime)", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = canvasGo.AddComponent<SaveTheFansHUD>();
            hud.mission = owner;

            // Root panel, top-centre, below the goal panel.
            var root = Rect("Root", canvasGo.transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(620f, 150f);
            root.anchoredPosition = new Vector2(0f, -205f); // below the music player panel
            hud.root = root.gameObject;

            // Header row (shared by both states)
            hud.heatLabel = Label("Heat", root, 24f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0.6f, 1f), 30f, 0f);
            // PIMLR #STF: the fan-name header is gone, so "WAVE 1/2   5 LEFT" sits centred, meeting in the middle.
            hud.waveLabel = Label("Wave", root, 24f, TextAlignmentOptions.Right, new Vector2(0f, 1f), new Vector2(0.5f, 1f), 30f, 0f);
            hud.waveLabel.margin = new Vector4(0f, 0f, 14f, 0f);
            hud.enemiesLabel = Label("Left", root, 24f, TextAlignmentOptions.Left, new Vector2(0.5f, 1f), new Vector2(1f, 1f), 30f, 0f);
            hud.enemiesLabel.margin = new Vector4(14f, 0f, 0f, 0f);

            // ---- combat: threat bar
            var combat = Rect("Combat", root);
            Stretch(combat);
            hud.combatGroup = combat.gameObject;

            var bar = Rect("ThreatBar", combat);
            bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, 56f);
            bar.anchoredPosition = new Vector2(0f, -36f);

            // Icon lane: enemy slides along it toward the fan on the right.
            const float icon = 40f;
            var lane = Rect("IconLane", bar);
            lane.anchorMin = new Vector2(0f, 1f); lane.anchorMax = new Vector2(1f, 1f);
            lane.pivot = new Vector2(0.5f, 1f);
            lane.offsetMin = new Vector2(icon * 0.5f, -icon); lane.offsetMax = new Vector2(-icon * 1.5f, 0f);

            hud.threatFanIcon = Icon("FanIcon", bar, FanGreen, "FAN", icon);
            hud.threatFanIcon.anchorMin = hud.threatFanIcon.anchorMax = new Vector2(1f, 1f);
            hud.threatFanIcon.pivot = new Vector2(1f, 1f);
            hud.threatFanIcon.anchoredPosition = Vector2.zero;

            hud.threatEnemyIcon = Icon("EnemyIcon", lane, EnemyRed, "!", icon);
            hud.threatEnemyIcon.anchorMin = hud.threatEnemyIcon.anchorMax = new Vector2(0f, 0.5f);
            hud.threatEnemyIcon.pivot = new Vector2(0.5f, 0.5f);
            hud.threatEnemyIcon.anchoredPosition = Vector2.zero;

            // Thin yellow track under the icons, red fill up to the enemy.
            var track = Image("Track", bar, TrackYellow).rectTransform;
            track.anchorMin = new Vector2(0f, 0f); track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = new Vector2(icon * 0.5f, 0f); track.offsetMax = new Vector2(-icon * 0.5f, 8f);
            hud.threatTrack = track;

            var fill = Image("Fill", track, EnemyRed).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            hud.threatFill = fill;

            hud.threatCountdownLabel = Label("Countdown", combat, 22f, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f), 28f, -96f);

            // Fan Hype prompts
            var prompt = Label("BoostReady", combat, 20f, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f), 26f, -124f);
            prompt.text = "FAN HYPE READY — press B / D-pad Up";
            prompt.color = TrackYellow;
            hud.boostReadyPrompt = prompt.gameObject;
            hud.boostTimerLabel = Label("BoostTimer", combat, 20f, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f), 26f, -124f);
            hud.boostTimerLabel.color = TrackYellow;

            // ---- intermission
            var inter = Rect("Intermission", root);
            Stretch(inter);
            hud.intermissionGroup = inter.gameObject;
            hud.timerLabel = Label("Timer", inter, 40f, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f), 48f, -34f);
            hud.instructionLabel = Label("Instruction", inter, 22f, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f), 28f, -84f);
            hud.distanceLabel = Label("Distance", inter, 20f, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f), 26f, -112f);

            // ---- banner, screen centre-top
            // PIMLR #STF: 0.62 (was 0.7) - clears the intermission stack above (timer / instruction / distance end ~0.68)
            // and sits just above the centred letterbox wave banner (PimlrVictorySequence).
            var banner = Label("Banner", canvasGo.transform, 44f, TextAlignmentOptions.Center, new Vector2(0f, 0.62f), new Vector2(1f, 0.62f), 60f, 0f);
            if (!SaveTheFansStyle.Font) banner.fontStyle = FontStyles.Bold;
            hud.bannerLabel = banner;
            banner.gameObject.SetActive(false);

            return hud;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        static Image Image(string name, Transform parent, Color c)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static RectTransform Icon(string name, Transform parent, Color c, string glyph, float size)
        {
            var img = Image(name, parent, c);
            var r = img.rectTransform;
            r.sizeDelta = new Vector2(size, size);
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            var t = Label("Glyph", r, glyph.Length > 1 ? 13f : 24f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, 0f, 0f);
            Stretch(t.rectTransform);
            t.text = glyph;
            if (!SaveTheFansStyle.Font) t.fontStyle = FontStyles.Bold;
            return r;
        }

        static TextMeshProUGUI Label(string name, Transform parent, float size, TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax, float height, float y)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            var r = t.rectTransform;
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(0f, height);
            r.anchoredPosition = new Vector2(0f, y);
            var font = SaveTheFansStyle.Font; // PIMLR #STF: Barlow-Bold SDF
            if (font) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            return t;
        }
    }
}
