// #UI Slice 4b: the gameplay music player, top-left of the HUD, with the track's Boost as a slide-down panel under it.
// Craig (Nov 27): the boost is an extension of the player, so the player is worth watching and the HUD has one element
// instead of two. The Boost panel reads "Boost: <name in bold>" with the effect on a second line, and stays out for as
// long as the track plays (pause / skip / track end / death all end the boost - see MusicSystem and AuthManager).
//
// Data: SceneManagerScript.Instance.musicSystem (MusicSystem) for the clip, play state and controls; MusicData for the
// album art (albumIcon); PimlrTrackInfo gives the title and "Artist ft. Guests" lines. The old "Music" panels stay in the prefab (MusicSystem
// still drives them) but are hidden by the Slice 4b builder.
// Buttons call the same MusicSystem methods the old panel did; L1 / R1 / Triangle keep working through MusicInputHandler.
// Touch screens (PimlrHudLayout sets 'compact'): art + title + progress + one big play/pause button (>= 44 pt); the
// other controls live in the full player on the Pause screen.
//
// Unreal note: WBP_HudMusicPlayer bound to the music subsystem's OnTrackChanged / OnPlayStateChanged; the Boost panel is
// a child widget with a slide animation, shown from the boost subsystem's ActiveBoostChanged delegate.

