// #UI: builds Assets/_Game/UI/Showcase/UI_Showcase.unity, a test bench for the PIMLR UI design system.
// Every element is themed (ThemedImage / ThemedText / ThemedButton), so editing PimlrUITheme.asset or swapping in
// real art re-skins the whole page. All names and numbers on the page are SAMPLE data for layout only; no game
// system is referenced. Re-running the menu rebuilds the scene from scratch.
//
// Menu: PIMLR > UI > Build Showcase Scene

using PIMLR.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class PimlrUIShowcaseBuilder
{
    public const string ScenePath = "Assets/_Game/UI/Showcase/UI_Showcase.unity";

    [MenuItem("PIMLR/UI/Build Showcase Scene")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build(true);
    }

    /// <summary>Builds and saves the showcase. openSingle=false builds it additively and closes it again.</summary>
    public static string Build(bool openSingle)
    {
        System.IO.Directory.CreateDirectory("Assets/_Game/UI/Showcase");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, openSingle ? NewSceneMode.Single : NewSceneMode.Additive);

        var camGo = new GameObject("Main Camera", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = PimlrUITheme.C(ColorToken.SurfaceBase);
        camGo.tag = "MainCamera";
        SceneManager_Move(camGo, scene);

        var canvasGo = new GameObject("Showcase Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager_Move(canvasGo, scene);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGo.transform;

        // Layer 1-3: backdrop, brand texture, scrim
        Stretch(Img(root, "L1 Backdrop", ColorToken.SurfaceBase, 1f, SpriteSlot.None));
        Stretch(Img(root, "L2 Pattern", ColorToken.BrandForest, 0.18f, SpriteSlot.Pattern));
        var halftone = Img(root, "L2 Halftone (hero corner)", ColorToken.BrandLime, 0.07f, SpriteSlot.Halftone);
        Place(halftone.rectTransform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(900, 420));

        // Header: Display (punch preset) + caption + mode chips
        var title = Txt(root, "Title", "PIMLR WORLD", TypeRole.Display, 1.15f);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -36), new Vector2(900, 100));
        var sub = Txt(root, "Subtitle", "UI design system — Direction C hybrid — sample data", TypeRole.Caption, 1f);
        Place(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(68, -138), new Vector2(1100, 30));

        var chips = Row(root, "Mode Chips", 12);
        Place((RectTransform)chips.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-64, -52), new Vector2(560, 48));
        chips.childAlignment = TextAnchor.MiddleRight;
        Chip(chips.transform, "STORY", ColorToken.BrandLime);
        Chip(chips.transform, "INFINITE", ColorToken.AccentInfo);
        Chip(chips.transform, "MUSIC", ColorToken.AccentMusic);

        // Column 1: buttons
        var c1 = Panel(root, "Buttons", "BUTTONS", new Vector2(64, -200), new Vector2(440, 560));
        Button first = Btn(c1, "PLAY STORY", ButtonVariant.Primary, true);
        Btn(c1, "FIT CHECK", ButtonVariant.Secondary, true);
        Btn(c1, "LEADERBOARDS", ButtonVariant.Tertiary, true);
        Btn(c1, "QUIT TO HUB", ButtonVariant.Destructive, true);
        Btn(c1, "SETTINGS", ButtonVariant.Icon, true);
        Btn(c1, "LOCKED", ButtonVariant.Primary, false);

        // Column 2: leaderboard rows (name-first identity)
        var c2 = Panel(root, "Leaderboard", "LEADERBOARD — STORY", new Vector2(536, -200), new Vector2(660, 560));
        LbRow(c2, 1, "SAMPLE PLAYER", "12:43", "87 WASHES", ColorToken.AccentGold, false);
        LbRow(c2, 2, "PLAYER TWO", "13:10", "81 WASHES", ColorToken.TextSecondary, false);
        LbRow(c2, 3, "PLAYER THREE", "14:02", "79 WASHES", ColorToken.AccentWarn, false);
        LbRow(c2, 7, "YOU (SAMPLE)", "16:55", "64 WASHES", ColorToken.BrandLime, true);
        LbRow(c2, 8, "PLAYER EIGHT", "17:20", "60 WASHES", ColorToken.TextSecondary, false);

        // Column 3: HUD pieces
        var c3 = Panel(root, "HUD", "GAMEPLAY HUD", new Vector2(1228, -200), new Vector2(628, 560));
        Bar(c3, "HEALTH", 0.72f, ColorToken.StateDanger);
        Bar(c3, "STAMINA", 0.45f, ColorToken.BrandLime);
        Bar(c3, "WAVE PROGRESS", 0.6f, ColorToken.AccentInfo);
        Weapon(c3);
        BoostCard(c3);

        // Bottom: type ramp + color swatches
        var ramp = Panel(root, "Type Ramp", "TYPE ROLES", new Vector2(64, -792), new Vector2(1132, 240));
        var rampRow = Row(ramp, "Roles", 28);
        LE(rampRow.gameObject, -1, 140);
        Sample(rampRow.transform, "DISPLAY", "WASHED", TypeRole.Display, 0.75f);
        Sample(rampRow.transform, "HEADING", "HATER FREE", TypeRole.Heading, 1f);
        Sample(rampRow.transform, "BODY", "Protect the fans.", TypeRole.Body, 1f);
        Sample(rampRow.transform, "UTILITY", "1,250  ·  24 / 120", TypeRole.Utility, 1f);

        var sw = Panel(root, "Swatches", "COLOR TOKENS", new Vector2(1228, -792), new Vector2(628, 240));
        var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<GridLayoutGroup>();
        grid.transform.SetParent(sw, false);
        grid.cellSize = new Vector2(60, 60); grid.spacing = new Vector2(10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 8;
        LE(grid.gameObject, -1, 140);
        foreach (ColorToken t in System.Enum.GetValues(typeof(ColorToken)))
        {
            if (t == ColorToken.Clear || t == ColorToken.White) continue;
            var s = Img(grid.transform, t.ToString(), t, 1f, SpriteSlot.Card);
            var tip = Txt(s.transform, "Label", Short(t), TypeRole.Caption, 0.55f);
            tip.GetComponent<ThemedText>().overrideColor = true;
            tip.GetComponent<ThemedText>().color = t == ColorToken.BrandLime || t == ColorToken.AccentGold || t == ColorToken.TextPrimary || t == ColorToken.TextSecondary || t == ColorToken.AccentInfo ? ColorToken.TextOnLime : ColorToken.TextPrimary;
            tip.GetComponent<ThemedText>().Apply();
            tip.alignment = TextAlignmentOptions.BottomLeft; tip.margin = new Vector4(5, 0, 0, 4);
            Stretch(tip);
        }

        // Toast (overlay layer)
        var toast = Img(root, "L8 Toast", ColorToken.SurfaceScrim, 1f, SpriteSlot.Panel);
        Place(toast.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(170, -40), new Vector2(460, 92));
        var tt = Txt(toast.transform, "Text", "WAVE 3 CLEARED", TypeRole.Display, 0.6f);
        tt.alignment = TextAlignmentOptions.Center; Stretch(tt);

        // Event system: new Input System module (matches the game), first selection for controller testing.
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager_Move(es, scene);
        es.GetComponent<EventSystem>().firstSelectedGameObject = first.gameObject;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (!openSingle) EditorSceneManager.CloseScene(scene, true);
        return ScenePath;
    }

    // ---------------- building blocks ----------------

    static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene s) =>
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, s);

    static Image Img(Transform parent, string name, ColorToken color, float alpha, SpriteSlot slot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        var th = go.AddComponent<ThemedImage>();
        th.color = color; th.alpha = alpha; th.sprite = slot;
        th.Apply();
        return img;
    }

    static TextMeshProUGUI Txt(Transform parent, string name, string text, TypeRole role, float scale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        var th = go.AddComponent<ThemedText>();
        th.role = role; th.sizeScale = scale;
        th.Apply();
        return t;
    }

    static Transform Panel(Transform root, string name, string heading, Vector2 pos, Vector2 size)
    {
        var bg = Img(root, "L4 Panel — " + name, ColorToken.SurfaceRaised, 0.94f, SpriteSlot.Panel);
        Place(bg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), pos, size);
        var edge = Img(bg.transform, "Edge", ColorToken.BrandForest, 1f, SpriteSlot.PanelEdge);
        Stretch(edge);
        // L6 accent: lime corner tag on the cut corner
        var tag = Img(bg.transform, "L6 Accent Tag", ColorToken.BrandLime, 1f, SpriteSlot.None);
        Place(tag.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -10), new Vector2(40, 6));
        tag.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);

        var content = new GameObject("L5 Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(bg.transform, false);
        Stretch((RectTransform)content.transform);
        var v = content.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(24, 24, 20, 24); v.spacing = 12;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var h = Txt(content.transform, "Heading", heading, TypeRole.Heading, 0.78f);
        LE(h.gameObject, -1, 40);
        var rule = Img(content.transform, "Rule", ColorToken.BrandLime, 1f, SpriteSlot.None);
        LE(rule.gameObject, -1, 3);
        return content.transform;
    }

    static Button Btn(Transform parent, string label, ButtonVariant variant, bool interactable)
    {
        var go = new GameObject("Button — " + variant + (interactable ? "" : " (disabled)"), typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        LE(go, -1, 56);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        btn.interactable = interactable;

        var ring = new GameObject("FocusRing", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        ring.transform.SetParent(go.transform, false);
        var rr = ring.rectTransform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = new Vector2(-6, -6); rr.offsetMax = new Vector2(6, 6);

        var t = Txt(go.transform, "Label", label, TypeRole.Heading, 0.6f);
        t.alignment = TextAlignmentOptions.Center; Stretch(t);

        var th = go.AddComponent<ThemedButton>();
        th.variant = variant; th.label = t; th.focusRing = ring;
        th.Apply();
        return btn;
    }

    static void Chip(Transform parent, string label, ColorToken color)
    {
        var c = Img(parent, "Chip — " + label, color, 1f, SpriteSlot.Chip);
        LE(c.gameObject, 150, 44);
        var t = Txt(c.transform, "Label", label, TypeRole.Caption, 0.9f);
        var tt = t.GetComponent<ThemedText>(); tt.overrideColor = true; tt.color = ColorToken.TextOnLime; tt.Apply();
        t.alignment = TextAlignmentOptions.Center; Stretch(t);
    }

    static void LbRow(Transform parent, int rank, string name, string time, string stat, ColorToken rankColor, bool current)
    {
        var row = Img(parent, $"Row {rank}", current ? ColorToken.BrandForest : ColorToken.SurfaceBase, current ? 0.55f : 0.9f, SpriteSlot.Card);
        LE(row.gameObject, -1, 72);
        if (current)
        {
            var edge = Img(row.transform, "Current Edge", ColorToken.BrandLime, 1f, SpriteSlot.None);
            Place(edge.rectTransform, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(6, 0));
            edge.rectTransform.pivot = new Vector2(0, 0.5f);
        }
        var badge = Img(row.transform, "Rank Badge", rankColor, rank <= 3 ? 1f : 0f, rank <= 3 ? SpriteSlot.Badge : SpriteSlot.None);
        Place(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(60, 60));
        badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        var rn = Txt(badge.transform, "Rank", rank.ToString(), TypeRole.Heading, rank <= 3 ? 0.8f : 0.9f);
        var rt = rn.GetComponent<ThemedText>(); rt.overrideColor = true; rt.color = rank <= 3 ? ColorToken.TextOnLime : rankColor; rt.Apply();
        rn.alignment = TextAlignmentOptions.Center; Stretch(rn);

        var n = Txt(row.transform, "Name", name, TypeRole.Heading, 0.75f);
        if (current) { var nt = n.GetComponent<ThemedText>(); nt.overrideColor = true; nt.color = ColorToken.BrandLime; nt.Apply(); }
        Place(n.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(92, 0), new Vector2(300, 40));
        n.rectTransform.pivot = new Vector2(0, 0.5f);
        var tm = Txt(row.transform, "Time", time, TypeRole.Utility, 1.15f);
        tm.alignment = TextAlignmentOptions.MidlineRight;
        Place(tm.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(110, 40));
        tm.rectTransform.pivot = new Vector2(1, 0.5f);
        var st = Txt(row.transform, "Stat", stat, TypeRole.Caption, 0.85f);
        st.alignment = TextAlignmentOptions.MidlineRight;
        Place(st.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(126, 40));
        st.rectTransform.pivot = new Vector2(1, 0.5f);
    }

    static void Bar(Transform parent, string label, float value, ColorToken color)
    {
        var holder = new GameObject("Bar — " + label, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        LE(holder, -1, 52);
        var l = Txt(holder.transform, "Label", label, TypeRole.Caption, 0.85f);
        Place(l.rectTransform, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 22));
        l.rectTransform.pivot = new Vector2(0.5f, 1);
        var v = Txt(holder.transform, "Value", Mathf.RoundToInt(value * 100) + "%", TypeRole.Utility, 0.8f);
        v.alignment = TextAlignmentOptions.MidlineRight;
        Place(v.rectTransform, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 22));
        v.rectTransform.pivot = new Vector2(0.5f, 1);
        var back = Img(holder.transform, "Back", ColorToken.SurfaceBase, 1f, SpriteSlot.BarBack);
        Place(back.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), new Vector2(0, 22));
        back.rectTransform.pivot = new Vector2(0.5f, 0);
        var fill = Img(back.transform, "Fill", color, 1f, SpriteSlot.BarFill);
        fill.GetComponent<ThemedImage>().autoImageType = false;
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = value;
        var fr = fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(4, 4); fr.offsetMax = new Vector2(-4, -4);
    }

    static void Weapon(Transform parent)
    {
        var w = Img(parent, "Weapon Block", ColorToken.SurfaceBase, 0.9f, SpriteSlot.Card);
        LE(w.gameObject, -1, 76);
        var n = Txt(w.transform, "Weapon", "HYDRO BLASTER", TypeRole.Heading, 0.7f);
        Place(n.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 10), new Vector2(320, 34));
        n.rectTransform.pivot = new Vector2(0, 0.5f);
        var c = Txt(w.transform, "Secondary", "WATER BALLOONS × 3", TypeRole.Caption, 0.8f);
        Place(c.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, -18), new Vector2(320, 24));
        c.rectTransform.pivot = new Vector2(0, 0.5f);
        var a = Txt(w.transform, "Ammo", "24 / 120", TypeRole.Utility, 1.5f);
        a.alignment = TextAlignmentOptions.MidlineRight;
        Place(a.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(200, 50));
        a.rectTransform.pivot = new Vector2(1, 0.5f);
    }

    static void BoostCard(Transform parent)
    {
        var card = Img(parent, "Track Boost Card (sample)", ColorToken.SurfaceBase, 0.95f, SpriteSlot.Card);
        LE(card.gameObject, -1, 112);
        var edge = Img(card.transform, "Music Edge", ColorToken.AccentMusic, 1f, SpriteSlot.None);
        Place(edge.rectTransform, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(6, 0));
        edge.rectTransform.pivot = new Vector2(0, 0.5f);
        var art = Img(card.transform, "Album Art Slot", ColorToken.AccentMusic, 0.35f, SpriteSlot.Card);
        Place(art.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(80, 80));
        art.rectTransform.pivot = new Vector2(0, 0.5f);
        var h = Txt(card.transform, "Header", "TRACK BOOST ACTIVE", TypeRole.Caption, 0.8f);
        var ht = h.GetComponent<ThemedText>(); ht.overrideColor = true; ht.color = ColorToken.AccentMusic; ht.Apply();
        Place(h.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(118, -14), new Vector2(-136, 22));
        h.rectTransform.pivot = new Vector2(0, 1);
        var t = Txt(card.transform, "Title", "SAMPLE TRACK — SAMPLE ARTIST", TypeRole.Heading, 0.62f);
        Place(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(118, -38), new Vector2(-136, 30));
        t.rectTransform.pivot = new Vector2(0, 1);
        var i = Txt(card.transform, "Effect", "Boost effect text comes from the music system", TypeRole.Body, 0.8f);
        var it = i.GetComponent<ThemedText>(); it.overrideColor = true; it.color = ColorToken.TextSecondary; it.Apply();
        Place(i.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(118, -72), new Vector2(-136, 26));
        i.rectTransform.pivot = new Vector2(0, 1);
    }

    static void Sample(Transform parent, string role, string text, TypeRole r, float scale)
    {
        var col = new GameObject(role, typeof(RectTransform), typeof(VerticalLayoutGroup));
        col.transform.SetParent(parent, false);
        var v = col.GetComponent<VerticalLayoutGroup>();
        v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.spacing = 6;
        var cap = Txt(col.transform, "Role", role, TypeRole.Caption, 0.8f);
        var ct = cap.GetComponent<ThemedText>(); ct.overrideColor = true; ct.color = ColorToken.BrandLime; ct.Apply();
        LE(cap.gameObject, -1, 22);
        var s = Txt(col.transform, "Sample", text, r, scale);
        LE(s.gameObject, -1, 72);
    }

    static string Short(ColorToken t)
    {
        switch (t)
        {
            case ColorToken.SurfaceBase: return "BASE";
            case ColorToken.SurfaceRaised: return "RAISED";
            case ColorToken.SurfaceScrim: return "SCRIM";
            case ColorToken.BrandForest: return "FOREST";
            case ColorToken.BrandBrick: return "BRICK";
            case ColorToken.BrandLime: return "LIME";
            case ColorToken.TextPrimary: return "TEXT";
            case ColorToken.TextSecondary: return "TEXT 2";
            case ColorToken.TextOnLime: return "ON LIME";
            case ColorToken.AccentMusic: return "MUSIC";
            case ColorToken.AccentInfo: return "INFO";
            case ColorToken.AccentWarn: return "WARN";
            case ColorToken.AccentGold: return "GOLD";
            case ColorToken.StateDanger: return "DANGER";
            case ColorToken.StateDisabled: return "OFF";
            default: return t.ToString();
        }
    }

    static HorizontalLayoutGroup Row(Transform parent, string name, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = spacing; h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false; h.childAlignment = TextAnchor.UpperLeft;
        return h;
    }

    static void LE(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (w > 0) { le.preferredWidth = w; le.minWidth = w; }
        if (h > 0) { le.preferredHeight = h; le.minHeight = h; }
    }

    static void Place(RectTransform r, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
    }

    static void Stretch(Graphic g) => Stretch(g.rectTransform);
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}
