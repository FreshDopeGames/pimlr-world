// PIMLR (Nov 18): the active track boost stays on screen, top-left of the gameplay HUD.
// When a boost is activated, UI_GoalPanel.StartBoost plays its big popup as before; once that popup is gone, this card
// slides in at the top-left with the same boost name and effect text, and stays there until the boost ends: the player
// switches to another boost or stops it (AuthManager.PowerChange(Nothing)), leaves the scene, or dies (game over).
// It sits under the "JUTPS Interface" HUD, so it fades out with the rest of the HUD during cinematics and menus.
//
// Hooks: UI_GoalPanel.StartBoost -> Show, AuthManager.PowerChange(Nothing) -> Clear.
//
// Unreal note: a small UMG widget on the HUD bound to an "ActiveBoostChanged" delegate on the boost subsystem;
// a widget animation for the slide-in, played after the activation popup's animation finishes.

using DG.Tweening;
using PIMLR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PimlrBoostHUD : MonoBehaviour
{
    /// <summary>Seconds after activation before the card appears (the activation popup is gone by then).</summary>
    public static float AppearDelay = 2.3f;
    public static Vector2 Margin = new Vector2(28f, 24f);
    public static float Width = 400f;
    // #UI (Slice 4): music purple + carbon card from the PIMLR UI theme (Track Boost = AccentMusic token).
    public static Color Accent => PimlrUITheme.C(ColorToken.AccentMusic);
    public static Color PanelColor => PimlrUITheme.C(ColorToken.SurfaceScrim, 1.1f);

    static PimlrBoostHUD instance;
    static string pendingTitle, pendingInfo;
    static bool pending;
    static float showAt;

    RectTransform card;
    TextMeshProUGUI header, title, info;
    CanvasGroup group;
    bool visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; pending = false; }

    /// <summary>A boost just started (title = track / boost name, info = its effect).</summary>
    public static void Show(string boostTitle, string boostInfo, TMP_FontAsset titleFont = null, TMP_FontAsset infoFont = null)
    {
        if (string.IsNullOrEmpty(boostTitle) && string.IsNullOrEmpty(boostInfo)) { Clear(); return; }
        pendingTitle = boostTitle;
        pendingInfo = boostInfo;
        pending = true;
        showAt = Time.unscaledTime + AppearDelay;
        var hud = Ensure();
        if (!hud) return;
        // #UI: theme fonts win; the caller's fonts are only a fallback when no theme asset is present.
        if (!PimlrUITheme.Current)
        {
            if (titleFont) { hud.header.font = titleFont; hud.title.font = titleFont; }
            if (infoFont) hud.info.font = infoFont;
        }
        hud.SlideOut(true); // a switch: the old card leaves while the new popup plays
    }

    /// <summary>No boost any more.</summary>
    public static void Clear()
    {
        pending = false;
        if (instance) instance.SlideOut(false);
    }

    static PimlrBoostHUD Ensure()
    {
        if (instance) return instance;
        var sms = SceneManagerScript.Instance;
        GameObject hud = sms && sms.uiManager ? sms.uiManager.GetMenu("JUTPS Interface") : null;
        if (!hud) return null;
        var go = new GameObject("PIMLR Active Boost", typeof(RectTransform));
        go.transform.SetParent(hud.transform, false);
        instance = go.AddComponent<PimlrBoostHUD>();
        instance.Build();
        return instance;
    }

    void Build()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Margin.x, -Margin.y);
        rt.sizeDelta = new Vector2(Width, 10f);

        var bg = new GameObject("Card", typeof(RectTransform)).AddComponent<Image>();
        bg.transform.SetParent(transform, false);
        bg.color = PanelColor;
        bg.raycastTarget = false;
        var cardSprite = PimlrUITheme.Current ? PimlrUITheme.Current.Sprite(SpriteSlot.Card) : null; // #UI
        if (cardSprite) { bg.sprite = cardSprite; bg.type = Image.Type.Sliced; }
        card = bg.rectTransform;
        card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
        card.pivot = new Vector2(0f, 1f);
        group = bg.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

        // Yellow edge on the left, like a tag.
        var edge = new GameObject("Edge", typeof(RectTransform)).AddComponent<Image>();
        edge.transform.SetParent(card, false);
        edge.color = Accent; edge.raycastTarget = false;
        var ert = edge.rectTransform;
        ert.anchorMin = new Vector2(0f, 0f); ert.anchorMax = new Vector2(0f, 1f);
        ert.pivot = new Vector2(0f, 0.5f); ert.sizeDelta = new Vector2(6f, 0f); ert.anchoredPosition = Vector2.zero;

        // #UI: Caption / Heading / Body roles. Sizes are canvas units on the 1280x720 HUD canvas (x1.5 at 1080p).
        var fallback = PIMLR.Missions.SaveTheFans.SaveTheFansStyle.Font;
        header = MakeText("Header", 15f, PimlrUITheme.C(ColorToken.AccentMusic), PimlrUITheme.Font(TypeRole.Caption) ?? fallback);
        title = MakeText("Title", 24f, PimlrUITheme.C(ColorToken.TextPrimary), PimlrUITheme.Font(TypeRole.Heading) ?? fallback);
        info = MakeText("Info", 17f, PimlrUITheme.C(ColorToken.TextSecondary), PimlrUITheme.Font(TypeRole.Body) ?? fallback);
        header.text = "TRACK BOOST ACTIVE";
        header.characterSpacing = 6f;
    }

    TextMeshProUGUI MakeText(string name, float size, Color color, TMP_FontAsset font)
    {
        var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(card, false);
        if (font) t.font = font;
        t.fontSize = size; t.color = color; t.raycastTarget = false;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.Normal;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(Width - 40f, 10f);
        return t;
    }

    void Layout()
    {
        float y = -12f;
        foreach (var t in new[] { header, title, info })
        {
            bool on = !string.IsNullOrEmpty(t.text);
            t.gameObject.SetActive(on);
            if (!on) continue;
            t.ForceMeshUpdate();
            float h = t.preferredHeight;
            t.rectTransform.sizeDelta = new Vector2(Width - 40f, h);
            t.rectTransform.anchoredPosition = new Vector2(20f, y);
            y -= h + 4f;
        }
        card.sizeDelta = new Vector2(Width, -y + 8f);
    }

    void Update()
    {
        if (pending && Time.unscaledTime >= showAt)
        {
            pending = false;
            title.text = pendingTitle ?? "";
            info.text = pendingInfo ?? "";
            Layout();
            SlideIn();
        }
        // Game over: the boost goes with the run.
        if (visible && PimlrDeathSequence.IsPlaying) Clear();
    }

    void SlideIn()
    {
        visible = true;
        card.DOKill(); group.DOKill();
        card.anchoredPosition = new Vector2(-40f, 0f);
        card.DOAnchorPos(Vector2.zero, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
        group.DOFade(1f, 0.25f).SetUpdate(true);
    }

    void SlideOut(bool quick)
    {
        if (!visible) return;
        visible = false;
        card.DOKill(); group.DOKill();
        float d = quick ? 0.15f : 0.3f;
        card.DOAnchorPos(new Vector2(-40f, 0f), d).SetUpdate(true);
        group.DOFade(0f, d).SetUpdate(true);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (card) card.DOKill();
        if (group) group.DOKill();
    }
}
