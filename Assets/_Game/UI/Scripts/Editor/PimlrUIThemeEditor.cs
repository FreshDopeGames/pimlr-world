// #UI: editor support for PimlrUITheme.
// - Inspector: lists validation issues (duplicates, missing tokens/roles/variants, unassigned fonts, bad presets)
//   with an "Add Missing Entries" fix and a "Re-apply to Open Scenes" button.
// - Reimport hook: when the theme asset changes on disk (git pull, external edit), open scenes re-skin.
// - Build gate: a build fails with a clear message if the theme has errors (e.g. a type role with no font).

using System.Collections.Generic;
using System.Linq;
using PIMLR.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[CustomEditor(typeof(PimlrUITheme))]
public class PimlrUIThemeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var theme = (PimlrUITheme)target;
        var issues = theme.Validate();
        int errors = issues.Count(i => i.severity == PimlrUITheme.Severity.Error);
        int warnings = issues.Count(i => i.severity == PimlrUITheme.Severity.Warning);

        if (issues.Count == 0) EditorGUILayout.HelpBox("Theme is valid.", MessageType.Info);
        else
        {
            EditorGUILayout.HelpBox($"{errors} error(s), {warnings} warning(s). Errors block builds.", errors > 0 ? MessageType.Error : MessageType.Warning);
            foreach (var i in issues.Where(i => i.severity != PimlrUITheme.Severity.Info))
                EditorGUILayout.HelpBox(i.message, i.severity == PimlrUITheme.Severity.Error ? MessageType.Error : MessageType.Warning);
            int infos = issues.Count(i => i.severity == PimlrUITheme.Severity.Info);
            if (infos > 0) EditorGUILayout.HelpBox($"{infos} note(s), e.g. empty optional sprite slots. See Theme Audit for the list.", MessageType.None);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Missing Entries")) { PimlrUIThemeTools.AddMissingEntries(theme); GUIUtility.ExitGUI(); }
        if (GUILayout.Button("Re-apply to Open Scenes")) theme.Refresh();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}

public static class PimlrUIThemeTools
{
    /// <summary>Adds an entry for every enum value the theme lacks (defaults from the approved token table). Never changes existing entries.</summary>
    public static void AddMissingEntries(PimlrUITheme theme)
    {
        Undo.RecordObject(theme, "Add Missing Theme Entries");

        var colors = (theme.colors ?? new PimlrUITheme.ColorEntry[0]).ToList();
        foreach (ColorToken t in System.Enum.GetValues(typeof(ColorToken)))
            if (!PimlrUITheme.IsBuiltIn(t) && colors.All(c => c.token != t))
                colors.Add(new PimlrUITheme.ColorEntry { token = t, color = PimlrUITheme.DefaultColor(t) });
        theme.colors = colors.ToArray();

        var type = (theme.type ?? new PimlrUITheme.TypeEntry[0]).ToList();
        foreach (TypeRole r in System.Enum.GetValues(typeof(TypeRole)))
            if (type.All(e => e.role != r)) type.Add(DefaultType(r, type));
        theme.type = type.ToArray();

        var buttons = (theme.buttons ?? new PimlrUITheme.ButtonStyle[0]).ToList();
        foreach (ButtonVariant v in System.Enum.GetValues(typeof(ButtonVariant)))
            if (buttons.All(b => b.variant != v))
                buttons.Add(new PimlrUITheme.ButtonStyle { variant = v, fill = ColorToken.SurfaceRaised, label = ColorToken.TextPrimary, fillAlpha = 1f, hoverLift = 0.12f, pressDrop = 0.2f });
        theme.buttons = buttons.ToArray();

        var sprites = (theme.sprites ?? new PimlrUITheme.SpriteEntry[0]).ToList();
        foreach (SpriteSlot s in System.Enum.GetValues(typeof(SpriteSlot)))
            if (s != SpriteSlot.None && sprites.All(e => e.slot != s)) sprites.Add(new PimlrUITheme.SpriteEntry { slot = s });
        theme.sprites = sprites.ToArray();

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        theme.Refresh();
    }

    static PimlrUITheme.TypeEntry DefaultType(TypeRole r, List<PimlrUITheme.TypeEntry> existing)
    {
        TMP_FontAsset FontOf(TypeRole other) => existing.FirstOrDefault(e => e.role == other).font;
        switch (r)
        {
            case TypeRole.PlayerName:
                return new PimlrUITheme.TypeEntry { role = r, font = FontOf(TypeRole.Heading), size = 30f, upperCase = false, tracking = 1f, color = ColorToken.TextPrimary };
            case TypeRole.Display:
                return new PimlrUITheme.TypeEntry { role = r, font = FontOf(TypeRole.Heading), size = 72f, upperCase = true, tracking = 2f, color = ColorToken.TextPrimary };
            case TypeRole.Heading:
                return new PimlrUITheme.TypeEntry { role = r, font = FontOf(TypeRole.Display), size = 36f, upperCase = true, tracking = 4f, color = ColorToken.TextPrimary };
            case TypeRole.Caption:
                return new PimlrUITheme.TypeEntry { role = r, font = FontOf(TypeRole.Body), size = 18f, upperCase = true, tracking = 6f, color = ColorToken.TextSecondary };
            case TypeRole.Utility:
                return new PimlrUITheme.TypeEntry { role = r, font = FontOf(TypeRole.Body), size = 24f, upperCase = false, tracking = 1f, color = ColorToken.TextPrimary };
            default:
                return new PimlrUITheme.TypeEntry { role = r, font = FontOf(TypeRole.Utility), size = 24f, upperCase = false, tracking = 0f, color = ColorToken.TextPrimary };
        }
    }
}

/// <summary>Theme asset changed on disk (git pull, external edit): re-skin whatever is open.</summary>
class PimlrUIThemeReimportHook : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var p in imported)
        {
            if (!p.EndsWith(".asset")) continue;
            if (AssetDatabase.GetMainAssetTypeAtPath(p) != typeof(PimlrUITheme)) continue;
            var theme = AssetDatabase.LoadAssetAtPath<PimlrUITheme>(p);
            if (theme) PimlrEditorDefer.NextTick(() => { if (theme) theme.Refresh(); });
        }
    }
}

/// <summary>Blocks a build when the UI theme has errors, so a missing required font is caught before shipping.</summary>
class PimlrUIThemeBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var theme = AssetDatabase.LoadAssetAtPath<PimlrUITheme>("Assets/_Game/Resources/Pimlr/UI/PimlrUITheme.asset");
        if (!theme)
        {
            Debug.LogWarning("[PIMLR UI] Build: no UI theme asset found; themed UI will keep its authored look.");
            return;
        }
        var issues = theme.Validate();
        foreach (var w in issues.Where(i => i.severity == PimlrUITheme.Severity.Warning)) Debug.LogWarning("[PIMLR UI] Build: " + w.message);
        var errors = issues.Where(i => i.severity == PimlrUITheme.Severity.Error).ToList();
        if (errors.Count > 0)
            throw new BuildFailedException("[PIMLR UI] Theme has errors (fix in the PimlrUITheme Inspector):\n- " + string.Join("\n- ", errors.Select(e => e.message)));
    }
}
