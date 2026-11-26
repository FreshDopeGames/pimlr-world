// #UI: builds Assets/_Game/UI/Showcase/UI_FitCheckPalette.unity, the compact Fit Check palette prototype.
// Right-side card ~420px wide; the left two-thirds stay free for the character preview (the centerpiece).
// Menu: PIMLR > UI > Build Fit Check Palette Prototype

using PIMLR.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class PimlrFitCheckPaletteBuilder
{
    public const string ScenePath = "Assets/_Game/UI/Showcase/UI_FitCheckPalette.unity";

    [MenuItem("PIMLR/UI/Build Fit Check Palette Prototype")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static string Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = PimlrUITheme.C(ColorToken.SurfaceBase);

        var cgo = new GameObject("Fit Check Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
        var root = cgo.transform;

        Stretch(Img(root, "L1 Backdrop", ColorToken.SurfaceBase, 1f, SpriteSlot.None).rectTransform);
        Stretch(Img(root, "L2 Pattern", ColorToken.BrandForest, 0.14f, SpriteSlot.Pattern).rectTransform);

        // Character preview stand-in (left / center stays clear)
        var stage = Img(root, "Character Preview (stand-in)", ColorToken.SurfaceScrim, 0.35f, SpriteSlot.Card);
        Place(stage.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(160, 0), new Vector2(900, 860));
        var st = Txt(stage.transform, "Label", "CHARACTER PREVIEW", TypeRole.Caption, 1f); st.alignment = TextAlignmentOptions.Center; Stretch(st.rectTransform);

        // Card
        var card = Img(root, "L4 Fit Check Card", ColorToken.SurfaceRaised, 0.96f, SpriteSlot.Panel);
        Place(card.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-96, 0), new Vector2(452, 456));
        var edge = Img(card.transform, "Edge", ColorToken.BrandForest, 1f, SpriteSlot.PanelEdge); Stretch(edge.rectTransform);

        var title = Txt(card.transform, "Title", "FIT CHECK", TypeRole.Heading, 1f);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -28), new Vector2(-56, 44)); title.rectTransform.pivot = new Vector2(0.5f, 1);

        // Tabs
        var tabsRow = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabsRow.transform.SetParent(card.transform, false);
        var trt = (RectTransform)tabsRow.transform; Place(trt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -92), new Vector2(-56, 48)); trt.pivot = new Vector2(0.5f, 1);
        var hl = tabsRow.GetComponent<HorizontalLayoutGroup>(); hl.spacing = 8; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = true;
        string[] parts = { "HAIR", "SHIRT", "PANTS", "SHOES" };
        var tabs = new Button[parts.Length];
        for (int i = 0; i < parts.Length; i++) tabs[i] = Tab(tabsRow.transform, parts[i]);

        var colorCap = Txt(card.transform, "Color Caption", "COLOR", TypeRole.Caption, 1f);
        Place(colorCap.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -168), new Vector2(-56, 26)); colorCap.rectTransform.pivot = new Vector2(0.5f, 1);

        // Grid
        var gridGo = new GameObject("Swatch Grid", typeof(RectTransform));
        gridGo.transform.SetParent(card.transform, false);
        var grt = (RectTransform)gridGo.transform; grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 1); grt.pivot = new Vector2(0.5f, 1); grt.anchoredPosition = new Vector2(0, -206);
        var grid = gridGo.AddComponent<PimlrSwatchGrid>();
        grt.sizeDelta = new Vector2(grid.columns * grid.chipSize.x + (grid.columns - 1) * grid.spacing.x, 2 * grid.chipSize.y + grid.spacing.y);

        var nameLbl = Txt(card.transform, "Color Name", "", TypeRole.Utility, 1f);
        Place(nameLbl.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -310), new Vector2(-56, 30)); nameLbl.rectTransform.pivot = new Vector2(0.5f, 1);
        nameLbl.alignment = TextAlignmentOptions.Center;
        grid.nameLabel = nameLbl;

        var status = Txt(card.transform, "Status", "", TypeRole.Caption, 1f);
        Place(status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -344), new Vector2(-56, 26)); status.rectTransform.pivot = new Vector2(0.5f, 1);
        status.alignment = TextAlignmentOptions.Center;
        status.GetComponent<ThemedText>().textCase = TextCase.Original;

        var save = Btn(card.transform, "SAVE FIT", ButtonVariant.Primary, new Vector2(28, 28), new Vector2(240, 56));
        var back = Btn(card.transform, "BACK", ButtonVariant.Tertiary, new Vector2(284, 28), new Vector2(140, 56));
        PimlrNavHint.On(back).isCancel = true;

        var proto = cgo.AddComponent<PimlrFitCheckPalettePrototype>();
        proto.tabs = tabs; proto.grid = grid; proto.saveButton = save; proto.backButton = back; proto.status = status;

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EditorSceneManager.SaveScene(scene, ScenePath);
        return ScenePath;
    }

    static Button Tab(Transform parent, string label)
    {
        var b = Btn(parent, label, ButtonVariant.Tertiary, Vector2.zero, new Vector2(96, 48), true);
        var bar = Img(b.transform, "Active Bar", ColorToken.BrandLime, 1f, SpriteSlot.None);
        var r = bar.rectTransform; r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0); r.offsetMin = new Vector2(6, 0); r.offsetMax = new Vector2(-6, 4);
        b.GetComponent<ThemedButton>().motion = ButtonMotion.Compact;
        return b;
    }

    static Button Btn(Transform parent, string label, ButtonVariant v, Vector2 posFromBottomLeft, Vector2 size, bool inLayout = false)
    {
        var go = new GameObject("Button — " + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        if (!inLayout) { rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero; rt.anchoredPosition = posFromBottomLeft; rt.sizeDelta = size; }
        var btn = go.GetComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();
        var ring = new GameObject("FocusRing", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        ring.transform.SetParent(go.transform, false);
        var rr = ring.rectTransform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = new Vector2(-6, -6); rr.offsetMax = new Vector2(6, 6);
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

    static void Place(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    { r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = new Vector2(aMin.x == aMax.x ? aMin.x : 0.5f, aMin.y == aMax.y ? aMin.y : 0.5f); r.anchoredPosition = pos; r.sizeDelta = size; }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}
