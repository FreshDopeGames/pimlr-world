using System;
using System.Collections.Generic;
using JUTPS;
using JUTPS.WeaponSystem; // PIMLR #STATS #ANL
using UnityEngine;
using UnityEngine.SceneManagement;

// PIMLR #STATS: owns the player's live movement / combat values. Each one is
//     value = StatTuning(artist rating) x every active modifier
// and is written to JUTPS / PimlrStamina. Boosts (AuthManager.PowerChange), music boosts, achievement rewards and
// later decor perks add a modifier here instead of writing JUTPS values, so a boost never wipes the artist's stats.
//
// The block is authoritative: if something else overwrites one of these values (e.g. PimlrPlayerFeel's turn speed
// on scene load) it re-applies on the next LateUpdate.
//
// No scene setup: it attaches itself to the Player (tag "Player" with a JUCharacterController) on each scene load,
// like PimlrStamina. To try other ratings before the Artist Roster exists, tick Override Ratings on the component at
// runtime and drag the sliders.
//
// Unreal: GAS. Six attributes on an AttributeSet; boosts and perks are GameplayEffects with multiply modifiers.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class PlayerStatBlock : MonoBehaviour
{
    public static PlayerStatBlock Local { get; private set; }

    /// <summary>Where base ratings come from. Phase 2's PlayerLoadout sets this to the selected artist's stats.</summary>
    public static Func<ArtistStats> BaseStatsProvider;

    /// <summary>Raised after values are written (Fit Check stats panel, analytics).</summary>
    public static event Action<PlayerStatBlock> Applied;

    [Header("Debug: try ratings before the roster exists")]
    public bool overrideRatings;
    public ArtistStats debugRatings = ArtistStats.Baseline;

    [Header("Live values (read only)")]
    [SerializeField] float liveSpeed;
    [SerializeField] float liveTurn;
    [SerializeField] float liveAir;
    [SerializeField] float liveJump;
    [SerializeField] float liveMaxHealth;
    [SerializeField] float liveStaminaMax;
    [SerializeField] float liveStaminaRegen;
    [SerializeField] float liveAccuracyMul;
    [SerializeField] string activeModifiers;

    // ------------------------------------------------------------------ modifiers (static: survive player re-creation)

    struct Modifier
    {
        public string source;
        public StatKind stat;
        public float multiplier;
    }

    static readonly List<Modifier> modifiers = new List<Modifier>();
    static int modifierVersion;

    /// <summary>Multiply one stat's final value. Several modifiers from different sources stack multiplicatively.</summary>
    public static void AddModifier(string source, StatKind stat, float multiplier)
    {
        modifiers.Add(new Modifier { source = source, stat = stat, multiplier = multiplier });
        modifierVersion++;
    }

    /// <summary>Remove every modifier a source added (e.g. "boost" when the boost track stops).</summary>
    public static void RemoveSource(string source)
    {
        if (modifiers.RemoveAll(m => m.source == source) > 0) modifierVersion++;
    }

    public static float Multiplier(StatKind stat)
    {
        float m = 1f;
        foreach (var mod in modifiers) if (mod.stat == stat) m *= mod.multiplier;
        return m;
    }

    /// <summary>Refill health to the new max on the next apply (used by the Max HP boost, as before).</summary>
    public static void RequestHealthRefill() => pendingRefill = true;
    static bool pendingRefill;

    // ------------------------------------------------------------------ component

    JUCharacterController ctrl;
    JUHealth health;
    PimlrStamina stamina;

    readonly Dictionary<Weapon, Vector2> weaponBase = new Dictionary<Weapon, Vector2>(); // x = Precision, y = Loss/shot
    Weapon lastRight, lastLeft;

    int appliedVersion = -1;
    ArtistStats appliedRatings;
    bool firstApply = true;

    // last values written, to spot outside writes
    float wSpeed, wTurn, wAir, wJump, wMaxHealth;

    public ArtistStats Ratings => overrideRatings ? debugRatings : (BaseStatsProvider != null ? BaseStatsProvider() : ArtistStats.Baseline);

    void Awake()
    {
        ctrl = GetComponent<JUCharacterController>();
        health = GetComponent<JUHealth>();
        Local = this;
    }

    void OnDestroy()
    {
        if (Local == this) Local = null;
    }

    public void MarkDirty() => appliedVersion = -1;

    void LateUpdate()
    {
        if (ctrl == null) return;

        if (stamina == null)
        {
            stamina = PimlrStamina.Player != null && PimlrStamina.Player.gameObject == gameObject ? PimlrStamina.Player : GetComponent<PimlrStamina>();
            if (stamina != null) MarkDirty();
        }

        if (ctrl.WeaponInUseRightHand != lastRight || ctrl.WeaponInUseLeftHand != lastLeft) MarkDirty();

        bool overwritten =
            !Mathf.Approximately(ctrl.Speed, wSpeed) ||
            !Mathf.Approximately(ctrl.RotationSpeed, wTurn) ||
            !Mathf.Approximately(ctrl.AirInfluenceControll, wAir) ||
            !Mathf.Approximately(ctrl.JumpForce, wJump) ||
            (health != null && !Mathf.Approximately(health.MaxHealth, wMaxHealth));

        ArtistStats r = Ratings;
        if (overwritten || pendingRefill || appliedVersion != modifierVersion || !Same(r, appliedRatings))
            Apply(r);
    }

    static bool Same(ArtistStats a, ArtistStats b) =>
        a.speed == b.speed && a.agility == b.agility && a.accuracy == b.accuracy &&
        a.health == b.health && a.stamina == b.stamina && a.jump == b.jump;

    void Apply(ArtistStats r)
    {
        var t = StatTuning.Active;

        float speedMul = Multiplier(StatKind.Speed);
        float agilityMul = Multiplier(StatKind.Agility);
        float accuracyMul = Multiplier(StatKind.Accuracy);
        float healthMul = Multiplier(StatKind.Health);
        float staminaMul = Multiplier(StatKind.Stamina);
        float jumpMul = Multiplier(StatKind.Jump);

        // movement
        ctrl.Speed = wSpeed = t.runSpeed.Evaluate(r.speed) * speedMul;
        ctrl.RotationSpeed = wTurn = PimlrPlayerFeel.TurnSpeed * t.turnSpeedMultiplier.Evaluate(r.agility) * agilityMul;
        ctrl.AirInfluenceControll = wAir = t.airControl.Evaluate(r.agility) * agilityMul;
        ctrl.JumpForce = wJump = t.jumpForce.Evaluate(r.jump) * jumpMul;

        // health: keep a full bar full, otherwise just clamp
        if (health != null)
        {
            float oldMax = health.MaxHealth;
            float newMax = t.maxHealth.Evaluate(r.health) * healthMul;
            bool wasFull = health.Health >= oldMax - 0.01f;
            health.MaxHealth = wMaxHealth = newMax;
            if (firstApply || pendingRefill || wasFull) health.Health = newMax;
            else health.Health = Mathf.Min(health.Health, newMax);
            if (health.healthSlider != null)
            {
                health.healthSlider.maxValue = newMax;
                health.healthSlider.value = health.Health;
            }
        }
        pendingRefill = false;

        // stamina
        if (stamina != null)
        {
            stamina.Max = t.staminaMax.Evaluate(r.stamina) * staminaMul;
            stamina.RegenPerSecond = t.staminaRegen.Evaluate(r.stamina) * staminaMul;
            if (firstApply) stamina.Refill();
        }

        // accuracy: multiply each equipped weapon's own values
        float precMul = t.precisionMultiplier.Evaluate(r.accuracy) * accuracyMul;
        float lossMul = t.spreadPerShotMultiplier.Evaluate(r.accuracy) / Mathf.Max(0.01f, accuracyMul);
        ApplyWeapon(ctrl.WeaponInUseRightHand, precMul, lossMul);
        ApplyWeapon(ctrl.WeaponInUseLeftHand, precMul, lossMul);
        lastRight = ctrl.WeaponInUseRightHand;
        lastLeft = ctrl.WeaponInUseLeftHand;

        appliedVersion = modifierVersion;
        appliedRatings = r;
        firstApply = false;

        liveSpeed = ctrl.Speed; liveTurn = ctrl.RotationSpeed; liveAir = ctrl.AirInfluenceControll; liveJump = ctrl.JumpForce;
        liveMaxHealth = health != null ? health.MaxHealth : 0f;
        liveStaminaMax = stamina != null ? stamina.Max : 0f;
        liveStaminaRegen = stamina != null ? stamina.RegenPerSecond : 0f;
        liveAccuracyMul = precMul;
        activeModifiers = Describe();

        Applied?.Invoke(this);
    }

    void ApplyWeapon(Weapon w, float precMul, float lossMul)
    {
        if (w == null) return;
        if (!weaponBase.TryGetValue(w, out Vector2 b))
            weaponBase[w] = b = new Vector2(w.Precision, w.LossOfAccuracyPerShot);
        w.Precision = b.x * precMul;
        w.LossOfAccuracyPerShot = b.y * lossMul;
    }

    static string Describe()
    {
        if (modifiers.Count == 0) return "none";
        var parts = new List<string>();
        foreach (var m in modifiers) parts.Add($"{m.source}:{m.stat} x{m.multiplier:0.##}");
        return string.Join(", ", parts);
    }

    // ------------------------------------------------------------------ automatic setup

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Attach();

    static void Attach()
    {
        var pl = GameObject.FindGameObjectWithTag("Player");
        if (pl != null && pl.GetComponent<JUCharacterController>() != null && pl.GetComponent<PlayerStatBlock>() == null)
            pl.AddComponent<PlayerStatBlock>();
    }

    /// <summary>The player's stat block, attaching it first if the player spawned after the scene loaded.</summary>
    public static PlayerStatBlock Resolve()
    {
        if (Local == null) Attach();
        return Local;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Local = null;
        BaseStatsProvider = null;
        Applied = null;
        modifiers.Clear();
        modifierVersion = 0;
        pendingRefill = false;
    }
}

/// <summary>
/// PIMLR #STATS: vehicle speed boost as a multiplier on each car's own starting values (vehicles have no artist).
/// Replaces AuthManager's hard-coded MaxVelocity 600 / TorqueForce 1200 and the 300 / 600 reset.
/// </summary>
public static class VehicleStatBoost
{
    static readonly Dictionary<JUTPS.VehicleSystem.CarController, Vector2> carBase = new Dictionary<JUTPS.VehicleSystem.CarController, Vector2>(); // x = MaxVelocity, y = TorqueForce

    public static void Apply(JUTPS.VehicleSystem.CarController car, float multiplier)
    {
        if (car == null) return;
        if (!carBase.TryGetValue(car, out Vector2 b))
            carBase[car] = b = new Vector2(car.VehicleEngine.MaxVelocity, car.VehicleEngine.TorqueForce);
        car.VehicleEngine.MaxVelocity = b.x * multiplier;
        car.VehicleEngine.TorqueForce = b.y * multiplier;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => carBase.Clear();
}
