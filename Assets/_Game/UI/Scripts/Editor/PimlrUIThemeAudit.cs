// #UI: read-only report of how far each scene is from the PIMLR UI design system. Run before/after every slice;
// the "untokened" counts should only go down. Also exports the theme tokens as JSON for the Unreal style asset.
//
// Menus: PIMLR > UI > Theme Audit (Open Scenes) / Theme Audit (Shipping Scenes) / Export Theme JSON
// Reports are written to <project>/Docs/UIAudit/ (outside Assets, so nothing is imported).

using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PIMLR.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PimlrUIThemeAudit
{
    public static readonly string[] ShippingScenes =
    {
        "Assets/_Game/Scene/PIMLR_World/00_MainMenu.unity",
        "Assets/_Game/Scene/PIMLR_World/SceneStaticEU.unity",
        "Assets/_Game/Scene/PIMLR_World/YannicksWorld.unity",
    };

    static readonly string[] DefaultSprites = { "UISprite", "Background", "InputFieldBackground", "Knob", "UIMask", "Checkmark", "DropdownArrow" };
    const string OutDir = "Docs/UIAudit";

    [MenuItem("PIMLR/UI/Theme Audit (Open Scenes)")]
    public static string AuditOpenScenes()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded) sb.AppendLine(AuditScene(s));
        }
        return Write("open-scenes", sb.ToString());
    }

    [MenuItem("PIMLR/UI/Theme Audit (Shipping Scenes)")]
    public static string AuditShippingScenes()
    {
        var sb = new StringBuilder();
        foreach (var path in ShippingScenes)
        {
            var already = SceneManager.GetSceneByPath(path);
            bool opened = !already.isLoaded;
            var s = opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : already;
            sb.AppendLine(AuditScene(s));
            if (opened) EditorSceneManager.CloseScene(s, true);
        }
        sb.AppendLine(AuditCodeBuiltUI());
        sb.AppendLine(AuditTheme());
        return Write("shipping", sb.ToString());
    }

    public static string AuditScene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        T[] All<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        var images = All<Image>();
        var tmps = All<TMP_Text>();
        var legacy = All<Text>();
        var buttons = All<Button>();
        var canvases = All<Canvas>().Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace).ToArray();

        var theme = PimlrUITheme.Current;
        var themeFonts = theme ? theme.type.Select(t => t.font).Where(f => f).ToHashSet() : new System.Collections.Generic.HashSet<TMP_FontAsset>();

        int themedImg = images.Count(i => i.GetComponent<ThemedImage>() || IsButtonGraphic(i));
        int slicedAsSimple = images.Count(i => i.sprite && i.sprite.border != Vector4.zero && i.type == Image.Type.Simple);
        int defaultSprites = images.Count(i => i.sprite && DefaultSprites.Contains(i.sprite.name));
        int themedTxt = tmps.Count(t => t.GetComponent<ThemedText>());
        int nullFont = tmps.Count(t => !t.font);
        int offTheme = tmps.Count(t => t.font && !themeFonts.Contains(t.font));
        int themedBtn = buttons.Count(b => b.GetComponent<ThemedButton>());

        var sb = new StringBuilder();
        sb.AppendLine($"## {scene.name}");
        sb.AppendLine();
        sb.AppendLine("| Check | Count |");
        sb.AppendLine("| --- | --- |");
        sb.AppendLine($"| Images themed / total | {themedImg} / {images.Length} |");
        sb.AppendLine($"| 9-slice sprites drawn as Simple (stretched corners) | {slicedAsSimple} |");
        sb.AppendLine($"| Unity default sprites (UISprite, Background, ...) | {defaultSprites} |");
        sb.AppendLine($"| TMP texts themed / total | {themedTxt} / {tmps.Length} |");
        sb.AppendLine($"| TMP texts using a font outside the theme | {offTheme} |");
        sb.AppendLine($"| TMP texts with no font | {nullFont} |");
        sb.AppendLine($"| Legacy UnityEngine.UI.Text | {legacy.Length} |");
        sb.AppendLine($"| Buttons themed / total | {themedBtn} / {buttons.Length} |");
        sb.AppendLine();
        sb.AppendLine("Screen-space canvases:");
        foreach (var c in canvases)
        {
            var cs = c.GetComponent<CanvasScaler>();
            bool ok = cs && cs.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                      && cs.referenceResolution == new Vector2(1920, 1080) && Mathf.Approximately(cs.matchWidthOrHeight, 0.5f);
            string desc = cs ? $"{cs.uiScaleMode} {cs.referenceResolution.x}x{cs.referenceResolution.y} match {cs.matchWidthOrHeight}" : "no CanvasScaler";
            sb.AppendLine($"- {(ok ? "OK " : "FIX")} `{Path(c.transform)}` ({desc})");
        }
        return sb.ToString();
    }

    static bool IsButtonGraphic(Image i)
    {
        var b = i.GetComponent<Button>();
        return b && b.targetGraphic == i && b.GetComponent<ThemedButton>();
    }

    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;

    /// <summary>Theme asset validation (same checks as the Inspector and the build gate).</summary>
    public static string AuditTheme()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Theme validation");
        sb.AppendLine();
        var t = PimlrUITheme.Current;
        if (!t) { sb.AppendLine("- ERROR: no theme asset at Resources/" + PimlrUITheme.ResourcePath); return sb.ToString(); }
        var issues = t.Validate();
        if (issues.Count == 0) sb.AppendLine("- Valid, no issues.");
        foreach (var i in issues.OrderByDescending(i => i.severity)) sb.AppendLine($"- {i.severity.ToString().ToUpper()}: {i.message}");
        return sb.ToString();
    }

    /// <summary>Code-built UI: scripts that create UI at runtime and still hardcode colors instead of using the theme.</summary>
    public static string AuditCodeBuiltUI()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Code-built UI (hardcoded colors)");
        sb.AppendLine();
        sb.AppendLine("| Script | new Color literals | Uses theme |");
        sb.AppendLine("| --- | --- | --- |");
        var files = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Game/Scripts", "Assets/Scripts", "Assets/AddedImplements" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".cs"));
        foreach (var f in files)
        {
            var src = File.ReadAllText(f);
            if (!Regex.IsMatch(src, @"AddComponent<(Image|TextMeshProUGUI|RawImage)>")) continue;
            int colors = Regex.Matches(src, @"new Color(32)?\(").Count;
            bool themed = src.Contains("PimlrUITheme");
            sb.AppendLine($"| {System.IO.Path.GetFileName(f)} | {colors} | {(themed ? "yes" : "no")} |");
        }
        return sb.ToString();
    }

    static string Write(string name, string body)
    {
        Directory.CreateDirectory(OutDir);
        string file = $"{OutDir}/ThemeAudit_{name}_{System.DateTime.Now:yyyy-MM-dd_HHmm}.md";
        File.WriteAllText(file, $"# PIMLR UI Theme Audit ({System.DateTime.Now:yyyy-MM-dd HH:mm})\n\n{body}");
        Debug.Log($"[PIMLR UI] Audit written to {System.IO.Path.GetFullPath(file)}");
        return body;
    }

    // ---------- Unreal hand-off ----------

    [System.Serializable] class JColor { public string token; public string hex; public float alpha; }
    [System.Serializable] class JType { public string role; public string font; public string preset; public float size; public bool upperCase; public float tracking; public string color; }
    [System.Serializable] class JSprite { public string slot; public string path; public Vector4 border; public Vector2 size; }
    [System.Serializable] class JButton { public string variant; public string fill; public string label; public float fillAlpha; public float hoverLift; public float pressDrop; }
    [System.Serializable] class JTheme { public JColor[] colors; public JType[] type; public JSprite[] sprites; public JButton[] buttons; public float hoverScale, hoverTime, pressTime, panelInTime, panelOutTime, toastTime; public Vector2 referenceResolution; }

    [MenuItem("PIMLR/UI/Export Theme JSON (for Unreal)")]
    public static string ExportJson()
    {
        var t = PimlrUITheme.Current;
        if (!t) { Debug.LogWarning("[PIMLR UI] No theme asset. Run PIMLR > UI > Setup Theme first."); return null; }
        var j = new JTheme
        {
            colors = t.colors.Select(c => new JColor { token = c.token.ToString(), hex = "#" + ColorUtility.ToHtmlStringRGB(c.color), alpha = c.color.a }).ToArray(),
            type = t.type.Select(e => new JType { role = e.role.ToString(), font = e.font ? e.font.name : null, preset = e.preset ? e.preset.name : null, size = e.size, upperCase = e.upperCase, tracking = e.tracking, color = e.color.ToString() }).ToArray(),
            sprites = t.sprites.Where(s => s.sprite).Select(s => new JSprite { slot = s.slot.ToString(), path = AssetDatabase.GetAssetPath(s.sprite), border = s.sprite.border, size = s.sprite.rect.size }).ToArray(),
            buttons = t.buttons.Select(b => new JButton { variant = b.variant.ToString(), fill = b.fill.ToString(), label = b.label.ToString(), fillAlpha = b.fillAlpha, hoverLift = b.hoverLift, pressDrop = b.pressDrop }).ToArray(),
            hoverScale = t.hoverScale, hoverTime = t.hoverTime, pressTime = t.pressTime, panelInTime = t.panelInTime, panelOutTime = t.panelOutTime, toastTime = t.toastTime,
            referenceResolution = t.referenceResolution,
        };
        Directory.CreateDirectory(OutDir);
        string file = $"{OutDir}/PimlrUITheme.json";
        File.WriteAllText(file, JsonUtility.ToJson(j, true));
        Debug.Log($"[PIMLR UI] Theme exported to {System.IO.Path.GetFullPath(file)}");
        return file;
    }
}
