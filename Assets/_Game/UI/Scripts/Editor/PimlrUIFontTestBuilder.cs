// #UI: builds Assets/_Game/UI/Showcase/UI_FontTest.unity — every type role rendered at its true on-screen pixel size
// for 720p, 1080p and 1440p side by side (Constant Pixel Size canvas, so a 1920x1080 capture is 1:1 pixels),
// plus sample player names that exercise accents, symbols, the Barlow fallback and unsupported scripts.
// Menu: PIMLR > UI > Build Font Test Scene

using PIMLR.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PimlrUIFontTestBuilder
{
    public const string ScenePath = "Assets/_Game/UI/Showcase/UI_FontTest.unity";

    static readonly (string label, float scale)[] Targets = { ("720p", 720f / 1080f), ("1080p", 1f), ("1440p", 1440f / 1080f) };

    [MenuItem("PIMLR/UI/Build Font Test Scene")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static string Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = PimlrUITheme.C(ColorToken.SurfaceBase);

        var cgo = new GameObject("Font Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cgo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var cs = cgo.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; cs.scaleFactor = 1f;

        var rows = new (TypeRole role, float roleScale, string sample, string note)[]
        {
            (TypeRole.Caption, 0.85f, "HEALTH  STAMINA  WAVE PROGRESS", "HUD label (Caption x0.85)"),
            (TypeRole.Caption, 1f, "TRACK BOOST ACTIVE  87 WASHES", "Caption"),
            (TypeRole.Body, 1f, "Protect the fans. Boost: +15% speed", "Body"),
            (TypeRole.Utility, 1f, "24 / 120   12:43   1,250 coins", "Utility"),
            (TypeRole.PlayerName, 1f, "José  Zoë  Nguyễn  xX_Sn1p3r_Xx", "PlayerName (original case)"),
            (TypeRole.PlayerName, 1f, "Владимир  Łukasz  O'Neil-Smith", "PlayerName"),
            (TypeRole.PlayerName, 1f, "Γιώργος  東京  Lil 💧  ★", "PlayerName: unsupported scripts"),
            (TypeRole.Heading, 1f, "LEADERBOARD  HATER FREE", "Heading"),
            (TypeRole.Display, 0.6f, "WAVE 3 CLEARED", "Display x0.6"),
        };

        float colW = 620f;
        for (int c = 0; c < Targets.Length; c++)
        {
            var (label, s) = Targets[c];
            float x = 20f + c * (colW + 20f);
            var head = Txt(cgo.transform, $"{label} header", $"{label}  (x{s:0.###})", TypeRole.Caption, 1f, x, 16f, colW, ColorToken.BrandLime);
            head.fontSize = 18f;
            float y = 52f;
            foreach (var r in rows)
            {
                PimlrUITheme.Current.TryType(r.role, out var e);
                float px = e.size * r.roleScale * s;
                var note = Txt(cgo.transform, "note", $"{r.note} — {px:0.#} px", TypeRole.Caption, 1f, x, y, colW, ColorToken.TextSecondary);
                note.fontSize = 13f;
                y += 18f;
                var t = Txt(cgo.transform, $"{label} {r.note}", r.sample, r.role, r.roleScale * s, x, y, colW, null);
                y += Mathf.Max(px * 1.35f, 16f) + 10f;
            }
        }
        EditorSceneManager.SaveScene(scene, ScenePath);
        return ScenePath;
    }

    static TextMeshProUGUI Txt(Transform parent, string name, string text, TypeRole role, float scale, float x, float y, float w, ColorToken? color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text; t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
        t.alignment = TextAlignmentOptions.TopLeft; t.raycastTarget = false;
        var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, 60);
        var th = go.AddComponent<ThemedText>(); th.role = role; th.sizeScale = scale;
        if (color.HasValue) { th.overrideColor = true; th.color = color.Value; }
        th.Apply();
        return t;
    }
}
