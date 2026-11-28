// #UI Slice 5: one track in the Music Library grid. Every tile is the same size (Craig: metadata must be predictably
// scannable), whatever its title length: cover art, one-line title, one-line "Artist ft. Guests", state badge.
// Long title / credit lines are left-aligned with a fade at the clipped edge, and scroll left-right only while the tile is
// selected (marquee with pauses at each end). The tile draws its own lime focus outline (IPimlrFocusVisual), so the
// navigator's generic ring doesn't double up.
// It's a Button, so PimlrMenuNavigator walks it like any menu button and mouse / touch click it normally.
//
// Unreal note: WBP_TrackTile (UCommonButtonBase) with a UScrollBox-free marquee done by a material or a
// translation animation on the text inside a clipping UOverlay.

using PIMLR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PimlrTrackTile : Button, IPimlrFocusVisual
{
    public MusicData Data { get; private set; }
    public bool DrawsOwnFocus => true;

    Image art, dim, badgeBg;
    TextMeshProUGUI title, credit, badge;
    RectTransform titleClip, creditClip;
    GameObject focusFrame, playingFrame;
    Image titleFadeL, titleFadeR, creditFadeL, creditFadeR;
    bool focused;
    float marqueeT;

    static Sprite fadeSprite;

    public static PimlrTrackTile Create(Transform parent)
    {
        var bg = PimlrHudKit.Img("Track Tile", parent, ColorToken.SurfaceRaised, 1f);
        bg.raycastTarget = true;
        var tile = bg.gameObject.AddComponent<PimlrTrackTile>();
        tile.targetGraphic = bg;
        tile.transition = Transition.None;
        tile.navigation = new Navigation { mode = Navigation.Mode.None }; // PimlrMenuNavigator moves spatially
        tile.Build();
        return tile;
    }

    void Build()
    {
        art = PimlrHudKit.Icon("Art", transform, null);
        art.color = Color.white; art.preserveAspect = false;
        dim = PimlrHudKit.Img("Locked Dim", art.transform, ColorToken.SurfaceScrim, 0.8f);
        PimlrHudKit.Stretch(dim.rectTransform);

        titleClip = Clip("Title Clip", out title, TypeRole.Heading, 25f, ColorToken.TextPrimary, out titleFadeL, out titleFadeR);
        creditClip = Clip("Credit Clip", out credit, TypeRole.Body, 24.4f, ColorToken.TextSecondary, out creditFadeL, out creditFadeR);

        badgeBg = PimlrHudKit.Img("Badge", transform, ColorToken.SurfaceBase, 1f);
        badge = PimlrHudKit.Txt("Label", badgeBg.transform, TypeRole.Utility, 21f, ColorToken.TextPrimary, TextCase.Upper, TextAlignmentOptions.MidlineLeft);
        var br = badge.rectTransform; br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = new Vector2(10f, 0f); br.offsetMax = new Vector2(-10f, 0f);
        badge.characterSpacing = 2f;

        playingFrame = PimlrHudKit.Edges("Playing Frame", transform, ColorToken.HealthGreen, 3f);
        focusFrame = PimlrHudKit.Edges("Focus Frame", transform, ColorToken.BrandLime, 4f);
        var ff = (RectTransform)focusFrame.transform; ff.offsetMin = new Vector2(-6f, -6f); ff.offsetMax = new Vector2(6f, 6f);
        focusFrame.SetActive(false);
        playingFrame.SetActive(false);
    }

    RectTransform Clip(string name, out TextMeshProUGUI text, TypeRole role, float px, ColorToken color, out Image fadeL, out Image fadeR)
    {
        var clip = PimlrHudKit.Rect(name, transform);
        clip.gameObject.AddComponent<RectMask2D>();
        text = PimlrHudKit.Txt("Text", clip, role, px, color, TextCase.Original);
        text.overflowMode = TextOverflowModes.Overflow;
        var r = text.rectTransform; r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f);
        fadeL = Fade(clip, false); fadeR = Fade(clip, true);
        return clip;
    }

    Image Fade(RectTransform clip, bool right)
    {
        var img = PimlrHudKit.Icon(right ? "Fade R" : "Fade L", clip, FadeSprite());
        img.preserveAspect = false;
        img.color = PimlrUITheme.C(ColorToken.SurfaceRaised);
        var r = img.rectTransform;
        r.anchorMin = new Vector2(right ? 1f : 0f, 0f); r.anchorMax = new Vector2(right ? 1f : 0f, 1f);
        r.pivot = new Vector2(1f, 0.5f); // left fade: pivot 1 + flipped x = drawn inside the clip
        r.sizeDelta = new Vector2(36f, 0f); r.anchoredPosition = Vector2.zero;
        if (!right) r.localScale = new Vector3(-1f, 1f, 1f);
        return img;
    }

    static Sprite FadeSprite()
    {
        if (fadeSprite) return fadeSprite;
        var tex = new Texture2D(32, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 32; x++) tex.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, x / 31f)));
        tex.Apply();
        fadeSprite = Sprite.Create(tex, new Rect(0, 0, 32, 1), new Vector2(0.5f, 0.5f));
        return fadeSprite;
    }

    /// <summary>Uniform layout for a cell of the given size (called by the grid).</summary>
    public void Layout(Vector2 cell)
    {
        float pad = 10f, w = cell.x - pad * 2f;
        PimlrHudKit.Place(art.rectTransform, new Vector2(0f, 1f), new Vector2(pad, -pad), new Vector2(w, w));
        float y = pad + w + 8f;
        PimlrHudKit.Place(titleClip, new Vector2(0f, 1f), new Vector2(pad, -y), new Vector2(w, 36f)); y += 38f;
        PimlrHudKit.Place(creditClip, new Vector2(0f, 1f), new Vector2(pad, -y), new Vector2(w, 32f)); y += 36f;
        PimlrHudKit.Place(badgeBg.rectTransform, new Vector2(0f, 1f), new Vector2(pad, -y), new Vector2(w, 36f));
    }

    public static float HeightFor(float width) => width + 118f; // art (width - 20) + title 38 + credit 36 + badge 36 + padding

    public void Bind(MusicData d)
    {
        Data = d;
        art.sprite = d != null ? d.albumIcon : null;
        art.enabled = art.sprite;
        title.text = d != null ? PimlrTrackInfo.Title(d) : "";
        credit.text = d != null ? PimlrTrackInfo.Credit(d) : "";
        marqueeT = 0f;
        Refresh(null);
    }

    public void Refresh(AudioClip libraryPlaying)
    {
        if (Data == null) return;
        var st = PimlrTrackStore.State(Data, libraryPlaying);
        bool locked = st == PimlrTrackState.CanBuy || st == PimlrTrackState.NeedCoins || st == PimlrTrackState.NeedAchievement;
        dim.enabled = locked;
        dim.color = PimlrUITheme.C(ColorToken.SurfaceScrim, st == PimlrTrackState.CanBuy ? 0.6f : 0.85f);
        playingFrame.SetActive(st == PimlrTrackState.Playing);
        title.color = PimlrUITheme.C(locked && st != PimlrTrackState.CanBuy ? ColorToken.TextSecondary : ColorToken.TextPrimary);

        string label; ColorToken bg, fg;
        switch (st)
        {
            case PimlrTrackState.Playing: label = "PLAYING"; bg = ColorToken.HealthGreen; fg = ColorToken.TextOnLime; break;
            case PimlrTrackState.Unlocked: label = "PLAY"; bg = ColorToken.SurfaceBase; fg = ColorToken.TextPrimary; break;
            case PimlrTrackState.CanBuy: label = $"UNLOCK · {Data.musicValue}"; bg = ColorToken.AccentGold; fg = ColorToken.TextOnLime; break;
            case PimlrTrackState.NeedCoins:
                label = $"LOCKED · NEED {Mathf.Max(0, Data.musicValue - PimlrTrackStore.Coins)} MORE"; bg = ColorToken.SurfaceBase; fg = ColorToken.TextSecondary; break;
            default:
                label = "LOCKED · " + (string.IsNullOrWhiteSpace(Data.lockedBtnMsg) ? "EARN IT" : Data.lockedBtnMsg.Trim()); bg = ColorToken.SurfaceBase; fg = ColorToken.TextSecondary; break;
        }
        if (badge.text != label) badge.text = label;
        badgeBg.color = PimlrUITheme.C(bg);
        badge.color = PimlrUITheme.C(fg);
    }

    public override void OnSelect(BaseEventData e) { base.OnSelect(e); focused = true; marqueeT = 0f; focusFrame.SetActive(true); }
    public override void OnDeselect(BaseEventData e) { base.OnDeselect(e); focused = false; focusFrame.SetActive(false); }
    public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); if (!focused) { focusFrame.SetActive(true); focused = true; marqueeT = 0f; } }
    public override void OnPointerExit(PointerEventData e)
    {
        base.OnPointerExit(e);
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject) return;
        focused = false; focusFrame.SetActive(false);
    }

    void Update()
    {
        if (!title) return;
        marqueeT += Time.unscaledDeltaTime;
        Marquee(title, titleClip, titleFadeL, titleFadeR);
        Marquee(credit, creditClip, creditFadeL, creditFadeR);
    }

    // Left-aligned at rest; while focused, glide to show the end, pause, glide back, pause (40 units/s).
    void Marquee(TextMeshProUGUI t, RectTransform clip, Image fadeL, Image fadeR)
    {
        float w = clip.rect.width, pref = t.preferredWidth;
        var r = t.rectTransform;
        if (!Mathf.Approximately(r.sizeDelta.x, pref)) r.sizeDelta = new Vector2(pref, 0f);
        float overflow = Mathf.Max(0f, pref - w + 6f);
        float x = 0f;
        if (focused && overflow > 0f)
        {
            const float pause = 1.2f, speed = 40f;
            float glide = overflow / speed, cycle = 2f * (pause + glide);
            float p = marqueeT % cycle;
            if (p < pause) x = 0f;
            else if (p < pause + glide) x = -(p - pause) * speed;
            else if (p < 2f * pause + glide) x = -overflow;
            else x = -overflow + (p - 2f * pause - glide) * speed;
        }
        r.anchoredPosition = new Vector2(x, 0f);
        fadeL.enabled = x < -1f;
        fadeR.enabled = overflow > 0f && x > -overflow + 1f;
    }
}
