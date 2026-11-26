// #UI: aspect-ratio / responsive check, part of every slice's definition of done.
// Sets the Game view to each target size, then inspects the LIVE UI under a root (Play mode recommended):
//   OFFSCREEN  part of a visible element is outside the screen (or the safe area)
//   OVERLAP    two interactive elements overlap (excluding parent/child)
//   OVERFLOW   a TMP text doesn't fit its box (wrapped/ellipsis/overflowing)
//   SMALLTEXT  rendered text smaller than 14 px at 720p-equivalent (scaled by the screen's short side)
//   TOUCH      (phone targets) an interactive element smaller than 44 pt on its short side
// Menu: PIMLR > UI > Aspect Check (Game view sizes are added once as custom sizes named "PIMLR ...").
//
// Usage from code (MCP): PimlrAspectCheck.SetSize(i) -> wait a frame -> PimlrAspectCheck.Check(root, i).
//
// Unreal note: the equivalent is the UMG Designer's screen-size presets + DPI preview, and "Safe Zone" debug.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PimlrAspectCheck
{
    public struct Target { public string name; public int w, h; public bool phone; }

    public static readonly Target[] Targets =
    {
        new Target { name = "16:9 1080p",            w = 1920, h = 1080 },
        new Target { name = "21:9 ultrawide",        w = 2560, h = 1080 },
        new Target { name = "4:3",                   w = 1440, h = 1080 },
        new Target { name = "Browser short window",  w = 1366, h = 600 },
        new Target { name = "Phone landscape 19.5:9", w = 2532, h = 1170, phone = true },
        new Target { name = "Phone portrait 9:19.5",  w = 1170, h = 2532, phone = true },
    };

    // ---------------- Game view size control (reflection; Unity keeps these internal) ----------------

    static readonly Assembly Ed = typeof(EditorWindow).Assembly;

    static object CurrentGroup()
    {
        var sizesType = Ed.GetType("UnityEditor.GameViewSizes");
        var instance = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        return sizesType.GetProperty("currentGroup").GetValue(instance);
    }

    static int FindOrAdd(int w, int h, string label)
    {
        var group = CurrentGroup();
        var gType = group.GetType();
        int total = (int)gType.GetMethod("GetTotalCount").Invoke(group, null);
        var getSize = gType.GetMethod("GetGameViewSize");
        for (int i = 0; i < total; i++)
        {
            var s = getSize.Invoke(group, new object[] { i });
            var st = s.GetType();
            if ((int)st.GetProperty("width").GetValue(s) == w && (int)st.GetProperty("height").GetValue(s) == h &&
                st.GetProperty("sizeType").GetValue(s).ToString() == "FixedResolution") return i;
        }
        var sizeType = Ed.GetType("UnityEditor.GameViewSize");
        var enumType = Ed.GetType("UnityEditor.GameViewSizeType");
        var fixedRes = System.Enum.Parse(enumType, "FixedResolution");
        var ctor = sizeType.GetConstructor(new[] { enumType, typeof(int), typeof(int), typeof(string) });
        var newSize = ctor.Invoke(new object[] { fixedRes, w, h, label });
        gType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
        return total; // appended at the end
    }

    public static string SetSize(int targetIndex)
    {
        var t = Targets[targetIndex];
        PIMLR.UI.PimlrResponsive.ForceTouch = t.phone ? true : (bool?)null; // phones are checked as touch devices
        int idx = FindOrAdd(t.w, t.h, "PIMLR " + t.name);
        var gvType = Ed.GetType("UnityEditor.GameView");
        var gv = EditorWindow.GetWindow(gvType, false, null, false);
        var cb = gvType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (cb != null) cb.Invoke(gv, new object[] { idx, null });
        else gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(gv, idx);
        gv.Repaint();
        return $"{t.name} ({t.w}x{t.h})";
    }

    [MenuItem("PIMLR/UI/Aspect Check (selected root)")]
    static void MenuCheckSelected()
    {
        var root = Selection.activeTransform;
        if (!root) { Debug.LogWarning("[PIMLR UI] Select the screen's root object first (e.g. CharaterCreatePanel)."); return; }
        Debug.Log(Check(root, -1));
    }

    // ---------------- checks ----------------

    public static string Check(Transform root, int targetIndex)
    {
        bool phone = targetIndex >= 0 && Targets[targetIndex].phone;
        float W = Screen.width, H = Screen.height, shortSide = Mathf.Min(W, H);
        var screen = new Rect(0, 0, W, H);
        var safe = Screen.safeArea;
        float minTextPx = 14f * shortSide / 720f;
        float minTouchPx = 44f * shortSide / 375f; // 44 pt on a 375-pt-wide phone

        var issues = new List<string>();
        var graphics = root.GetComponentsInChildren<Graphic>(false).Where(g => g.isActiveAndEnabled && g.canvasRenderer.GetAlpha() > 0.01f && g.color.a > 0.01f).ToList();

        foreach (var g in graphics)
        {
            var r = ScreenRect(g.rectTransform);
            if (r.width < 1 || r.height < 1) continue;
            if (r.width >= W * 0.95f && r.height >= H * 0.95f) continue; // full-screen backdrops/scrims may bleed
            if (!Contains(screen, r, 2f)) issues.Add($"OFFSCREEN  {Path(g.transform, root)}  ({Fmt(r)} vs screen {W}x{H})");
            else if (!Contains(safe, r, 2f)) issues.Add($"SAFEAREA   {Path(g.transform, root)}");
        }

        foreach (var t in root.GetComponentsInChildren<TMP_Text>(false).Where(t => t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text)))
        {
            t.ForceMeshUpdate();
            float px = t.fontSize * t.rectTransform.lossyScale.y;
            if (t.isTextOverflowing || t.isTextTruncated) issues.Add($"OVERFLOW   {Path(t.transform, root)}  \"{Short(t.text)}\"");
            if (px + 0.25f < minTextPx) issues.Add($"SMALLTEXT  {Path(t.transform, root)}  {px:0.#}px < {minTextPx:0.#}px");
        }

        var sels = root.GetComponentsInChildren<Selectable>(false).Where(s => s.isActiveAndEnabled && s.interactable).ToList();
        for (int i = 0; i < sels.Count; i++)
        {
            var a = ScreenRect((RectTransform)sels[i].transform);
            if (phone && Mathf.Min(a.width, a.height) + 0.5f < minTouchPx)
                issues.Add($"TOUCH      {Path(sels[i].transform, root)}  {Mathf.Min(a.width, a.height):0}px < {minTouchPx:0}px");
            for (int j = i + 1; j < sels.Count; j++)
            {
                if (sels[i].transform.IsChildOf(sels[j].transform) || sels[j].transform.IsChildOf(sels[i].transform)) continue;
                var b = ScreenRect((RectTransform)sels[j].transform);
                float ov = Overlap(a, b);
                if (ov > 0.10f * Mathf.Min(a.width * a.height, b.width * b.height))
                    issues.Add($"OVERLAP    {Path(sels[i].transform, root)}  x  {Path(sels[j].transform, root)}");
            }
        }

        var head = targetIndex >= 0 ? $"{Targets[targetIndex].name} ({W}x{H})" : $"{W}x{H}";
        var sb = new StringBuilder();
        sb.AppendLine($"{(issues.Count == 0 ? "PASS" : "FAIL")} {head}: {issues.Count} issue(s)");
        foreach (var i in issues.Distinct().Take(40)) sb.AppendLine("  " + i);
        return sb.ToString();
    }

    static Rect ScreenRect(RectTransform rt)
    {
        var c = new Vector3[4]; rt.GetWorldCorners(c);
        var canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = canvas && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        for (int i = 0; i < 4; i++) c[i] = RectTransformUtility.WorldToScreenPoint(cam, c[i]);
        float xMin = c.Min(p => p.x), xMax = c.Max(p => p.x), yMin = c.Min(p => p.y), yMax = c.Max(p => p.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    static bool Contains(Rect outer, Rect r, float tol) =>
        r.xMin >= outer.xMin - tol && r.yMin >= outer.yMin - tol && r.xMax <= outer.xMax + tol && r.yMax <= outer.yMax + tol;

    static float Overlap(Rect a, Rect b)
    {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return w > 0 && h > 0 ? w * h : 0f;
    }

    static string Path(Transform t, Transform root) => t == root ? t.name : Path(t.parent, root) + "/" + t.name;
    static string Fmt(Rect r) => $"{r.xMin:0},{r.yMin:0} {r.width:0}x{r.height:0}";
    static string Short(string s) => s.Length > 28 ? s.Substring(0, 28) + "…" : s;
}
