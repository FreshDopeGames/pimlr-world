// PIMLR #GREN: the player's water balloons ("grenades"), the player's SECONDARY WEAPON. Added to the player
// automatically in gameplay scenes (any scene with a GameExecutionManager), so no prefab or scene edits are needed.
//   - Balloons only come from pickups (PimlrGrenadePickup, prefab _Game/Prefabs/Pickups/WaterBalloonPickup), capped at
//     maxCount. Picking them up never auto-equips; there is no shop and no story auto-equip.
//   - Once you carry any, D-pad Left / Right (Q / E) cycles  [empty hands] -> water gun -> BALLOONS -> other guns...
//     (the balloon slot sits right after the first gun, i.e. the secondary slot). This script takes over JUTPS's
//     next/previous switching while you carry balloons, and hands it back when you run out.
//   - COOK TO THROW (Nov 16): with balloons equipped, press and HOLD RT / R2 / left mouse. The throw animation winds
//     up and holds at the top of the wind-up while the button is held; the blue arrow (PimlrThrowArc) grows as the
//     throw "cooks". Release to finish the animation and let go. Distance = minRange (tap) -> maxRange (fullCookTime).
//     LT (right mouse) still plants the player and turns them with the camera to line the throw up.
//   - The balloon leaves the palm on the clip's measured release frame (PimlrThrowAnimation); PimlrThrowLock keeps
//     the player planted from the press until the clip ends.
//   - The burst damages enemies only (Water_Explosion_Player -> WaterSplashDamage, Target = Enemies).
//   - HUD weapon panel + the pause menu's Secondary slot are driven by PimlrSecondaryItemUI (added alongside this).
// Tuning: Assets/_Game/Resources/PIMLR/PlayerGrenades.asset.
//
// Unreal note: an ActorComponent on the player Character registered as an inventory slot; Enhanced Input actions for
// NextWeapon / PreviousWeapon / Aim / Fire. Fire uses a Hold trigger: Started -> play the throw Anim Montage, a
// "WindupHold" notify pauses it (Montage_Pause) while the action is Ongoing, Completed -> Montage_Resume; the
// "Release" notify spawns the balloon Actor with ProjectileMovementComponent velocity from
// SuggestProjectileVelocity_CustomArc. Elapsed hold time (FInputActionInstance::GetElapsedTime) maps through a
// UCurveFloat to the throw distance; the arc preview is PredictProjectilePath.

