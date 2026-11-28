// #UI Slice 4b: small helpers for building gameplay-HUD pieces from code (music player, weapon carousel).
// Everything is themed through PimlrUITheme tokens (ThemedImage / ThemedText), so the theme asset re-skins it live and
// Craig's finished art drops into sprite slots without code changes.
// Sizes are canvas units on the 1280x720 "Global UI" HUD canvas (x1.5 = px at 1080p).
//
// Unreal note: these become small UMG base widgets (WBP_HudCard, WBP_HudText) whose style comes from UPimlrUIStyle.

using PIMLR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class PimlrHudKit
{
    /// <summary>Smallest HUD text in theme design px (1080p). 24.4 keeps text >= 14 px-at-720p even on 4:3, where the
    /// 1280x720 HUD canvas renders smallest (same reason objectives use Caption x1.16).</summary>
    public const float MinDesignPx = 24.4f;

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent ? parent.gameObject.layer : 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Anchor + pivot at one corner/point, then position and size.</summary>
    public static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = anchor;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    public static void Stretch(RectTransform r, float inset = 0f)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
    }

    public static Image Img(string name, Transform parent, ColorToken color, float alpha = 1f, SpriteSlot slot = SpriteSlot.None)
    {
        var r = Rect(name, parent);
        var img = r.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        var ti = r.gameObject.AddComponent<ThemedImage>();
        ti.color = color; ti.alpha = alpha; ti.sprite = slot;
        ti.Apply();
        return img;
    }

    /// <summary>A plain Image whose color a script sets (icons copied from existing art).</summary>
    public static Image Icon(string name, Transform parent, Sprite sprite)
    {
        var r = Rect(name, parent);
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
        img.color = PimlrUITheme.C(ColorToken.TextPrimary);
        return img;
    }

    public static TextMeshProUGUI Txt(string name, Transform parent, TypeRole role, float designPx, ColorToken color,
                                      TextCase textCase = TextCase.FromRole, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        var r = Rect(name, parent);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.raycastTarget = false;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        var tt = r.gameObject.AddComponent<ThemedText>();
        tt.role = role;
        tt.textCase = textCase;
        tt.overrideColor = true; tt.color = color;
        float roleSize = 24f;
        var theme = PimlrUITheme.Current;
        if (theme && theme.TryType(role, out var e) && e.size > 0f) roleSize = e.size;
        tt.sizeScale = Mathf.Max(designPx, MinDesignPx) / roleSize;
        tt.Apply();
        return t;
    }

    /// <summary>Thin edges (top/bottom/left/right) drawn as four Images, for frames until a 9-slice sprite exists.</summary>
    public static GameObject Edges(string name, Transform parent, ColorToken color, float thickness, bool top = true, bool bottom = true, bool left = true, bool right = true)
    {
        var holder = Rect(name, parent);
        Stretch(holder);
        void Edge(string n, Vector2 aMin, Vector2 aMax, Vector2 size)
        {
            var img = Img(n, holder, color);
            var rt = img.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(aMin.x == aMax.x ? (aMin.x == 0f ? thickness * 0.5f : -thickness * 0.5f) : 0f,
                                              aMin.y == aMax.y ? (aMin.y == 0f ? thickness * 0.5f : -thickness * 0.5f) : 0f);
        }
        if (top) Edge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, thickness));
        if (bottom) Edge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, thickness));
        if (left) Edge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(thickness, 0f));
        if (right) Edge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(thickness, 0f));
        return holder.gameObject;
    }

    /// <summary>Hex for TMP rich text from a theme token.</summary>
    public static string Hex(ColorToken t) => "#" + ColorUtility.ToHtmlStringRGB(PimlrUITheme.C(t));

    /// <summary>Canvas units per screen pixel of the canvas this rect lives on (1 / scaleFactor).</summary>
    public static float UnitsPerPixel(Component c)
    {
        var canvas = c ? c.GetComponentInParent<Canvas>() : null;
        return canvas && canvas.rootCanvas.scaleFactor > 0f ? 1f / canvas.rootCanvas.scaleFactor : 1f;
    }
}
