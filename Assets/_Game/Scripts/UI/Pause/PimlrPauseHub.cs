// #UI Slice 5: the Gameplay Pause hub (brief section 9 + Craig's Nov 27 answers).
// Lives on "PIMLR Pause Hub (#UI)" under Global UI > Pause Screen > Overlay Panel, so it's shown exactly while the game is
// paused (JUTPS InventoryUIManager.OpenInventory / ExitInventory still own pausing, timeScale and the cursor).
//   Header     PAUSED, PIMLR Coin balance, resume prompt for the active input
//   Equipment  Primary / Secondary cards (icon, name, ammo); D-pad left / right pages a category with several items
//   Buttons    RESUME, CONTROLS (input-aware list), SETTINGS (the existing PimlrSettingsMenu), QUIT TO LIVING ROOM (confirm)
//   Library    the largest panel: project switcher (L2 / R2), filter (All / Unlocked / Locked), sort; Now Playing (fixed)
//              with scrubber, shuffle / previous / play / next / repeat and ACTIVATE BOOST; the run's parked track; a
//              uniform track grid that scrolls on its own (right stick). Locked tracks unlock here with PIMLR Coin.
// Music: PimlrPauseAudio parks the run's track + base soundtrack on open and restores them on close; the Library plays
// through PimlrLibraryPlayer. Activate Boost cues the track from its start as the run's track and resumes the game.
// Layout: everything sits in a PimlrReferenceFrame (1080p units). Landscape = Library on the right; portrait = Library
// on the lower part of the screen; touch screens get >= 44 pt targets.
// Everything is built at runtime from theme tokens (no prefab edits beyond this object).
//
// Unreal note: WBP_PauseHub (CommonUI activatable widget pushed on the menu layer stack) with child widgets
// WBP_EquipmentCard, WBP_MusicLibrary, WBP_TrackTile; the music side binds to the music subsystem.

