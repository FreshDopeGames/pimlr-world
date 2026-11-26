// #UI Slice 3: themed Leaderboards in SceneStaticEU.
// Build: creates Assets/_Game/UI/Prefabs/LeaderboardRow_Themed.prefab (the original LeaderboardRow.prefab is untouched),
//        adds "PIMLR_Leaderboard_v2" (1080p reference frame + card) under Living Room UI/Canvas/Leaderboards,
//        switches OFF the old controls (kept, not deleted; BGShade stays), and points LeaderboardUI's existing fields
//        (buttons, highlights, row holder, row prefab, header/status/your-rank texts) at the new controls.
//        Every original field value and on/off state is recorded in a PimlrUISnapshot for Revert.
// Revert: restores LeaderboardUI's fields and the old controls exactly, removes the new board.
// Data, ranking, online fetching and the period/sort logic stay in LeaderboardUI / LeaderboardManager / PimlrOnline.
//
// Menus: PIMLR > UI > Slice 3: Build Leaderboards / Slice 3: Revert Leaderboards  (SceneStaticEU must be open)

using System.Collections.Generic;
using System.Linq;
using PIMLR.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PimlrLeaderboardSlice3Builder
{
    const string FrameName = "PIMLR_Leaderboard_v2";
    const string Slice = "slice3";
    const string RowPrefabPath = "Assets/_Game/UI/Prefabs/LeaderboardRow_Themed.prefab";

    static LeaderboardUI FindUI()
    {
        var ui = Resources.FindObjectsOfTypeAll<LeaderboardUI>().FirstOrDefault(x => x.gameObject.scene.IsValid() && x.gameObject.scene.name == "SceneStaticEU");
        if (!ui) Debug.LogError("[PIMLR UI] Open SceneStaticEU first.");
        return ui;
    }

    [MenuItem("PIMLR/UI/Slice 3: Build Leaderboards")]
    public static string Build()
    {
        var ui = FindUI();
        if (!ui) return "no LeaderboardUI";
        Revert(false);

        // ---- snapshot: LeaderboardUI's object-reference fields + the controls we switch off ----
        var snap = ui.GetComponent<PimlrUISnapshot>() ?? Undo.AddComponent<PimlrUISnapshot>(ui.gameObject);
        var entry = new PimlrUISnapshot.Entry { slice = Slice, target = ui };
        var so = new SerializedObject(ui);
        var it = so.GetIterator(); it.NextVisible(true);
        while (it.NextVisible(false))
            if (it.propertyType == SerializedPropertyType.ObjectReference) { entry.fieldNames.Add(it.propertyPath); entry.fieldValues.Add(it.objectReferenceValue); }
        foreach (Transform c in ui.transform)
            if (c.gameObject.activeSelf && c.name != "BGShade") { entry.deactivated.Add(c.gameObject); Undo.RecordObject(c.gameObject, "Leaderboards v2"); c.gameObject.SetActive(false); }
        snap.entries.Add(entry);
        EditorUtility.SetDirty(snap);

        var rowPrefab = BuildRowPrefab();

        // ---- board ----
        var frameGo = new GameObject(FrameName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(frameGo, "Leaderboards v2");
        frameGo.transform.SetParent(ui.transform, false);
        var frame = frameGo.AddComponent<PimlrReferenceFrame>(); frame.fillCanvasInPortrait = true; frame.Fit(true);

        var card = Img(frameGo.transform, "L4 Leaderboard Card", ColorToken.SurfaceRaised, 0.96f, SpriteSlot.Panel);
        Stretch(Img(card.transform, "Edge", ColorToken.BrandForest, 1f, SpriteSlot.PanelEdge).rectTransform);
        var ct = card.transform;

        var back = Btn(ct, "BACK", ButtonVariant.Tertiary, out _, ColorToken.BrandLime);
        var title = Txt(ct, "Title", "LEADERBOARDS", TypeRole.Display, 0.62f);
        var yourStats = Txt(ct, "Your Rank", "", TypeRole.Utility, 1f);
        yourStats.alignment = TextAlignmentOptions.MidlineRight; Themed(yourStats).textCase = TextCase.Original;
        Themed(yourStats).overrideColor = true; Themed(yourStats).color = ColorToken.BrandLime; Themed(yourStats).Apply();

        var story = Btn(ct, "STORY MODE", ButtonVariant.Tertiary, out var storyHi, ColorToken.BrandLime);
        var infinite = Btn(ct, "INFINITE MODE", ButtonVariant.Tertiary, out var infHi, ColorToken.AccentInfo);
        var today = Btn(ct, "TODAY", ButtonVariant.Tertiary, out var todayHi, ColorToken.BrandLime);
        var week = Btn(ct, "THIS WEEK", ButtonVariant.Tertiary, out var weekHi, ColorToken.BrandLime);
        var all = Btn(ct, "ALL TIME", ButtonVariant.Tertiary, out var allHi, ColorToken.BrandLime);
        var waves = Btn(ct, "WAVES", ButtonVariant.Tertiary, out var wavesHi, ColorToken.AccentInfo);
        var washes = Btn(ct, "WASHES", ButtonVariant.Tertiary, out var washesHi, ColorToken.AccentInfo);
        var time = Btn(ct, "TIME", ButtonVariant.Tertiary, out var timeHi, ColorToken.AccentInfo);
        foreach (var b in new[] { today, week, all, waves, washes, time }) b.GetComponent<ThemedButton>().motion = ButtonMotion.Compact;

        var header = Txt(ct, "Header", "", TypeRole.Heading, 0.62f);
        header.overflowMode = TextOverflowModes.Ellipsis;

        // list: scroll view with mask, vertical layout, thin themed scrollbar
        var listImg = Img(ct, "Listings", ColorToken.SurfaceBase, 0.6f, SpriteSlot.Card);
        var scroll = listImg.gameObject.AddComponent<ScrollRect>();
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(listImg.transform, false); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = new Vector2(8, 8); viewport.offsetMax = new Vector2(-22, -8);
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
        var vlg = content.GetComponent<VerticalLayoutGroup>(); vlg.spacing = 6; vlg.padding = new RectOffset(2, 2, 2, 2);
        vlg.childControlWidth = vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var track = Img(listImg.transform, "Scrollbar", ColorToken.SurfaceBase, 1f, SpriteSlot.None);
        track.raycastTarget = true;
        var tr = track.rectTransform; tr.anchorMin = new Vector2(1, 0); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(1, 0.5f); tr.offsetMin = new Vector2(-14, 8); tr.offsetMax = new Vector2(-6, -8);
        var handleArea = new GameObject("Sliding Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(track.transform, false); handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one; handleArea.offsetMin = handleArea.offsetMax = Vector2.zero;
        var handle = Img(handleArea, "Handle", ColorToken.BrandLime, 0.85f, SpriteSlot.None); handle.raycastTarget = true;
        Stretch(handle.rectTransform);
        var sb = track.gameObject.AddComponent<Scrollbar>(); sb.direction = Scrollbar.Direction.BottomToTop; sb.handleRect = handle.rectTransform; sb.targetGraphic = handle;
        var sbNav = sb.navigation; sbNav.mode = Navigation.Mode.None; sb.navigation = sbNav; // the cursor moves row by row instead
        scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
        scroll.verticalScrollbar = sb; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        var status = Txt(ct, "Status", "", TypeRole.Heading, 0.8f);
        status.alignment = TextAlignmentOptions.Center; status.textWrappingMode = TextWrappingModes.Normal;
        Themed(status).overrideColor = true; Themed(status).color = ColorToken.TextSecondary; Themed(status).Apply();
        status.gameObject.SetActive(false);

        // ---- wire LeaderboardUI to the new controls (originals are in the snapshot) ----
        var uiSo = new SerializedObject(ui);
        void SetRef(string field, Object value) { var p = uiSo.FindProperty(field); if (p != null) p.objectReferenceValue = value; else Debug.LogWarning("[PIMLR UI] LeaderboardUI field not found: " + field); }
        SetRef("rowHolder", content); SetRef("storyModeRowHolder", content); SetRef("infiniteModeRowHolder", content);
        SetRef("rowPrefab", rowPrefab); SetRef("storyModeRowPrefab", rowPrefab);
        SetRef("backButton", back); SetRef("storyModeButton", story); SetRef("infiniteModeButton", infinite);
        SetRef("storyModeHighlight", storyHi); SetRef("infiniteModeHighlight", infHi);
        SetRef("waveTabButton", waves); SetRef("killsTabButton", washes); SetRef("timeTabButton", time);
        SetRef("waveTabHighlight", wavesHi); SetRef("killsTabHighlight", washesHi); SetRef("timeTabHighlight", timeHi);
        SetRef("todayButton", today); SetRef("weekButton", week); SetRef("allTimeButton", all);
        SetRef("todayHighlight", todayHi); SetRef("weekHighlight", weekHi); SetRef("allTimeHighlight", allHi);
        SetRef("headerText", header); SetRef("statusText", status); SetRef("yourStatsText", yourStats);
        uiSo.ApplyModifiedProperties();

        // ---- cursor flow + responsive layout ----
        var nav = card.gameObject.AddComponent<PimlrLeaderboardNav>();
        nav.back = back; nav.story = story; nav.infinite = infinite; nav.storyActive = storyHi.gameObject; nav.infiniteActive = infHi.gameObject;
        nav.periods = new Selectable[] { today, week, all }; nav.periodActive = new[] { todayHi.gameObject, weekHi.gameObject, allHi.gameObject };
        nav.sorts = new Selectable[] { waves, washes, time }; nav.sortActive = new[] { wavesHi.gameObject, washesHi.gameObject, timeHi.gameObject };
        nav.rowHolder = content; nav.scroll = scroll;
        PimlrNavHint.On(story).defaultPriority = 10; // opening the board focuses STORY MODE

        var resp = card.gameObject.AddComponent<PimlrLeaderboardResponsive>();
        resp.frame = frame; resp.card = card.rectTransform; resp.back = (RectTransform)back.transform; resp.title = title.rectTransform; resp.yourStats = yourStats.rectTransform;
        resp.story = (RectTransform)story.transform; resp.infinite = (RectTransform)infinite.transform;
        resp.periods = new[] { (RectTransform)today.transform, (RectTransform)week.transform, (RectTransform)all.transform };
        resp.sorts = new[] { (RectTransform)waves.transform, (RectTransform)washes.transform, (RectTransform)time.transform };
        resp.header = header.rectTransform; resp.list = listImg.rectTransform; resp.status = status.rectTransform; resp.nav = nav;
        resp.Apply(false);

        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        EditorSceneManager.SaveScene(ui.gameObject.scene);
        return $"built: row prefab {RowPrefabPath}, board wired, {entry.deactivated.Count} old controls switched off, {entry.fieldNames.Count} LeaderboardUI fields recorded";
    }

    [MenuItem("PIMLR/UI/Slice 3: Revert Leaderboards")]
    public static void RevertMenu() => Revert(true);

    public static string Revert(bool save)
    {
        var ui = FindUI();
        if (!ui) return "no LeaderboardUI";
        var frame = ui.transform.Find(FrameName);
        if (frame) Undo.DestroyObjectImmediate(frame.gameObject);
        var snap = ui.GetComponent<PimlrUISnapshot>();
        if (snap)
        {
            foreach (var e in snap.entries.Where(e => e.slice == Slice))
            {
                if (e.target)
                {
                    var so = new SerializedObject(e.target);
                    for (int i = 0; i < e.fieldNames.Count; i++) { var p = so.FindProperty(e.fieldNames[i]); if (p != null) p.objectReferenceValue = e.fieldValues[i]; }
                    so.ApplyModifiedProperties();
                }
                foreach (var g in e.deactivated) if (g) { Undo.RecordObject(g, "Revert Leaderboards v2"); g.SetActive(true); }
            }
            snap.entries.RemoveAll(e => e.slice == Slice);
            if (snap.entries.Count == 0) Undo.DestroyObjectImmediate(snap);
        }
        if (save) { EditorSceneManager.MarkSceneDirty(ui.gameObject.scene); EditorSceneManager.SaveScene(ui.gameObject.scene); }
        return "reverted";
    }

    // ---------------- themed row prefab ----------------

    static LeaderboardRow BuildRowPrefab()
    {
        System.IO.Directory.CreateDirectory("Assets/_Game/UI/Prefabs");
        var root = new GameObject("LeaderboardRow (Themed)", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var bg = root.AddComponent<ThemedImage>(); bg.color = ColorToken.SurfaceBase; bg.alpha = 0.92f; bg.sprite = SpriteSlot.Card;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(1400, 64);
        var le = root.GetComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 64;

        var hi = Img(root.transform, "Highlight (you)", ColorToken.BrandForest, 0.45f, SpriteSlot.Card); Stretch(hi.rectTransform);
        var edge = Img(hi.transform, "Edge", ColorToken.BrandLime, 1f, SpriteSlot.None);
        var er = edge.rectTransform; er.anchorMin = new Vector2(0, 0); er.anchorMax = new Vector2(0, 1); er.pivot = new Vector2(0, 0.5f); er.sizeDelta = new Vector2(6, 0);
        hi.gameObject.SetActive(false);

        var cursorFill = new GameObject("Cursor Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        cursorFill.transform.SetParent(root.transform, false); cursorFill.raycastTarget = false; Stretch(cursorFill.rectTransform);
        var cursorEdge = new GameObject("Cursor Outline", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        cursorEdge.transform.SetParent(root.transform, false); cursorEdge.raycastTarget = false;
        var co = cursorEdge.rectTransform; co.anchorMin = Vector2.zero; co.anchorMax = Vector2.one; co.offsetMin = new Vector2(-3, -3); co.offsetMax = new Vector2(3, 3);
        var th = PimlrUITheme.Current; if (th) { var s = th.Sprite(SpriteSlot.FocusRing); if (s) { cursorEdge.sprite = s; cursorEdge.type = Image.Type.Sliced; cursorEdge.pixelsPerUnitMultiplier = 2.5f; } }

        var badge = Img(root.transform, "Rank Badge", ColorToken.AccentGold, 1f, SpriteSlot.Badge); badge.GetComponent<ThemedImage>().keepColor = true;
        var rank = Txt(root.transform, "Rank", "1", TypeRole.Heading, 0.8f); rank.alignment = TextAlignmentOptions.Center; Themed(rank).keepColor = true;
        var name = Txt(root.transform, "Name", "Player", TypeRole.PlayerName, 1f); name.overflowMode = TextOverflowModes.Ellipsis; Themed(name).keepColor = true;
        var tag = Img(root.transform, "You Tag", ColorToken.BrandLime, 1f, SpriteSlot.Chip);
        var tagT = Txt(tag.transform, "Label", "YOU", TypeRole.Caption, 1f); tagT.alignment = TextAlignmentOptions.Center; Stretch(tagT.rectTransform);
        Themed(tagT).overrideColor = true; Themed(tagT).color = ColorToken.TextOnLime; Themed(tagT).Apply();
        tag.gameObject.SetActive(false);
        var value = Txt(root.transform, "Value", "0:00", TypeRole.Utility, 1.25f);
        var detail = Txt(root.transform, "Detail", "", TypeRole.Caption, 1f); detail.overflowMode = TextOverflowModes.Ellipsis; Themed(detail).textCase = TextCase.Original;

        var layout = root.AddComponent<PimlrLeaderboardRowLayout>();
        layout.badge = badge.rectTransform; layout.rank = rank.rectTransform; layout.nameText = name.rectTransform; layout.youTag = tag.rectTransform;
        layout.value = value.rectTransform; layout.detail = detail.rectTransform; layout.layout = le;

        var listRow = root.AddComponent<PimlrListRow>(); listRow.cursorEdge = cursorEdge; listRow.cursorFill = cursorFill; listRow.targetGraphic = root.GetComponent<Image>();

        var row = root.AddComponent<LeaderboardRow>();
        row.rankText = rank; row.nameText = name; row.valueText = value;
        var rso = new SerializedObject(row);
        rso.FindProperty("highlight").objectReferenceValue = hi;
        rso.FindProperty("detailText").objectReferenceValue = detail;
        rso.FindProperty("rankBadge").objectReferenceValue = badge;
        rso.FindProperty("youTag").objectReferenceValue = tag.gameObject;
        rso.FindProperty("rowLayout").objectReferenceValue = layout;
        rso.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<LeaderboardRow>();
    }

    // ---------------- building blocks (1080p design units) ----------------

    static ThemedText Themed(TMP_Text t) => t.GetComponent<ThemedText>();

    static Button Btn(Transform parent, string label, ButtonVariant v, out Image activeHighlight, ColorToken activeColor)
    {
        var go = new GameObject("Button — " + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(200, 64);
        var btn = go.GetComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();
        // highlight (switched on/off by LeaderboardUI): tinted fill + bar in the mode/period color
        activeHighlight = Img(go.transform, "Active", activeColor, 0.16f, SpriteSlot.Button); Stretch(activeHighlight.rectTransform);
        var bar = Img(activeHighlight.transform, "Bar", activeColor, 1f, SpriteSlot.None);
        var br = bar.rectTransform; br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(0.5f, 0); br.offsetMin = new Vector2(10, 0); br.offsetMax = new Vector2(-10, 5);
        activeHighlight.gameObject.SetActive(false);
        var ring = new GameObject("FocusRing", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        ring.transform.SetParent(go.transform, false);
        var rr = ring.rectTransform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = new Vector2(-5, -5); rr.offsetMax = new Vector2(5, 5);
        var t = Txt(go.transform, "Label", label, TypeRole.Heading, 0.6f); t.alignment = TextAlignmentOptions.Center; Stretch(t.rectTransform);
        var tb = go.AddComponent<ThemedButton>(); tb.variant = v; tb.label = t; tb.focusRing = ring; tb.Apply();
        return btn;
    }

    static Image Img(Transform parent, string name, ColorToken c, float a, SpriteSlot s)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.raycastTarget = false;
        var t = go.AddComponent<ThemedImage>(); t.color = c; t.alpha = a; t.sprite = s; t.Apply();
        return img;
    }

    static TextMeshProUGUI Txt(Transform parent, string name, string text, TypeRole role, float scale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>(); t.text = text; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap; t.alignment = TextAlignmentOptions.MidlineLeft;
        var th = go.AddComponent<ThemedText>(); th.role = role; th.sizeScale = scale; th.Apply();
        return t;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}
