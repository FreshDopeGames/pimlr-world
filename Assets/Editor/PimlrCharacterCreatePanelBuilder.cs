#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PIMLR: builds the restyled character-create UI into the Living Room UI Variant prefab (SceneStaticEU).
///   Left card : "SELECT A BODY PART" - Hair / Shirt / Pants / Shoes tiles
///   Right card: "SELECT A COLOR"     - 3x4 grid of the 12 PimlrPalette colors, 10px highlight on the selected chip
/// Safe to re-run: it deletes its own PIMLR_CharacterCreate object and rebuilds it.
/// The legacy left strip (PartSelectionBoard) and the four *Colors columns are only deactivated, not deleted.
/// Tweak sizes/colors in the constants below (or in the Inspector afterwards) and re-run.
/// </summary>
public static class PimlrCharacterCreatePanelBuilder
{
    const string PrefabPath = "Assets/_Game/Resources/Living Room UI Variant.prefab";
    const string FontGuid = "ebe7cfa37616c904091a4ca55537a327"; // Barlow-Bold SDF (Leaderboards header font)
    const string RootName = "PIMLR_CharacterCreate";

    // Layout, in canvas units (Global UI canvas reference = 1280x720). Cards sit either side of the centered player.
    static readonly Vector2 PartCardPos = new Vector2(-370f, 10f);
    static readonly Vector2 PartCardSize = new Vector2(240f, 440f);
    static readonly Vector2 ColorCardPos = new Vector2(380f, 10f);
    static readonly Vector2 ColorCardSize = new Vector2(330f, 440f);
    const float Border = 3f;          // card + tile border thickness
    const float ChipCell = 66f;       // chip size incl. highlight ring
    const float ChipSpacing = 8f;
    const float ChipRing = 10f;       // selected highlight border

    static readonly Color Accent = new Color(0.095f, 0.745f, 0f, 1f);      // SAVE button green
    static readonly Color CardFill = new Color(0.05f, 0.05f, 0.06f, 0.90f);
    static readonly Color TileFill = new Color(0.12f, 0.12f, 0.13f, 1f);
    static readonly Color Muted = new Color(1f, 1f, 1f, 0.70f);

    static Sprite rounded;
    static TMP_FontAsset font;

    [MenuItem("PIMLR/UI/Build Character Create Panel")]
    public static void Build()
    {
        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        if (!font) font = TMP_Settings.defaultFontAsset;

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var panel = FindDeep(root.transform, "CharaterCreatePanel");
            var menu = panel ? panel.Find("Menu") : null;
            if (!menu)
            {
                Debug.LogError($"[PIMLR] CharaterCreatePanel/Menu not found in {PrefabPath}.");
                return;
            }

            var old = menu.Find(RootName);
            if (old) Object.DestroyImmediate(old.gameObject);

            // Reuse the existing body-part icons before hiding the legacy UI.
            var legacyButtons = menu.Find("PartSelectionBoard/PartHeader1/ContentButtons");
            Sprite Icon(string n)
            {
                var t = legacyButtons ? legacyButtons.Find(n) : null;
                var img = t ? t.GetComponent<Image>() : null;
                return img ? img.sprite : null;
            }
            Sprite hair = Icon("Hair"), shirt = Icon("Shirt"), pants = Icon("Pants"), shoes = Icon("Shoes");

            foreach (var n in new[] { "PartSelectionBoard", "ShirtColors", "PantColors", "ShoesColors", "HairColors" })
            {
                var t = menu.Find(n);
                if (t) t.gameObject.SetActive(false);
            }

            // Container + controller
            var container = NewUI(RootName, menu);
            Stretch(container, 0, 0, 0, 0);
            var controller = container.gameObject.AddComponent<PimlrCharacterCreateController>();

            // Left card: body parts
            var partContent = Card("BodyPartCard", container, PartCardPos, PartCardSize, "SELECT A BODY PART");
            var vlg = partContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
            Tile(partContent, "Hair", "HAIR", hair, 1);
            Tile(partContent, "Shirt", "SHIRT", shirt, 0);
            Tile(partContent, "Pants", "PANTS", pants, 2);
            Tile(partContent, "Shoes", "SHOES", shoes, 3);

            // Right card: colors
            var colorContent = Card("ColorCard", container, ColorCardPos, ColorCardSize, "SELECT A COLOR");

            var partLabel = Text("PartLabel", colorContent, "SHIRT", 18f, Accent, TextAlignmentOptions.Center, 4f);
            TopBand(partLabel.rectTransform, 0f, 28f);

            float gridW = 4 * ChipCell + 3 * ChipSpacing;
            float gridH = 3 * ChipCell + 2 * ChipSpacing;
            var grid = NewUI("ColorGrid", colorContent);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.anchoredPosition = new Vector2(0f, -40f);
            grid.sizeDelta = new Vector2(gridW, gridH);
            var glg = grid.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(ChipCell, ChipCell);
            glg.spacing = new Vector2(ChipSpacing, ChipSpacing);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;
            glg.childAlignment = TextAnchor.UpperCenter;
            foreach (int index in PimlrPalette.DisplayOrder) Chip(grid, index);

            var colorName = Text("ColorName", colorContent, "", 22f, Color.white, TextAlignmentOptions.Center, 4f);
            TopBand(colorName.rectTransform, 40f + gridH + 16f, 32f);

            var hint = Text("Hint", colorContent, "PICK A PART, THEN A COLOR", 13f, Muted, TextAlignmentOptions.Center, 2f);
            var hrt = hint.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 0f);
            hrt.pivot = new Vector2(0.5f, 0f);
            hrt.anchoredPosition = new Vector2(0f, 4f); hrt.sizeDelta = new Vector2(0f, 20f);

