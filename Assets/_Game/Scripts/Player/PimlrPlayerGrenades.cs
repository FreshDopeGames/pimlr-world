// PIMLR #GREN: the player's water balloons ("grenades"). Added to the player automatically in gameplay scenes
// (any scene with a GameExecutionManager), so no prefab or scene edits are needed.
//   - G (keyboard) / D-pad Down (controller) throws one at whatever the crosshair points at (arc solved to land
//     there, like the bombers' throws), from the right hand, after the 'Throw' animation's wind-up.
//   - Balloons come from pickups (PimlrGrenadePickup, prefab _Game/Prefabs/Pickups/WaterBalloonPickup) and are
//     capped at maxCount. A small HUD counter shows how many you carry.
//   - The burst damages enemies only (Water_Explosion_Player -> WaterSplashDamage, Target = Enemies).
// Tuning: Assets/_Game/Resources/PIMLR/PlayerGrenades.asset.
//
// Unreal note: an ActorComponent on the player Character with an Enhanced Input "ThrowGrenade" action; the balloon
// is an Actor with a ProjectileMovementComponent (velocity from UGameplayStatics::SuggestProjectileVelocity) and a
// radial damage on hit (ApplyRadialDamageWithFalloff, damage type filtered to enemies).

using System;
using System.Collections;
using JUTPS;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PimlrPlayerGrenades : MonoBehaviour
{
    public static PimlrPlayerGrenades Instance { get; private set; }
    public static event Action<int> CountChanged;

    public int Count { get; private set; }
    public int MaxCount => S ? S.maxCount : 5;

    static PimlrPlayerGrenadeSettings settings;
    static PimlrPlayerGrenadeSettings S
    {
        get
        {
            if (!settings) settings = Resources.Load<PimlrPlayerGrenadeSettings>("PIMLR/PlayerGrenades");
            return settings;
        }
    }

    JUCharacterController pc;
    JUHealth health;
    Animator animator;
    Collider[] ownColliders;
    float nextThrow;
    bool throwing;
    TMP_Text hudLabel;
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
        ownColliders = GetComponentsInChildren<Collider>(true);
        Count = S ? Mathf.Min(S.startCount, S.maxCount) : 0;
        if (!S) Debug.LogWarning("[PIMLR Grenades] Resources/PIMLR/PlayerGrenades.asset is missing - throwing is disabled.");
        BuildHud();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (hudGroup) Destroy(hudGroup.gameObject);
    }

    // ------------------------------------------------------------------ public API
    /// <summary>Adds balloons (capped at MaxCount). Returns how many were actually taken.</summary>
    public int Add(int amount)
    {
        int before = Count;
        Count = Mathf.Clamp(Count + amount, 0, MaxCount);
        if (Count != before) { hudFlash = 2.5f; CountChanged?.Invoke(Count); }
        return Count - before;
    }

    public bool IsFull => Count >= MaxCount;

    // ------------------------------------------------------------------ input
    void Update()
    {
        UpdateHud();
        if (!S || !S.balloonPrefab || throwing || Count <= 0 || Time.time < nextThrow) return;
        if (!Pressed()) return;
        if (!CanThrow()) return;
        StartCoroutine(Throw());
    }

    bool Pressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[S.keyboardKey].wasPressedThisFrame) return true;
        var pad = Gamepad.current;
        return pad != null && pad[S.gamepadButton].wasPressedThisFrame;
    }

    bool CanThrow()
    {
        if (!pc || pc.IsDriving || !pc.CanMove || (health && health.IsDead)) return false;
        if (Cursor.visible || PimlrInput.GameplayButtonsBlocked) return false;   // a menu / dialogue is up
        if (PimlrInterstitial.Playing || PimlrSettingsMenu.IsOpen) return false;
        if (Time.timeScale == 0f) return false;
        return true;
    }

    IEnumerator Throw()
    {
        throwing = true;
        nextThrow = Time.time + S.cooldown;
        Count--;
        hudFlash = 2.5f;
        CountChanged?.Invoke(Count);

        if (S.useThrowAnimation && animator && HasTrigger(animator, "Throw")) animator.SetTrigger("Throw");
        if (S.releaseDelay > 0f) yield return new WaitForSeconds(S.releaseDelay);

        Release();
        PimlrRumble.Pulse(0.15f, 0.35f, 0.1f);
        throwing = false;
    }

    void Release()
    {
        Vector3 origin = HandPosition();
        Vector3 target = AimPoint(origin);

        var balloon = Instantiate(S.balloonPrefab, origin, Quaternion.LookRotation(Flat(target - origin)));
        foreach (var g in balloon.GetComponentsInChildren<Grenade>()) g.PlayerOwned = true;
        foreach (var a in balloon.GetComponentsInChildren<Collider>())
            foreach (var b in ownColliders) if (a && b) Physics.IgnoreCollision(a, b, true);

        if (!balloon.TryGetComponent(out Rigidbody rb)) rb = balloon.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Ballistic arc that lands on the aim point.
        Vector3 delta = target - origin;
        float flat = Flat(delta).magnitude;
        float t = Mathf.Clamp(flat / S.arcHorizontalSpeed, S.minFlightTime, S.maxFlightTime);
        Vector3 v = delta / t - 0.5f * Physics.gravity * t;
        rb.linearVelocity = v;
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 4f;
    }

    Vector3 HandPosition()
    {
        Transform hand = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        Vector3 p = hand ? hand.position : transform.position + Vector3.up * 1.5f;
        return p + transform.forward * 0.25f + Vector3.up * 0.1f;
    }

    // Where the crosshair points (screen centre), up to maxRange. Ignores the player's own colliders.
    Vector3 AimPoint(Vector3 origin)
    {
        var cam = Camera.main;
        if (!cam) return origin + transform.forward * S.maxRange * 0.6f;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float camToPlayer = Vector3.Dot(transform.position - ray.origin, ray.direction); // skip what's behind the player
        var hits = Physics.RaycastAll(ray, S.maxRange + Mathf.Max(0f, camToPlayer), ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; Vector3 point = ray.GetPoint(S.maxRange + Mathf.Max(0f, camToPlayer));
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < camToPlayer) continue;
            if (h.distance < best) { best = h.distance; point = h.point; }
        }
        // Keep it within range of the player, and on the ground if the ray went into the sky.
        Vector3 fromPlayer = point - origin;
        if (fromPlayer.magnitude > S.maxRange) point = origin + fromPlayer.normalized * S.maxRange;
        if (best == float.MaxValue && Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out var ground, 30f, ~0, QueryTriggerInteraction.Ignore))
            point = ground.point;
        return point;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v : Vector3.forward; }

    static bool HasTrigger(Animator a, string name)
    {
        foreach (var p in a.parameters) if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
        return false;
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

        var bg = new GameObject("BG", typeof(RectTransform)).AddComponent<Image>();
        bg.transform.SetParent(go.transform, false);
        bg.color = new Color(0f, 0.12f, 0.3f, 0.75f);
        bg.raycastTarget = false;
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(430f, 138f); // right of the water-drop ammo icons
        rt.sizeDelta = new Vector2(250f, 46f);

        hudLabel = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        hudLabel.transform.SetParent(bg.transform, false);
        var lrt = hudLabel.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        hudLabel.fontSize = 24; hudLabel.alignment = TextAlignmentOptions.Center; hudLabel.color = Color.white;
        hudLabel.raycastTarget = false;
        UpdateHud();
    }

    void UpdateHud()
    {
        if (!hudLabel) return;
        string key = PimlrInput.UsingGamepad ? "D-PAD ▼" : (S ? S.keyboardKey.ToString().ToUpper() : "G");
        hudLabel.text = $"BALLOONS  {Count}/{MaxCount}   <size=70%><color=#FFD21A>[{key}]</color></size>";
        hudFlash -= Time.unscaledDeltaTime;
        // Shown while you carry any (and briefly after the last one goes), hidden behind menus and cutscenes.
        bool show = (Count > 0 || hudFlash > 0f) && !Cursor.visible && !PimlrInterstitial.Playing && pc && pc.CanMove;
        hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
    }
}
