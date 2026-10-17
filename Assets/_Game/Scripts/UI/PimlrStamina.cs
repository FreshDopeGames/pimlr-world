using JUTPS;
using JUTPS.CharacterBrain;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PIMLR (post-playtest): real player stamina + a yellow HUD bar under the health bar.
///
/// JUTPS has no stamina pool (its "sprint taper" just ramps sprint speed up and down), so this adds one:
///   - Sprinting (Shift held / L3 toggled) drains it while moving on the ground. Normal running is free.
///   - Each punch costs a chunk. With no stamina left the punch doesn't happen.
///   - Hitting 0 = exhausted: no sprinting until it refills to RecoverAt (normal running still works).
///   - It refills after a short rest.
///
/// No scene setup: it attaches itself to the Player when a scene loads. The HUD meter is UIStaminaBar on your own
/// Filled Image.
/// Tune the numbers on the PimlrStamina component on the Player at runtime, or change the defaults here.
/// </summary>
[DisallowMultipleComponent]
public class PimlrStamina : MonoBehaviour
{
    public static PimlrStamina Player { get; private set; }

    [Header("Stamina")]
    public float Max = 100f;
    [Tooltip("Drained per second while sprinting (Shift / L3). Normal running doesn't use stamina.")]
    public float SprintDrainPerSecond = 18f;
    [Tooltip("Cost of one punch.")]
    public float PunchCost = 12f;
    [Tooltip("Refilled per second once resting.")]
    public float RegenPerSecond = 30f;
    [Tooltip("Seconds after the last spend before it starts refilling.")]
    public float RegenDelay = 0.75f;
    [Tooltip("After running out, sprinting is locked until stamina is back to this.")]
    public float RecoverAt = 30f;
    [Tooltip("Block punches that the player can't afford.")]
    public bool BlockPunchWhenEmpty = true;

    public float Current { get; private set; }
    public float Normalized => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;
    public bool Exhausted { get; private set; }

    JUCharacterController ctrl;
    float lastSpendTime = -10f;

    void Awake()
    {
        ctrl = GetComponent<JUCharacterController>();
        Current = Max;
        Player = this;
    }

    void OnDestroy() { if (Player == this) Player = null; }

    void Update()
    {
        if (ctrl == null) return;
        float dt = Time.deltaTime;

        bool sprinting = ctrl.IsSprintInputActive && ctrl.IsMoving && ctrl.IsGrounded && !ctrl.IsDriving && !ctrl.IsDead;
        if (sprinting)
        {
            Current = Mathf.Max(0f, Current - SprintDrainPerSecond * dt);
            lastSpendTime = Time.time;
        }
        else if (Time.time - lastSpendTime >= RegenDelay)
        {
            Current = Mathf.Min(Max, Current + RegenPerSecond * dt);
        }

        if (Current <= 0f) Exhausted = true;
        else if (Exhausted && Current >= RecoverAt) Exhausted = false;
    }

    /// <summary>Spend stamina. Returns false (spending nothing) if the action can't be afforded.</summary>
    public bool TrySpend(float amount)
    {
        if (BlockPunchWhenEmpty && (Exhausted || Current <= 0f)) return false;
        Current = Mathf.Max(0f, Current - amount);
        lastSpendTime = Time.time;
        if (Current <= 0f) Exhausted = true;
        return true;
    }

    public void Refill() { Current = Max; Exhausted = false; }

    // ---- hooks used by JUTPS (player only; AI characters are unaffected) ----
    public static bool IsExhausted(JUCharacterBrain c) => Player != null && Player.ctrl == c && Player.Exhausted;

    /// <summary>Called by JUCharacterBrain._DoPunch. False = cancel the punch.</summary>
    public static bool TryPunch(JUCharacterBrain c)
    {
        if (Player == null || Player.ctrl != c) return true;
        return Player.TrySpend(Player.PunchCost);
    }

    // ---- automatic setup ----
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
        // Player-only. The HUD bar is your own Image driven by UIStaminaBar; nothing is built here.
        var pl = GameObject.FindGameObjectWithTag("Player");
        if (pl != null && pl.GetComponent<JUCharacterController>() != null && pl.GetComponent<PimlrStamina>() == null)
            pl.AddComponent<PimlrStamina>();
    }

    /// <summary>The player's stamina, attaching it first if the player spawned after the scene loaded.</summary>
    public static PimlrStamina Resolve()
    {
        if (Player == null) Attach();
        return Player;
    }
}
