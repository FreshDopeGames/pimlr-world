// PIMLR #STF (Save The Fans) — the mission manager. One per YannicksWorld.
// Written against the unityv6 branch (Unity 6000.6.0f1, URP 17.6, AI Navigation 2.0, Input System 1.20).
//
// Flow per fan ("heat"):
//   INTERMISSION  gold ring + minimap blip + travel beacon on the next fan, countdown runs.
//                 The heat starts when the player reaches the fan's arrival radius on foot.
//                 Arrive early = bonus coins. Timer runs out = the heat starts anyway (default),
//                 so the fan is already under attack when you show up late.
//   INTRO         camera flies Fan -> the first enemy's portal as it steps out -> player.
//   WAVES         increasingly numerous waves; enemies go for the fan, some can be pulled off.
//                 Restock stations near the fan are live. Kills fill the Fan Hype meter.
//   SAVED         coins, fan cheers, ring flashes green, next fan lights up.
//   WASHED OUT    enemies are cleared, short pause, the same heat restarts (quick camera shot).
//
// How it plugs into the existing game:
//   * Enemies are the normal prefabs (PIMLR_Melee / PIMLR_Gunner / PIMLR_Bomber) and arrive
//     through PimlrEnemySpawnDirector portals, at this mission's own validated spawn points.
//   * Every JUHealth death calls GameExecutionManager.OnKillEnemy(), which pays coins, counts
//     the kill for the leaderboard run and bumps zombiesKilled; at 10 the Story zone advances.
//     The coins and the kill count are wanted. The zone advance is not, so while the mission
//     runs it holds zombiesKilled at the value it had when the mission started (LateUpdate,
//     i.e. after JUHealth's Invoke-driven death check and before GameExecutionManager.Update).
//   * Progress is saved per fan in PlayerPrefs, so dying (YannicksWorld reloads) resumes at the
//     current fan. GameExecutionManager owns WHEN the mission runs (see the setup guide patch).
//
// Unreal note: a per-level system, so a UWorldSubsystem or a GameMode component — NOT a
// GameInstanceSubsystem (it resets when the level reloads). The coroutine flow becomes a
// StateTree or a latent "Run Heat" async action; UnityEvents become BlueprintAssignable delegates.