using DG.Tweening;
using PIMLR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PimlrHudMusicPlayer : MonoBehaviour
{
    public static PimlrHudMusicPlayer Instance { get; private set; }

    [Header("Icons (copied from the old player by the Slice 4b builder)")]
    public Sprite shuffleSprite;
    public Sprite previousSprite, playSprite, pauseSprite, nextSprite, repeatSprite;

    [Header("Layout (HUD canvas units, 1280x720)")]
    public float width = 440f;
    [Tooltip("Touch layout: no shuffle / skip / repeat buttons, one big play/pause. Set by PimlrHudLayout.")]
    public bool compact;
    [Tooltip("Play/pause size in compact mode; PimlrHudLayout raises it to the 44 pt touch minimum.")]
    public float compactPlaySize = 64f;

    public const float Pad = 12f, BoostHeight = 64f, BoostInset = 16f;
    public float BodyHeight { get; private set; } = 112f;

    [SerializeField, HideInInspector] RectTransform body, boostPanel, progressBack;
    [SerializeField, HideInInspector] Image art, artBack, progressFill, frameSprite, boostIcon;
    [SerializeField, HideInInspector] GameObject frameEdges;
    [SerializeField, HideInInspector] TextMeshProUGUI title, subtitle, boostLine, boostInfo;
    [SerializeField, HideInInspector] Button shuffleBtn, prevBtn, playBtn, nextBtn, repeatBtn;
    [SerializeField, HideInInspector] CanvasGroup group, boostGroup;

    bool boostShown;
    AudioClip shownClip;
    bool laidOutCompact;
    float laidOutWidth = -1f, laidOutPlay = -1f;

    MusicSystem Ms => SceneManagerScript.Instance ? SceneManagerScript.Instance.musicSystem : null;

    void OnEnable()
    {
        Instance = this;
        if (!body) Build();
        boostShown = false;
        PlaceBoost(false, instant: true);
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
        if (boostPanel) boostPanel.DOKill();
        if (boostGroup) boostGroup.DOKill();
    }

    // ------------------------------------------------------------------ build (editor builder or first run)

    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        group = GetComponent<CanvasGroup>(); if (!group) group = gameObject.AddComponent<CanvasGroup>(); // (no ?? on Unity objects)

        // Boost panel first, so it sits behind the player and slides out from under it.
        boostPanel = PimlrHudKit.Img("Boost Panel", transform, ColorToken.SurfaceBase, 0.92f).rectTransform;
        boostGroup = boostPanel.gameObject.AddComponent<CanvasGroup>();
        boostGroup.interactable = false; boostGroup.blocksRaycasts = false;
        PimlrHudKit.Edges("Edges", boostPanel, ColorToken.HealthGreen, 2f, top: false);
        boostIcon = PimlrHudKit.Img("Boost Icon", boostPanel, ColorToken.HealthGreen, 1f, SpriteSlot.BoostIcon);
        boostIcon.preserveAspect = true;
        boostLine = PimlrHudKit.Txt("Boost Name", boostPanel, TypeRole.Body, 25.5f, ColorToken.TextPrimary, TextCase.Original);
        boostInfo = PimlrHudKit.Txt("Boost Effect", boostPanel, TypeRole.Body, 24.4f, ColorToken.TextPrimary, TextCase.Original);

        body = PimlrHudKit.Img("Body", transform, ColorToken.SurfaceBase, 0.92f).rectTransform;
        frameSprite = PimlrHudKit.Img("Frame (sprite slot)", body, ColorToken.HealthGreen, 1f, SpriteSlot.HudPlayerFrame);
        PimlrHudKit.Stretch(frameSprite.rectTransform);
        frameEdges = PimlrHudKit.Edges("Frame (edges)", body, ColorToken.HealthGreen, 2f);

        artBack = PimlrHudKit.Img("Album Back", body, ColorToken.SurfaceRaised, 1f);
        art = PimlrHudKit.Icon("Album Art", artBack.transform, null);
        art.color = Color.white;
        PimlrHudKit.Stretch(art.rectTransform);

        title = PimlrHudKit.Txt("Track", body, TypeRole.Heading, 27f, ColorToken.TextPrimary, TextCase.Original);
        subtitle = PimlrHudKit.Txt("Details", body, TypeRole.Body, 24.4f, ColorToken.TextSecondary, TextCase.Original);

        shuffleBtn = MakeButton("Shuffle", shuffleSprite, () => { var m = Ms; if (m) m.OnShuffleBtnClick(); });
        prevBtn = MakeButton("Previous", previousSprite, () => { var m = Ms; if (m) m.ChangeMusicBackward(); });
        playBtn = MakeButton("Play Pause", pauseSprite, () => { var m = Ms; if (m) m.PlayMusic(); });
        nextBtn = MakeButton("Next", nextSprite, () => { var m = Ms; if (m) m.ChangeMusicForward(); });
        repeatBtn = MakeButton("Repeat", repeatSprite, () => { var m = Ms; if (m) m.OnRepeateBtnClick(); });

        progressBack = PimlrHudKit.Img("Progress", body, ColorToken.StateDisabled, 1f).rectTransform;
        progressFill = PimlrHudKit.Img("Fill", progressBack, ColorToken.HealthGreen, 1f);
        var fr = progressFill.rectTransform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = fr.offsetMax = Vector2.zero;

        laidOutWidth = -1f;
        Layout();
        PlaceBoost(false, instant: true);
    }

    Button MakeButton(string name, Sprite sprite, UnityEngine.Events.UnityAction action)
    {
        var hit = PimlrHudKit.Img(name, body, ColorToken.Clear, 1f);
        hit.raycastTarget = true;
        var icon = PimlrHudKit.Icon("Icon", hit.transform, sprite);
        var ir = icon.rectTransform;
        ir.anchorMin = new Vector2(0.15f, 0.15f); ir.anchorMax = new Vector2(0.85f, 0.85f); ir.offsetMin = ir.offsetMax = Vector2.zero;
        var b = hit.gameObject.AddComponent<Button>();
        b.targetGraphic = icon;
        b.navigation = new Navigation { mode = Navigation.Mode.None }; // HUD buttons never take controller focus
        var colors = b.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = PimlrUITheme.C(ColorToken.BrandLime);
        colors.pressedColor = PimlrUITheme.C(ColorToken.HealthGreen);
        colors.selectedColor = Color.white;
        b.colors = colors;
        if (Application.isPlaying) b.onClick.AddListener(action);
        return b;
    }

    // Persistent listeners aren't saved for lambdas, so wire the buttons at runtime.
    void Awake()
    {
        if (!body) return;
        Wire(shuffleBtn, () => { var m = Ms; if (m) m.OnShuffleBtnClick(); });
        Wire(prevBtn, () => { var m = Ms; if (m) m.ChangeMusicBackward(); });
        Wire(playBtn, () => { var m = Ms; if (m) m.PlayMusic(); });
        Wire(nextBtn, () => { var m = Ms; if (m) m.ChangeMusicForward(); });
        Wire(repeatBtn, () => { var m = Ms; if (m) m.OnRepeateBtnClick(); });
    }

    static void Wire(Button b, UnityEngine.Events.UnityAction a)
    {
        if (!b) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(a);
    }

    // ------------------------------------------------------------------ layout

    public void Layout()
    {
        if (!body) return;
        laidOutCompact = compact; laidOutWidth = width; laidOutPlay = compactPlaySize;
        var self = (RectTransform)transform;

        float artSize = compact ? 64f : 88f;
        float play = compact ? Mathf.Max(compactPlaySize, 44f) : 30f;
        BodyHeight = compact ? Mathf.Max(artSize, play) + Pad * 2f : 112f;
        self.sizeDelta = new Vector2(width, BodyHeight);

        PimlrHudKit.Place(body, new Vector2(0f, 1f), Vector2.zero, new Vector2(width, BodyHeight));
        // Frame: Craig's 9-slice art when the theme has it, thin green edges until then.
        bool hasFrame = frameSprite.sprite;
        frameSprite.enabled = hasFrame; // stays active so the theme can still hand it a sprite
        frameEdges.SetActive(!hasFrame);
        PimlrHudKit.Place(artBack.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -(BodyHeight - artSize) * 0.5f), new Vector2(artSize, artSize));

        float textX = Pad + artSize + 14f;
        float textW = width - textX - Pad - (compact ? play + 8f : 0f);
        PimlrHudKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(textX, compact ? -(Pad + 2f) : -8f), new Vector2(textW, 28f));
        PimlrHudKit.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(textX, compact ? -(Pad + 30f) : -36f), new Vector2(textW, 22f));
        PimlrHudKit.Place(progressBack, new Vector2(0f, 1f), new Vector2(textX, -(BodyHeight - Pad - 4f)), new Vector2(textW, 4f));

        var row = new[] { shuffleBtn, prevBtn, playBtn, nextBtn, repeatBtn };
        if (compact)
        {
            foreach (var b in row) if (b) b.gameObject.SetActive(b == playBtn);
            PimlrHudKit.Place((RectTransform)playBtn.transform, new Vector2(1f, 1f), new Vector2(-Pad, -(BodyHeight - play) * 0.5f), new Vector2(play, play));
        }
        else
        {
            float slot = textW / row.Length;
            for (int i = 0; i < row.Length; i++)
            {
                if (!row[i]) continue;
                row[i].gameObject.SetActive(true);
                float s = row[i] == playBtn ? 34f : 30f;
                PimlrHudKit.Place((RectTransform)row[i].transform, new Vector2(0f, 1f),
                    new Vector2(textX + slot * (i + 0.5f) - s * 0.5f, -62f - (34f - s) * 0.5f), new Vector2(s, s));
            }
        }

        // Boost panel: same left/right inset in both layouts.
        boostPanel.anchorMin = boostPanel.anchorMax = boostPanel.pivot = new Vector2(0f, 1f);
        boostPanel.sizeDelta = new Vector2(width - BoostInset * 2f, BoostHeight);
        bool hasIcon = boostIcon.sprite;
        boostIcon.gameObject.SetActive(hasIcon);
        PimlrHudKit.Place(boostIcon.rectTransform, new Vector2(0f, 1f), new Vector2(10f, -12f), new Vector2(40f, 40f));
        float bx = hasIcon ? 58f : 14f, bw = width - BoostInset * 2f - bx - 12f;
        PimlrHudKit.Place(boostLine.rectTransform, new Vector2(0f, 1f), new Vector2(bx, -6f), new Vector2(bw, 28f));
        PimlrHudKit.Place(boostInfo.rectTransform, new Vector2(0f, 1f), new Vector2(bx, -34f), new Vector2(bw, 24f));
        PlaceBoost(boostShown, instant: true);
    }

    // ------------------------------------------------------------------ boost

    public void ShowBoost(string boostName, string effect)
    {
        if (!body) return;
        boostLine.text = $"<color={PimlrHudKit.Hex(ColorToken.TextSecondary)}>Boost:</color> <b><color={PimlrHudKit.Hex(ColorToken.HealthGreen)}>{boostName}</color></b>";
        boostInfo.text = effect ?? "";
        boostShown = true;
        PlaceBoost(true, instant: false);
    }

    public void HideBoost()
    {
        if (!boostShown) return;
        boostShown = false;
        PlaceBoost(false, instant: false);
    }

    public bool BoostShown => boostShown;

    void PlaceBoost(bool shown, bool instant)
    {
        if (!boostPanel) return;
        float x = BoostInset;
        float yShown = -(BodyHeight - 2f), yHidden = -BodyHeight + BoostHeight;
        boostPanel.DOKill(); boostGroup.DOKill();
        if (instant || !Application.isPlaying)
        {
            boostPanel.anchoredPosition = new Vector2(x, shown ? yShown : yHidden);
            boostGroup.alpha = shown ? 1f : 0f;
            return;
        }
        float t = shown ? 0.3f : 0.2f;
        boostPanel.DOAnchorPos(new Vector2(x, shown ? yShown : yHidden), t).SetEase(shown ? Ease.OutCubic : Ease.InCubic).SetUpdate(true);
        boostGroup.DOFade(shown ? 1f : 0f, t).SetUpdate(true);
    }

    // ------------------------------------------------------------------ live data

    void Update()
    {
        if (!body) return;
        if (compact != laidOutCompact || !Mathf.Approximately(width, laidOutWidth) || !Mathf.Approximately(compactPlaySize, laidOutPlay)) Layout();

        var ms = Ms;
        var src = ms ? ms.musicSystem : null;
        bool on = ms && ms.isActiveAndEnabled && src && !PimlrPauseAudio.Open; // the Pause screen has its own Now Playing
        group.alpha = on ? 1f : 0f;
        group.blocksRaycasts = on;
        if (!on) return;

        var clip = src.clip;
        if (clip != shownClip)
        {
            shownClip = clip;
            // Title on the first line, "Artist ft. Guests" on the second (MusicData track info, or the clip name).
            var d = PimlrTrackInfo.Find(clip);
            title.text = d != null ? PimlrTrackInfo.Title(d) : PimlrTrackInfo.Parse(clip ? clip.name : "").title;
            if (d != null) subtitle.text = PimlrTrackInfo.Credit(d);
            else { var pc = PimlrTrackInfo.Parse(clip ? clip.name : ""); subtitle.text = string.IsNullOrEmpty(pc.featured) ? pc.artist : pc.artist + " ft. " + pc.featured; }
            Sprite cover = d != null ? d.albumIcon : null;
            art.sprite = cover;
            art.enabled = cover;
        }

        float len = clip ? clip.length : 0f;
        var fr = progressFill.rectTransform;
        float k = len > 0f ? Mathf.Clamp01(src.time / len) : 0f;
        if (!Mathf.Approximately(fr.anchorMax.x, k)) fr.anchorMax = new Vector2(k, 1f);

        bool playing = src.isPlaying;
        var playIcon = playBtn.targetGraphic as Image;
        var want = playing ? (pauseSprite ? pauseSprite : ms.pauseIcon) : (playSprite ? playSprite : ms.playIcon);
        if (playIcon && want && playIcon.sprite != want) playIcon.sprite = want;
        if (repeatBtn && repeatBtn.targetGraphic)
            repeatBtn.targetGraphic.color = PimlrUITheme.C(src.loop ? ColorToken.BrandLime : ColorToken.TextPrimary);

        if (boostShown && PimlrDeathSequence.IsPlaying) HideBoost(); // game over: the boost goes with the run
    }
}