            controller.partLabel = partLabel;
            controller.colorNameLabel = colorName;
            controller.defaultClothIndex = 0;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            if (ok) Debug.Log($"[PIMLR] Character Create panel rebuilt in {PrefabPath}.");
            else Debug.LogError("[PIMLR] Saving the prefab failed.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------- pieces ----------

    /// <summary>Bordered card (accent frame + dark fill) with a title and underline. Returns the content area.</summary>
    static RectTransform Card(string name, Transform parent, Vector2 pos, Vector2 size, string title)
    {
        var card = NewUI(name, parent);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = pos;
        card.sizeDelta = size;
        Img(card, Accent);

        var fill = NewUI("Fill", card);
        Stretch(fill, Border, Border, Border, Border);
        Img(fill, CardFill);

        var t = Text("Title", fill, title, 22f, Color.white, TextAlignmentOptions.Center, 3f);
        TopBand(t.rectTransform, 14f, 34f);

        var line = NewUI("Underline", fill);
        line.anchorMin = new Vector2(0f, 1f); line.anchorMax = new Vector2(1f, 1f);
        line.pivot = new Vector2(0.5f, 1f);
        line.anchoredPosition = new Vector2(0f, -54f);
        line.sizeDelta = new Vector2(-56f, 2f); // 28 inset each side, 2 tall
        var li = line.gameObject.AddComponent<Image>();
        li.color = Accent; li.raycastTarget = false;

        var content = NewUI("Content", fill);
        Stretch(content, 16f, 16f, 16f, 68f);
        return content;
    }

    static void Tile(Transform parent, string name, string label, Sprite icon, int clothIndex)
    {
        var tile = NewUI(name, parent);
        tile.sizeDelta = new Vector2(0f, 76f);
        var frame = Img(tile, PimlrBodyPartTile.IdleColor);
        frame.raycastTarget = true;

        var inner = NewUI("Inner", tile);
        Stretch(inner, Border, Border, Border, Border);
        Img(inner, TileFill);

        var iconRt = NewUI("Icon", inner);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(40f, 0f);
        iconRt.sizeDelta = new Vector2(46f, 46f);
        var ii = iconRt.gameObject.AddComponent<Image>();
        ii.sprite = icon; ii.preserveAspect = true; ii.raycastTarget = false;
        if (!icon) ii.color = new Color(1f, 1f, 1f, 0f);

        var txt = Text("Label", inner, label, 22f, Color.white, TextAlignmentOptions.MidlineLeft, 3f);
        var trt = txt.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(78f, 0f); trt.offsetMax = new Vector2(-8f, 0f);

        var button = tile.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        var t = tile.gameObject.AddComponent<PimlrBodyPartTile>();
        t.clothIndex = clothIndex; t.displayName = label; t.frame = frame; t.button = button;
    }

    static void Chip(Transform parent, int paletteIndex)
    {
        var chip = NewUI("Chip_" + PimlrPalette.NameOf(paletteIndex), parent);

        var ring = NewUI("Highlight", chip);
        Stretch(ring, 0, 0, 0, 0);
        var ringImg = Img(ring, Color.white);
        ringImg.enabled = false;

        var sw = NewUI("Swatch", chip);
        Stretch(sw, ChipRing, ChipRing, ChipRing, ChipRing);
        var swImg = Img(sw, PimlrPalette.Get(paletteIndex));
        swImg.raycastTarget = true;
        var outline = sw.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var button = chip.gameObject.AddComponent<Button>();
        button.targetGraphic = swImg;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        var c = chip.gameObject.AddComponent<PimlrColorChip>();
        c.paletteIndex = paletteIndex; c.swatch = swImg; c.highlight = ringImg; c.button = button;
    }

    // ---------- helpers ----------

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static Image Img(RectTransform rt, Color c)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = rounded;
        img.type = Image.Type.Sliced;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
        TextAlignmentOptions align, float spacing)
    {
        var rt = NewUI(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.characterSpacing = spacing;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Full-width band anchored to the top edge, `top` units down, `height` tall.</summary>
    static void TopBand(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(0f, height);
    }

    static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindDeep(c, name);
            if (r) return r;
        }
        return null;
    }
}
#endif
