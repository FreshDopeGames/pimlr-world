// PIMLR #GREN: the player's water balloons ("grenades"), the player's SECONDARY WEAPON. Added to the player
// automatically in gameplay scenes (any scene with a GameExecutionManager), so no prefab or scene edits are needed.
//   - Balloons only come from pickups (PimlrGrenadePickup, prefab _Game/Prefabs/Pickups/WaterBalloonPickup), capped at
//     maxCount. Picking them up never auto-equips; there is no shop and no story auto-equip.
//   - Once you carry any, D-pad Left / Right (Q / E) cycles  [empty hands] -> water gun -> BALLOONS -> other guns...
//     (the balloon slot sits right after the first gun, i.e. the secondary slot). This script takes over JUTPS's
//     next/previous switching while you carry balloons, and hands it back when you run out.
//   - Balloons equipped: one sits in the right palm. Hold LT (right mouse) to aim: you stop moving, turn with the
//     camera, and the left stick (W / S) sets the throw distance, shown by the blue trajectory arrow (PimlrThrowArc).
//     RT (left mouse) throws, aimed or not, at that distance in the camera's direction.
//   - The throw plays Throw Object (long throws) or Goalie Throw (short / medium), the same clips the bombers use
//     (PimlrThrowAnimation); the balloon leaves the palm on the clip's measured release frame, and PimlrThrowLock keeps
//     the player planted until the clip ends.
//   - The burst damages enemies only (Water_Explosion_Player -> WaterSplashDamage, Target = Enemies).
// Tuning: Assets/_Game/Resources/PIMLR/PlayerGrenades.asset.
//
// Unreal note: an ActorComponent on the player Character registered as an inventory slot; Enhanced Input actions for
// NextWeapon / PreviousWeapon / Aim / Fire / AimDistance (1D axis). The arc preview is PredictProjectilePath; the
// throw is an Anim Montage with a Release notify that spawns the balloon Actor (ProjectileMovementComponent velocity
// from SuggestProjectileVelocity_CustomArc).

