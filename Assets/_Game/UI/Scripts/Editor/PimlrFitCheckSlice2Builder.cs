// #UI Slice 2: themed Fit Check in SceneStaticEU.
// Build: adds "PIMLR_FitCheck_v2" under Menu/PIMLR_CharacterCreate (a 1080p reference frame holding one compact card:
//        icon tabs + PimlrSwatchGrid + color name + SAVED feedback), switches OFF the old BodyPartCard / ColorCard
//        (kept, not deleted), themes the existing Save_Btn and FIT CHECK title in place, and wires the controller.
// Revert: removes all of that and turns the old cards back on.
// Save logic is untouched: picks still go through PimlrCharacterCreateController.SelectColor -> changeColor.SetColor,
// and Save_Btn keeps its Inspector listener (CharaterCreatePanel.OnAvaterDoneClick).
//
// Menus: PIMLR > UI > Slice 2: Build Fit Check / Slice 2: Revert Fit Check  (SceneStaticEU must be open)

using System.Linq;
using PIMLR.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PimlrFitCheckSlice2Builder
{
    const string FrameName = "PIMLR_FitCheck_v2";

    static PimlrCharacterCreateController FindController()
    {
        var c = Resources.FindObjectsOfTypeAll<PimlrCharacterCreateController>().FirstOrDefault(x => x.gameObject.scene.IsValid() && x.gameObject.scene.name == "SceneStaticEU");
        if (!c) Debug.LogError("[PIMLR UI] Open SceneStaticEU first.");
        return c;
    }

    static Transform Panel(PimlrCharacterCreateController c) => c.GetComponentsInParent<Transform>(true).First(t => t.name == "CharaterCreatePanel");

    [MenuItem("PIMLR/UI/Slice 2: Build Fit Check")]
    public static string Build()
    {
        var ctrl = FindController();
        if (!ctrl) return "no controller";
        Revert(false);

        var oldTiles = ctrl.GetComponentsInChildren<PimlrBodyPartTile>(true).Where(t => !t.GetComponent<ThemedButton>()).ToList();
        foreach (var n in new[] { "BodyPartCard", "ColorCard" })
        {
            var t = ctrl.transform.Find(n);
            if (t) { Undo.RecordObject(t.gameObject, "Fit Check v2"); t.gameObject.SetActive(false); }
        }

        // 1080p frame + card
        var frameGo = new GameObject(FrameName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(frameGo, "Fit Check v2");
        frameGo.transform.SetParent(ctrl.transform, false);
        var frameComp = frameGo.AddComponent<PimlrReferenceFrame>(); frameComp.Fit(true);

        var card = Img(frameGo.transform, "L4 Fit Check Card", ColorToken.SurfaceRaised, 0.96f, SpriteSlot.Panel);
        Place(card.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-96, 30), new Vector2(452, 410));
        Stretch(Img(card.transform, "Edge", ColorToken.BrandForest, 1f, SpriteSlot.PanelEdge).rectTransform);

        // Tabs, in the old tiles' order (Hair, Shirt, Pants, Shoes), reusing their icons.
        var row = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(card.transform, false);
        var rrt = (RectTransform)row.transform; Place(rrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -24), new Vector2(-48, 96)); rrt.pivot = new Vector2(0.5f, 1);
        var hl = row.GetComponent<HorizontalLayoutGroup>(); hl.spacing = 8; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;

        var order = oldTiles.OrderBy(t => t.transform.GetSiblingIndex()).ToList();
        Button defaultTab = null;
        foreach (var old in order)
        {
            var icon = old.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i != old.frame && i.gameObject != old.gameObject && i.sprite && !i.sprite.name.StartsWith("UI"));
            var tab = Tab(row.transform, old.displayName, icon ? icon.sprite : null, out var bar);
            var tile = tab.gameObject.AddComponent<PimlrBodyPartTile>();
            tile.clothIndex = old.clothIndex; tile.displayName = old.displayName; tile.button = tab; tile.activeBar = bar;
            if (old.clothIndex == ctrl.defaultClothIndex) defaultTab = tab;
        }
        if (defaultTab) PimlrNavHint.On(defaultTab).defaultPriority = 10; // opening the panel focuses the default part

        var cap = Txt(card.transform, "Color Caption", "COLOR", TypeRole.Caption, 1f);
        Place(cap.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -138), new Vector2(-56, 26)); cap.rectTransform.pivot = new Vector2(0.5f, 1);

        var gridGo = new GameObject("Swatch Grid", typeof(RectTransform));
        gridGo.transform.SetParent(card.transform, false);
        var grt = (RectTransform)gridGo.transform; grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 1); grt.pivot = new Vector2(0.5f, 1); grt.anchoredPosition = new Vector2(0, -176);
        var grid = gridGo.AddComponent<PimlrSwatchGrid>();
        grt.sizeDelta = new Vector2(grid.columns * grid.chipSize.x + (grid.columns - 1) * grid.spacing.x, 2 * grid.chipSize.y + grid.spacing.y);

        var nameLbl = Txt(card.transform, "Color Name", "", TypeRole.Utility, 1f);
        Place(nameLbl.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -284), new Vector2(-56, 30)); nameLbl.rectTransform.pivot = new Vector2(0.5f, 1);
        nameLbl.alignment = TextAlignmentOptions.Center;
        grid.nameLabel = nameLbl;

        var saved = Txt(card.transform, "Saved Feedback", "", TypeRole.Caption, 1f);
        var st = saved.GetComponent<ThemedText>(); st.overrideColor = true; st.color = ColorToken.BrandLime; st.Apply();
        Place(saved.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -322), new Vector2(-56, 26)); saved.rectTransform.pivot = new Vector2(0.5f, 1);
        saved.alignment = TextAlignmentOptions.Center;

        var hint = Txt(card.transform, "Hint", "PICK A PART, THEN A COLOR", TypeRole.Caption, 1f);
        Place(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 22), new Vector2(-40, 26)); hint.rectTransform.pivot = new Vector2(0.5f, 0);
        hint.alignment = TextAlignmentOptions.Center; hint.enableAutoSizing = false;

        // Existing Save button + title: themed in place (prefab children can't be moved; listeners untouched).
        // Their original values are snapshotted first so Revert restores them exactly.
        var panel = Panel(ctrl);
        var save = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Save_Btn");
        var title = panel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.text.Trim().ToUpper() == "FIT CHECK");
        var snap = ctrl.GetComponent<PimlrUISnapshot>() ?? Undo.AddComponent<PimlrUISnapshot>(ctrl.gameObject);
        void Snap(Component c) { if (c) snap.entries.Add(PimlrUISnapshot.Capture("slice2", c)); }
        if (save) { Snap(save); Snap(save.image); Snap(save.GetComponentInChildren<TMP_Text>(true)); }
        Snap(title);
        EditorUtility.SetDirty(snap);
        if (save)
        {
            var ring = new GameObject("FocusRing (#UI)", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Undo.RegisterCreatedObjectUndo(ring.gameObject, "Fit Check v2");
            ring.transform.SetParent(save.transform, false); ring.transform.SetAsFirstSibling();
            var r = ring.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(-5, -5); r.offsetMax = new Vector2(5, 5);
            var tb = Undo.AddComponent<ThemedButton>(save.gameObject);
            tb.variant = ButtonVariant.Primary; tb.focusRing = ring; tb.label = save.GetComponentInChildren<TMP_Text>(true);
            if (tb.label) { var lt = Undo.AddComponent<ThemedText>(tb.label.gameObject); lt.role = TypeRole.Heading; lt.sizeScale = 0.78f; lt.Apply(); }
            tb.Apply();
        }
        if (title) { var tt = Undo.AddComponent<ThemedText>(title.gameObject); tt.role = TypeRole.Display; tt.sizeScale = 0.56f; tt.Apply(); }

        // Responsive layout (landscape / touch / portrait bottom sheet); SAVE's desktop placement is captured on first enable.
        var resp = card.gameObject.AddComponent<PimlrFitCheckResponsive>();
        resp.frame = frameComp; resp.card = card.rectTransform; resp.tabs = rrt; resp.colorCaption = cap.rectTransform;
        resp.nameLabel = nameLbl.rectTransform; resp.savedLabel = saved.rectTransform; resp.hint = hint.rectTransform; resp.hintText = hint;
        resp.grid = grid; resp.saveButton = save ? (RectTransform)save.transform : null;
        if (save) { var sr = (RectTransform)save.transform; snap.entries.Add(new PimlrUISnapshot.Entry { slice = "slice2-rect", target = sr, json = JsonUtility.ToJson(new RectSnap { aMin = sr.anchorMin, aMax = sr.anchorMax, pivot = sr.pivot, pos = sr.anchoredPosition, size = sr.sizeDelta }) }); }

        // Portrait framing for the Fit Check camera (projection only; the camera's own settings are untouched).
        var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Camera - Character Customization" && c.gameObject.scene == ctrl.gameObject.scene);
        var colorer = Object.FindObjectsByType<changeColor>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.GetType() == typeof(changeColor) && c.gameObject.scene == ctrl.gameObject.scene);
        var framing = card.gameObject.AddComponent<PimlrPortraitCameraFraming>();
        framing.targetCamera = cam; framing.sheet = card.rectTransform;
        framing.characterRoot = colorer && colorer.renderers != null && colorer.renderers.Length > 0 && colorer.renderers[0] ? colorer.renderers[0].transform.root : null;

        Undo.RecordObject(ctrl, "Fit Check v2");
        ctrl.swatchGrid = grid; ctrl.doneButton = save; ctrl.savedFeedback = saved;
        EditorUtility.SetDirty(ctrl);

        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
        EditorSceneManager.SaveScene(ctrl.gameObject.scene);
        return $"built: {order.Count} tabs, grid wired, Save_Btn {(save ? "themed" : "NOT FOUND")}, title {(title ? "themed" : "NOT FOUND")}";
    }

    [MenuItem("PIMLR/UI/Slice 2: Revert Fit Check")]
    public static void RevertMenu() => Revert(true);

    public static string Revert(bool save)
    {
        var ctrl = FindController();
        if (!ctrl) return "no controller";
        var frame = ctrl.transform.Find(FrameName);
        if (frame) Undo.DestroyObjectImmediate(frame.gameObject);
        foreach (var n in new[] { "BodyPartCard", "ColorCard" })
        {
            var t = ctrl.transform.Find(n);
            if (t) { Undo.RecordObject(t.gameObject, "Revert Fit Check v2"); t.gameObject.SetActive(true); }
        }
        var panel = Panel(ctrl);
        // Remove themed components first (they listen for theme changes), then restore the snapshotted originals.
        var btn = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Save_Btn");
        if (btn)
        {
            var tb = btn.GetComponent<ThemedButton>(); if (tb) Undo.DestroyObjectImmediate(tb);
            var ring = btn.transform.Find("FocusRing (#UI)"); if (ring) Undo.DestroyObjectImmediate(ring.gameObject);
            foreach (var tt in btn.GetComponentsInChildren<ThemedText>(true)) Undo.DestroyObjectImmediate(tt);
        }
        foreach (var tt in panel.GetComponentsInChildren<ThemedText>(true).Where(t => t.GetComponent<TMP_Text>().text.Trim().ToUpper() == "FIT CHECK").ToList()) Undo.DestroyObjectImmediate(tt);
        var snap = ctrl.GetComponent<PimlrUISnapshot>();
        if (snap)
        {
            foreach (var e in snap.entries.Where(e => e.slice == "slice2" && e.target))
            { Undo.RecordObject(e.target, "Revert Fit Check v2"); PimlrUISnapshot.Restore(e); EditorUtility.SetDirty(e.target); }
            foreach (var e in snap.entries.Where(e => e.slice == "slice2-rect" && e.target is RectTransform))
            { var rt = (RectTransform)e.target; var v = JsonUtility.FromJson<RectSnap>(e.json); Undo.RecordObject(rt, "Revert Fit Check v2"); rt.anchorMin = v.aMin; rt.anchorMax = v.aMax; rt.pivot = v.pivot; rt.anchoredPosition = v.pos; rt.sizeDelta = v.size; }
            snap.entries.RemoveAll(e => e.slice == "slice2" || e.slice == "slice2-rect");
            if (snap.entries.Count == 0) Undo.DestroyObjectImmediate(snap);
        }
        Undo.RecordObject(ctrl, "Revert Fit Check v2");
        ctrl.swatchGrid = null; ctrl.doneButton = null; ctrl.savedFeedback = null;
        if (save) { EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene); EditorSceneManager.SaveScene(ctrl.gameObject.scene); }
        return "reverted";
    }

    [System.Serializable] struct RectSnap { public Vector2 aMin, aMax, pivot, pos, size; } // plain values only (no object refs)

    // ---------------- building blocks (1080p units inside the frame) ----------------

    static Button Tab(Transform parent, string label, Sprite icon, out GameObject activeBar)
    {
        var go = new GameObject("Tab — " + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var btn = go.GetComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();
        var ring = new GameObject("FocusRing", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        ring.transform.SetParent(go.transform, false);
        var rr = ring.rectTransform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = new Vector2(-5, -5); rr.offsetMax = new Vector2(5, 5);
        if (icon)
        {
            var ic = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            ic.transform.SetParent(go.transform, false); ic.sprite = icon; ic.preserveAspect = true; ic.raycastTarget = false;
            var ir = ic.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 1); ir.pivot = new Vector2(0.5f, 1); ir.anchoredPosition = new Vector2(0, -12); ir.sizeDelta = new Vector2(40, 40);
            var ti = ic.gameObject.AddComponent<ThemedImage>(); ti.color = ColorToken.TextPrimary; ti.Apply();
        }
        var t = Txt(go.transform, "Label", label, TypeRole.Heading, 0.6f);
        t.alignment = TextAlignmentOptions.Center;
        var lr = t.rectTransform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 0); lr.pivot = new Vector2(0.5f, 0); lr.anchoredPosition = new Vector2(0, 12); lr.sizeDelta = new Vector2(0, 26);
        var bar = Img(go.transform, "Active Bar", ColorToken.BrandLime, 1f, SpriteSlot.None);
        var br = bar.rectTransform; br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(0.5f, 0); br.offsetMin = new Vector2(8, 0); br.offsetMax = new Vector2(-8, 4);
        bar.gameObject.SetActive(false);
        activeBar = bar.gameObject;
        var tb = go.AddComponent<ThemedButton>(); tb.variant = ButtonVariant.Tertiary; tb.label = t; tb.focusRing = ring; tb.motion = ButtonMotion.Compact; tb.Apply();
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

    static void Place(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    { r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = new Vector2(aMin.x == aMax.x ? aMin.x : 0.5f, aMin.y == aMax.y ? aMin.y : 0.5f); r.anchoredPosition = pos; r.sizeDelta = size; }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}
