// #UI: one-click setup for the PIMLR UI theme: placeholder 9-slice art, the hero text material preset and the
// default PimlrUITheme asset (Resources/PIMLR/UI). Re-running is safe: art is regenerated, the theme keeps any
// sprite you've already swapped in for real art and only fills slots that are still empty.
//
// Menu: PIMLR > UI > Setup Theme (placeholder art + default theme)

using System.Collections.Generic;
using System.IO;
using PIMLR.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class PimlrUIThemeSetup
{
    const string ArtDir = "Assets/_Game/UI/Art/Placeholder";
    const string FontDir = "Assets/_Game/UI/Fonts";
    const string ThemePath = "Assets/_Game/Resources/PIMLR/UI/PimlrUITheme.asset";
    // Dynamic SDF generated from _Game/Fonts/Russo_One/RussoOne-Regular.ttf (the static asset there has an empty atlas).
    const string RussoPath = "Assets/_Game/UI/Fonts/RussoOne-Regular SDF (Dynamic).asset";
    const string BarlowMedium = "Assets/_Game/Fonts/Barlow/Barlow-Medium SDF.asset";
    const string BarlowSemiBold = "Assets/_Game/Fonts/Barlow/Barlow-SemiBold SDF.asset";

    [MenuItem("PIMLR/UI/Setup Theme (placeholder art + default theme)")]
    public static void SetupAll()
    {
        var sprites = GeneratePlaceholderArt();
        EnsureDisplayFont();
        var punch = CreatePunchPreset();
        var theme = CreateOrUpdateTheme(sprites, punch);
        Selection.activeObject = theme;
        Debug.Log($"[PIMLR UI] Theme ready at {ThemePath} ({sprites.Count} placeholder sprites).", theme);
    }

    // ---------- Placeholder art (white shapes; color comes from the theme) ----------

    public static Dictionary<SpriteSlot, Sprite> GeneratePlaceholderArt()
    {
        Directory.CreateDirectory(ArtDir);
        var made = new Dictionary<SpriteSlot, Sprite>();

        void Make(SpriteSlot slot, string file, int w, int h, System.Func<float, float, float> coverage, Vector4 border, bool tile)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 4x4 supersampling for clean anti-aliased edges
                    float a = 0f;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            a += coverage(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a / 16f) * 255f));
                }
            tex.SetPixels32(px);
            string path = $"{ArtDir}/{file}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spriteBorder = border;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // required for Sliced / Tiled
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
            made[slot] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Panel: angled top-right corner (street-tag cut). 9-slice border 48.
        Make(SpriteSlot.Panel, "ui_panel_angled", 256, 256, (x, y) => In(Clipped(x, y, 0, 0, 256, 256, 40, 0)), new Vector4(48, 48, 48, 48), false);
        Make(SpriteSlot.PanelEdge, "ui_panel_edge", 256, 256,
            (x, y) => In(Clipped(x, y, 0, 0, 256, 256, 40, 0)) * (1f - In(Clipped(x, y, 3, 3, 250, 250, 40 - 1.8f, 0))), new Vector4(48, 48, 48, 48), false);
        Make(SpriteSlot.Card, "ui_card", 192, 192, (x, y) => In(Clipped(x, y, 0, 0, 192, 192, 20, 0)), new Vector4(32, 32, 32, 32), false);
        // Button: cut top-right + bottom-left.
        Make(SpriteSlot.Button, "ui_btn_base", 256, 96, (x, y) => In(Clipped(x, y, 0, 0, 256, 96, 18, 18)), new Vector4(40, 32, 40, 32), false);
        Make(SpriteSlot.FocusRing, "ui_btn_focus", 256, 96,
            (x, y) => In(Clipped(x, y, 0, 0, 256, 96, 18, 18)) * (1f - In(Clipped(x, y, 4, 4, 248, 88, 16.3f, 16.3f))), new Vector4(40, 32, 40, 32), false);
        Make(SpriteSlot.BarBack, "ui_bar_back", 64, 24, (x, y) => In(Clipped(x, y, 0, 0, 64, 24, 8, 8)), new Vector4(12, 0, 12, 0), false);
        Make(SpriteSlot.BarFill, "ui_bar_fill", 64, 24, (x, y) => 1f, Vector4.zero, false);
        Make(SpriteSlot.Chip, "ui_chip", 128, 48, (x, y) => Pill(x, y, 128, 48), new Vector4(24, 0, 24, 0), false);
        Make(SpriteSlot.Badge, "ui_badge_burst", 256, 256, (x, y) => Star(x, y, 128, 128, 124, 100, 12), Vector4.zero, false);
        // Pattern: seamless dot + plus grid (stand-in for the PIMLR icon pattern).
        Make(SpriteSlot.Pattern, "ui_tex_pattern", 256, 256, (x, y) =>
        {
            float gx = Mathf.Repeat(x, 64f) - 32f, gy = Mathf.Repeat(y, 64f) - 32f;
            bool plus = (Mathf.Abs(gx) < 1.5f && Mathf.Abs(gy) < 8f) || (Mathf.Abs(gy) < 1.5f && Mathf.Abs(gx) < 8f);
            float dx = Mathf.Repeat(x + 32f, 64f) - 32f, dy = Mathf.Repeat(y + 32f, 64f) - 32f;
            bool dot = dx * dx + dy * dy < 16f;
            return plus || dot ? 1f : 0f;
        }, Vector4.zero, true);
        // Halftone: uniform comic dots on a 45-degree grid.
        Make(SpriteSlot.Halftone, "ui_tex_halftone", 128, 128, (x, y) =>
        {
            float u = (x + y) * 0.7071f, v = (x - y) * 0.7071f;
            float cu = Mathf.Repeat(u, 11.3137f) - 5.6568f, cv = Mathf.Repeat(v, 11.3137f) - 5.6568f;
            return cu * cu + cv * cv < 6.5f ? 1f : 0f;
        }, Vector4.zero, true);

        return made;
    }

    static float In(bool b) => b ? 1f : 0f;

    // Rectangle (ox,oy,w,h) with 45-degree cuts on the top-right and bottom-left corners.
    static bool Clipped(float x, float y, float ox, float oy, float w, float h, float cutTR, float cutBL)
    {
        float lx = x - ox, ly = y - oy;
        if (lx < 0 || ly < 0 || lx > w || ly > h) return false;
        if (cutTR > 0 && (w - lx) + (h - ly) < cutTR) return false;
        if (cutBL > 0 && lx + ly < cutBL) return false;
        return true;
    }

    static float Pill(float x, float y, float w, float h)
    {
        float r = h / 2f;
        float cx = Mathf.Clamp(x, r, w - r);
        float dx = x - cx, dy = y - r;
        return dx * dx + dy * dy <= r * r ? 1f : 0f;
    }

    static float Star(float x, float y, float cx, float cy, float rOut, float rIn, int points)
    {
        float dx = x - cx, dy = y - cy;
        float ang = Mathf.Atan2(dy, dx), dist = Mathf.Sqrt(dx * dx + dy * dy);
        float seg = Mathf.PI / points;
        float t = Mathf.Abs(Mathf.Repeat(ang, 2f * seg) - seg) / seg; // 0 at a point, 1 at a valley
        return dist <= Mathf.Lerp(rOut, rIn, t) ? 1f : 0f;
    }

    // ---------- Display/Heading font: dynamic SDF from the Russo One TTF ----------

    const string RussoTtf = "Assets/_Game/Fonts/Russo_One/RussoOne-Regular.ttf";

    /// <summary>Creates the Russo One dynamic SDF if missing, with a material set up like the Barlow assets
    /// (full TextMeshPro/Distance Field). The API's default Mobile material renders with glyph bleed in this project.</summary>
    public static TMP_FontAsset EnsureDisplayFont()
    {
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RussoPath);
        if (!fa)
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(RussoTtf);
            if (!ttf) { Debug.LogWarning("[PIMLR UI] Russo One TTF not found at " + RussoTtf); return null; }
            Directory.CreateDirectory(FontDir);
            fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = System.IO.Path.GetFileNameWithoutExtension(RussoPath);
            AssetDatabase.CreateAsset(fa, RussoPath);
            fa.atlasTextures[0].name = fa.name + " Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~×—–·•’", out _);
        }
        var reference = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BarlowMedium);
        var m = fa.material;
        if (reference)
        {
            m.shader = reference.material.shader;
            m.CopyPropertiesFromMaterial(reference.material);
            m.shaderKeywords = reference.material.shaderKeywords;
            if (!fa.fallbackFontAssetTable.Contains(reference)) fa.fallbackFontAssetTable.Add(reference);
        }
        m.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
        m.SetFloat(ShaderUtilities.ID_TextureWidth, fa.atlasWidth);
        m.SetFloat(ShaderUtilities.ID_TextureHeight, fa.atlasHeight);
        m.SetFloat(ShaderUtilities.ID_GradientScale, fa.atlasPadding + 1);
        m.SetFloat(ShaderUtilities.ID_WeightNormal, fa.normalStyle);
        m.SetFloat(ShaderUtilities.ID_WeightBold, fa.boldStyle);
        ShaderUtilities.UpdateShaderRatios(m);
        EditorUtility.SetDirty(m); EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    // ---------- Hero text preset (Direction C: comic punch for splash, banners, podium) ----------

    public static Material CreatePunchPreset()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RussoPath);
        if (!font) { Debug.LogWarning("[PIMLR UI] Russo One SDF not found at " + RussoPath); return null; }
        Directory.CreateDirectory(FontDir);
        string path = $"{FontDir}/{font.name} - Punch.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(font.material) { name = font.name + " - Punch" };
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.CopyPropertiesFromMaterial(font.material);

        mat.SetFloat("_OutlineWidth", 0.16f);
        mat.SetColor("_OutlineColor", new Color(0.055f, 0.055f, 0.05f, 1f));
        mat.SetFloat("_FaceDilate", 0.12f);
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor("_UnderlayColor", new Color(0.055f, 0.055f, 0.05f, 1f));
        mat.SetFloat("_UnderlayOffsetX", 0.7f);
        mat.SetFloat("_UnderlayOffsetY", -0.7f);
        mat.SetFloat("_UnderlayDilate", 0.2f);
        mat.SetFloat("_UnderlaySoftness", 0f);
        ShaderUtilities.UpdateShaderRatios(mat);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // ---------- Default theme asset ----------

    public static PimlrUITheme CreateOrUpdateTheme(Dictionary<SpriteSlot, Sprite> placeholders, Material punch)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
        var theme = AssetDatabase.LoadAssetAtPath<PimlrUITheme>(ThemePath);
        bool isNew = !theme;
        if (isNew)
        {
            theme = ScriptableObject.CreateInstance<PimlrUITheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        // Colors: always written from the approved token table (edit the asset afterwards to tune).
        if (isNew || theme.colors == null || theme.colors.Length == 0)
        {
            theme.colors = new[]
            {
                CE(ColorToken.SurfaceBase, "#121211"), CE(ColorToken.SurfaceRaised, "#1E1E1C"), CE(ColorToken.SurfaceScrim, "#070707", 0.75f),
                CE(ColorToken.BrandForest, "#3C6C1A"), CE(ColorToken.BrandBrick, "#872818"), CE(ColorToken.BrandLime, "#CDF952"),
                CE(ColorToken.TextPrimary, "#F4F1EA"), CE(ColorToken.TextSecondary, "#A8A69E"), CE(ColorToken.TextOnLime, "#222220"),
                CE(ColorToken.AccentMusic, "#9B5CF6"), CE(ColorToken.AccentInfo, "#22D3EE"), CE(ColorToken.AccentWarn, "#FF8A1F"),
                CE(ColorToken.AccentGold, "#F5C542"), CE(ColorToken.StateDanger, "#E5484D"), CE(ColorToken.StateDisabled, "#55554F"),
            };
        }

        if (isNew || theme.type == null || theme.type.Length == 0)
        {
            var russo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RussoPath);
            var medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BarlowMedium);
            var semi = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BarlowSemiBold);
            theme.type = new[]
            {
                TE(TypeRole.Display, russo, punch, 72f, true, 2f, ColorToken.TextPrimary),
                TE(TypeRole.Heading, russo, null, 36f, true, 4f, ColorToken.TextPrimary),
                TE(TypeRole.Body, medium, null, 24f, false, 0f, ColorToken.TextPrimary),
                TE(TypeRole.Utility, semi, null, 24f, false, 1f, ColorToken.TextPrimary),
                TE(TypeRole.Caption, medium, null, 21f, true, 5f, ColorToken.TextSecondary),
            };
        }

        if (isNew || theme.buttons == null || theme.buttons.Length == 0)
        {
            theme.buttons = new[]
            {
                BS(ButtonVariant.Primary, ColorToken.BrandLime, ColorToken.TextOnLime, 1f, 0.25f, 0.25f),
                BS(ButtonVariant.Secondary, ColorToken.BrandForest, ColorToken.TextPrimary, 1f, 0.15f, 0.25f),
                BS(ButtonVariant.Tertiary, ColorToken.SurfaceRaised, ColorToken.BrandLime, 0f, 0.12f, 0.2f),
                BS(ButtonVariant.Destructive, ColorToken.BrandBrick, ColorToken.TextPrimary, 1f, 0.15f, 0.25f),
                BS(ButtonVariant.Icon, ColorToken.SurfaceRaised, ColorToken.TextPrimary, 1f, 0.12f, 0.2f),
            };
        }

        // Sprites: keep anything already assigned (your real art); fill empty slots with placeholders.
        var map = new Dictionary<SpriteSlot, Sprite>();
        if (theme.sprites != null) foreach (var s in theme.sprites) if (s.sprite) map[s.slot] = s.sprite;
        foreach (var kv in placeholders) if (!map.ContainsKey(kv.Key) && kv.Value) map[kv.Key] = kv.Value;
        var list = new List<PimlrUITheme.SpriteEntry>();
        foreach (SpriteSlot slot in System.Enum.GetValues(typeof(SpriteSlot)))
            if (slot != SpriteSlot.None) list.Add(new PimlrUITheme.SpriteEntry { slot = slot, sprite = map.TryGetValue(slot, out var sp) ? sp : null });
        theme.sprites = list.ToArray();

        // Any enum value added since (e.g. TypeRole.PlayerName) gets a default entry; existing entries are never changed.
        PimlrUIThemeTools.AddMissingEntries(theme);

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        theme.Refresh();
        return theme;
    }

    static PimlrUITheme.ColorEntry CE(ColorToken t, string hex, float a = 1f)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        c.a = a;
        return new PimlrUITheme.ColorEntry { token = t, color = c };
    }

    static PimlrUITheme.TypeEntry TE(TypeRole r, TMP_FontAsset f, Material preset, float size, bool upper, float tracking, ColorToken color) =>
        new PimlrUITheme.TypeEntry { role = r, font = f, preset = preset, size = size, upperCase = upper, tracking = tracking, color = color };

    static PimlrUITheme.ButtonStyle BS(ButtonVariant v, ColorToken fill, ColorToken label, float alpha, float lift, float drop) =>
        new PimlrUITheme.ButtonStyle { variant = v, fill = fill, label = label, fillAlpha = alpha, hoverLift = lift, pressDrop = drop };
}