using System;
using System.Collections;
using System.Collections.Generic;
using JUTPS;
using JUTPS.ItemSystem;
using JUTPS.JUInputSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PimlrPlayerGrenades : MonoBehaviour
{
    public static PimlrPlayerGrenades Instance { get; private set; }
    public static event Action<int> CountChanged;
    /// <summary>Raised when the balloons are equipped (true) or put away (false).</summary>
    public static event Action<bool> EquippedChanged;

    public int Count { get; private set; }
    public int MaxCount => S ? S.maxCount : 5;
    public bool IsEquipped => equipped;
    public bool IsAiming => aiming;
    public bool IsFull => Count >= MaxCount;
    /// <summary>Current throw distance (metres), set with the left stick while aiming.</summary>
    public float AimDistance => aimDistance;

    static PimlrPlayerGrenadeSettings settings;
    static PimlrPlayerGrenadeSettings S
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

    bool equipped, aiming, throwing;
    float nextThrow, aimDistance;
    int itemBeforeBalloons = -1;
    bool tookOverSwitching, savedSwitching, savedPunch;
    readonly List<int> slots = new List<int>(8);

    TMP_Text hudLabel;
    Image hudBg;
    CanvasGroup hudGroup;
    float hudFlash;

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
        aimDistance = S ? Mathf.Clamp(S.startRange, S.minRange, S.maxRange) : 15f;
        if (!S) Debug.LogWarning("[PIMLR Grenades] Resources/PIMLR/PlayerGrenades.asset is missing - balloons are disabled.");
        else arc = PimlrThrowArc.Create(S.arcFillMaterial, S.arcOutlineMaterial);
        BuildHud();
    }

    void OnDisable()
    {
        if (throwLock) throwLock.Release(this);
        if (throwLock) throwLock.Release(arcHolder);
        throwing = false; aiming = false;
        if (arc) arc.Hide();
        SetHeldVisible(false);
        GiveBackSwitching();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (hudGroup) Destroy(hudGroup.gameObject);
        if (arc) Destroy(arc.gameObject);
        if (held) Destroy(held);
    }

    // ------------------------------------------------------------------ public API
    /// <summary>Adds balloons (capped at MaxCount). Returns how many were actually taken. Never equips them.</summary>
    public int Add(int amount)
    {
        int before = Count;
        Count = Mathf.Clamp(Count + amount, 0, MaxCount);
        if (Count != before) { hudFlash = 3f; CountChanged?.Invoke(Count); }
        return Count - before;
    }

    // ------------------------------------------------------------------ frame
    readonly object arcHolder = new object(); // lock key for "aiming" (separate from the throw itself)

    void Update()
    {
        UpdateHud();
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
        if (aiming) AdjustDistance();

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
            if (aiming && !throwing) DrawArc();
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
        hudFlash = 3f;
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
    /// <summary>Scripted aim / throw (tutorials, automated tests): AimOverride holds the aim; RequestThrow() presses RT once.</summary>
    [NonSerialized] public bool AimOverride;
    bool throwRequested;
    public void RequestThrow() => throwRequested = true;

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

    void AdjustDistance()
    {
        float y = 0f;
        var pad = Gamepad.current;
        if (pad != null) { float v = pad.leftStick.ReadValue().y; if (Mathf.Abs(v) > 0.2f) y += (Mathf.Abs(v) - 0.2f) / 0.8f * Mathf.Sign(v); }
        var kb = Keyboard.current;
        if (kb != null) { if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f; if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f; }
        y = Mathf.Clamp(y, -1f, 1f);
        aimDistance = Mathf.Clamp(aimDistance + y * S.rangeAdjustSpeed * Time.deltaTime, S.minRange, S.maxRange);
    }

    bool CanThrow()
    {
        if (!pc || pc.IsDriving || !pc.CanMove || (health && health.IsDead) || pc.IsRagdolled) return false;
        if (Cursor.visible || PimlrInput.GameplayButtonsBlocked) return false;   // a menu / dialogue is up
        if (PimlrInterstitial.Playing || PimlrSettingsMenu.IsOpen || JUPauseGame.Paused) return false;
        if (Time.timeScale == 0f) return false;
        return true;
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

    // Landing point: aimDistance along the aim direction from the player's feet, dropped onto whatever is below.
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

    void DrawArc()
    {
        Vector3 dir = AimDirection(transform);
        var variant = PimlrThrowAnimation.Pick(S.throwVariant == PimlrThrowVariantMode.Random ? PimlrThrowVariantMode.ByDistance : S.throwVariant, aimDistance, S.maxRange);
        Vector3 p0 = transform.position + Quaternion.LookRotation(dir) * Vector3.Scale(PimlrThrowAnimation.ReleaseLocal(variant), transform.lossyScale);
        Vector3 target = TargetPoint(dir, aimDistance);
        float t = PimlrThrowAnimation.FlightTime(p0, target, S.arcHorizontalSpeed, S.minFlightTime, S.maxFlightTime);
        arc.Show(p0, PimlrThrowAnimation.SolveVelocity(p0, target, t, Physics.gravity), Physics.gravity, t, S);
    }

    // ------------------------------------------------------------------ throw
    IEnumerator Throw()
    {
        throwing = true;
        throwLock.Hold(this);
        Vector3 dir = AimDirection(transform);
        float distance = aimDistance;
        var variant = PimlrThrowAnimation.Pick(S.throwVariant, distance, S.maxRange);
        if (arc) arc.Hide();

        // The balloon already in the palm becomes the thrown one.
        GameObject balloon = TakeHeld();
        bool animated = PimlrThrowAnimation.CanPlay(animator);
        if (animated) PimlrThrowAnimation.Play(animator, variant);

        float elapsed = 0f; bool sawState = false; float releaseAt = PimlrThrowAnimation.ReleaseNormalized(variant);
        while (animated && elapsed < 3f)
        {
            if (pc.IsDead || pc.IsRagdolled || pc.IsDriving) break;
            if (PimlrThrowAnimation.TryGetProgress(animator, out float p)) { sawState = true; if (p >= releaseAt) break; }
            else if (sawState || elapsed >= S.fallbackReleaseDelay) break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (pc.IsDead || pc.IsDriving || pc.IsRagdolled)
        {
            if (balloon) Destroy(balloon);       // interrupted: the balloon is lost with the throw
        }
        else
        {
            Count--;
            hudFlash = 3f;
            CountChanged?.Invoke(Count);
            Release(balloon, TargetPoint(dir, distance));
            PimlrRumble.Pulse(0.15f, 0.35f, 0.1f);
        }

        // Stay planted through the follow-through, until the Animator blends out of the throw.
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

    // ------------------------------------------------------------------ HUD (placeholder: reskin later)
    void BuildHud()
    {
        var go = new GameObject("PIMLR Balloon Counter", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        hudGroup = go.AddComponent<CanvasGroup>();
        hudGroup.interactable = false; hudGroup.blocksRaycasts = false;

        hudBg = new GameObject("BG", typeof(RectTransform)).AddComponent<Image>();
        hudBg.transform.SetParent(go.transform, false);
        hudBg.color = new Color(0f, 0.12f, 0.3f, 0.75f);
        hudBg.raycastTarget = false;
        var rt = hudBg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(470f, 138f); // right of the water-drop ammo icons
        rt.sizeDelta = new Vector2(330f, 46f);

        hudLabel = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        hudLabel.transform.SetParent(hudBg.transform, false);
        var lrt = hudLabel.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        hudLabel.fontSize = 22; hudLabel.alignment = TextAlignmentOptions.Center; hudLabel.color = Color.white;
        hudLabel.raycastTarget = false;
        UpdateHud();
    }

    void UpdateHud()
    {
        if (!hudLabel) return;
        bool pad = PimlrInput.UsingGamepad;
        string hint;
        if (!equipped) hint = pad ? "D-PAD L / R" : "Q / E";
        else if (aiming) hint = $"{Mathf.RoundToInt(aimDistance)} m  " + (pad ? "RT THROW" : "LMB THROW");
        else hint = pad ? "LT AIM · RT THROW" : "RMB AIM · LMB THROW";
        hudLabel.text = $"BALLOONS  {Count}/{MaxCount}   <size=70%><color=#FFD21A>[{hint}]</color></size>";
        if (hudBg) hudBg.color = equipped ? new Color(0.05f, 0.35f, 0.8f, 0.85f) : new Color(0f, 0.12f, 0.3f, 0.75f);
        hudFlash -= Time.unscaledDeltaTime;
        // Shown while you carry any (and briefly after the last one goes), hidden behind menus and cutscenes.
        bool show = (Count > 0 || equipped || hudFlash > 0f) && !Cursor.visible && !PimlrInterstitial.Playing && pc && pc.CanMove;
        hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
    }
}