using System.Collections;
using System.Collections.Generic;
using JUTPS;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PIMLR.Missions.SaveTheFans
{
    [System.Serializable] public class HeatEvent : UnityEvent<int> { }
    [System.Serializable] public class WaveEvent : UnityEvent<int, int> { }

    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("PIMLR/Save The Fans/Save The Fans Mission")]
    public class SaveTheFansMission : MonoBehaviour
    {
        public enum State { Idle, Intermission, Cinematic, BetweenWaves, Wave, HeatSaved, HeatFailed, Complete }
        public enum TimerExpiredBehaviour { StartHeatAnyway, RestartTimer }

        public static SaveTheFansMission Instance { get; private set; }

        public const string PrefNextHeat = "PIMLR_STF_NextHeat";

        // ------------------------------------------------------------------ inspector
        [Header("Heats, in play order (nearest fan first)")]
        public List<FanHeat> heats = new List<FanHeat>();

        [Header("Start")]
        [Tooltip("Off for the real game: GameExecutionManager calls BeginMission() (see setup guide). On = handy for testing.")]
        public bool beginOnStart = false;
        [Tooltip("Continue from the last saved fan (e.g. after dying) instead of fan 1.")]
        public bool resumeSavedProgress = true;

        [Header("Intermission (travel between fans)")]
        [Tooltip("Default seconds to reach the next fan. Each FanHeat can override it (Travel Seconds).")]
        [Min(5f)] public float intermissionSeconds = 60f;
        [Tooltip("Does the walk to the FIRST fan have a timer too?")]
        public bool timeFirstFan = false;
        public TimerExpiredBehaviour onTimerExpired = TimerExpiredBehaviour.StartHeatAnyway;
        [Tooltip("PIMLR Coins per second left on the clock when you arrive.")]
        [Min(0)] public int earlyArrivalCoinsPerSecond = 1;
        [Tooltip("Player must be off the bike / out of the car for the heat to start (HUD shows a 'hop off' prompt).")]
        public bool requireOnFoot = true;

        [Header("Enemy targeting")]
        [Tooltip("Used when a spawn entry has no profile of its own.")]
        public FanAggroProfile defaultAggroProfile;
        [Tooltip("Never more than this many enemies chasing the player at once. The rest stay on the fan.")]
        [Min(0)] public int maxSimultaneousGrudges = 3;

        [Header("Spawning")]
        [Tooltip("Enemies step out of a PimlrEnemySpawnDirector portal at the spawn point (same as Story / Infinite).")]
        public bool usePortalArrival = true;
        [Tooltip("Spawned enemies are parented here (keeps the hierarchy tidy). Optional.")]
        public Transform enemiesParent;
        [Tooltip("Avoid spawn points closer than this to the player.")]
        [Min(0f)] public float minSpawnDistanceFromPlayer = 12f;
        [Tooltip("Prefer spawn points the gameplay camera can't see (no pop-in).")]
        public bool preferSpawnsOutOfView = true;
        [Tooltip("Buildings are on the Default layer in YannicksWorld.")]
        public LayerMask lineOfSightBlockers = 1;

        [Header("Story integration")]
        [Tooltip("Holds GameExecutionManager.zombiesKilled steady while the mission runs so mission kills can't advance the Story zone. Coins and leaderboard kills still count.")]
        public bool protectStoryKillCounter = true;
        [Tooltip("Show the wave kill count in the existing goal panel counter.")]
        public bool useGoalPanelKillCounter = true;

        [Header("Failure / retry")]
        [Tooltip("PIMLR #STF: a washed fan fails the whole run: every fan resets and the player starts again from wave 1 of the first fan. Off = retry only the fan that was washed (old behaviour).")]
        public bool restartFromFirstFanOnWashout = true;
        [Min(0f)] public float retryDelay = 4f;
        [Tooltip("PIMLR #STF: where a restart puts the player (near Sirihanna). Empty = found at runtime (the AI Chat Interaction / 'Sirihanna' object).")]
        public Transform restartSpot;
        [Tooltip("Distance in front of the restart spot the player is placed.")]
        [Min(0f)] public float restartSpotOffset = 3f;
        [Min(0.05f)] public float restartFadeOut = 0.6f;
        [Tooltip("Seconds held on black after the teleport (lets the gameplay camera settle behind the player).")]
        [Min(0f)] public float restartBlackHold = 0.8f;
        [Min(0.05f)] public float restartFadeIn = 0.8f;
        [Min(0f)] public float celebrateSeconds = 3f;

        [Header("Fan Hype (Music Boost)")]
        [Tooltip("Hype points (kills x profile hypeValue) to charge the Fan Hype drop.")]
        [Min(1)] public int hypeForBoost = 10;
        [Tooltip("Hype is multiplied by this while a track with a boost is playing (AuthManager.achievementReward != Nothing). This is what makes buying and playing boost tracks pay off here.")]
        [Min(1f)] public float boostTrackHypeMultiplier = 1.5f;
        [Min(1f)] public float boostDuration = 12f;
        [Tooltip("During Fan Hype the fan only takes (1 - this) of incoming wash.")]
        [Range(0f, 1f)] public float boostFanWashResistance = 0.6f;
        [Tooltip("Wash points the fan recovers the moment Fan Hype is dropped.")]
        [Min(0f)] public float boostInstantRecovery = 15f;
        public Key boostKey = Key.B;                                   // free on keyboard
        public GamepadButton boostGamepadButton = GamepadButton.DpadUp; // free on the pad (D-pad L/R = switch weapon)

        [Header("Camera")]
        public MissionCameraDirector cameraDirector;

        [Header("Events")]
        public UnityEvent onMissionStarted;
        public HeatEvent onIntermissionStarted;
        public HeatEvent onHeatStarted;
        public WaveEvent onWaveStarted;
        public WaveEvent onWaveCleared;
        public HeatEvent onHeatSaved;
        public HeatEvent onHeatFailed;
        [Tooltip("PIMLR #STF: a fan was washed and the run went back to the first fan.")]
        public UnityEvent onMissionRestarted;
        public UnityEvent onMissionCompleted;
        public UnityEvent onBoostReady;
        public UnityEvent onBoostStarted;
        public UnityEvent onBoostEnded;

        // ------------------------------------------------------------------ runtime (read by HUD)
        public State CurrentState { get; private set; } = State.Idle;
        public int CurrentHeatIndex { get; private set; } = -1;
        public int HeatCount => heats.Count;
        public FanHeat CurrentFan => CurrentHeatIndex >= 0 && CurrentHeatIndex < heats.Count ? heats[CurrentHeatIndex] : null;
        public int CurrentWaveIndex { get; private set; } = -1;
        public int WaveCount => CurrentFan && CurrentFan.waveSet ? CurrentFan.waveSet.waves.Count : 0;
        public int WaveTotal { get; private set; }
        public int WaveKilled { get; private set; }
        public int EnemiesRemaining => Mathf.Max(0, WaveTotal - WaveKilled);
        public bool IntermissionTimed { get; private set; }
        public float IntermissionTimeLeft { get; private set; }
        public bool NeedsToExitVehicle { get; private set; }
        public float DistanceToCurrentFan => CurrentFan && PlayerTransform ? Vector3.Distance(PlayerTransform.position, CurrentFan.transform.position) : 0f;
        public float Hype { get; private set; }
        public float Hype01 => Mathf.Clamp01(Hype / hypeForBoost);
        public bool BoostReady { get; private set; }
        public bool BoostActive { get; private set; }
        public float BoostTimeLeft { get; private set; }
        public int RetriesThisHeat { get; private set; }
        /// PIMLR #STF: how many times this run went back to the first fan.
        public int RunRestarts { get; private set; }
        public bool IsRunning => CurrentState != State.Idle && CurrentState != State.Complete;

        /// True while a purchased track with a boost is playing.
        public static bool BoostTrackPlaying =>
            AuthManager.Instance && AuthManager.Instance.achievementReward != AchievementReward.Nothing;

        public Transform PlayerTransform
        {
            get
            {
                if (!playerCtrl) playerCtrl = PlayerControlLock.ResolvePlayer();
                return playerCtrl ? playerCtrl.transform : null;
            }
        }

        JUCharacterController playerCtrl;
        readonly List<FanAggroBrain> alive = new List<FanAggroBrain>();
        readonly HashSet<FanAggroBrain> grudges = new HashSet<FanAggroBrain>();
        Coroutine missionRoutine, boostRoutine;
        bool heatFailed;
        bool lastHeatWon;
        int spawnCursor;
        int pinnedStoryKills = -1;
        bool killCounterDirty;

        // ------------------------------------------------------------------ lifecycle
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Debug.LogWarning("[SaveTheFans] Two mission managers in the scene; disabling the extra one.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        void Start()
        {
            foreach (var h in heats) if (h) h.SetHighlighted(false);
            if (beginOnStart) BeginMission();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            PlayerControlLock.ForceUnlock();
        }

        void Update()
        {
            if (!BoostReady || BoostActive) return;
            if (CurrentState != State.Wave && CurrentState != State.BetweenWaves) return;
            if (PimlrInput.GameplayButtonsBlocked || Time.timeScale == 0f) return;

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb[boostKey].wasPressedThisFrame) || (pad != null && pad[boostGamepadButton].wasPressedThisFrame))
                ActivateBoost();
        }

        void LateUpdate()
        {
            if (!IsRunning) return;

            // See the header: keep mission kills from advancing the Story zone.
            var gem = GameExecutionManager.Instance;
            if (protectStoryKillCounter && gem && pinnedStoryKills >= 0 && gem.zombiesKilled != pinnedStoryKills)
            {
                gem.zombiesKilled = pinnedStoryKills;
                killCounterDirty = true; // OnKillEnemy just wrote "N / 10" into the goal panel
            }

            if (killCounterDirty) PushKillCounter();
        }

        // ------------------------------------------------------------------ public API
        /// Called by GameExecutionManager when the Story reaches this mission (or tick Begin On Start to test).
        public void BeginMission()
        {
            if (IsRunning) return;
            if (heats.Count == 0) { Debug.LogError("[SaveTheFans] No heats assigned.", this); return; }

            int start = resumeSavedProgress ? Mathf.Clamp(PlayerPrefs.GetInt(PrefNextHeat, 0), 0, heats.Count - 1) : 0;
            var gem = GameExecutionManager.Instance;
            pinnedStoryKills = gem ? gem.zombiesKilled : -1;
            missionRoutine = StartCoroutine(RunMission(start));
        }

        public void AbortMission()
        {
            if (missionRoutine != null) StopCoroutine(missionRoutine);
            missionRoutine = null;
            EndBoost();
            DespawnAll();
            foreach (var h in heats) if (h) { h.SetHighlighted(false); h.SetRestockActive(false); h.Invulnerable = false; }
            PlayerControlLock.ForceUnlock();
            CurrentState = State.Idle;
        }

        /// Forget which fan the player was on. Call when a new Story run starts or progress is reset.
        public static void ClearSavedProgress()
        {
            PlayerPrefs.DeleteKey(PrefNextHeat);
            PlayerPrefs.Save();
        }

        [ContextMenu("Reset Saved Progress")]
        void ResetProgressFromMenu() => ClearSavedProgress();

        /// Also callable from a HUD button.
        public void ActivateBoost()
        {
            if (!BoostReady || BoostActive) return;
            boostRoutine = StartCoroutine(RunBoost());
        }

        // Called by FanAggroBrain ------------------------------------------------
        public bool TryAcquireGrudge(FanAggroBrain brain)
        {
            if (grudges.Contains(brain)) return true;
            if (grudges.Count >= maxSimultaneousGrudges) return false;
            grudges.Add(brain);
            return true;
        }

        public void ReleaseGrudge(FanAggroBrain brain) => grudges.Remove(brain);

        public void ReportEnemyDown(FanAggroBrain brain)
        {
            if (!alive.Remove(brain)) return;
            WaveKilled++;
            killCounterDirty = true;

            if (BoostActive || BoostReady) return;
            float gain = (brain.profile ? brain.profile.hypeValue : 1) * (BoostTrackPlaying ? boostTrackHypeMultiplier : 1f);
            Hype = Mathf.Min(hypeForBoost, Hype + gain);
            if (Hype >= hypeForBoost)
            {
                BoostReady = true;
                onBoostReady?.Invoke();
            }
        }

        // ------------------------------------------------------------------ flow
        IEnumerator RunMission(int startHeat)
        {
            CurrentState = State.Intermission; // so IsRunning is true from the first frame
            SaveTheFansHUD.EnsureExists(this); // PIMLR #STF: placeholder HUD + threat bar if the scene has none
            onMissionStarted?.Invoke();
            var p = PlayerTransform;
            if (p) AmmoRestockStation.CaptureBaseline(p.gameObject);

            for (int i = startHeat; i < heats.Count; i++)
            {
                CurrentHeatIndex = i;
                var fan = heats[i];
                if (!fan || !fan.waveSet) { Debug.LogError($"[SaveTheFans] Heat {i + 1} is missing a FanHeat or wave set; skipping.", this); continue; }

                // Timed only when the player travelled here from the previous fan in this session.
                // After a death the scene reloads and the player is back at the start, so that leg is untimed.
                bool timed = i > startHeat || (i == 0 && timeFirstFan);
                yield return Intermission(fan, timed);

                RetriesThisHeat = 0;
                bool firstAttempt = true;
                bool restart = false;
                while (true)
                {
                    yield return RunHeat(fan, firstAttempt);
                    if (lastHeatWon) break;
                    firstAttempt = false;
                    RetriesThisHeat++;
                    yield return new WaitForSeconds(retryDelay); // the washed fan stays down for a beat
                    if (restartFromFirstFanOnWashout) { restart = true; break; }
                }

                if (restart)
                {
                    // PIMLR #STF: washed fan = run failed. Fade out, every fan back to full and standing, progress
                    // forgotten, player put back near Sirihanna, fade in; then the walk to the first fan (untimed) for wave 1.
                    RunRestarts++;
                    yield return RestartSequence();
                    ClearSavedProgress();
                    startHeat = 0;
                    onMissionRestarted?.Invoke();
                    i = -1; // the loop's i++ makes this 0
                    continue;
                }

                PlayerPrefs.SetInt(PrefNextHeat, i + 1);
                PlayerPrefs.Save();
            }

            // Let any last death checks land (JUHealth polls every 0.5 s) before the counter is released.
            yield return new WaitForSeconds(0.75f);

            ClearSavedProgress();
            CurrentState = State.Complete;
            missionRoutine = null;
            onMissionCompleted?.Invoke();

            // Hands the Story back (added by the setup guide's GameExecutionManager patch; ignored if absent).
            var gem = GameExecutionManager.Instance;
            if (gem) gem.SendMessage("OnSaveTheFansComplete", SendMessageOptions.DontRequireReceiver);
        }

        IEnumerator Intermission(FanHeat fan, bool timed)
        {
            CurrentState = State.Intermission;
            float allowed = fan.travelSeconds > 0f ? fan.travelSeconds : intermissionSeconds;
            IntermissionTimed = timed;
            IntermissionTimeLeft = timed ? allowed : 0f;
            fan.ResetWash();
            fan.SetHighlighted(true, travelBeacon: true);
            onIntermissionStarted?.Invoke(CurrentHeatIndex);

            bool expired = false;
            while (true)
            {
                var p = PlayerTransform;
                bool inRange = p && FlatDistance(p.position, fan.transform.position) <= fan.arrivalRadius;
                NeedsToExitVehicle = inRange && requireOnFoot && PimlrInput.PlayerInVehicle;

                if (inRange && !NeedsToExitVehicle) break;

                if (timed)
                {
                    IntermissionTimeLeft -= Time.deltaTime;
                    if (IntermissionTimeLeft <= 0f)
                    {
                        if (onTimerExpired == TimerExpiredBehaviour.StartHeatAnyway) { expired = true; IntermissionTimeLeft = 0f; break; }
                        IntermissionTimeLeft = allowed;
                    }
                }
                yield return null;
            }

            NeedsToExitVehicle = false;
            if (timed && !expired && earlyArrivalCoinsPerSecond > 0)
                AddCoins(Mathf.CeilToInt(IntermissionTimeLeft) * earlyArrivalCoinsPerSecond);

            fan.SetHighlighted(true, travelBeacon: false);
        }

        IEnumerator RunHeat(FanHeat fan, bool firstAttempt)
        {
            heatFailed = false;
            lastHeatWon = false;
            spawnCursor = Random.Range(0, Mathf.Max(1, fan.spawnPoints.Length));
            fan.ResetWash();
            fan.Invulnerable = true;
            fan.SetRestockActive(true);
            fan.WashedOut += OnFanWashedOut;
            fan.NotifyHeatStarted();
            onHeatStarted?.Invoke(CurrentHeatIndex);

            var waves = fan.waveSet.waves;
            for (int w = 0; w < waves.Count && !heatFailed; w++)
            {
                var wave = waves[w];
                CurrentWaveIndex = w;
                var queue = FanWaveSet.BuildSpawnQueue(wave);
                WaveTotal = queue.Count;
                WaveKilled = 0;
                killCounterDirty = true;
                int next = 0;

                if (w == 0)
                {
                    CurrentState = State.Cinematic;
                    FanAggroBrain first = null;

                    // The first enemy's portal opens when the camera gets there, so the player sees it step out.
                    Vector3 firstSpot = PickSpawnPosition(fan);
                    System.Action spawnFirst = () =>
                    {
                        if (first || queue.Count == 0) return;
                        first = Spawn(queue[next++], fan, firstSpot);
                        if (first) first.SetHeld(true);
                    };

                    if (!cameraDirector || PimlrInput.PlayerInVehicle) fan.PlayGreeting(); // PIMLR #STF: no intro camera
                    if (cameraDirector && !PimlrInput.PlayerInVehicle)
                        yield return firstAttempt
                            ? cameraDirector.PlayHeatIntro(fan, firstSpot, spawnFirst, PlayerTransform)
                            : cameraDirector.PlayQuickFanShot(fan);

                    spawnFirst(); // skipped / no director / retry: make sure it exists
                    if (first) first.SetHeld(false);
                    fan.Invulnerable = false;
                }
                else
                {
                    CurrentState = State.BetweenWaves;
                    fan.Recover(fan.waveSet.recoveryBetweenWaves);
                    float t = 0f;
                    while (t < wave.preWaveDelay && !heatFailed) { t += Time.deltaTime; yield return null; }
                    if (heatFailed) break;
                }

                CurrentState = State.Wave;
                if (w > 0) fan.PlayGreeting(); // PIMLR #STF: the fan greets the player again at every new wave
                onWaveStarted?.Invoke(CurrentHeatIndex, w);

                float nextSpawnAt = Time.time + (w == 0 ? wave.spawnInterval : 0f);
                while (!heatFailed && (next < queue.Count || alive.Count > 0))
                {
                    alive.RemoveAll(b => b == null); // destroyed without reporting (shouldn't happen, but be safe)
                    if (next < queue.Count && alive.Count < wave.maxAlive && Time.time >= nextSpawnAt)
                    {
                        Spawn(queue[next++], fan, null);
                        nextSpawnAt = Time.time + wave.spawnInterval;
                    }
                    yield return null;
                }

                if (!heatFailed) onWaveCleared?.Invoke(CurrentHeatIndex, w);
            }

            fan.WashedOut -= OnFanWashedOut;
            fan.SetRestockActive(false);
            fan.Invulnerable = false;

            if (heatFailed)
            {
                CurrentState = State.HeatFailed;
                EndBoost();
                DespawnAll();
                onHeatFailed?.Invoke(CurrentHeatIndex);
                yield break;
            }

            CurrentState = State.HeatSaved;
            EndBoost();
            fan.NotifySaved();
            AddCoins(fan.waveSet.coinReward);
            onHeatSaved?.Invoke(CurrentHeatIndex);
            yield return new WaitForSeconds(celebrateSeconds);
            fan.SetHighlighted(false);
            lastHeatWon = true;
        }

        void OnFanWashedOut(FanHeat fan) => heatFailed = true;

        // PIMLR #STF: fade-out / fade-in restart. Holds PimlrInterstitial so nothing arrives while the screen is black.
        IEnumerator RestartSequence()
        {
            PimlrInterstitial.Begin();
            bool locked = false;
            try
            {
                yield return ScreenFader.FadeToBlack(restartFadeOut);

                var pc = PlayerControlLock.ResolvePlayer();
                if (pc && pc.IsDriving)
                {
                    JUTPS.ActionScripts.DriveVehicles.ExitVehicle(pc.gameObject);
                    yield return new WaitForSecondsRealtime(0.3f); // exit animation / re-enable the character
                }
                PlayerControlLock.Lock(); // after leaving the vehicle, so the exit isn't blocked
                locked = true;

                DespawnAll();
                foreach (var h in heats) if (h) { h.ResetWash(); h.SetHighlighted(false); h.SetRestockActive(false); h.Invulnerable = false; }
                TeleportPlayerToRestartSpot(pc);

                yield return new WaitForSecondsRealtime(restartBlackHold);
                yield return ScreenFader.FadeFromBlack(restartFadeIn);
            }
            finally
            {
                if (locked) PlayerControlLock.Unlock();
                PimlrInterstitial.End();
            }
        }

        void TeleportPlayerToRestartSpot(JUCharacterController pc)
        {
            if (!pc) return;
            Transform spot = restartSpot;
            if (!spot)
            {
                var chats = FindObjectsByType<AiChatInteraction>(FindObjectsInactive.Include);
                if (chats.Length > 0) spot = chats[0].transform;
                if (!spot) { var go = GameObject.Find("Sirihanna"); if (go) spot = go.transform; }
            }
            if (!spot) { Debug.LogWarning("[SaveTheFans] Restart: no Sirihanna / restart spot found, player left where they are.", this); return; }

            Vector3 fwd = spot.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 target = spot.position + fwd.normalized * restartSpotOffset;
            if (NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas)) target = hit.position;
            else if (NavMesh.SamplePosition(spot.position, out hit, 6f, NavMesh.AllAreas)) target = hit.position;

            Vector3 look = spot.position - target; look.y = 0f;
            Quaternion rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : pc.transform.rotation;
            var rb = pc.GetComponent<Rigidbody>();
            if (rb) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.position = target; rb.rotation = rot; }
            pc.transform.SetPositionAndRotation(target, rot);
            Physics.SyncTransforms();
        }

        IEnumerator RunBoost()
        {
            BoostReady = false;
            BoostActive = true;
            Hype = 0f;
            BoostTimeLeft = boostDuration;

            var fan = CurrentFan;
            if (fan)
            {
                fan.WashResistance = boostFanWashResistance;
                fan.Recover(boostInstantRecovery);
            }
            onBoostStarted?.Invoke();

            while (BoostTimeLeft > 0f && (CurrentState == State.Wave || CurrentState == State.BetweenWaves))
            {
                BoostTimeLeft -= Time.deltaTime;
                yield return null;
            }

            boostRoutine = null;
            EndBoost();
        }

        void EndBoost()
        {
            if (boostRoutine != null) { StopCoroutine(boostRoutine); boostRoutine = null; }
            if (!BoostActive) return;
            BoostActive = false;
            BoostTimeLeft = 0f;
            if (CurrentFan) CurrentFan.WashResistance = 0f;
            onBoostEnded?.Invoke();
        }

        // ------------------------------------------------------------------ spawning
        FanAggroBrain Spawn(FanSpawnEntry entry, FanHeat fan, Vector3? at)
        {
            if (entry == null || !entry.prefab) return null;

            Vector3 pos = at ?? PickSpawnPosition(fan);
            Vector3 toFan = fan.transform.position - pos; toFan.y = 0f;
            Quaternion rot = toFan.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toFan) : Quaternion.identity;

            var go = Instantiate(entry.prefab, pos, rot, enemiesParent);
            go.name = $"STF_{entry.label}_{CurrentHeatIndex + 1}_{CurrentWaveIndex + 1}";

            var brain = go.GetComponent<FanAggroBrain>();
            if (!brain) brain = go.AddComponent<FanAggroBrain>();
            brain.Init(this, fan, PlayerTransform, entry.aggroProfile ? entry.aggroProfile : defaultAggroProfile, fan.waveSet.washRateMultiplier);
            alive.Add(brain);

            // Portal + robot FX, exactly where we put it (relocate: false keeps our validated spawn point).
            if (usePortalArrival) PimlrEnemySpawnDirector.Arrive(go, relocate: false);
            else go.SetActive(true);
            return brain;
        }

        Vector3 PickSpawnPosition(FanHeat fan)
        {
            var points = fan.spawnPoints;
            Vector2 ring = Random.insideUnitCircle.normalized * 22f;
            Vector3 fallback = fan.transform.position + new Vector3(ring.x, 0f, ring.y);
            if (points == null || points.Length == 0) return SnapToNavMesh(fallback);

            var p = PlayerTransform;
            var cam = Camera.main;
            Transform best = null;
            float bestScore = float.MinValue;

            // Round-robin start so consecutive spawns spread across points.
            for (int k = 0; k < points.Length; k++)
            {
                var sp = points[(spawnCursor + k) % points.Length];
                if (!sp) continue;

                float score = Random.value * 0.5f;
                if (!p || FlatDistance(sp.position, p.position) >= minSpawnDistanceFromPlayer) score += 2f;
                if (preferSpawnsOutOfView && cam && !IsVisible(cam, sp.position + Vector3.up * 1.2f)) score += 1f;
                if (score > bestScore) { bestScore = score; best = sp; }
            }
            spawnCursor = (spawnCursor + 1) % points.Length;

            return SnapToNavMesh(best ? best.position : fallback);
        }

        bool IsVisible(Camera cam, Vector3 point)
        {
            Vector3 vp = cam.WorldToViewportPoint(point);
            if (vp.z <= 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f) return false;
            return !Physics.Linecast(cam.transform.position, point, lineOfSightBlockers, QueryTriggerInteraction.Ignore);
        }

        static Vector3 SnapToNavMesh(Vector3 pos)
        {
            return NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas) ? hit.position : pos;
        }

        void DespawnAll()
        {
            var copy = new List<FanAggroBrain>(alive);
            alive.Clear();
            grudges.Clear();
            foreach (var b in copy) if (b) b.Despawn();
        }

        // ------------------------------------------------------------------ helpers
        void PushKillCounter()
        {
            killCounterDirty = false;
            if (!useGoalPanelKillCounter) return;
            if (CurrentState != State.Wave && CurrentState != State.BetweenWaves && CurrentState != State.Cinematic) return;
            var sms = SceneManagerScript.Instance;
            if (sms != null && sms.goalPanel != null)
                sms.goalPanel.SetCurrentKillInfo($"{WaveKilled} / {WaveTotal}");
        }

        static void AddCoins(int amount)
        {
            if (amount <= 0 || !CoinManager.Instance) return;
            CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() + amount);
            RunStats.AddCoins(amount); // shows in the run summary like every other coin earned in a run
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