using System.Collections.Generic;
using System.Linq;
using JUTPS;
using JUTPS.InventorySystem.UI;
using JUTPS.WeaponSystem;
using PIMLR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PimlrPauseHub : MonoBehaviour
{
    public static PimlrPauseHub Instance { get; private set; }

    enum Filter { All, Unlocked, Locked }
    enum Sort { TrackNumber, Title, Price }

    // ---- built UI ----
    RectTransform frame, header, equip, buttons, library, libHeader, nowPlaying, grid, gridContent, footer, modalLayer;
    TextMeshProUGUI pausedTitle, coinText, resumeHint, footerText;
    Image coinIcon;
    EquipCard primary, secondary;
    Button resumeBtn, controlsBtn, settingsBtn, quitBtn;
    TextMeshProUGUI projectName, projectCount, sortLabel;
    Button projPrev, projNext, sortBtn;
    readonly Dictionary<Filter, Button> filterBtns = new Dictionary<Filter, Button>();
    Image npArt; TextMeshProUGUI npLabel, npTitle, npCredit, npTime, npLength;
    Slider scrub;
    Button shuffleBtn, prevBtn, playBtn, nextBtn, repeatBtn, activateBtn;
    TextMeshProUGUI playLabel, repeatLabel, shuffleLabel, activateLabel;
    RectTransform runCard; TextMeshProUGUI runText;
    ScrollRect gridScroll; GridLayoutGroup gridLayout;
    readonly List<PimlrTrackTile> tiles = new List<PimlrTrackTile>();
    RectTransform toast; TextMeshProUGUI toastText; float toastUntil;

    // ---- state ----
    string project;
    Filter filter = Filter.All;
    Sort sort = Sort.TrackNumber;
    int layoutSig;
    bool built, touch, portrait;
    float refreshTimer, openedAt;
    InventoryUIManager pauseManager;

    // ------------------------------------------------------------------ lifecycle

    void OnEnable()
    {
        Instance = this;
        if (!built) Build();
        pauseManager = GetComponentInParent<InventoryUIManager>(true);
        PimlrPauseAudio.Enter();
        PimlrLibraryPlayer.Changed += OnLibraryChanged;
        PimlrMenuNavigator.ClaimDpadHorizontal = () => isActiveAndEnabled && !ModalOpen;
        openedAt = Time.unscaledTime;

        // Default project: the run's track's project, else the first.
        var runClip = RunClip();
        var runData = PimlrTrackInfo.Find(runClip);
        var projects = PimlrTrackStore.Projects();
        project = PimlrLibraryPlayer.Instance.Project ?? (runData != null ? PimlrTrackInfo.Project(runData) : projects.FirstOrDefault());
        layoutSig = 0;
        CloseModal();
        RebuildGrid();
        RefreshAll();
        PIMLR.Analytics.PimlrAnalytics.Log("library_open", PIMLR.Analytics.AnalyticsData.With("project", project ?? "")
            .Add("run_track", runClip ? runClip.name : ""));
    }

    void OnDisable()
    {
        PimlrLibraryPlayer.Changed -= OnLibraryChanged;
        if (PimlrMenuNavigator.ClaimDpadHorizontal != null) PimlrMenuNavigator.ClaimDpadHorizontal = null;
        PIMLR.Analytics.PimlrAnalytics.Log("library_close", PIMLR.Analytics.AnalyticsData.With("seconds", Mathf.Round(Time.unscaledTime - openedAt))
            .Add("library_track", PimlrLibraryPlayer.Instance.Current != null ? PimlrLibraryPlayer.Instance.Current.audioClip.name : ""));
        PimlrPauseAudio.Exit();
        if (Instance == this) Instance = null;
    }

    void OnLibraryChanged() => RefreshNowPlaying();

    // ------------------------------------------------------------------ build

    void Build()
    {
        built = true;
        FitToCanvas();

        var dimBg = PimlrHudKit.Img("Dim", transform, ColorToken.SurfaceScrim, 0.85f);
        PimlrHudKit.Stretch(dimBg.rectTransform);
        dimBg.raycastTarget = true; // clicks never fall through to the old overlay (which resumes)

        frame = PimlrHudKit.Rect("Frame (1080p)", transform);
        frame.gameObject.AddComponent<PimlrReferenceFrame>().fillCanvasInPortrait = true;

        // header
        header = PimlrHudKit.Rect("Header", frame);
        pausedTitle = PimlrHudKit.Txt("PAUSED", header, TypeRole.Display, 64f, ColorToken.TextPrimary, TextCase.Upper);
        pausedTitle.text = "PAUSED";
        var coinChip = PimlrHudKit.Img("Coins", header, ColorToken.SurfaceBase, 1f).rectTransform;
        coinIcon = PimlrHudKit.Img("Coin", coinChip, ColorToken.AccentGold, 1f);
        var coinSprite = FindCoinSprite(); if (coinSprite) { coinIcon.sprite = coinSprite; coinIcon.color = Color.white; coinIcon.preserveAspect = true; }
        coinText = PimlrHudKit.Txt("Balance", coinChip, TypeRole.Utility, 30f, ColorToken.TextPrimary, TextCase.Original);
        resumeHint = PimlrHudKit.Txt("Resume Hint", header, TypeRole.Utility, 24.4f, ColorToken.TextSecondary, TextCase.Upper, TextAlignmentOptions.MidlineRight);

        // equipment
        equip = PimlrHudKit.Img("Equipment", frame, ColorToken.SurfaceBase, 0.96f).rectTransform;
        Label("EQUIPMENT", equip);
        primary = new EquipCard(equip, "PRIMARY", this);
        secondary = new EquipCard(equip, "SECONDARY", this);

        // buttons
        buttons = PimlrHudKit.Rect("Buttons", frame);
        resumeBtn = MenuButton("RESUME", buttons, ButtonVariant.Primary, Resume);
        var rh = PimlrNavHint.On(resumeBtn); rh.defaultPriority = 50; rh.isCancel = true;
        controlsBtn = MenuButton("CONTROLS", buttons, ButtonVariant.Secondary, OpenControls);
        settingsBtn = MenuButton("SETTINGS", buttons, ButtonVariant.Secondary, OpenSettings);
        quitBtn = MenuButton("QUIT TO LIVING ROOM", buttons, ButtonVariant.Destructive, ConfirmQuit);

        BuildLibrary();

        footer = PimlrHudKit.Rect("Footer", frame);
        footerText = PimlrHudKit.Txt("Hints", footer, TypeRole.Utility, 24.4f, ColorToken.TextSecondary, TextCase.Original);
        footerText.enableAutoSizing = true;
        PimlrHudKit.Stretch(footerText.rectTransform);

        modalLayer = PimlrHudKit.Rect("Modals", frame);
        PimlrHudKit.Stretch(modalLayer);

        toast = PimlrHudKit.Img("Toast", frame, ColorToken.SurfaceRaised, 1f).rectTransform;
        PimlrHudKit.Edges("Edge", toast, ColorToken.BrandLime, 3f, top: false, right: false, bottom: false);
        toastText = PimlrHudKit.Txt("Text", toast, TypeRole.Body, 26f, ColorToken.TextPrimary, TextCase.Original, TextAlignmentOptions.Center);
        PimlrHudKit.Stretch(toastText.rectTransform, 12f);
        toast.gameObject.SetActive(false);
    }

    void BuildLibrary()
    {
        library = PimlrHudKit.Img("Music Library", frame, ColorToken.SurfaceBase, 0.96f).rectTransform;

        libHeader = PimlrHudKit.Rect("Header", library);
        Label("MUSIC LIBRARY", libHeader);
        projPrev = IconButton("Project Prev", libHeader, "<", () => SwitchProject(-1));
        projectName = PimlrHudKit.Txt("Project", libHeader, TypeRole.Heading, 28f, ColorToken.BrandLime, TextCase.Upper);
        projectName.enableAutoSizing = true;
        projectCount = PimlrHudKit.Txt("Count", libHeader, TypeRole.Utility, 24.4f, ColorToken.TextSecondary, TextCase.Original);
        projNext = IconButton("Project Next", libHeader, ">", () => SwitchProject(+1));
        foreach (Filter f in System.Enum.GetValues(typeof(Filter)))
        {
            var ff = f;
            filterBtns[f] = Chip(f.ToString().ToUpper(), libHeader, () => { filter = ff; RebuildGrid(); RefreshAll(); });
        }
        sortBtn = Chip("SORT", libHeader, () => { sort = (Sort)(((int)sort + 1) % 3); RebuildGrid(); RefreshAll(); });
        sortLabel = sortBtn.GetComponentInChildren<TextMeshProUGUI>();

        // Now Playing (fixed)
        nowPlaying = PimlrHudKit.Rect("Now Playing", library);
        var artBack = PimlrHudKit.Img("Art Back", nowPlaying, ColorToken.SurfaceRaised, 1f);
        npArt = PimlrHudKit.Icon("Art", artBack.transform, null); npArt.color = Color.white; npArt.preserveAspect = false;
        PimlrHudKit.Stretch(npArt.rectTransform);
        PimlrHudKit.Edges("Frame", artBack.transform, ColorToken.HealthGreen, 3f);
        npLabel = PimlrHudKit.Txt("Label", nowPlaying, TypeRole.Caption, 21f, ColorToken.HealthGreen, TextCase.Upper);
        npTitle = PimlrHudKit.Txt("Title", nowPlaying, TypeRole.Heading, 34f, ColorToken.TextPrimary, TextCase.Original);
        npCredit = PimlrHudKit.Txt("Credit", nowPlaying, TypeRole.Body, 26f, ColorToken.TextSecondary, TextCase.Original);
        npTime = PimlrHudKit.Txt("Time", nowPlaying, TypeRole.Utility, 24.4f, ColorToken.TextSecondary, TextCase.Original);
        npLength = PimlrHudKit.Txt("Length", nowPlaying, TypeRole.Utility, 24.4f, ColorToken.TextSecondary, TextCase.Original, TextAlignmentOptions.MidlineRight);
        scrub = BuildSlider(nowPlaying);
        shuffleBtn = IconButton("Shuffle", nowPlaying, "SHUFFLE", () => { var p = PimlrLibraryPlayer.Instance; p.Shuffle = !p.Shuffle; });
        shuffleLabel = shuffleBtn.GetComponentInChildren<TextMeshProUGUI>();
        prevBtn = IconButton("Previous", nowPlaying, "<<", () => PimlrLibraryPlayer.Instance.Previous());
        playBtn = IconButton("Play Pause", nowPlaying, "PLAY", PlayPauseLibrary);
        playLabel = playBtn.GetComponentInChildren<TextMeshProUGUI>();
        nextBtn = IconButton("Next", nowPlaying, ">>", () => PimlrLibraryPlayer.Instance.Next());
        repeatBtn = IconButton("Repeat", nowPlaying, "REPEAT", () => { var p = PimlrLibraryPlayer.Instance; p.RepeatMode = (PimlrLibraryPlayer.Repeat)(((int)p.RepeatMode + 1) % 3); });
        repeatLabel = repeatBtn.GetComponentInChildren<TextMeshProUGUI>();
        activateBtn = MenuButton("ACTIVATE BOOST", nowPlaying, ButtonVariant.Primary, () => ActivateBoost(PimlrLibraryPlayer.Instance.Current));
        activateLabel = activateBtn.GetComponentInChildren<TextMeshProUGUI>();
        { var tt = activateLabel.GetComponent<ThemedText>(); tt.role = TypeRole.Utility; tt.sizeScale = 26f / 24f; tt.Apply(); }

        runCard = PimlrHudKit.Img("Run Track", nowPlaying, ColorToken.SurfaceRaised, 1f).rectTransform;
        runText = PimlrHudKit.Txt("Text", runCard, TypeRole.Body, 24.4f, ColorToken.TextSecondary, TextCase.Original, TextAlignmentOptions.TopLeft);
        runText.textWrappingMode = TextWrappingModes.Normal; runText.overflowMode = TextOverflowModes.Ellipsis;
        PimlrHudKit.Stretch(runText.rectTransform, 14f);

        // Grid (scrolls on its own)
        grid = PimlrHudKit.Rect("Track Grid", library);
        gridScroll = grid.gameObject.AddComponent<ScrollRect>();
        var viewport = PimlrHudKit.Rect("Viewport", grid);
        PimlrHudKit.Stretch(viewport);
        viewport.offsetMax = new Vector2(-18f, 0f); // room for the scroll bar
        viewport.gameObject.AddComponent<RectMask2D>().padding = new Vector4(-8f, -8f, -8f, -8f);
        var vpImg = viewport.gameObject.AddComponent<Image>(); vpImg.color = new Color(0, 0, 0, 0); // wheel / drag target
        gridContent = PimlrHudKit.Rect("Content", viewport);
        gridContent.anchorMin = new Vector2(0f, 1f); gridContent.anchorMax = new Vector2(1f, 1f); gridContent.pivot = new Vector2(0.5f, 1f);
        gridContent.sizeDelta = Vector2.zero;
        gridLayout = gridContent.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.padding = new RectOffset(8, 8, 8, 8);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft; gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        var fitter = gridContent.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        gridScroll.viewport = viewport; gridScroll.content = gridContent;
        gridScroll.horizontal = false; gridScroll.vertical = true;
        gridScroll.movementType = ScrollRect.MovementType.Clamped; gridScroll.scrollSensitivity = 60f;
        var bar = PimlrHudKit.Img("Scrollbar", grid, ColorToken.SurfaceRaised, 1f).rectTransform;
        bar.anchorMin = new Vector2(1f, 0f); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(1f, 0.5f); bar.sizeDelta = new Vector2(8f, 0f); bar.anchoredPosition = Vector2.zero;
        var handle = PimlrHudKit.Img("Handle", bar, ColorToken.TextSecondary, 1f).rectTransform; PimlrHudKit.Stretch(handle);
        var sb = bar.gameObject.AddComponent<Scrollbar>(); sb.direction = Scrollbar.Direction.BottomToTop; sb.handleRect = handle; sb.targetGraphic = handle.GetComponent<Image>();
        sb.navigation = new Navigation { mode = Navigation.Mode.None };
        PimlrNavHint.On(sb).skip = true;
        gridScroll.verticalScrollbar = sb; gridScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    // ------------------------------------------------------------------ layout

    void LateUpdate()
    {
        if (!built) return;
        FitToCanvas();
        var size = frame.rect.size;
        int sig;
        unchecked { sig = PimlrResponsive.Signature() ^ ((int)size.x * 7919) ^ ((int)size.y * 104729); }
        if (sig != layoutSig) { layoutSig = sig; Layout(size); }
    }

    // The old Pause Screen / Overlay Panel rects are bigger than the canvas (sizeDelta 810 x 456 on a stretched rect), so
    // the hub sizes itself to the root canvas instead of its parent.
    void FitToCanvas()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (!canvas) return;
        var c = (RectTransform)canvas.rootCanvas.transform;
        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        if (rt.sizeDelta != c.rect.size) rt.sizeDelta = c.rect.size;
        // ...and is scaled (Pause Screen isn't 1:1), so undo the parents' scale too.
        float parentScale = rt.parent ? Mathf.Abs(rt.parent.lossyScale.x) : 1f;
        float want = parentScale > 0f ? Mathf.Abs(c.lossyScale.x) / parentScale : 1f;
        if (!Mathf.Approximately(rt.localScale.x, want)) rt.localScale = new Vector3(want, want, 1f);
        if ((rt.position - c.position).sqrMagnitude > 0.01f) rt.position = c.position;
    }

    static void At(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }

    void Layout(Vector2 S)
    {
        touch = PimlrResponsive.Touch;
        portrait = PimlrResponsive.Orientation == LayoutOrientation.Portrait;
        var root = GetComponentInParent<Canvas>() ? GetComponentInParent<Canvas>().rootCanvas : null;
        float unitPx = (root ? root.scaleFactor : 1f) * Mathf.Abs(frame.localScale.x);
        float T = touch ? Mathf.Max(64f, PimlrResponsive.MinTouchPx / Mathf.Max(0.01f, unitPx) + 1f) : 0f; // touch minimum in frame units
        float Btn(float desktop) => Mathf.Max(desktop, T);
        float top = (Screen.height - Screen.safeArea.yMax) / Mathf.Max(0.01f, unitPx);

        float M = portrait ? 28f : 48f;
        float headerH = Mathf.Max(84f, Btn(0f));
        At(header, M, 24f + top, S.x - 2f * M, headerH);
        At(pausedTitle.rectTransform, 0f, 0f, 420f, headerH);
        var coinChip = coinText.transform.parent as RectTransform;
        float hintW = portrait ? 0f : 520f;
        At(coinChip, S.x - 2f * M - hintW - 220f - (portrait ? Btn(64f) + 16f : 0f), (headerH - 60f) * 0.5f, 220f, 60f);
        At(coinIcon.rectTransform, 10f, 8f, 44f, 44f);
        At(coinText.rectTransform, 64f, 0f, 146f, 60f);
        resumeHint.gameObject.SetActive(!portrait);
        At(resumeHint.rectTransform, S.x - 2f * M - hintW, 0f, hintW, headerH);

        float bodyTop = 24f + top + headerH + 20f;
        float footerH = touch ? 0f : 48f;
        footer.gameObject.SetActive(!touch);
        At(footer, M, S.y - footerH - 12f, S.x - 2f * M, footerH);

        if (!portrait)
        {
            float leftW = Mathf.Clamp(S.x * 0.3f, 480f, 620f);
            float equipH = touch ? 250f : 300f;
            At(equip, M, bodyTop, leftW, equipH);
            LayoutEquip(leftW, equipH);
            float by = bodyTop + equipH + 20f;
            float bh = Btn(68f);
            if (touch)
            {   // 2 x 2 so four touch-size buttons fit beside a short phone screen
                float bw = (leftW - 12f) * 0.5f;
                At(buttons, M, by, leftW, bh * 2f + 12f);
                At((RectTransform)resumeBtn.transform, 0f, 0f, bw, bh); At((RectTransform)controlsBtn.transform, bw + 12f, 0f, bw, bh);
                At((RectTransform)settingsBtn.transform, 0f, bh + 12f, bw, bh); At((RectTransform)quitBtn.transform, bw + 12f, bh + 12f, bw, bh);
            }
            else
            {
                At(buttons, M, by, leftW, bh * 4f + 36f);
                int i = 0; foreach (var b in new[] { resumeBtn, controlsBtn, settingsBtn, quitBtn }) At((RectTransform)b.transform, 0f, i++ * (bh + 12f), leftW, bh);
            }
            float lx = M + leftW + 28f;
            At(library, lx, bodyTop, S.x - lx - M, S.y - bodyTop - footerH - 24f);
        }
        else
        {
            float w = S.x - 2f * M;
            float equipH = Mathf.Max(240f, T + 80f);
            At(equip, M, bodyTop, w, equipH);
            LayoutEquip(w, equipH);
            float bh = Btn(72f), bw = (w - 24f) / 3f, by = bodyTop + equipH + 16f;
            At(buttons, M, by, w, bh);
            // Resume lives in the header (top-right) in portrait; the other three share a row.
            At((RectTransform)controlsBtn.transform, 0f, 0f, bw, bh); At((RectTransform)settingsBtn.transform, bw + 12f, 0f, bw, bh); At((RectTransform)quitBtn.transform, 2f * (bw + 12f), 0f, bw, bh);
            resumeBtn.transform.SetParent(header, false);
            At((RectTransform)resumeBtn.transform, w - Btn(64f) - 0f, (headerH - Btn(64f)) * 0.5f, Btn(64f) + (touch ? 0f : 120f), Btn(64f));
            float ly = by + bh + 24f;
            At(library, 0f, ly, S.x, S.y - ly);
        }
        if (!portrait && resumeBtn.transform.parent != buttons) { resumeBtn.transform.SetParent(buttons, false); Layout(S); return; }
        LayoutLibrary(library.rect.size.x > 0 ? library.sizeDelta : Vector2.zero, T);
        RebuildGrid();
        RefreshAll();
    }

    void LayoutEquip(float w, float h)
    {
        float pad = 16f, labelH = 36f;
        float cw = (w - pad * 3f) * 0.5f, ch = h - labelH - pad * 1.5f;
        primary.Layout(pad, labelH + 8f, cw, ch);
        secondary.Layout(pad * 2f + cw, labelH + 8f, cw, ch);
    }

    void LayoutLibrary(Vector2 L, float T)
    {
        float pad = 20f;
        float chipH = Mathf.Max(44f, T);
        // header: label | < project count > | filters + sort (portrait: second row)
        float filtersW = 3 * 150f + 240f + 3 * 8f;
        bool twoRows = portrait || 240f + chipH * 2f + Mathf.Clamp(L.x * 0.3f, 220f, 460f) + 100f + 24f + filtersW > L.x - 2f * pad;
        float headH = twoRows ? chipH * 2f + 28f : Mathf.Max(64f, chipH + 16f);
        At(libHeader, pad, 12f, L.x - 2f * pad, headH);
        var lbl = libHeader.Find("Label") as RectTransform;
        At(lbl, 0f, 0f, 230f, chipH);
        float x = 240f;
        At((RectTransform)projPrev.transform, x, 0f, chipH, chipH); x += chipH + 8f;
        float nameW = twoRows ? Mathf.Max(220f, L.x - 2f * pad - x - 4f - 74f - chipH - 8f) : Mathf.Clamp(L.x * 0.3f, 220f, 460f);
        At(projectName.rectTransform, x, 0f, nameW, chipH); x += nameW + 4f;
        At(projectCount.rectTransform, x, 0f, 70f, chipH); x += 74f;
        At((RectTransform)projNext.transform, x, 0f, chipH, chipH);
        float fy = twoRows ? chipH + 12f : 0f;
        float fx = twoRows ? 0f : L.x - 2f * pad - filtersW;
        foreach (var b in filterBtns.Values.Concat(new[] { sortBtn })) { float bw = b == sortBtn ? 240f : 150f; At((RectTransform)b.transform, fx, fy, bw, chipH); fx += bw + 8f; }

        // Now Playing
        float ny = 12f + headH + 12f;
        float ctrl = Mathf.Max(52f, T);
        float art = portrait ? 170f : 180f;
        float npH = Mathf.Max(art, 34f + 44f + 34f + 36f + ctrl + 24f) + 8f;
        At(nowPlaying, pad, ny, L.x - 2f * pad, npH);
        var artBack = nowPlaying.Find("Art Back") as RectTransform;
        At(artBack, 0f, 0f, art, art);
        float tx = art + 24f;
        float runW = portrait || L.x < 1000f ? 0f : 300f;
        runCard.gameObject.SetActive(runW > 0f);
        float tw = L.x - 2f * pad - tx - (runW > 0f ? runW + 20f : 0f);
        At(npLabel.rectTransform, tx, -4f, tw, 34f);
        At(npTitle.rectTransform, tx, 28f, tw, 44f);
        At(npCredit.rectTransform, tx, 72f, tw, 34f);
        At(npTime.rectTransform, tx, 110f, 80f, 30f);
        At((RectTransform)scrub.transform, tx + 86f, 110f, tw - 172f, 30f);
        At(npLength.rectTransform, tx + tw - 80f, 110f, 80f, 30f);
        float cy = 148f, bx = tx;
        foreach (var b in new[] { shuffleBtn, prevBtn, playBtn, nextBtn, repeatBtn })
        {
            float bw = b == shuffleBtn || b == repeatBtn ? Mathf.Max(ctrl, 160f) : (b == playBtn ? Mathf.Max(ctrl, 110f) : ctrl);
            At((RectTransform)b.transform, bx, cy, bw, ctrl); bx += bw + 10f;
        }
        float aw = Mathf.Max(240f, tw - (bx - tx) - 10f);
        if (bx - tx + aw > tw) { At((RectTransform)activateBtn.transform, tx, cy + ctrl + 10f, Mathf.Min(tw, 640f), ctrl); npH += ctrl + 10f; nowPlaying.sizeDelta = new Vector2(nowPlaying.sizeDelta.x, npH); }
        else At((RectTransform)activateBtn.transform, bx, cy, aw, ctrl);
        if (runW > 0f) At(runCard, L.x - 2f * pad - runW, 0f, runW, npH - 8f);

        // grid fills the rest
        float gy = ny + npH + 12f;
        At(grid, pad, gy, L.x - 2f * pad, L.y - gy - pad);
    }

    // ------------------------------------------------------------------ grid

    List<MusicData> VisibleTracks()
    {
        var list = PimlrTrackStore.Tracks(project ?? "");
        if (filter == Filter.Unlocked) list = list.Where(PimlrTrackStore.Owned).ToList();
        else if (filter == Filter.Locked) list = list.Where(d => !PimlrTrackStore.Owned(d)).ToList();
        if (sort == Sort.Title) list = list.OrderBy(d => PimlrTrackInfo.Title(d)).ToList();
        else if (sort == Sort.Price) list = list.OrderBy(d => PimlrTrackStore.Owned(d) ? 0 : d.musicValue).ToList();
        return list;
    }

    void RebuildGrid()
    {
        if (!gridLayout) return;
        float w = ((RectTransform)gridScroll.viewport).rect.width - 16f;
        if (w <= 10f) w = grid.sizeDelta.x - 34f;
        float minCell = touch ? 250f : 220f, gap = 16f;
        int cols = Mathf.Max(2, Mathf.FloorToInt((w + gap) / (minCell + gap)));
        float cw = Mathf.Floor((w - gap * (cols - 1)) / cols);
        gridLayout.constraintCount = cols;
        gridLayout.spacing = new Vector2(gap, gap);
        gridLayout.cellSize = new Vector2(cw, PimlrTrackTile.HeightFor(cw)); // uniform: every tile the same size

        var data = VisibleTracks();
        while (tiles.Count < data.Count)
        {
            var t = PimlrTrackTile.Create(gridContent);
            var tile = t;
            t.onClick.AddListener(() => OnTileSubmit(tile));
            tiles.Add(t);
        }
        for (int i = 0; i < tiles.Count; i++)
        {
            bool on = i < data.Count;
            if (tiles[i].gameObject.activeSelf != on) tiles[i].gameObject.SetActive(on);
            if (!on) continue;
            tiles[i].Bind(data[i]);
            tiles[i].Layout(gridLayout.cellSize);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent);
    }

    void OnTileSubmit(PimlrTrackTile tile)
    {
        var d = tile.Data;
        if (d == null) return;
        var lib = PimlrLibraryPlayer.Instance;
        switch (PimlrTrackStore.State(d, lib.IsPlaying ? lib.Source.clip : null))
        {
            case PimlrTrackState.Playing: lib.TogglePause(); break;
            case PimlrTrackState.Unlocked:
                if (lib.Current == d) lib.TogglePause(); else lib.Play(d, "tap");
                break;
            case PimlrTrackState.CanBuy:
                Confirm("UNLOCK TRACK", $"Unlock <b>{PimlrTrackInfo.Title(d)}</b> by {PimlrTrackInfo.Credit(d)} for <b>{d.musicValue}</b> PIMLR Coin?\nIts boost: <b>{Strip(d.boostname)}</b> - {Strip(d.boostInfo)}",
                    "UNLOCK", () =>
                    {
                        var r = PimlrTrackStore.Buy(d);
                        if (r == PimlrTrackStore.BuyResult.Bought) { Toast($"Unlocked {PimlrTrackInfo.Title(d)}"); lib.Play(d, "unlock"); }
                        else Toast(r == PimlrTrackStore.BuyResult.NeedCoins ? "Not enough PIMLR Coin" : "Can't unlock this track yet");
                        RefreshAll();
                    });
                break;
            case PimlrTrackState.NeedCoins:
                Toast($"You need {d.musicValue - PimlrTrackStore.Coins} more PIMLR Coin to unlock {PimlrTrackInfo.Title(d)}");
                break;
            case PimlrTrackState.NeedAchievement:
                Toast($"{PimlrTrackInfo.Title(d)} unlocks after: {(string.IsNullOrWhiteSpace(d.lockedBtnMsg) ? "an achievement" : d.lockedBtnMsg.Trim())}");
                break;
        }
    }

    static string Strip(string s) => string.IsNullOrEmpty(s) ? "" : System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");

    void SwitchProject(int dir)
    {
        var projects = PimlrTrackStore.Projects();
        if (projects.Count == 0) return;
        int i = Mathf.Max(0, projects.IndexOf(project));
        project = projects[(i + dir + projects.Count) % projects.Count];
        if (gridScroll) gridScroll.verticalNormalizedPosition = 1f;
        RebuildGrid();
        RefreshAll();
    }

    // ------------------------------------------------------------------ actions

    void PlayPauseLibrary()
    {
        var lib = PimlrLibraryPlayer.Instance;
        if (lib.Current == null)
        {   // nothing chosen yet: start the project's first unlocked track
            var first = PimlrTrackStore.Tracks(project ?? "").FirstOrDefault(PimlrTrackStore.Owned);
            if (first != null) lib.Play(first, "play_button");
            return;
        }
        lib.TogglePause();
    }

    void Resume()
    {
        if (pauseManager) pauseManager.ExitInventory();
        else gameObject.SetActive(false);
    }

    /// <summary>Make a Library track the run's track from its start (its boost included) and resume the game.</summary>
    public void ActivateBoost(MusicData d)
    {
        if (d == null || !PimlrTrackStore.Owned(d)) return;
        var ms = SceneManagerScript.Instance ? SceneManagerScript.Instance.musicSystem : null;
        if (!ms) { Toast("No music player in this scene"); return; }
        PIMLR.Analytics.PimlrAnalytics.Log("boost_activate", PimlrMusicAnalytics.TrackData(d).Add("from", "pause_library")
            .Add("boost", d.achievementReward).Add("replaced", RunClip() ? RunClip().name : ""));
        PimlrPauseAudio.Exit(resumeRun: false);
        Resume();
        if (!ms.audioList.Contains(d.audioClip)) ms.audioList.Add(d.audioClip);
        ms.ChangeMusic(d.audioClip.name); // cues from the start and starts its boost (UI_GoalPanel.StartBoost)
    }

    void OpenSettings()
    {
        if (PimlrSettingsMenu.Instance) PimlrSettingsMenu.Instance.Open(gameObject);
    }

    void ConfirmQuit()
    {
        Confirm("QUIT TO LIVING ROOM?", "This run ends and you head back to the Living Room. Progress from this run that hasn't been saved will be lost.",
            "QUIT", () =>
            {
                PimlrPauseAudio.Exit();
                Resume();
                if (SceneManagerScript.Instance) SceneManagerScript.Instance.LoadScene("SceneStaticEU");
            }, destructive: true);
    }

    void OpenControls()
    {
        var modal = Modal("CONTROLS", 1100f, 760f, out var body);
        var scheme = PimlrInput.Scheme;
        var sub = PimlrHudKit.Txt("Scheme", body, TypeRole.Caption, 21f, ColorToken.BrandLime, TextCase.Upper);
        sub.text = scheme == PimlrControlScheme.KeyboardMouse ? "KEYBOARD & MOUSE" : scheme == PimlrControlScheme.PlayStation ? "PLAYSTATION CONTROLLER" : "XBOX CONTROLLER";
        At(sub.rectTransform, 0f, 0f, 600f, 32f);
        var scrollHost = PimlrHudKit.Rect("List", body);
        At(scrollHost, 0f, 44f, body.rect.width > 0 ? body.rect.width : 1020f, 520f);
        var sr = scrollHost.gameObject.AddComponent<ScrollRect>();
        var vp = PimlrHudKit.Rect("Viewport", scrollHost); PimlrHudKit.Stretch(vp); vp.gameObject.AddComponent<RectMask2D>();
        var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(0, 0, 0, 0);
        var content = PimlrHudKit.Rect("Content", vp);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
        var rows = PimlrControlsInfo.RowsFor(scheme);
        float rowH = 56f;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = PimlrHudKit.Img("Row", content, i % 2 == 0 ? ColorToken.SurfaceRaised : ColorToken.SurfaceBase, 1f).rectTransform;
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, rowH); row.anchoredPosition = new Vector2(0f, -i * rowH);
            var key = PimlrHudKit.Txt("Key", row, TypeRole.Utility, 26f, ColorToken.BrandLime, TextCase.Original);
            At(key.rectTransform, 20f, 0f, 260f, rowH); key.text = rows[i].key;
            var act = PimlrHudKit.Txt("Action", row, TypeRole.Body, 26f, ColorToken.TextPrimary, TextCase.Original);
            At(act.rectTransform, 300f, 0f, Mathf.Max(400f, body.sizeDelta.x - 320f), rowH); act.text = rows[i].action; act.enableAutoSizing = true;
        }
        content.sizeDelta = new Vector2(0f, rows.Count * rowH);
        sr.viewport = vp; sr.content = content; sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 60f;
        var back = MenuButton("BACK", body, ButtonVariant.Secondary, CloseModal);
        PimlrNavHint.On(back).isCancel = true; PimlrNavHint.On(back).defaultPriority = 100;
        At((RectTransform)back.transform, 0f, 580f, 260f, Mathf.Max(64f, touch ? 116f : 0f));
    }

    // ------------------------------------------------------------------ modals / toast

    bool ModalOpen => modalLayer && modalLayer.childCount > 0;

    void CloseModal()
    {
        if (!modalLayer) return;
        for (int i = modalLayer.childCount - 1; i >= 0; i--) Destroy(modalLayer.GetChild(i).gameObject);
        if (EventSystem.current && resumeBtn) EventSystem.current.SetSelectedGameObject(null); // navigator re-picks the default
    }

    RectTransform Modal(string title, float w, float h, out RectTransform body)
    {
        CloseModal();
        var dim = PimlrHudKit.Img("Modal", modalLayer, ColorToken.SurfaceBase, 0.85f);
        dim.raycastTarget = true; PimlrHudKit.Stretch(dim.rectTransform);
        var panel = PimlrHudKit.Img("Panel", dim.transform, ColorToken.SurfaceBase, 1f).rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(Mathf.Min(w, frame.rect.width - 40f), h); panel.anchoredPosition = Vector2.zero;
        PimlrHudKit.Edges("Edge", panel, ColorToken.BrandLime, 3f, bottom: false, left: false, right: false);
        var t = PimlrHudKit.Txt("Title", panel, TypeRole.Heading, 40f, ColorToken.TextPrimary, TextCase.Upper);
        t.text = title; At(t.rectTransform, 40f, 28f, panel.sizeDelta.x - 80f, 52f);
        body = PimlrHudKit.Rect("Body", panel);
        At(body, 40f, 100f, panel.sizeDelta.x - 80f, h - 130f);
        return panel;
    }

    void Confirm(string title, string text, string ok, System.Action onOk, bool destructive = false)
    {
        Modal(title, 900f, 380f, out var body);
        var msg = PimlrHudKit.Txt("Message", body, TypeRole.Body, 28f, ColorToken.TextPrimary, TextCase.Original, TextAlignmentOptions.TopLeft);
        msg.textWrappingMode = TextWrappingModes.Normal; msg.text = text;
        At(msg.rectTransform, 0f, 0f, body.sizeDelta.x, 150f);
        float bh = Mathf.Max(64f, touch ? 116f : 0f);
        var yes = MenuButton(ok, body, destructive ? ButtonVariant.Destructive : ButtonVariant.Primary, () => { CloseModal(); onOk(); });
        var no = MenuButton("CANCEL", body, ButtonVariant.Secondary, CloseModal);
        PimlrNavHint.On(no).isCancel = true; PimlrNavHint.On(destructive ? no : yes).defaultPriority = 100; // quitting defaults to Cancel
        At((RectTransform)yes.transform, 0f, body.sizeDelta.y - bh, 300f, bh);
        At((RectTransform)no.transform, 320f, body.sizeDelta.y - bh, 300f, bh);
    }

    void Toast(string text)
    {
        toastText.text = text;
        toast.anchorMin = toast.anchorMax = toast.pivot = new Vector2(0.5f, 0f);
        toast.sizeDelta = new Vector2(Mathf.Min(1000f, frame.rect.width - 60f), 72f);
        toast.anchoredPosition = new Vector2(0f, 90f);
        toast.gameObject.SetActive(true);
        toast.SetAsLastSibling();
        toastUntil = Time.unscaledTime + 3f;
    }

    // ------------------------------------------------------------------ refresh

    void Update()
    {
        if (!built) return;
        if (toast.gameObject.activeSelf && Time.unscaledTime > toastUntil) toast.gameObject.SetActive(false);

        var pad = Gamepad.current;
        if (pad != null && !ModalOpen && !(PimlrSettingsMenu.IsOpen))
        {
            if (pad.leftTrigger.wasPressedThisFrame) SwitchProject(-1);
            if (pad.rightTrigger.wasPressedThisFrame) SwitchProject(+1);
            if (pad.dpad.left.wasPressedThisFrame) PageFocusedEquipment(-1);
            if (pad.dpad.right.wasPressedThisFrame) PageFocusedEquipment(+1);
            // Square / X on a focused unlocked tile = Activate Boost
            var sel = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (pad.buttonWest.wasPressedThisFrame && sel && sel.TryGetComponent(out PimlrTrackTile tile) && tile.Data != null && PimlrTrackStore.Owned(tile.Data))
                ActivateBoost(tile.Data);
        }

        RefreshNowPlaying();
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f) { refreshTimer = 0.25f; RefreshAll(); }
    }

    void PageFocusedEquipment(int dir)
    {
        var sel = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (sel && secondary.Owns(sel)) secondary.Page(dir); else primary.Page(dir);
    }

    void RefreshAll()
    {
        if (!built) return;
        coinText.text = PimlrTrackStore.Coins.ToString("N0");
        var scheme = PimlrInput.Scheme;
        string resumeKey = scheme == PimlrControlScheme.PlayStation ? "OPTIONS" : scheme == PimlrControlScheme.Xbox ? "MENU" : "TAB";
        resumeHint.text = $"<color={PimlrHudKit.Hex(ColorToken.TextPrimary)}>[{resumeKey}]</color> RESUME";
        footerText.text = Hints(scheme);

        var projects = PimlrTrackStore.Projects();
        projectName.text = project ?? "";
        projectCount.text = projects.Count > 0 ? $"{projects.IndexOf(project) + 1}/{projects.Count}" : "";
        projPrev.interactable = projNext.interactable = projects.Count > 1;
        foreach (var kv in filterBtns) Paint(kv.Value, kv.Key == filter);
        sortLabel.text = "SORT: " + (sort == Sort.TrackNumber ? "TRACK #" : sort == Sort.Title ? "TITLE" : "PRICE");

        var lib = PimlrLibraryPlayer.Instance;
        var playing = lib.IsPlaying ? lib.Source.clip : null;
        foreach (var t in tiles) if (t.gameObject.activeSelf) t.Refresh(playing);

        primary.ShowPrimary(); secondary.ShowSecondary();

        // the run's parked track
        var rc = RunClip(); var rd = PimlrTrackInfo.Find(rc);
        if (rc)
        {
            var reward = AuthManager.Instance ? AuthManager.Instance.achievementReward : AchievementReward.Nothing;
            string boost = rd != null && reward != AchievementReward.Nothing ? $"\nBoost: <b><color={PimlrHudKit.Hex(ColorToken.HealthGreen)}>{Strip(rd.boostname)}</color></b>" : "";
            runText.text = $"<size=80%>YOUR RUN'S TRACK</size>\n<color={PimlrHudKit.Hex(ColorToken.TextPrimary)}><b>{(rd != null ? PimlrTrackInfo.Title(rd) : rc.name)}</b></color>{boost}\n<size=85%>Picks up where it left off when you resume.</size>";
        }
        else runText.text = "<size=80%>YOUR RUN'S TRACK</size>\nNone playing";
    }

    void RefreshNowPlaying()
    {
        if (!built) return;
        var lib = PimlrLibraryPlayer.Instance;
        var d = lib.Current;
        npLabel.text = d == null ? "MUSIC LIBRARY" : lib.IsPlaying ? "NOW PLAYING" : "PAUSED";
        npTitle.text = d != null ? PimlrTrackInfo.Title(d) : "Pick a track";
        npCredit.text = d != null ? PimlrTrackInfo.Credit(d) : "Plays through this project while you're paused";
        npArt.sprite = d != null ? d.albumIcon : null; npArt.enabled = npArt.sprite;
        npTime.text = Clock(lib.TimeSeconds); npLength.text = Clock(lib.Length);
        scrub.SetValueWithoutNotify(lib.Time01);
        scrub.interactable = d != null && !touch; // touch: display-only (a 44 pt scrubber doesn't fit the row)
        if (gridScroll && gridScroll.verticalScrollbar) gridScroll.verticalScrollbar.interactable = !touch;
        playLabel.text = lib.IsPlaying ? "PAUSE" : "PLAY";
        shuffleLabel.text = lib.Shuffle ? "SHUFFLE ON" : "SHUFFLE";
        Paint(shuffleBtn, lib.Shuffle);
        repeatLabel.text = lib.RepeatMode == PimlrLibraryPlayer.Repeat.One ? "REPEAT 1" : lib.RepeatMode == PimlrLibraryPlayer.Repeat.All ? "REPEAT ALL" : "REPEAT OFF";
        Paint(repeatBtn, lib.RepeatMode != PimlrLibraryPlayer.Repeat.Off);
        bool canActivate = d != null && PimlrTrackStore.Owned(d) && SceneManagerScript.Instance && SceneManagerScript.Instance.musicSystem && SceneManagerScript.Instance.goalPanel;
        activateBtn.interactable = canActivate;
        activateLabel.text = d != null && !string.IsNullOrEmpty(d.boostname) ? $"ACTIVATE BOOST: {Strip(d.boostname).ToUpper()}" : "ACTIVATE BOOST";
    }

    static string Clock(float s) => s <= 0f ? "0:00" : $"{(int)(s / 60f)}:{(int)(s % 60f):00}";

    static string Hints(PimlrControlScheme s)
    {
        if (s == PimlrControlScheme.KeyboardMouse)
            return "Click a track to play  ·  Mouse wheel scrolls tracks  ·  [ ] previous / next  ·  P play / pause  ·  TAB resume";
        bool ps = s == PimlrControlScheme.PlayStation;
        return $"L-STICK move  ·  R-STICK scroll  ·  D-PAD LEFT / RIGHT weapon  ·  {(ps ? "L2 / R2" : "LT / RT")} project  ·  " +
               $"{(ps ? "X" : "A")} select  ·  {(ps ? "SQUARE" : "X")} activate boost  ·  {(ps ? "CIRCLE" : "B")} back";
    }

    static AudioClip RunClip()
    {
        var ms = SceneManagerScript.Instance ? SceneManagerScript.Instance.musicSystem : null;
        return ms && ms.musicSystem ? ms.musicSystem.clip : null;
    }

    static Sprite FindCoinSprite()
    {
        foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sr.name == "PIMLR Coin" && sr.sprite) return sr.sprite;
        return null;
    }

    // ------------------------------------------------------------------ small widgets

    static void Label(string text, Transform parent)
    {
        var t = PimlrHudKit.Txt("Label", parent, TypeRole.Caption, 21f, ColorToken.TextSecondary, TextCase.Upper);
        t.text = text;
        At(t.rectTransform, 16f, 4f, 400f, 36f);
    }

    Button MenuButton(string text, Transform parent, ButtonVariant variant, UnityEngine.Events.UnityAction onClick)
    {
        var img = PimlrHudKit.Img(text + " Button", parent, ColorToken.SurfaceRaised, 1f, SpriteSlot.Button);
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        var label = PimlrHudKit.Txt("Label", img.transform, TypeRole.Heading, 27f, ColorToken.TextPrimary, TextCase.Upper, TextAlignmentOptions.Center);
        label.text = text;
        label.richText = false; // "<" / ">" labels are glyphs, not tags
        label.enableAutoSizing = true; // ThemedText: role size = max, readability floor = min
        label.overflowMode = TextOverflowModes.Overflow;
        PimlrHudKit.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(8f, 0f); label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        var tb = img.gameObject.AddComponent<ThemedButton>();
        tb.variant = variant; tb.label = label;
        tb.Apply();
        b.onClick.AddListener(onClick);
        return b;
    }

    Button IconButton(string name, Transform parent, string glyph, UnityEngine.Events.UnityAction onClick)
    {
        var b = MenuButton(glyph, parent, ButtonVariant.Secondary, onClick);
        b.name = name;
        var tt = b.GetComponentInChildren<ThemedText>(); tt.role = TypeRole.Utility; tt.sizeScale = 25f / 24f; tt.Apply();
        return b;
    }

    Button Chip(string text, Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        var b = MenuButton(text, parent, ButtonVariant.Tertiary, onClick);
        var l = b.GetComponentInChildren<TextMeshProUGUI>();
        var tt = l.GetComponent<ThemedText>(); tt.role = TypeRole.Utility; tt.sizeScale = 24.4f / 24f; tt.Apply();
        return b;
    }

    static void Paint(Button b, bool on)
    {
        var tb = b ? b.GetComponent<ThemedButton>() : null;
        if (!tb) return;
        var want = on ? ButtonVariant.Primary : ButtonVariant.Tertiary;
        if (tb.variant != want) { tb.variant = want; tb.Apply(); }
    }

    Slider BuildSlider(Transform parent)
    {
        var root = PimlrHudKit.Rect("Scrubber", parent);
        var back = PimlrHudKit.Img("Track", root, ColorToken.StateDisabled, 1f).rectTransform;
        back.anchorMin = new Vector2(0f, 0.5f); back.anchorMax = new Vector2(1f, 0.5f); back.sizeDelta = new Vector2(0f, 6f);
        var fillArea = PimlrHudKit.Rect("Fill Area", root); PimlrHudKit.Stretch(fillArea);
        var fill = PimlrHudKit.Img("Fill", fillArea, ColorToken.HealthGreen, 1f).rectTransform;
        fill.anchorMin = new Vector2(0f, 0.5f); fill.anchorMax = new Vector2(0f, 0.5f); fill.sizeDelta = new Vector2(0f, 6f);
        var handleArea = PimlrHudKit.Rect("Handle Area", root); PimlrHudKit.Stretch(handleArea);
        var handle = PimlrHudKit.Img("Handle", handleArea, ColorToken.TextPrimary, 1f);
        handle.raycastTarget = true; handle.rectTransform.sizeDelta = new Vector2(10f, 26f);
        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.minValue = 0f; s.maxValue = 1f;
        s.navigation = new Navigation { mode = Navigation.Mode.None };
        s.onValueChanged.AddListener(v => PimlrLibraryPlayer.Instance.Seek01(v));
        return s;
    }

    // ------------------------------------------------------------------ equipment card

    class EquipCard
    {
        readonly RectTransform root;
        readonly Image icon;
        readonly TextMeshProUGUI cat, name, ammo, pager;
        readonly Button button;
        int page;

        public EquipCard(Transform parent, string category, PimlrPauseHub hub)
        {
            var bg = PimlrHudKit.Img(category, parent, ColorToken.SurfaceRaised, 1f);
            bg.raycastTarget = true;
            root = bg.rectTransform;
            button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg; button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            cat = PimlrHudKit.Txt("Category", root, TypeRole.Caption, 21f, ColorToken.TextSecondary, TextCase.Upper); cat.text = category;
            icon = PimlrHudKit.Icon("Icon", root, null); icon.color = Color.white;
            name = PimlrHudKit.Txt("Name", root, TypeRole.Utility, 26f, ColorToken.TextPrimary, TextCase.Upper); // narrower than Russo: fits "WATER BALLOONS"
            name.enableAutoSizing = true;
            ammo = PimlrHudKit.Txt("Ammo", root, TypeRole.Utility, 30f, ColorToken.TextPrimary, TextCase.Original);
            pager = PimlrHudKit.Txt("Pager", root, TypeRole.Utility, 24.4f, ColorToken.TextSecondary, TextCase.Original, TextAlignmentOptions.MidlineRight);
            pager.richText = false;
        }

        public bool Owns(GameObject go) => go == button.gameObject;

        public void Layout(float x, float y, float w, float h)
        {
            At(root, x, y, w, h);
            At(cat.rectTransform, 12f, 6f, w - 24f, 34f);
            float iconH = Mathf.Max(40f, h - 40f - 38f - 44f - 12f);
            At(icon.rectTransform, 12f, 42f, w - 24f, iconH);
            At(name.rectTransform, 12f, 42f + iconH + 2f, w - 24f, 38f);
            At(ammo.rectTransform, 12f, h - 42f, w * 0.6f, 36f);
            At(pager.rectTransform, w * 0.5f, h - 42f, w * 0.5f - 12f, 36f);
        }

        List<Weapon> Weapons()
        {
            var pc = JUGameManager.InstancedPlayer;
            var inv = pc ? pc.Inventory : null;
            var list = new List<Weapon>();
            if (inv && inv.HoldableItensRightHand != null)
                foreach (var h in inv.HoldableItensRightHand) if (h && h.Unlocked && h is Weapon w) list.Add(w);
            return list;
        }

        public void Page(int dir) { page += dir; } // display paging; equipping from here comes with the Living Room Armory

        public void ShowPrimary()
        {
            var list = Weapons();
            if (list.Count == 0) { Show(null, "NO WEAPON", "", 0, 0); return; }
            var pc = JUGameManager.InstancedPlayer;
            int held = pc ? list.IndexOf(pc.HoldableItemInUseRightHand as Weapon) : -1;
            int i = ((held >= 0 ? held : 0) + page) % list.Count; if (i < 0) i += list.Count;
            var w = list[i];
            Show(w.ItemIcon, w.ItemName, $"{w.BulletsAmounts}<color={PimlrHudKit.Hex(ColorToken.TextSecondary)}>/{w.TotalBullets}</color>", i + 1, list.Count);
        }

        public void ShowSecondary()
        {
            var g = PimlrPlayerGrenades.Instance;
            var S = PimlrPlayerGrenades.S;
            if (!g) { Show(null, "NONE", "", 0, 0); return; }
            Show(S ? S.hudIcon : null, S && !string.IsNullOrEmpty(S.hudName) ? S.hudName : "WATER GRENADES",
                 $"{g.Count}<color={PimlrHudKit.Hex(ColorToken.TextSecondary)}>/{g.MaxCount}</color>", 1, 1);
        }

        void Show(Sprite s, string n, string a, int index, int count)
        {
            icon.sprite = s; icon.enabled = s;
            name.text = n; ammo.text = a;
            pager.text = count > 1 ? $"< {index}/{count} >" : count == 1 ? "1/1" : "";
        }
    }
}
