// PIMLR #SET: the Settings menu. Built at runtime (no scene or prefab edits), and a SETTINGS button is added to:
//   - the Pause screen (YannicksWorld): a copy of its LIVING ROOM button, just above it,
//   - the Living Room menu (SceneStaticEU): a copy of its FIT CHECK button, next to it.
// Rows: Look Left / Right, Look Up / Down (multipliers on the camera's own sensitivity), Invert Camera Y,
// Controller Rumble, Background Music volume. Mouse: drag the bars or click - / +. Controller: D-pad between
// rows, A on - / + / ON-OFF, B = Back (PimlrMenuNavigator). Esc also closes it.
//
// Unreal note: a UMG widget (UUserWidget) pushed on the menu stack from the pause / living-room widgets, bound to
// the UGameUserSettings subclass that replaces PimlrSettings.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PimlrSettingsMenu : MonoBehaviour
{
    public static PimlrSettingsMenu Instance { get; private set; }
    public static bool IsOpen => Instance && Instance.root && Instance.root.activeSelf;

    enum Placement { MirrorTopRight, RightOfLeaderboards }
    // Where the SETTINGS button goes: (host object path suffix, button to copy, placement)
    static readonly (string host, string source, Placement place)[] Hosts =
    {
        // Pause screen: top-right corner, mirroring LIVING ROOM at top-left (the coin counter slides left to make room)
        ("Pause Screen/Overlay Panel/Pause BG", "Home Button", Placement.MirrorTopRight),
        // Living Room: a copy of FIT CHECK to the right of LEADERBOARDS (mirrors TAB MENU on the left)
        ("PLMRMainMenuPanel/BG Shade/Level Select Menu", "Avatar Create Button", Placement.RightOfLeaderboards),
    };
    const string InjectedName = "Settings Button (PIMLR)";

    static readonly Color Panel = new Color(0.06f, 0.06f, 0.08f, 1f);
    static readonly Color Row = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color Accent = new Color(1f, 0.82f, 0.1f, 1f);   // PIMLR yellow
    static readonly Color Btn = new Color(0.16f, 0.16f, 0.2f, 1f);
    static readonly Color Fill = new Color(0.095f, 0.745f, 0f, 1f); // menu green

    GameObject root;
    TMP_FontAsset font;
    GameObject openedFrom;
    GameObject previousSelection;
    float scanTimer;
    readonly List<Action> refreshers = new List<Action>();
    Button firstButton;

    void Awake() { Instance = this; }

    void Update()
    {
        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer <= 0f) { scanTimer = 0.5f; InjectButtons(); }

        if (!IsOpen) return;
        if (openedFrom && !openedFrom.activeInHierarchy) { Close(); return; } // pause closed underneath us
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
    }

    // ------------------------------------------------------------------ entry buttons
    void InjectButtons()
    {
        foreach (var rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
        {
            foreach (var (host, source, place) in Hosts)
            {
                if (!PathEndsWith(rt, host)) continue;
                if (FindDeep(rt, InjectedName)) continue;
                var src = FindDeep(rt, source);
                if (!src) continue;
                MakeEntryButton(rt, src, place);
            }
        }
    }

    void MakeEntryButton(Transform host, Transform source, Placement place)
    {
        var copy = Instantiate(source.gameObject, source.parent, false);
        copy.name = InjectedName;
        copy.SetActive(true);
        var crt = (RectTransform)copy.transform;
        var srt = (RectTransform)source;
        copy.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
        float textScale = 1f;

        if (place == Placement.MirrorTopRight && srt.parent is RectTransform par)
        {
            const float k = 0.62f;
            crt.localScale = srt.localScale * k;
            float margin = (srt.anchoredPosition.x - srt.rect.width * srt.localScale.x * 0.5f) - par.rect.xMin;
            float w = srt.rect.width * crt.localScale.x;
            crt.anchoredPosition = new Vector2(par.rect.xMax - margin - w * 0.5f, srt.anchoredPosition.y);
            var coins = srt.parent.Find("PIMLR Coins") as RectTransform;
            if (coins) coins.anchoredPosition = new Vector2(-(margin + w + 25f), coins.anchoredPosition.y);
            textScale = 1.35f;
        }
        else
        {
            // Same gap as FIT CHECK -> LEADERBOARDS, measured in this button's parent space (the buttons live under
            // different parents, and the canvas isn't in screen pixels).
            var lb = FindDeep(host.parent ? host.parent : host, "Leaderboard Button") as RectTransform;
            var par2 = (RectTransform)srt.parent;
            if (lb)
            {
                float lbLeft = LocalX(par2, lb, 0), lbRight = LocalX(par2, lb, 2), srcRight = LocalX(par2, srt, 2);
                float gap = lbLeft - srcRight;
                float w = LocalX(par2, crt, 2) - LocalX(par2, crt, 0);
                var lp = crt.localPosition;
                lp.x = lbRight + gap + w * 0.5f + (crt.pivot.x - 0.5f) * w;
                crt.localPosition = lp;
            }
            else crt.anchoredPosition = srt.anchoredPosition - new Vector2(srt.rect.width * srt.localScale.x + 16f, 0f);
        }

        // Text -> SETTINGS; icons that belonged to the original button are hidden.
        TMP_Text label = null;
        foreach (var t in copy.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!t.gameObject.activeSelf) continue;
            label = t;
            t.text = "SETTINGS";
            t.fontSize *= textScale;
            if (!font) font = t.font;
        }
        foreach (var img in copy.GetComponentsInChildren<Image>(true))
            if (img.gameObject != copy && (img.name.Contains("Arrow") || img.name.Contains("Icon") || img.name == "Image")) img.gameObject.SetActive(false);
        if (label)
        {
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
        }

        var b = copy.GetComponent<Button>();
        b.onClick = new Button.ButtonClickedEvent(); // drop the original button's persistent (Inspector) calls
        GameObject hostGo = host.gameObject;
        b.onClick.AddListener(() => Open(hostGo));
        foreach (var hint in copy.GetComponents<PimlrNavHint>()) Destroy(hint);
    }

    // ------------------------------------------------------------------ open / close
    public void Open(GameObject from = null)
    {
        if (!root) Build();
        openedFrom = from;
        previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        Refresh();
        root.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (EventSystem.current && firstButton) EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
    }

    public void Close()
    {
        if (!root || !root.activeSelf) return;
        root.SetActive(false);
        if (EventSystem.current && previousSelection && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        PimlrInput.BlockGameplayInputFor(0.25f, false); // the B / click that closed it shouldn't fire or jump
    }

    void Refresh() { foreach (var r in refreshers) r(); }

    // ------------------------------------------------------------------ UI
    void Build()
    {
        root = new GameObject("PIMLR Settings Menu", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000; // above every menu, below ScreenFader
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        var dim = Box("Dim", root.transform, new Color(0f, 0f, 0f, 0.7f));
        Stretch(dim.rectTransform);

        var panel = Box("Panel", root.transform, Panel);
        var prt = panel.rectTransform;
        prt.sizeDelta = new Vector2(1000, 720);
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = Accent; outline.effectDistance = new Vector2(3, -3);

        var title = Text("Title", panel.transform, "SETTINGS", 54, Accent, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0, 300), new Vector2(900, 70));

        float y = 190f;
        SliderRow(panel.transform, "LOOK LEFT / RIGHT", ref y, () => PimlrSettings.LookX, v => PimlrSettings.LookX = v,
            PimlrSettings.MinLook, PimlrSettings.MaxLook, 0.1f, v => $"{v:0.0}x");
        SliderRow(panel.transform, "LOOK UP / DOWN", ref y, () => PimlrSettings.LookY, v => PimlrSettings.LookY = v,
            PimlrSettings.MinLook, PimlrSettings.MaxLook, 0.1f, v => $"{v:0.0}x");
        ToggleRow(panel.transform, "INVERT CAMERA Y", ref y, () => PimlrSettings.InvertY, v => PimlrSettings.InvertY = v);
        ToggleRow(panel.transform, "CONTROLLER RUMBLE", ref y, () => PimlrSettings.Rumble, v =>
        {
            PimlrSettings.Rumble = v;
            if (v) PimlrRumble.Pulse(0.4f, 0.6f, 0.25f); // a taste of it
        });
        SliderRow(panel.transform, "BACKGROUND MUSIC", ref y, () => PimlrSettings.MusicVolume, v => PimlrSettings.MusicVolume = v,
            0f, 1f, 0.05f, v => $"{Mathf.RoundToInt(v * 100f)}%");

        var reset = MakeButton(panel.transform, "Reset Button", "RESET TO DEFAULTS", 26);
        Place((RectTransform)reset.transform, new Vector2(-200, -290), new Vector2(340, 70));
        reset.onClick.AddListener(() => { PimlrSettings.ResetToDefaults(); Refresh(); });

        var back = MakeButton(panel.transform, "Back Button", "BACK", 30);
        Place((RectTransform)back.transform, new Vector2(200, -290), new Vector2(340, 70));
        back.onClick.AddListener(Close);
        var hint = back.gameObject.AddComponent<PimlrNavHint>(); hint.isCancel = true;

        root.SetActive(false);
    }

    void SliderRow(Transform parent, string name, ref float y, Func<float> get, Action<float> set,
                   float min, float max, float step, Func<float, string> format)
    {
        var row = RowBase(parent, name, ref y);

        var minus = MakeButton(row, name + " -", "−", 40);
        Place((RectTransform)minus.transform, new Vector2(-50, 0), new Vector2(64, 64));

        // Bar: a Slider for the mouse (drag / click). Controllers use - / + (the navigator skips sliders).
        var sGo = new GameObject("Bar", typeof(RectTransform));
        sGo.transform.SetParent(row, false);
        Place((RectTransform)sGo.transform, new Vector2(130, 0), new Vector2(260, 22));
        var bg = Box("Background", sGo.transform, new Color(1, 1, 1, 0.15f)); Stretch(bg.rectTransform);
        var fillArea = new GameObject("Fill Area", typeof(RectTransform)); fillArea.transform.SetParent(sGo.transform, false);
        Stretch((RectTransform)fillArea.transform);
        var fill = Box("Fill", fillArea.transform, Fill); Stretch(fill.rectTransform);
        var handleArea = new GameObject("Handle Area", typeof(RectTransform)); handleArea.transform.SetParent(sGo.transform, false);
        Stretch((RectTransform)handleArea.transform);
        var handle = Box("Handle", handleArea.transform, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(18, 36);
        var slider = sGo.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.minValue = min; slider.maxValue = max;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        var plus = MakeButton(row, name + " +", "+", 40);
        Place((RectTransform)plus.transform, new Vector2(310, 0), new Vector2(64, 64));

        var value = Text("Value", row, "", 30, Color.white, TextAlignmentOptions.Center);
        Place(value.rectTransform, new Vector2(410, 0), new Vector2(100, 60));

        bool syncing = false;
        Action refresh = () => { syncing = true; float v = get(); slider.SetValueWithoutNotify(v); value.text = format(v); syncing = false; };
        slider.onValueChanged.AddListener(v =>
        {
            if (syncing) return;
            v = Mathf.Round(v / step) * step;
            set(Mathf.Clamp(v, min, max)); refresh();
        });
        minus.onClick.AddListener(() => { set(Mathf.Clamp(Mathf.Round((get() - step) / step) * step, min, max)); refresh(); });
        plus.onClick.AddListener(() => { set(Mathf.Clamp(Mathf.Round((get() + step) / step) * step, min, max)); refresh(); });
        refreshers.Add(refresh);
        if (!firstButton) firstButton = minus;
    }

    void ToggleRow(Transform parent, string name, ref float y, Func<bool> get, Action<bool> set)
    {
        var row = RowBase(parent, name, ref y);
        var b = MakeButton(row, name + " Toggle", "", 30);
        Place((RectTransform)b.transform, new Vector2(130, 0), new Vector2(424, 64));
        var label = b.GetComponentInChildren<TMP_Text>();
        var img = b.GetComponent<Image>();
        Action refresh = () =>
        {
            bool on = get();
            label.text = on ? "ON" : "OFF";
            img.color = on ? Fill : Btn;
        };
        b.onClick.AddListener(() => { set(!get()); refresh(); });
        refreshers.Add(refresh);
        if (!firstButton) firstButton = b;
    }

    Transform RowBase(Transform parent, string name, ref float y)
    {
        var row = Box(name, parent, Row);
        Place(row.rectTransform, new Vector2(0, y), new Vector2(920, 84));
        var label = Text("Label", row.transform, name, 30, Color.white, TextAlignmentOptions.Left);
        Place(label.rectTransform, new Vector2(-270, 0), new Vector2(340, 60));
        y -= 96f;
        return row.transform;
    }

    Button MakeButton(Transform parent, string name, string text, float size)
    {
        var img = Box(name, parent, Btn);
        var b = img.gameObject.AddComponent<Button>();
        var colors = b.colors;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.selectedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = colors;
        b.targetGraphic = img;
        img.raycastTarget = true;
        var t = Text("Text", img.transform, text, size, Color.white, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        return b;
    }

    TMP_Text Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static Image Box(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = c;
        return img;
    }

    static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static readonly Vector3[] corners = new Vector3[4];
    static float LocalX(RectTransform space, RectTransform r, int corner)
    {
        r.GetWorldCorners(corners);
        return space.InverseTransformPoint(corners[corner]).x;
    }

    static bool PathEndsWith(Transform t, string suffix)
    {
        var parts = suffix.Split('/');
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (!t || t.name != parts[i]) return false;
            t = t.parent;
        }
        return true;
    }

    static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t) { if (c.name == name) return c; }
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f) return f; }
        return null;
    }
}
