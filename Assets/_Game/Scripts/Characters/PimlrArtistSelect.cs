// PIMLR (Nov 18): SELECT YOUR ARTIST - a turntable carousel in the Living Room (SceneStaticEU), shown on arrival from the
// splash screen (New Game and Keep Playing), before the intro dialogue / the Living Room menu. Not shown when coming back
// from YannicksWorld. The pick is saved (PimlrArtists.Select) and the intro dialogue uses that artist's version if one
// is set on the roster (Resources/PIMLR/ArtistRoster.asset).
//
//   - Artists stand on a ring that turns to bring the current one to the front, under a soft spotlight, with the room
//     dimmed. The ring always has 6 stands and loops forever: with one artist every stand shows that artist; with more,
//     the hidden back stand is refilled as it turns, so browsing never hits an end.
//   - Browse: arrows on screen, A/D or Left/Right keys, mouse wheel; D-pad Left/Right or LB/RB on a controller.
//   - Spin the front artist: click-drag on the model, or the right stick.
//   - Select: SELECT button, Enter / Space, or A / Cross.
// The real player is hidden while the carousel is up and the camera is borrowed through PimlrHubCamera; lights, ambient
// light, camera and player are put back exactly as they were.
//
// Unreal note: a "CharacterSelect" level-streaming sublevel (or a set-dressed corner) with a rotating USceneComponent ring
// of USkeletalMeshComponents (one per stand, mesh swapped as the ring turns), a CineCameraActor via SetViewTargetWithBlend,
// a RectLight/SpotLight rig and a light-function or post-process to dim the room; a CommonUI activatable widget for the
// name/arrows/select, with Enhanced Input actions for Browse (1D axis), Spin (right stick X / mouse drag) and Confirm.

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PimlrArtistSelect : MonoBehaviour
{
    // ---------------- staging (SceneStaticEU, Living_Room_2: the long open floor between the window wall and the door)
    // Sizes below are for a 1-unit-scale character and are multiplied by the player's scale (the Living Room and its
    // player are built at 2x).
    public static Vector3 RingCenter = new Vector3(0.6f, 0f, -9.0f);
    public static float RingRadius = 1.1f;
    /// <summary>Direction from the ring centre to the front stand (and on toward the camera): toward the window wall.</summary>
    public static Vector3 FrontDirection = Vector3.forward;
    public static float CameraDistance = 2.8f;   // from the front stand
    public static float CameraHeight = 1.3f;
    public static float LookHeight = 0.75f;     // below the waist, so the artist stands above the name / SELECT
    public static float CameraFov = 64f;
    float S = 1f; // player scale
    public const int Stands = 6;
    /// <summary>Stands closer to the front than this (in stands) are shown: 1.5 = front + one each side (3 max).</summary>
    public static float VisibleHalfSpan = 1.5f;

    // ---------------- feel
    public static float TurnSeconds = 0.45f;
    public static float DragDegreesPerPixel = 0.45f;
    public static float StickDegreesPerSecond = 220f;
    public static float RoomLightScale = 0.06f;
    public static float AmbientScale = 0.12f;
    public static Color Green = new Color(0.095f, 0.745f, 0f, 1f);       // menu green
    public static Color Yellow = new Color(1f, 0.82f, 0.1f, 1f);         // PIMLR yellow

    /// <summary>True while the carousel is on screen.</summary>
    public static bool IsOpen { get; private set; }

    static PimlrArtistSelect instance;
    Action onDone;

    // ring
    Transform ring;
    readonly GameObject[] standModels = new GameObject[Stands];
    readonly string[] standArtist = new string[Stands];
    readonly float[] standSpin = new float[Stands];
    float current, target;           // logical index (unbounded), animated
    int selectedLogical;
    float turnVel;
    GameObject templateSource;

    // room
    struct LightState { public Light light; public float intensity; public bool enabled; }
    readonly List<LightState> lights = new List<LightState>();
    float ambientBefore, reflectionBefore;
    readonly List<GameObject> addedLights = new List<GameObject>();
    readonly List<Renderer> hiddenPlayerRenderers = new List<Renderer>();

    // ui
    GameObject canvasGo;
    TextMeshProUGUI nameText, taglineText, countText, hintText;
    Button selectButton;
    bool dragging;
    float lastPointerX;
    float stickRepeat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; IsOpen = false; }

    /// <summary>Show the carousel; onDone runs after the player confirms an artist and the room is back to normal.</summary>
    public static void Open(Action onConfirmed)
    {
        if (IsOpen) return;
        var go = new GameObject("PIMLR Artist Select");
        instance = go.AddComponent<PimlrArtistSelect>();
        instance.onDone = onConfirmed;
        instance.Begin();
    }

    // ------------------------------------------------------------------ setup / teardown
    void Begin()
    {
        IsOpen = true;
        var player = GameObject.FindGameObjectWithTag("Player");
        templateSource = player;
        S = player ? Mathf.Max(0.1f, player.transform.lossyScale.y) : 1f;

        // Hide the real player (the carousel shows copies).
        if (player)
            foreach (var r in player.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; hiddenPlayerRenderers.Add(r); }

        DimRoom();
        BuildRing();
        BuildUi();

        selectedLogical = Mathf.Max(0, PimlrArtists.IndexOf(PimlrArtists.SelectedId));
        current = target = selectedLogical;
        RefreshStands(force: true);
        PlaceRing();
        RefreshLabels();

        if (PimlrHubCamera.Take())
        {
            Vector3 front = RingCenter + FrontDirection.normalized * RingRadius * S;
            Vector3 camPos = front + FrontDirection.normalized * CameraDistance * S + Vector3.up * CameraHeight * S;
            Vector3 look = front + Vector3.up * LookHeight * S;
            PimlrHubCamera.Set(camPos, Quaternion.LookRotation(look - camPos), CameraFov);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void Finish()
    {
        if (!IsOpen) return;
        IsOpen = false;

        PimlrHubCamera.Release();
        RestoreRoom();
        foreach (var r in hiddenPlayerRenderers) if (r) r.enabled = true;
        hiddenPlayerRenderers.Clear();
        // After the player is visible again: the pick may swap the player's look (PimlrArtistAvatar hides Thurz's body).
        PimlrArtists.Select(ArtistAt(selectedLogical).id);
        if (canvasGo) Destroy(canvasGo);
        Destroy(gameObject);

        PimlrInput.BlockGameplayInputFor(0.4f, false); // the confirming press shouldn't also act in the menu/dialogue
        var cb = onDone; onDone = null;
        cb?.Invoke();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (IsOpen)
        {
            // Scene unloading mid-selection: put things back, no callback.
            IsOpen = false;
            PimlrHubCamera.Release();
            RestoreRoom();
            foreach (var r in hiddenPlayerRenderers) if (r) r.enabled = true;
            if (canvasGo) Destroy(canvasGo);
        }
    }

    // ------------------------------------------------------------------ lighting
    void DimRoom()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            lights.Add(new LightState { light = l, intensity = l.intensity, enabled = l.enabled });
            l.intensity *= RoomLightScale;
        }
        ambientBefore = RenderSettings.ambientIntensity;
        reflectionBefore = RenderSettings.reflectionIntensity;
        RenderSettings.ambientIntensity = ambientBefore * AmbientScale;
        RenderSettings.reflectionIntensity = reflectionBefore * 0.35f;

        Vector3 dir = FrontDirection.normalized;
        Vector3 front = RingCenter + dir * RingRadius * S;

        // Key: a wide, soft spotlight from above and slightly in front of the artist.
        var key = new GameObject("PIMLR Select Spot").AddComponent<Light>();
        key.type = LightType.Spot;
        key.transform.position = front + (dir * 1.6f + Vector3.up * 3.6f) * S;
        key.transform.rotation = Quaternion.LookRotation((front + Vector3.up * 0.9f * S) - key.transform.position);
        key.spotAngle = 52f; key.innerSpotAngle = 18f;
        key.range = 9f * S; key.intensity = 14f;
        key.color = new Color(1f, 0.96f, 0.9f);
        key.shadows = LightShadows.Soft; key.shadowStrength = 0.55f;
        addedLights.Add(key.gameObject);

        // Fill: behind the camera, low and broad, no shadows - lifts the shadow side so nothing reads as a hard shadow.
        var fill = new GameObject("PIMLR Select Fill").AddComponent<Light>();
        fill.type = LightType.Point;
        fill.transform.position = front + (dir * (CameraDistance + 1.2f) + Vector3.up * 1.8f) * S;
        fill.range = 9f * S; fill.intensity = 2.2f;
        fill.color = new Color(0.85f, 0.9f, 1f);
        fill.shadows = LightShadows.None;
        addedLights.Add(fill.gameObject);

        // Rim: a faint cool light from behind the ring to separate the artist from the dark wall.
        var rim = new GameObject("PIMLR Select Rim").AddComponent<Light>();
        rim.type = LightType.Point;
        rim.transform.position = front + (-dir * 1.4f + Vector3.up * 2.4f) * S;
        rim.range = 4f * S; rim.intensity = 1.6f;
        rim.color = new Color(0.55f, 0.75f, 1f);
        rim.shadows = LightShadows.None;
        addedLights.Add(rim.gameObject);
    }

    void RestoreRoom()
    {
        foreach (var s in lights) if (s.light) { s.light.intensity = s.intensity; s.light.enabled = s.enabled; }
        lights.Clear();
        RenderSettings.ambientIntensity = ambientBefore;
        RenderSettings.reflectionIntensity = reflectionBefore;
        foreach (var g in addedLights) if (g) Destroy(g);
        addedLights.Clear();
    }

    // ------------------------------------------------------------------ ring
    void BuildRing()
    {
        ring = new GameObject("Ring").transform;
        ring.SetParent(transform, false);
        ring.position = RingCenter;
    }

    static PimlrArtistRoster.Artist ArtistAt(int logical)
    {
        var all = PimlrArtists.All;
        int n = all.Count;
        return all[((logical % n) + n) % n];
    }

    // Each physical stand p shows the logical index nearest the current one that is congruent to p (mod Stands).
    int LogicalFor(int p)
    {
        int c = Mathf.RoundToInt(current);
        int offset = ((p - c) % Stands + Stands) % Stands;   // 0..5
        if (offset > Stands / 2) offset -= Stands;           // -2..3
        return c + offset;
    }

    void RefreshStands(bool force)
    {
        for (int p = 0; p < Stands; p++)
        {
            var artist = ArtistAt(LogicalFor(p));
            if (!force && standArtist[p] == artist.id && standModels[p]) continue;
            if (standModels[p]) Destroy(standModels[p]);
            standModels[p] = MakeModel(artist);
            standArtist[p] = artist.id;
            standSpin[p] = 0f;
            if (standModels[p]) standModels[p].transform.SetParent(ring, false);
        }
    }

    void PlaceRing()
    {
        Vector3 dir = FrontDirection.normalized;
        float baseYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        for (int p = 0; p < Stands; p++)
        {
            var m = standModels[p];
            if (!m) continue;
            float rel = LogicalFor(p) - current;                  // 0 = front
            // PIMLR (Nov 18): at most 3 artists on screen - the front one and one each side.
            bool show = Mathf.Abs(rel) < VisibleHalfSpan;
            if (m.activeSelf != show) m.SetActive(show);
            float ang = baseYaw + rel * (360f / Stands);
            Vector3 offset = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * RingRadius * S;
            m.transform.position = RingCenter + offset;
            // Everyone faces outward (toward the camera when in front), plus the player's spin on the front one.
            m.transform.rotation = Quaternion.Euler(0f, ang + standSpin[p], 0f);
        }
    }

    GameObject MakeModel(PimlrArtistRoster.Artist artist)
    {
        GameObject src = artist.displayPrefab ? artist.displayPrefab : templateSource;
        if (!src) return null;

        // Copy under an inactive holder so none of the gameplay scripts wake up, then strip them.
        var holder = new GameObject("holder"); holder.SetActive(false);
        var copy = Instantiate(src, holder.transform);
        copy.name = "Artist - " + artist.displayName;
        // The scene player may be wearing another artist (PimlrArtistAvatar): the Thurz stand shows the base body.
        if (!artist.displayPrefab) { var worn = copy.transform.Find(PimlrArtistAvatar.VisualName); if (worn) DestroyImmediate(worn.gameObject); }
        StripToVisuals(copy); // prefabs too: no gameplay scripts on a display model
        foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.enabled = true; // the source's are hidden
        copy.transform.SetParent(null, false);
        // The scene player already has this scene's scale. A prefab is authored at 1 unit, so scale it into the scene
        // (the Living Room and its player are built at 2x), times the roster's per-artist Display Scale.
        copy.transform.localScale = artist.displayPrefab
            ? src.transform.localScale * S * (artist.displayScale > 0f ? artist.displayScale : 1f)
            : src.transform.lossyScale;
        Destroy(holder);
        copy.SetActive(true);

        var anim = copy.GetComponentInChildren<Animator>();
        if (anim)
        {
            // No controller = T-pose. Borrow the scene player's (its idle state works for any humanoid avatar).
            if (!anim.runtimeAnimatorController && templateSource)
            {
                var playerAnim = templateSource.GetComponent<Animator>();
                if (playerAnim) anim.runtimeAnimatorController = playerAnim.runtimeAnimatorController;
            }
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        return copy;
    }

    // True if another component on the same object still has [RequireComponent] for this one's type.
    static bool RequiredByOther(Component c)
    {
        var t = c.GetType();
        foreach (var other in c.GetComponents<Component>())
        {
            if (!other || other == c) continue;
            foreach (RequireComponent rc in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(t)) || (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(t)) ||
                    (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(t)))
                    return true;
        }
        return false;
    }

    // Keep only what draws and animates the model.
    internal static void StripToVisuals(GameObject go)
    {
        for (int pass = 0; pass < 8; pass++)
        {
            bool removed = false;
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (!c || c is Transform || c is Animator || c is Renderer || c is MeshFilter || c is LODGroup) continue;
                if (!(c is Collider || c is Rigidbody || c is Joint || c is AudioSource || c is MonoBehaviour || c is Behaviour)) continue;
                if (RequiredByOther(c)) continue; // remove the dependent first (next pass), so Unity doesn't log an error
                DestroyImmediate(c);
                removed = true;
            }
            if (!removed) break;
        }
        // JUTPS item models (the water gun etc.) hang off the hand: hide them, the carousel shows the artist.
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            if (t.name == "Item Wield Rotation Center") t.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ input
    void Update()
    {
        if (!IsOpen) return;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        var kb = Keyboard.current;
        var pad = Gamepad.current;
        var mouse = Mouse.current;

        int step = 0;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) step = -1;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) step = 1;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) { Confirm(); return; }
        }
        if (pad != null)
        {
            if (pad.dpad.left.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame) step = -1;
            if (pad.dpad.right.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame) step = 1;
            // Left stick flick browses too (with repeat).
            float lx = pad.leftStick.ReadValue().x;
            stickRepeat -= Time.unscaledDeltaTime;
            if (Mathf.Abs(lx) > 0.6f && stickRepeat <= 0f) { step = lx > 0 ? 1 : -1; stickRepeat = 0.35f; }
            else if (Mathf.Abs(lx) < 0.3f) stickRepeat = 0f;

            float rx = pad.rightStick.ReadValue().x;
            if (Mathf.Abs(rx) > 0.15f) Spin(rx * StickDegreesPerSecond * Time.unscaledDeltaTime);
            // A / Cross is handled by the SELECT button (PimlrMenuNavigator focuses it); this covers no-focus cases.
            if (pad.buttonSouth.wasPressedThisFrame && !PimlrMenuNavigator.IsNavigating) { Confirm(); return; }
        }
        if (mouse != null)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.1f) step = wheel > 0 ? -1 : 1;

            bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            if (mouse.leftButton.wasPressedThisFrame && !overUi) { dragging = true; lastPointerX = mouse.position.ReadValue().x; }
            if (!mouse.leftButton.isPressed) dragging = false;
            if (dragging)
            {
                float x = mouse.position.ReadValue().x;
                Spin(-(x - lastPointerX) * DragDegreesPerPixel);
                lastPointerX = x;
            }
        }
        if (step != 0) Browse(step);

        // Animate the ring toward the target index.
        current = Mathf.SmoothDamp(current, target, ref turnVel, TurnSeconds * 0.4f, Mathf.Infinity, Time.unscaledDeltaTime);
        if (Mathf.Abs(current - target) < 0.001f) current = target;
        RefreshStands(force: false);
        PlaceRing();
    }

    void Browse(int dir)
    {
        // Snap the front model's spin back as it leaves the front.
        int front = FrontStand();
        if (front >= 0) standSpin[front] = 0f;
        selectedLogical += dir;
        target = selectedLogical;
        RefreshLabels();
    }

    int FrontStand()
    {
        for (int p = 0; p < Stands; p++) if (LogicalFor(p) == selectedLogical) return p;
        return -1;
    }

    void Spin(float degrees)
    {
        int p = FrontStand();
        if (p >= 0) standSpin[p] += degrees;
    }

    void Confirm()
    {
        if (!IsOpen) return;
        StartCoroutine(ConfirmRoutine());
    }

    IEnumerator ConfirmRoutine()
    {
        enabled = false; // no more input
        if (selectButton) selectButton.interactable = false;
        // A beat on the choice: a quick pulse of the name.
        if (nameText)
        {
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                nameText.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI));
                yield return null;
            }
        }
        Finish();
    }

    // ------------------------------------------------------------------ UI
    void BuildUi()
    {
        canvasGo = new GameObject("PIMLR Artist Select UI", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var font = PimlrArtists.Roster.titleFont ? PimlrArtists.Roster.titleFont : PIMLR.Missions.SaveTheFans.SaveTheFansStyle.Font;
        var body = PimlrArtists.Roster.bodyFont ? PimlrArtists.Roster.bodyFont : font;
        var root = canvasGo.transform;

        // Soft dark bars top and bottom so the text reads over the room.
        Bar(root, "Top Shade", true);
        Bar(root, "Bottom Shade", false);

        var title = Label(root, "Title", "SELECT YOUR ARTIST", font, 64f, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -70f));
        title.characterSpacing = 8f;
        var sub = Label(root, "Subtitle", "PIMLR WORLD", body, 22f, Yellow, new Vector2(0.5f, 1f), new Vector2(0f, -140f));
        sub.characterSpacing = 18f;

        nameText = Label(root, "Artist Name", "", font, 84f, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 250f));
        taglineText = Label(root, "Tagline", "", body, 26f, new Color(1f, 1f, 1f, 0.8f), new Vector2(0.5f, 0f), new Vector2(0f, 200f));
        countText = Label(root, "Count", "", body, 20f, Yellow, new Vector2(0.5f, 0f), new Vector2(0f, 330f));

        // Arrows (mouse); skipped by controller focus, which browses with the D-pad / bumpers instead.
        Arrow(root, "Prev", "<", new Vector2(0f, 0.5f), new Vector2(150f, 40f), () => Browse(-1), font);
        Arrow(root, "Next", ">", new Vector2(1f, 0.5f), new Vector2(-150f, 40f), () => Browse(1), font);

        // SELECT
        var sel = new GameObject("Select Button", typeof(RectTransform)).AddComponent<Image>();
        sel.transform.SetParent(root, false);
        sel.color = Green;
        var srt = sel.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
        srt.sizeDelta = new Vector2(340f, 78f);
        srt.anchoredPosition = new Vector2(0f, 120f);
        selectButton = sel.gameObject.AddComponent<Button>();
        selectButton.targetGraphic = sel;
        selectButton.onClick.AddListener(Confirm);
        var hint = PimlrNavHint.On(selectButton);
        if (hint) { hint.defaultPriority = 100; hint.isMenu = true; }
        var st = Label(sel.transform, "Text", "SELECT", font, 38f, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero);
        st.characterSpacing = 6f;

        hintText = Label(root, "Hints", "", body, 20f, new Color(1f, 1f, 1f, 0.65f), new Vector2(0.5f, 0f), new Vector2(0f, 48f));
        UpdateHints();
        PimlrInput.SchemeChanged += UpdateHints;
    }

    void OnDisable() { PimlrInput.SchemeChanged -= UpdateHints; }

    void UpdateHints()
    {
        if (!hintText) return;
        string k(string s) => $"<color=#{ColorUtility.ToHtmlStringRGB(Yellow)}>{s}</color>";
        hintText.text = PimlrInput.UsingGamepad
            ? $"{k("D-PAD / LB RB")} browse     {k("RIGHT STICK")} spin     {k(PimlrInput.Label(PimlrButton.Interact))} select"
            : $"{k("A / D")} or {k("ARROWS")} browse     {k("DRAG")} spin     {k("ENTER")} select";
    }

    void RefreshLabels()
    {
        var a = ArtistAt(selectedLogical);
        int n = PimlrArtists.All.Count;
        int i = ((selectedLogical % n) + n) % n;
        if (nameText) nameText.text = string.IsNullOrEmpty(a.displayName) ? a.id.ToUpperInvariant() : a.displayName;
        if (taglineText) taglineText.text = a.tagline ?? "";
        if (countText) countText.text = $"{i + 1} / {n}";
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, Vector2 anchor, Vector2 pos)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        if (font) t.font = font;
        t.text = text; t.fontSize = size; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(1400f, size * 1.4f);
        r.anchoredPosition = pos;
        return t;
    }

    static void Bar(Transform parent, string name, bool top)
    {
        var img = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
        img.transform.SetParent(parent, false);
        img.raycastTarget = false;
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
        {
            float k = y / 63f;                       // 0 at bottom
            float a = top ? k : 1f - k;              // darkest at the screen edge
            tex.SetPixel(0, y, new Color(0f, 0f, 0f, a * a * 0.78f));
        }
        tex.Apply();
        img.texture = tex;
        var r = img.rectTransform;
        r.anchorMin = new Vector2(0f, top ? 1f : 0f);
        r.anchorMax = new Vector2(1f, top ? 1f : 0f);
        r.pivot = new Vector2(0.5f, top ? 1f : 0f);
        r.sizeDelta = new Vector2(0f, top ? 260f : 420f);
        r.anchoredPosition = Vector2.zero;
    }

    void Arrow(Transform parent, string name, string glyph, Vector2 anchor, Vector2 pos, Action click, TMP_FontAsset font)
    {
        var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = new Color(0f, 0f, 0f, 0.45f);
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = anchor;
        r.sizeDelta = new Vector2(96f, 96f);
        r.anchoredPosition = pos;
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors; colors.highlightedColor = new Color(1f, 0.82f, 0.1f, 1f); b.colors = colors;
        b.onClick.AddListener(() => click());
        var hint = PimlrNavHint.On(b);
        if (hint) hint.skip = true;
        Label(img.transform, "Glyph", glyph, font, 64f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f));
    }
}
