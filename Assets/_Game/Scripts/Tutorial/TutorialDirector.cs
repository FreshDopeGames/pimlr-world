// PIMLR #TUT: Controls warm-up before Story Mode.
// A short, low-stakes walk through move / look / sprint / jump / roll, then aim / fire / reload with a LENT water gun
// and one toned-down PIMLR_Melee robot, then a second robot after the player plays 'Boom Bye Bye' (Thurz - ADVISORY)
// on the music player for its 3x Ammo Size boost. The gun is taken back afterward (ammo and unlock state restored), so the
// player still has to visit Sirihanna to get it for real. Once per user session (see TutorialSession); once per user session (see TutorialSession). Runs at the start of a fresh Story run (Zone.ChatWilly),
// before the Sirihanna trigger is switched on.
//
// How it plugs into the existing game:
//   * GameExecutionManager.Start (ChatWilly branch): TutorialDirector.RunThen(() => ai_Chat_Triger.SetActive(true), enemyPrefab)
//   * GameExecutionManager.OnKillEnemy: returns early while IsRunning (no coins, no zombiesKilled, no leaderboard kill).
//   * RunStats.RestartClock(): called on finish, so warm-up time doesn't count toward the Story leaderboard time.
//   * Each skill is detected from JUCharacterController state flags (IsSprinting, IsJumping, IsRolling, IsPunching),
//     not raw buttons, so keyboard, Xbox and PlayStation all work with no bindings to wire.
//   * Button names come from PimlrInput.Label, so prompts follow the device the player is using.
//   * The enemy comes in through PimlrEnemySpawnDirector.Arrive (portal + robot FX) and PimlrObjectiveArrow points at it.
//   * Self-bootstrapping: if no TutorialDirector is placed in the scene, RunThen creates one with the defaults below.
//     Place one in YannicksWorld only if you want to tune values in the Inspector.
//
// Unreal mapping:
//   TutorialSession flag      -> UTutorialSubsystem : UGameInstanceSubsystem (bool + BlueprintAssignable delegate)
//   TutorialDirector          -> ATutorialDirector actor (or UWorldSubsystem) driven by a step index / StateTree
//   Step list                 -> UTutorialStepData : UPrimaryDataAsset (TArray<FTutorialStep>)
//   State-flag checks         -> query your Character (bIsSprinting, bIsRolling...) or bind Enhanced Input actions
//   Health floor              -> clamp inside the health component's TakeDamage (before death is evaluated)
//   Coroutines                -> latent Blueprint flow / FTimerHandle