using System;
using System.Collections;
using System.Collections.Generic;
using JUTPS;
using JUTPS.ItemSystem;
using JUTPS.JUInputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PimlrPlayerGrenades : MonoBehaviour
{
    public static PimlrPlayerGrenades Instance { get; private set; }
    public static event Action<int> CountChanged;
    /// <summary>Raised when the balloons are equipped (true) or put away (false).</summary>
    public static event Action<bool> EquippedChanged;

    public int Count { get; private set; }
    /// <summary>How long the 'cycle weapons' hint stays over the player's head after a pickup.</summary>
    public static float PickupTooltipSeconds = 3.5f;
    public int MaxCount => S ? S.maxCount : 5;
    public bool IsEquipped => equipped;
    public bool IsAiming => aiming;
    /// <summary>True from the throw press until the balloon leaves the hand.</summary>
    public bool IsCooking => cooking;
    public bool IsFull => Count >= MaxCount;
    /// <summary>Current throw distance (metres): grows while cooking, minRange otherwise.</summary>
    public float AimDistance => aimDistance;
    /// <summary>0-1 share of a full cook (for UI).</summary>
    public float CookFraction => cooking ? cookFraction : 0f;

    static PimlrPlayerGrenadeSettings settings;
    internal static PimlrPlayerGrenadeSettings S
    {
        get
        {
            if (!settings) settings = Resources.Load<PimlrPlayerGrenadeSettings>("PIMLR/PlayerGrenades");
            return settings;
        }
    }

    const int BalloonSlot = int.MinValue;

    JUCharacterController pc;
    JUHealth health;
    Animator animator;
    ItemSwitchManager switchManager;
    Collider[] ownColliders;
    Transform grip;
    PimlrThrowLock throwLock;
    PimlrThrowArc arc;
    GameObject held;

    bool equipped, aiming, throwing, cooking;
    float nextThrow, aimDistance, cookFraction;
    int itemBeforeBalloons = -1;
    bool tookOverSwitching, savedSwitching, savedPunch;
    readonly List<int> slots = new List<int>(8);

    // Animator speed while the wind-up pose is held (restored on release / interruption).
    bool animFrozen;
    float animSpeedBefore = 1f;

    // ------------------------------------------------------------------ auto-attach
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToPlayer();
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => AttachToPlayer();

    static void AttachToPlayer()
    {
        if (!UnityEngine.Object.FindFirstObjectByType<GameExecutionManager>()) return; // gameplay scenes only (not the Living Room)
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var c = go.GetComponent<JUCharacterController>();
            if (c && !c.GetComponent<PimlrPlayerGrenades>()) { c.gameObject.AddComponent<PimlrPlayerGrenades>(); return; }
        }
    }

    void Awake()
    {
        Instance = this;
        pc = GetComponent<JUCharacterController>();
        health = GetComponent<JUHealth>();
        animator = GetComponent<Animator>();
        switchManager = GetComponent<ItemSwitchManager>();
        ownColliders = GetComponentsInChildren<Collider>(true);
        grip = PimlrThrowAnimation.Grip(animator);
        throwLock = PimlrThrowLock.For(this);
        Count = S ? Mathf.Min(S.startCount, S.maxCount) : 0;
        aimDistance = S ? S.minRange : 4f;
        if (!S) Debug.LogWarning("[PIMLR Grenades] Resources/PIMLR/PlayerGrenades.asset is missing - balloons are disabled.");
        else arc = PimlrThrowArc.Create(S.arcFillMaterial, S.arcOutlineMaterial);
        if (!GetComponent<PimlrSecondaryItemUI>()) gameObject.AddComponent<PimlrSecondaryItemUI>(); // HUD icon/ammo + pause slot
    }

    void OnDisable()
    {
        UnfreezeAnimator();
        if (throwLock) throwLock.Release(this);
        if (throwLock) throwLock.Release(arcHolder);
        throwing = false; aiming = false; cooking = false;
        if (arc) arc.Hide();
        SetHeldVisible(false);
        GiveBackSwitching();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (arc) Destroy(arc.gameObject);
        if (held) Destroy(held);
    }

    // ------------------------------------------------------------------ public API
    /// <summary>Adds balloons (capped at MaxCount). Returns how many were actually taken. Never equips them.</summary>
    public int Add(int amount)
    {
        int before = Count;
        Count = Mathf.Clamp(Count + amount, 0, MaxCount);
        if (Count != before)
        {
            CountChanged?.Invoke(Count);
            // PIMLR (Nov 18): remind the player how to get to the balloons.
            PimlrTooltip.ShowOverPlayer($"Use {PimlrTooltip.Key(PimlrTooltip.PrevWeaponLabel)} and {PimlrTooltip.Key(PimlrTooltip.NextWeaponLabel)} to cycle weapons", PickupTooltipSeconds);
        }
        return Count - before;
    }

    // ------------------------------------------------------------------ frame
    readonly object arcHolder = new object(); // lock key for "aiming" (separate from the throw itself)

    void Update()
    {
        if (!S || !pc) return;

        ManageSwitching();
        if (tookOverSwitching && CanCycle()) HandleCycleInput();

        if (equipped)
        {
            // JUTPS equipped something else (a weapon pickup, a cutscene handing over the gun, entering a vehicle...).
            if (pc.IsItemEquiped || pc.IsDriving || pc.IsDead) Unequip(false);
            else if (Count <= 0 && !throwing) Unequip(true); // threw the last one: back to the gun you had
        }

        bool canAct = equipped && CanThrow();
        bool wantAim = canAct && AimHeld();
        if (wantAim != aiming)
        {
            aiming = wantAim;
            if (aiming) throwLock.Hold(arcHolder); else throwLock.Release(arcHolder);
        }

        if (canAct && !throwing && Count > 0 && Time.time >= nextThrow && ThrowPressed())
            StartCoroutine(Throw());

        SetHeldVisible(equipped && !throwing && Count > 0 && !pc.IsDriving);
    }

    void LateUpdate()
    {
        if (!S || !pc) return;
        if (equipped && (aiming || throwing)) FaceCamera();

        if (arc)
        {
            // While cooking the arrow grows with the cook; LT on its own previews a quick (tap) throw.
            if (cooking || (aiming && !throwing)) DrawArc(cooking ? aimDistance : S.minRange);
            else arc.Hide();
        }
    }

    // ------------------------------------------------------------------ weapon cycle (D-pad Left / Right, Q / E)
    // While the player carries balloons (or has them out) JUTPS's own next/previous switching is paused and this
    // script cycles instead, so the balloon slot can sit between the guns.
    void ManageSwitching()
    {
        bool want = Count > 0 || equipped;
        if (want && !tookOverSwitching)
        {
            tookOverSwitching = true;
            if (switchManager) { savedSwitching = switchManager.EnableNextAndPreviousWeaponSwitch; switchManager.EnableNextAndPreviousWeaponSwitch = false; }
        }
        else if (!want && tookOverSwitching) GiveBackSwitching();
    }

    void GiveBackSwitching()
    {
        if (!tookOverSwitching) return;
        tookOverSwitching = false;
        if (switchManager) switchManager.EnableNextAndPreviousWeaponSwitch = savedSwitching;
    }

    bool CanCycle()
    {
        if (throwing || pc.IsDriving || pc.IsDead || pc.IsRagdolled || pc.IsRolling || pc.IsReloading) return false;
        if (Cursor.visible || PimlrInput.GameplayButtonsBlocked || PimlrInterstitial.Playing || PimlrSettingsMenu.IsOpen) return false;
        if (JUPauseGame.Paused || Time.timeScale == 0f) return false;
        return true;
    }

    void HandleCycleInput()
    {
        int dir = 0;
        if (JUInput.Instance() != null)
        {
            if (JUInput.GetButtonDown(JUInput.Buttons.NextWeaponButton)) dir = 1;
            else if (JUInput.GetButtonDown(JUInput.Buttons.PreviousWeaponButton)) dir = -1;
        }
        if (dir != 0) Cycle(dir);
    }

    /// <summary>Steps the weapon cycle one slot forward (+1) or back (-1), balloons included. Same as D-pad Right / Left.</summary>
    public void Cycle(int dir)
    {
        if (dir == 0 || !pc || !CanCycle()) return;
        dir = dir > 0 ? 1 : -1;

        // Slots: empty hands, then the unlocked guns in inventory order, with balloons second among the weapons.
        slots.Clear();
        slots.Add(-1);
        bool gunsAllowed = !switchManager || switchManager.IsPlayer; // before the water gun is handed over, no guns
        var inv = pc.Inventory;
        if (gunsAllowed && inv != null && inv.HoldableItensRightHand != null)
            foreach (var item in inv.HoldableItensRightHand)
                if (item && item.Unlocked) slots.Add(item.ItemSwitchID);
        if (Count > 0 || equipped) slots.Insert(Mathf.Min(2, slots.Count), BalloonSlot);

        int cur = equipped ? slots.IndexOf(BalloonSlot) : slots.IndexOf(CurrentGunId());
        if (cur < 0) cur = 0;
        int next = slots[(cur + dir + slots.Count) % slots.Count];

        if (next == BalloonSlot) Equip();
        else
        {
            if (equipped) Unequip(false);
            pc.SwitchToItem(next);
        }
    }

    int CurrentGunId()
    {
        if (!pc.IsItemEquiped) return -1;
        if (pc.HoldableItemInUseRightHand) return pc.HoldableItemInUseRightHand.ItemSwitchID;
        return pc.Inventory != null ? pc.Inventory.CurrentRightHandItemID : -1;
    }

    void Equip()
    {
        if (equipped || Count <= 0) return;
        itemBeforeBalloons = CurrentGunId();
        pc.SwitchToItem(-1);
        equipped = true;
        savedPunch = pc.EnablePunchAttacks;
        pc.EnablePunchAttacks = false;   // RT throws instead of punching while balloons are out
        EquippedChanged?.Invoke(true);
    }

    void Unequip(bool switchBack)
    {
        if (!equipped) return;
        equipped = false;
        if (aiming) { aiming = false; throwLock.Release(arcHolder); }
        pc.EnablePunchAttacks = savedPunch;
        SetHeldVisible(false);
        if (arc) arc.Hide();
        if (switchBack && !pc.IsDriving && !pc.IsDead)
        {
            bool ok = itemBeforeBalloons < 0;
            if (!ok && pc.Inventory != null)
                foreach (var item in pc.Inventory.HoldableItensRightHand)
                    if (item && item.Unlocked && item.ItemSwitchID == itemBeforeBalloons) ok = true;
            pc.SwitchToItem(ok ? itemBeforeBalloons : -1);
        }
        EquippedChanged?.Invoke(false);
    }

    // ------------------------------------------------------------------ input
    /// <summary>Scripted aim (tutorials, automated tests): holds LT while true.</summary>
    [NonSerialized] public bool AimOverride;
    bool throwRequested;
    float scriptedCookUntil = -1f;

    /// <summary>Scripted throw (tutorials, automated tests): presses RT and keeps it held for cookSeconds.</summary>
    public void RequestThrow(float cookSeconds = 0f)
    {
        throwRequested = true;
        scriptedCookUntil = Time.time + Mathf.Max(0f, cookSeconds);
    }

    bool AimHeld()
    {
        if (AimOverride) return true;
        if (JUInput.Instance() != null) return JUInput.GetButton(JUInput.Buttons.AimingButton);
        return (Gamepad.current != null && Gamepad.current.leftTrigger.isPressed) || (Mouse.current != null && Mouse.current.rightButton.isPressed);
    }

    bool ThrowPressed()
    {
        if (throwRequested) { throwRequested = false; return true; }
        if (JUInput.Instance() != null) return JUInput.GetButtonDown(JUInput.Buttons.ShotButton);
        return (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
    }

    bool ThrowHeld()
    {
        if (Time.time < scriptedCookUntil) return true;
        if (JUInput.Instance() != null) return JUInput.GetButton(JUInput.Buttons.ShotButton);
        return (Gamepad.current != null && Gamepad.current.rightTrigger.isPressed) || (Mouse.current != null && Mouse.current.leftButton.isPressed);
    }

    bool CanThrow()
    {
        if (!pc || pc.IsDriving || !pc.CanMove || (health && health.IsDead) || pc.IsRagdolled) return false;
        if (Cursor.visible || PimlrInput.GameplayButtonsBlocked) return false;   // a menu / dialogue is up
        if (PimlrInterstitial.Playing || PimlrSettingsMenu.IsOpen || JUPauseGame.Paused) return false;
        if (Time.timeScale == 0f) return false;
        return true;
    }

    // Hold time -> throw distance.
    float CookDistance(float heldSeconds)
    {
        float k = Mathf.Clamp01(heldSeconds / Mathf.Max(0.05f, S.fullCookTime));
        float shaped = S.cookResponse != null && S.cookResponse.length > 0 ? Mathf.Clamp01(S.cookResponse.Evaluate(k)) : k;
        cookFraction = k;
        return Mathf.Lerp(S.minRange, S.maxRange, shaped);
    }

    // ------------------------------------------------------------------ aiming helpers
    static Vector3 AimDirection(Transform fallback)
    {
        var cam = Camera.main;
        Vector3 f = cam ? cam.transform.forward : fallback.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    // Turn on the spot to face where the camera looks (keeps JUTPS's movement-direction helper where it was).
    void FaceCamera()
    {
        Quaternion want = Quaternion.LookRotation(AimDirection(transform), Vector3.up);
        Transform dirT = pc.DirectionTransform;
        Quaternion keep = dirT ? dirT.rotation : Quaternion.identity;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 720f * Time.deltaTime);
        if (dirT) dirT.rotation = keep;
    }

    // Landing point: distance along the aim direction from the player's feet, dropped onto whatever is below.
    Vector3 TargetPoint(Vector3 dir, float distance)
    {
        Vector3 flat = transform.position + dir * distance;
        var hits = Physics.RaycastAll(flat + Vector3.up * 8f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; Vector3 point = flat;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best) { best = h.distance; point = h.point; }
        }
        return point;
    }

    void DrawArc(float distance)
    {
        Vector3 dir = AimDirection(transform);
        var variant = S.cookedThrowVariant;
        Vector3 p0 = transform.position + Quaternion.LookRotation(dir) * Vector3.Scale(PimlrThrowAnimation.ReleaseLocal(variant), transform.lossyScale);
        Vector3 target = TargetPoint(dir, distance);
        float t = PimlrThrowAnimation.FlightTime(p0, target, S.arcHorizontalSpeed, S.minFlightTime, S.maxFlightTime);
        arc.Show(p0, PimlrThrowAnimation.SolveVelocity(p0, target, t, Physics.gravity), Physics.gravity, t, S);
    }

    // ------------------------------------------------------------------ throw (cook to throw)
    bool Interrupted() => pc.IsDead || pc.IsRagdolled || pc.IsDriving || (health && health.IsDead);

    void FreezeAnimator()
    {
        if (animFrozen || !animator) return;
        animSpeedBefore = animator.speed > 0f ? animator.speed : 1f;
        animator.speed = 0f;
        animFrozen = true;
    }

    void UnfreezeAnimator()
    {
        if (!animFrozen) return;
        if (animator) animator.speed = animSpeedBefore;
        animFrozen = false;
    }

    IEnumerator Throw()
    {
        throwing = true;
        cooking = true;
        throwLock.Hold(this);
        var variant = S.cookedThrowVariant;
        float cookStart = Time.time;
        aimDistance = CookDistance(0f);

        // The balloon already in the palm becomes the thrown one (it stays parented to the hand while cooking).
        GameObject balloon = TakeHeld();
        bool animated = PimlrThrowAnimation.CanPlay(animator);
        if (animated) PimlrThrowAnimation.Play(animator, variant);

        float releaseAt = PimlrThrowAnimation.ReleaseNormalized(variant);
        float holdAt = releaseAt * S.windupHoldShare;
        bool released = false;          // the player let go of the button
        float elapsed = 0f, nextRumble = Time.time + S.cookRumbleInterval;
        bool sawState = false;

        // Phase 1: wind up, hold the pose while the button is held, then play on to the release frame.
        while (true)
        {
            if (Interrupted()) break;

            if (!released)
            {
                if (ThrowHeld())
                {
                    aimDistance = CookDistance(Time.time - cookStart);
                    if (S.cookRumbleInterval > 0f && Time.time >= nextRumble && cookFraction < 1f)
                    {
                        nextRumble = Time.time + S.cookRumbleInterval;
                        PimlrRumble.Pulse(0.03f + 0.07f * cookFraction, 0.05f + 0.15f * cookFraction, 0.05f);
                    }
                }
                else
                {
                    released = true;
                    aimDistance = CookDistance(Time.time - cookStart); // locked in at the moment of release
                    UnfreezeAnimator();
                }
            }

            if (animated)
            {
                if (PimlrThrowAnimation.TryGetProgress(animator, out float p))
                {
                    sawState = true;
                    if (!released && p >= holdAt) FreezeAnimator();       // top of the wind-up: hold here
                    if (released && p >= releaseAt) break;                 // the arm is coming through: let go
                }
                else if (released && (sawState || elapsed >= S.fallbackReleaseDelay)) break; // never reached / already left the throw state
            }
            else if (released) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        UnfreezeAnimator();
        cooking = false;
        Vector3 dir = AimDirection(transform);

        if (Interrupted())
        {
            if (balloon) Destroy(balloon);       // interrupted: the balloon is lost with the throw
        }
        else
        {
            Count--;
            CountChanged?.Invoke(Count);
            Release(balloon, TargetPoint(dir, aimDistance));
            PimlrRumble.Pulse(0.15f, 0.35f, 0.1f);
        }

        // Phase 2: stay planted through the follow-through, until the Animator blends out of the throw.
        elapsed = 0f;
        yield return null;
        while (animated && elapsed < 3f && PimlrThrowAnimation.TryGetProgress(animator, out _))
        {
            if (pc.IsDead || pc.IsDriving) break;
            elapsed += Time.deltaTime;
            yield return null;
        }
        PimlrThrowAnimation.ClearTrigger(animator);
        throwLock.Release(this);
        nextThrow = Time.time + S.cooldown;
        aimDistance = S.minRange;
        throwing = false;
    }

    void Release(GameObject balloon, Vector3 target)
    {
        Vector3 origin = grip ? grip.position : transform.position + Vector3.up * 1.5f + transform.forward * 0.4f;
        if (!balloon) balloon = Instantiate(S.balloonPrefab, origin, Quaternion.LookRotation(transform.forward));
        balloon.transform.SetParent(null, true);
        balloon.transform.position = origin;
        foreach (var c in balloon.GetComponentsInChildren<Collider>(true)) c.enabled = true;
        foreach (var g in balloon.GetComponentsInChildren<Grenade>(true)) { g.PlayerOwned = true; g.enabled = true; }
        foreach (var a in balloon.GetComponentsInChildren<Collider>())
            foreach (var b in ownColliders) if (a && b) Physics.IgnoreCollision(a, b, true);

        if (!balloon.TryGetComponent(out Rigidbody rb)) rb = balloon.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = 0f;

        // Ballistic arc that lands on the target (the same solve the arrow previewed).
        float t = PimlrThrowAnimation.FlightTime(origin, target, S.arcHorizontalSpeed, S.minFlightTime, S.maxFlightTime);
        Vector3 g2 = rb.useGravity ? Physics.gravity : Vector3.zero;
        rb.linearVelocity = PimlrThrowAnimation.SolveVelocity(origin, target, t, g2);
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 4f;
    }

    // ------------------------------------------------------------------ the balloon in the palm
    void SetHeldVisible(bool on)
    {
        if (on && !held && S && S.balloonPrefab && grip) held = SpawnHeld();
        if (held && held.activeSelf != on) held.SetActive(on);
    }

    // The real balloon prefab, parked in the palm with physics, colliders and fuse off.
    GameObject SpawnHeld()
    {
        var b = Instantiate(S.balloonPrefab, grip.position, grip.rotation, grip);
        b.name = "PIMLR Held Balloon";
        if (b.TryGetComponent(out Rigidbody rb)) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
        foreach (var c in b.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var g in b.GetComponentsInChildren<Grenade>(true)) { g.PlayerOwned = true; g.enabled = false; }
        return b;
    }

    GameObject TakeHeld()
    {
        if (!held && S && S.balloonPrefab && grip) held = SpawnHeld();
        var b = held;
        held = null;
        if (b) b.SetActive(true);
        return b;
    }
}