using System;
using System.Collections;
using System.Collections.Generic;
using JUTPS;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class TutorialDirector : MonoBehaviour
{
    public enum Check { Message, Move, Look, Sprint, Jump, Roll, Punch, Fight, LendGun, Aim, Shoot, Reload, ReturnGun, PlayBoostTrack }

    [Serializable]
    public class Step
    {
        public Check check;
        [Tooltip("Tokens: {Move} {Look} {Run} {Jump} {Roll} {Aim} {Fire} {Reload} {Play} {Skip} - replaced with the player's current device labels.")]
        [TextArea(1, 3)] public string text;
        [Tooltip("Move: metres. Look: degrees of camera turn. Sprint: seconds sprinting. Jump/Roll/Aim/Shoot/Reload: count. Message/LendGun/ReturnGun: seconds on screen.")]
        public float amount = 1f;
        [Tooltip("Skipped automatically while the player is on a gamepad (e.g. Roll has no gamepad binding yet).")]
        public bool keyboardOnly;

        public Step(Check c, string t, float a, bool kbOnly = false) { check = c; text = t; amount = a; keyboardOnly = kbOnly; }
    }

    public static TutorialDirector Instance { get; private set; }
    public static bool IsRunning { get; private set; }
    public static event Action TutorialFinished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; IsRunning = false; TutorialFinished = null; }

    [Header("Steps (Reset the component to restore defaults)")]
    [SerializeField] List<Step> steps = DefaultSteps();

    [Header("Enemy")]
    [Tooltip("Leave empty to use the prefab GameExecutionManager passes in (the Zone 1 spawner's PIMLR_Melee).")]
    [SerializeField] GameObject enemyPrefab;
    [SerializeField] float enemySpawnDistance = 8f;
    [Tooltip("Multiplier on the robot's hit damage (PIMLR_Melee hits for 30; 0.34 -> ~10).")]
    [SerializeField, Range(0.05f, 1f)] float enemyDamageScale = 0.34f;
    [Tooltip("Multiplier on the robot's health.")]
    [SerializeField, Range(0.1f, 1f)] float enemyHealthScale = 0.5f;

    [Header("Player safety")]
    [Tooltip("Player health never drops below this fraction of max while the warm-up runs.")]
    [SerializeField, Range(0.05f, 1f)] float playerHealthFloor = 0.35f;

    [Header("Skip")]
    [SerializeField] float skipHoldSeconds = 1.25f;
    [SerializeField] float pauseBetweenSteps = 0.6f;

    JUCharacterController player;
    JUHealth playerHealth;
    GameObject spawnedEnemy;
    TutorialPromptUI ui;
    Action pendingContinuation;
    GameObject pendingEnemyPrefab;
    bool skipRequested;
    float skipHeld;
    Step currentStep;

    // ------------------------------------------------------------------ entry point

    /// <summary>Run the warm-up (if this session hasn't had it yet), then invoke continueWith.
    /// Without a warm-up to run, continueWith is invoked immediately.</summary>
    public static void RunThen(Action continueWith, GameObject fallbackEnemyPrefab = null)
    {
        if (TutorialSession.CompletedThisSession) { continueWith?.Invoke(); return; }
        if (IsRunning && Instance) { Instance.pendingContinuation += continueWith; return; }
        if (!Instance) Instance = new GameObject("PIMLR_TutorialDirector").AddComponent<TutorialDirector>();
        Instance.pendingEnemyPrefab = fallbackEnemyPrefab;
        Instance.Begin(continueWith);
    }

    public void RequestSkip() => skipRequested = true;

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        if (IsRunning) PimlrObjectiveArrow.ClearOverride();
        Instance = null;
        IsRunning = false;
        if (ui) Destroy(ui.gameObject);
    }

    void Begin(Action continueWith)
    {
        pendingContinuation = continueWith;
        skipRequested = false;
        skipHeld = 0f;
        IsRunning = true; // set now so OnKillEnemy is suppressed even before the first frame
        StartCoroutine(Run());
    }

    // ------------------------------------------------------------------ main flow

    IEnumerator Run()
    {
        // Never start behind the loading screen / fade.
        yield return PimlrInterstitial.WaitUntilClear(0.25f);

        var go = GameObject.FindWithTag("Player");
        player = go ? go.GetComponent<JUCharacterController>() : null;
        playerHealth = go ? go.GetComponent<JUHealth>() : null;
        if (!player)
        {
            Debug.LogWarning("PIMLR #TUT: no Player found - skipping the warm-up.");
            Finish();
            yield break;
        }

        if (!ui) ui = TutorialPromptUI.Create();
        PimlrInput.SchemeChanged += RefreshLabels;
        RefreshLabels();
        Debug.Log("PIMLR #TUT: controls warm-up started.");

        foreach (var step in steps)
        {
            if (skipRequested) break;
            if (step.keyboardOnly && PimlrInput.UsingGamepad) continue;

            currentStep = step;
            ui.Show(Format(step.text));
            yield return RunStep(step);
            if (skipRequested) break;

            if (step.check != Check.Message)
            {
                ui.FlashComplete();
                yield return new WaitForSecondsRealtime(pauseBetweenSteps);
            }
        }

        Finish();
    }

    IEnumerator RunStep(Step step)
    {
        switch (step.check)
        {
            case Check.Message:
            {
                float t = 0f;
                while (t < step.amount && !skipRequested) { t += Time.unscaledDeltaTime; yield return null; }
                break;
            }

            case Check.Move:
            {
                float travelled = 0f;
                Vector3 last = Flat(player.transform.position);
                while (travelled < step.amount && !skipRequested)
                {
                    Vector3 now = Flat(player.transform.position);
                    float d = Vector3.Distance(now, last);
                    if (d < 2f) travelled += d; // ignore teleports / respawn snaps
                    last = now;
                    ui.SetProgress(Bar(travelled / step.amount));
                    yield return null;
                }
                break;
            }

            case Check.Look:
            {
                Transform cam = Camera.main ? Camera.main.transform : null;
                if (!cam) yield break;
                float turned = 0f, lastYaw = cam.eulerAngles.y;
                while (turned < step.amount && !skipRequested)
                {
                    if (!cam) yield break;
                    float yaw = cam.eulerAngles.y;
                    turned += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
                    lastYaw = yaw;
                    ui.SetProgress(Bar(turned / step.amount));
                    yield return null;
                }
                break;
            }

            case Check.Sprint:
            {
                float held = 0f;
                while (held < step.amount && !skipRequested)
                {
                    if (player.IsSprinting && player.IsMoving) held += Time.deltaTime;
                    ui.SetProgress(Bar(held / step.amount));
                    yield return null;
                }
                break;
            }

            case Check.Jump:  yield return CountRisingEdges(step, () => player.IsJumping);  break;
            case Check.Roll:  yield return CountRisingEdges(step, () => player.IsRolling);  break;
            case Check.Punch: yield return CountRisingEdges(step, () => player.IsPunching); break;

            case Check.Fight:
                yield return Fight();
                break;

            case Check.LendGun:
                LendWaterGun();
                yield return Hold(step.amount);
                break;

            case Check.Aim:    yield return CountRisingEdges(step, () => player.IsAiming);    break;
            case Check.Reload: yield return CountRisingEdges(step, () => player.IsReloading); break;

            case Check.Shoot:
            {
                var w = lentGun as JUTPS.WeaponSystem.Weapon;
                if (!w) yield break;
                int need = Mathf.Max(1, Mathf.RoundToInt(step.amount)), shots = 0, last = w.BulletsAmounts;
                ui.SetProgress($"0 / {need}");
                while (shots < need && !skipRequested)
                {
                    int now = w.BulletsAmounts;
                    if (now < last) { shots += last - now; ui.SetProgress($"{Mathf.Min(shots, need)} / {need}"); }
                    last = now; // a reload raises it again - that's fine
                    yield return null;
                }
                break;
            }

            case Check.PlayBoostTrack:
                yield return PlayBoostTrack();
                break;

            case Check.ReturnGun:
                yield return ReturnWaterGun();
                yield return Hold(step.amount);
                break;
        }
    }

    IEnumerator Hold(float seconds)
    {
        float t = 0f;
        while (t < seconds && !skipRequested) { t += Time.unscaledDeltaTime; yield return null; }
    }

    // ------------------------------------------------------------------ music boost
    // Playing a track calls UI_GoalPanel.StartBoost -> AuthManager.PowerChange(track's reward). The tutorial cues up the
    // 'Boom Bye Bye' track (free, so always in the player's list), pauses any music so the player has to press Play
    // themselves, and waits for AuthManager.achievementReward to become that track's reward (AmmoSize). The music and
    // boost are left playing afterward - it's the player's own track.
    // Unreal: the music player component broadcasts OnTrackStarted(TrackData); the boost is a GameplayEffect.
    [Header("Music boost")]
    [Tooltip("Boost name of the track the tutorial cues up (MusicData.boostname).")]
    [SerializeField] string boostTrackName = "Boom Bye Bye";

    IEnumerator PlayBoostTrack()
    {
        var sms = SceneManagerScript.Instance;
        var music = sms ? sms.musicSystem : null;
        if (!music || !music.musicSystem || sms.musicData == null || !AuthManager.Instance)
        {
            Debug.LogWarning("PIMLR #TUT: no music player / AuthManager - skipping the music boost step.");
            yield break;
        }

        MusicData track = null;
        foreach (var m in sms.musicData) if (m != null && m.audioClip && m.boostname != null && m.boostname.Contains(boostTrackName)) { track = m; break; }
        if (track == null) foreach (var m in sms.musicData) if (m != null && m.audioClip && m.achievementReward == AchievementReward.AmmoSize) { track = m; break; }
        if (track == null || track.achievementReward == AchievementReward.Nothing) { Debug.LogWarning("PIMLR #TUT: boost track not found."); yield break; }

        // Pause whatever is playing (this also clears any boost), then cue the track from the top.
        if (music.musicSystem.isPlaying) music.PlayMusic();
        if (!music.audioList.Contains(track.audioClip)) music.audioList.Insert(0, track.audioClip); // free track: normally already there
        music.musicSystem.clip = track.audioClip;
        music.songName = track.audioClip.name;
        SetPrivate(music, "currentTime", 0f);
        SetPrivate(music, "currentSoundClipCount", music.audioList.IndexOf(track.audioClip));

        while (!skipRequested && AuthManager.Instance && AuthManager.Instance.achievementReward != track.achievementReward)
            yield return null;
    }

    static void SetPrivate(object target, string field, object value)
    {
        var fi = target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fi != null) fi.SetValue(target, value);
    }

    // ------------------------------------------------------------------ practice water gun
    // The water gun is AllHoldableItems[0] (the item GameExecutionManager equips at every stage). Weapon-switch input
    // (ItemSwitchManager.IsPlayer) stays OFF, so the player can't change weapons. Unreal: give/remove the weapon
    // actor on the Character (or an Inventory component call), caching and restoring its ammo struct.
    JUTPS.ItemSystem.HoldableItem lentGun;
    bool gunLent, lentGunWasUnlocked;
    int lentMagazine, lentReserve;

    void LendWaterGun()
    {
        var inv = player ? player.Inventory : null;
        if (gunLent || !inv || inv.AllHoldableItems == null || inv.AllHoldableItems.Length == 0 || !inv.AllHoldableItems[0]) return;
        lentGun = inv.AllHoldableItems[0];
        lentGunWasUnlocked = lentGun.Unlocked;
        lentGun.Unlocked = true;
        if (lentGun is JUTPS.WeaponSystem.Weapon w)
        {
            lentMagazine = w.BulletsAmounts; lentReserve = w.TotalBullets;
            w.BulletsAmounts = w.BulletsPerMagazine;                                    // full tank for practice
            w.TotalBullets = Mathf.Max(w.TotalBullets, w.BulletsPerMagazine * 4);
        }
        player.SwitchToItem(lentGun.ItemSwitchID, !lentGun.IsLeftHandItem);
        gunLent = true;
        Debug.Log("PIMLR #TUT: practice water gun lent (" + lentGun.name + ").");
    }

    IEnumerator ReturnWaterGun()
    {
        if (!gunLent) yield break;
        gunLent = false;
        // SwitchToItem refuses mid-reload / mid-roll, so keep asking for a moment.
        float until = Time.unscaledTime + 3f;
        while (player && player.IsItemEquiped && Time.unscaledTime < until)
        {
            player.SwitchToItem(-1);
            yield return null;
        }
        if (lentGun is JUTPS.WeaponSystem.Weapon w) { w.BulletsAmounts = lentMagazine; w.TotalBullets = lentReserve; }
        if (lentGun) lentGun.Unlocked = lentGunWasUnlocked;
        if (player && player.IsItemEquiped) Debug.LogWarning("PIMLR #TUT: couldn't holster the practice water gun.");
        else Debug.Log("PIMLR #TUT: practice water gun returned.");
        lentGun = null;
    }

    IEnumerator CountRisingEdges(Step step, Func<bool> flag)
    {
        int need = Mathf.Max(1, Mathf.RoundToInt(step.amount)), count = 0;
        bool was = flag();
        if (need > 1) ui.SetProgress($"0 / {need}");
        while (count < need && !skipRequested)
        {
            bool now = flag();
            if (now && !was)
            {
                count++;
                if (need > 1) ui.SetProgress($"{count} / {need}");
            }
            was = now;
            yield return null;
        }
    }

    IEnumerator Fight()
    {
        var prefab = enemyPrefab ? enemyPrefab : pendingEnemyPrefab;
        if (!prefab)
        {
            Debug.LogWarning("PIMLR #TUT: no enemy prefab - skipping the fight.");
            yield break;
        }

        Vector3 spot = PickEnemySpot();
        Vector3 toPlayer = Flat(player.transform.position - spot);
        Quaternion facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
        spawnedEnemy = Instantiate(prefab, spot, facing);
        spawnedEnemy.name = prefab.name + " (Tutorial)";

        // Low stakes: weaker hits, less health.
        foreach (var dmg in spawnedEnemy.GetComponentsInChildren<Damager>(true)) dmg.Damage *= enemyDamageScale;
        var enemyHealth = spawnedEnemy.GetComponentInChildren<JUHealth>(true);
        if (enemyHealth)
        {
            enemyHealth.MaxHealth *= enemyHealthScale;
            enemyHealth.Health = enemyHealth.MaxHealth;
        }

        // Same portal + robot FX entrance as every other enemy (no camera cutscene: none is requested here).
        PimlrEnemySpawnDirector.Arrive(spawnedEnemy, relocate: false);
        PimlrObjectiveArrow.SetOverride(spawnedEnemy.transform, 3f);

        // Note: Arrive switches the enemy off until it steps out, so "inactive" must not count as defeated.
        while (!skipRequested && spawnedEnemy && !(enemyHealth && enemyHealth.IsDead))
            yield return null;

        PimlrObjectiveArrow.ClearOverride(spawnedEnemy ? spawnedEnemy.transform : null);
    }

    Vector3 PickEnemySpot()
    {
        Vector3 origin = player.transform.position;
        Vector3 fwd = Flat(Camera.main ? Camera.main.transform.forward : player.transform.forward).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;

        // Try ahead of the camera first, then fan out, so the robot appears where the player is looking.
        float[] angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 180f };
        foreach (float a in angles)
        {
            Vector3 want = origin + Quaternion.Euler(0f, a, 0f) * fwd * enemySpawnDistance;
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                return PimlrEnemySpawnDirector.FindClearSpot(hit.position, null);
        }
        return origin + fwd * enemySpawnDistance;
    }

    void Finish()
    {
        StopAllCoroutines();
        StartCoroutine(ReturnWaterGun()); // no-op unless the gun is still out (e.g. the warm-up was skipped)
        PimlrInput.SchemeChanged -= RefreshLabels;
        PimlrObjectiveArrow.ClearOverride(spawnedEnemy ? spawnedEnemy.transform : null);
        if (spawnedEnemy)
        {
            var hp = spawnedEnemy.GetComponentInChildren<JUHealth>(true);
            Destroy(spawnedEnemy, hp && hp.IsDead ? 3f : 0f); // let the death FX play out
        }
        spawnedEnemy = null;
        currentStep = null;

        if (ui) { ui.Hide(); ui.SetSkipFill(0f); }
        if (playerHealth && !playerHealth.IsDead) playerHealth.Health = playerHealth.MaxHealth; // start the Story fresh

        // PIMLR (leaderboards): the warm-up doesn't count toward Story completion time.
        if (RunStats.Active && RunStats.Mode == RunMode.Story) RunStats.RestartClock();

        TutorialSession.MarkCompleted(); // skipping counts as done for this session
        IsRunning = false;
        Debug.Log("PIMLR #TUT: controls warm-up finished" + (skipRequested ? " (skipped)." : "."));

        TutorialFinished?.Invoke();
        var next = pendingContinuation;
        pendingContinuation = null;
        next?.Invoke();
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        if (!IsRunning || !ui) return;

        bool held = (Keyboard.current != null && Keyboard.current.tabKey.isPressed) ||
                    (Gamepad.current != null && Gamepad.current.selectButton.isPressed);
        if (held)
        {
            skipHeld += Time.unscaledDeltaTime;
            ui.SetSkipFill(skipHeld / skipHoldSeconds);
            if (skipHeld >= skipHoldSeconds) RequestSkip();
        }
        else if (skipHeld > 0f)
        {
            skipHeld = 0f;
            ui.SetSkipFill(0f);
        }
    }

    // Low-stakes guarantee. Runs after damage lands; safe because no tutorial hit (~10) can cross the floor (35).
    void LateUpdate()
    {
        if (!IsRunning || !playerHealth || playerHealth.IsDead) return;
        float floor = playerHealth.MaxHealth * playerHealthFloor;
        if (playerHealth.Health < floor) playerHealth.Health = floor;
    }

    // ------------------------------------------------------------------ labels

    void RefreshLabels()
    {
        if (!ui) return;
        ui.SetSkipHint("Hold " + SkipLabel() + " to skip the warm-up");
        if (currentStep != null) ui.SetPrompt(Format(currentStep.text));
    }

    static string Format(string s)
    {
        bool pad = PimlrInput.UsingGamepad;
        return s.Replace("{Move}", pad ? "the Left Stick" : "WASD")
                .Replace("{Look}", pad ? "the Right Stick" : "the Mouse")
                .Replace("{Sprint}", pad ? "Click " + Key(PimlrInput.Label(PimlrButton.Run)) + " to toggle sprint while moving"   // L3 toggles
                                          : "Hold " + Key(PimlrInput.Label(PimlrButton.Run)) + " while moving to sprint")      // Shift is hold
                .Replace("{Run}", Key(PimlrInput.Label(PimlrButton.Run)))
                .Replace("{Jump}", Key(PimlrInput.Label(PimlrButton.Jump)))
                .Replace("{Fire}", Key(PimlrInput.Label(PimlrButton.Fire)))
                .Replace("{Aim}", Key(PimlrInput.Label(PimlrButton.Aim)))
                .Replace("{Reload}", Key(PimlrInput.Label(PimlrButton.Reload)))
                .Replace("{Play}", Key(PimlrInput.Label(PimlrButton.PlayTrack)))
                .Replace("{Roll}", Key(PimlrInput.Label(PimlrButton.Roll)))
                .Replace("{Skip}", Key(SkipLabel()));
    }

    static string SkipLabel()
    {
        switch (PimlrInput.Scheme)
        {
            case PimlrControlScheme.PlayStation: return "Share";
            case PimlrControlScheme.Xbox: return "View";
            default: return "Tab";
        }
    }

    static string Key(string label) => "<color=#FFD126>[" + label + "]</color>";
    static string Bar(float t) => Mathf.FloorToInt(Mathf.Clamp01(t) * 100f) + "%";
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static List<Step> DefaultSteps() => new List<Step>
    {
        new Step(Check.Message, "Quick warm-up before you hit the streets.", 2.5f),
        new Step(Check.Move,    "Move with {Move}", 6f),
        new Step(Check.Look,    "Look around with {Look}", 150f),
        new Step(Check.Sprint,  "{Sprint} - it burns stamina", 1f),
        new Step(Check.Jump,    "Press {Jump} to jump", 1f),
        new Step(Check.Roll,    "Tap {Roll} to dodge-roll", 1f),
        new Step(Check.LendGun,   "Here's a practice water gun. Robots short out when they get wet.", 2.5f),
        new Step(Check.Aim,       "Hold {Aim} to aim", 1f),
        new Step(Check.Shoot,     "Press {Fire} to spray", 5f),
        new Step(Check.Reload,    "Press {Reload} to refill the tank", 1f),
        new Step(Check.Message,   "Incoming! A rogue bot is breaking through...", 2f),
        new Step(Check.Fight,     "Wash the robot - roll away from its swings!", 1f),
        new Step(Check.Message,   "Music powers you up - every track you own has a boost.", 2.5f),
        new Step(Check.PlayBoostTrack, "Press {Play} to play 'Boom Bye Bye' (3x Ammo Size)", 1f),
        new Step(Check.Message,   "Boosted! Your water shots hit 3x bigger. Another bot is coming...", 2.5f),
        new Step(Check.Fight,     "Wash the second robot with your boost!", 1f),
        new Step(Check.ReturnGun, "Nice! Practice is over - the water gun goes back.", 2f),
        new Step(Check.Message,   "Go find Sirihanna to get a water gun of your own.", 3f),
    };

    void Reset() => steps = DefaultSteps();
}
