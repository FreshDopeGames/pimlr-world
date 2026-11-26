// #UI: single source of truth for the PIMLR UI look (colors, type roles, sprites, button variants, motion).
// Runtime copy lives at Resources/PIMLR/UI/PimlrUITheme.asset, loaded on demand like STFStyle (no scene references needed).
// Editing the asset in the Inspector re-skins every Themed* component live, in Edit and Play mode.
//
// Lifecycle rules (see PimlrUIThemeSelfTest for the checks):
// - Components subscribe in OnEnable and unsubscribe in OnDisable, so scene loads, pooling and Destroy never leave
//   stale handlers. RaiseChanged also skips and drops handlers whose Unity object was destroyed.
// - The Changed event is NOT cleared on SubsystemRegistration: with domain reload off, components that stay enabled
//   across Play mode entry would otherwise lose their subscription. Only caches and warnings reset there.
// - A missing theme asset or entry never throws: lookups fall back to built-in defaults and warn once.
//
// Unreal note: becomes UPimlrUIStyle (UDataAsset) read by base UMG widgets in PreConstruct; ButtonStyles map to
// UCommonButtonStyle, TypeEntries to UCommonTextStyle. Use PIMLR > UI > Export Theme JSON to carry the values over.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PIMLR.UI
{
    [CreateAssetMenu(menuName = "PIMLR/UI Theme", fileName = "PimlrUITheme")]
    public class PimlrUITheme : ScriptableObject
    {
        [Serializable] public struct ColorEntry { public ColorToken token; public Color color; }

        [Serializable]
        public struct TypeEntry
        {
            public TypeRole role;
            public TMP_FontAsset font;
            [Tooltip("Optional TMP material preset (outline / hard shadow). Must be made from the same font asset.")]
            public Material preset;
            [Tooltip("Size in px at the 1920x1080 reference resolution.")]
            public float size;
            public bool upperCase;
            public float tracking;
            public ColorToken color;
        }

        [Serializable] public struct SpriteEntry { public SpriteSlot slot; public Sprite sprite; }

        [Serializable]
        public struct ButtonStyle
        {
            public ButtonVariant variant;
            public ColorToken fill;
            public ColorToken label;
            [Range(0f, 1f)] public float fillAlpha;
            [Tooltip("How much lighter the fill gets on hover/focus (0-1).")]
            [Range(0f, 0.5f)] public float hoverLift;
            [Tooltip("How much darker the fill gets on press (0-1).")]
            [Range(0f, 0.5f)] public float pressDrop;
        }

        public enum Severity { Info, Warning, Error }
        public struct Issue { public Severity severity; public string message; public override string ToString() => $"[{severity}] {message}"; }

        public const string ResourcePath = "PIMLR/UI/PimlrUITheme";

        public ColorEntry[] colors = Array.Empty<ColorEntry>();
        public TypeEntry[] type = Array.Empty<TypeEntry>();
        public SpriteEntry[] sprites = Array.Empty<SpriteEntry>();
        public ButtonStyle[] buttons = Array.Empty<ButtonStyle>();

        [Header("Textures")]
        [Tooltip("Opacity of every brand-pattern layer (ThemedImage with the Pattern slot). 0.07 keeps carbon surfaces from reading green.")]
        [Range(0f, 1f)] public float patternOpacity = 0.07f;

        [Header("Focus")]
        public ColorToken focusColor = ColorToken.BrandLime;

        [Header("Motion (seconds, unscaled time)")]
        public float hoverScale = 1.04f;
        [Tooltip("Hover scale for ButtonMotion.Compact (inventory slots, list rows, icon buttons).")]
        public float compactHoverScale = 1.02f;
        public float hoverTime = 0.08f;
        public float pressTime = 0.05f;
        public float panelInTime = 0.22f;
        public float panelOutTime = 0.15f;
        public float toastTime = 0.35f;

        [Header("Layout (px at 1920x1080)")]
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [Range(0f, 1f)] public float matchWidthOrHeight = 0.5f;
        public float spacingUnit = 8f;
        [Tooltip("Smallest allowed text size in px at the 1920x1080 reference. 21 = 14 px on a 720p screen. ThemedText never renders below it.")]
        public float minTextSize = 21f;

        // ---------------- change notification ----------------

        /// <summary>Raised whenever the active theme's values change (Inspector edit, theme swap, reimport). Themed* components re-apply.</summary>
        public static event Action Changed;

        /// <summary>Live handler count, for lifecycle tests: it must return to its baseline after objects are destroyed.</summary>
        public static int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;

        static PimlrUITheme cached;
        static PimlrUITheme overrideTheme;
        static bool warnedMissingTheme;
        static readonly HashSet<string> warnedOnce = new HashSet<string>();
#if UNITY_EDITOR
        static bool raisePending;
#endif

        Dictionary<ColorToken, Color> colorMap;
        Dictionary<TypeRole, TypeEntry> typeMap;
        Dictionary<SpriteSlot, Sprite> spriteMap;
        Dictionary<ButtonVariant, ButtonStyle> buttonMap;

        /// <summary>The active theme: an override set at runtime (A/B tests, showcase), else the Resources copy. Null only if the asset is missing (warned once).</summary>
        public static PimlrUITheme Current
        {
            get
            {
                if (overrideTheme) return overrideTheme;
                if (!cached) cached = Resources.Load<PimlrUITheme>(ResourcePath);
                if (!cached && !warnedMissingTheme)
                {
                    warnedMissingTheme = true;
                    Debug.LogWarning($"[PIMLR UI] No theme at Resources/{ResourcePath}. Themed UI keeps its authored look. Run PIMLR > UI > Setup Theme.");
                }
                return cached;
            }
        }

        /// <summary>Swap the whole look at runtime (pass null to return to the default asset).</summary>
        public static void SetOverride(PimlrUITheme theme)
        {
            overrideTheme = theme;
            if (theme) theme.Rebuild(); // arrays may have been edited after the asset/clone was created
            RaiseChanged();
        }

        /// <summary>Notify every live Themed* component. Handlers on destroyed objects are dropped; one bad handler can't stop the rest.</summary>
        public static void RaiseChanged()
        {
            var list = Changed?.GetInvocationList();
            if (list == null) return;
            foreach (var d in list)
            {
                if (d.Target is UnityEngine.Object o && !o) { Changed -= (Action)d; continue; }
                try { ((Action)d)(); }
                catch (Exception e) { Debug.LogException(e, d.Target as UnityEngine.Object); }
            }
        }

        /// <summary>Editor-safe deferred notify: coalesces bursts (Inspector drags, undo) into one re-skin on the next editor tick.</summary>
        public static void RaiseChangedDeferred()
        {
#if UNITY_EDITOR
            if (raisePending) return;
            raisePending = true;
            PimlrEditorDefer.NextTick(() => { raisePending = false; RaiseChanged(); });
#else
            RaiseChanged();
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cached = null;
            overrideTheme = null;
            warnedMissingTheme = false;
            warnedOnce.Clear();
        }

        void OnEnable() => Rebuild();

        void OnValidate()
        {
            // Only data work here: graphics must not be touched inside OnValidate, so the re-skin is deferred.
            Rebuild();
            RaiseChangedDeferred();
        }

        /// <summary>Call after changing the theme's arrays from code (editor tools): rebuilds lookups and re-skins everything now.</summary>
        public void Refresh()
        {
            Rebuild();
            RaiseChanged();
        }

        void Rebuild()
        {
            colorMap = new Dictionary<ColorToken, Color>();
            if (colors != null) foreach (var c in colors) if (!colorMap.ContainsKey(c.token)) colorMap[c.token] = c.color; // first entry wins on duplicates
            typeMap = new Dictionary<TypeRole, TypeEntry>();
            if (type != null) foreach (var t in type) if (!typeMap.ContainsKey(t.role)) typeMap[t.role] = t;
            spriteMap = new Dictionary<SpriteSlot, Sprite>();
            if (sprites != null) foreach (var s in sprites) if (!spriteMap.ContainsKey(s.slot)) spriteMap[s.slot] = s.sprite;
            buttonMap = new Dictionary<ButtonVariant, ButtonStyle>();
            if (buttons != null) foreach (var b in buttons) if (!buttonMap.ContainsKey(b.variant)) buttonMap[b.variant] = b;
        }

        static void WarnOnce(string key, string msg)
        {
            if (warnedOnce.Add(key)) Debug.LogWarning("[PIMLR UI] " + msg);
        }

        // ---------------- lookups (never throw) ----------------

        /// <summary>Approved token values, used when the asset is missing an entry.</summary>
        public static Color DefaultColor(ColorToken t)
        {
            switch (t)
            {
                case ColorToken.SurfaceBase: return Hex(0x12, 0x12, 0x11);   // carbon (brand charcoal #222220 now lives in TextOnLime)
                case ColorToken.SurfaceRaised: return Hex(0x1E, 0x1E, 0x1C);
                case ColorToken.SurfaceScrim: return Hex(0x07, 0x07, 0x07, 0.75f);
                case ColorToken.BrandForest: return Hex(0x3C, 0x6C, 0x1A);
                case ColorToken.BrandBrick: return Hex(0x87, 0x28, 0x18);
                case ColorToken.BrandLime: return Hex(0xCD, 0xF9, 0x52);
                case ColorToken.TextPrimary: return Hex(0xF4, 0xF1, 0xEA);
                case ColorToken.TextSecondary: return Hex(0xA8, 0xA6, 0x9E);
                case ColorToken.TextOnLime: return Hex(0x22, 0x22, 0x20);
                case ColorToken.AccentMusic: return Hex(0x9B, 0x5C, 0xF6);
                case ColorToken.AccentInfo: return Hex(0x22, 0xD3, 0xEE);
                case ColorToken.AccentWarn: return Hex(0xFF, 0x8A, 0x1F);
                case ColorToken.AccentGold: return Hex(0xF5, 0xC5, 0x42);
                case ColorToken.StateDanger: return Hex(0xE5, 0x48, 0x4D);
                case ColorToken.StateDisabled: return Hex(0x55, 0x55, 0x4F);
                case ColorToken.Clear: return new Color(0f, 0f, 0f, 0f);
                default: return UnityEngine.Color.white;
            }
        }

        static Color Hex(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);

        /// <summary>White and Clear are built-in; everything else comes from the asset, or the default table if missing.</summary>
        public static bool IsBuiltIn(ColorToken t) => t == ColorToken.White || t == ColorToken.Clear;

        public Color Color(ColorToken token)
        {
            if (IsBuiltIn(token)) return DefaultColor(token);
            if (colorMap == null) Rebuild();
            if (colorMap.TryGetValue(token, out var c)) return c;
            WarnOnce("color:" + token, $"Theme '{name}' has no color for {token}; using the default. Fix it in the theme Inspector.");
            return DefaultColor(token);
        }

        public Color Color(ColorToken token, float alpha)
        {
            var c = Color(token);
            c.a *= alpha;
            return c;
        }

        /// <summary>False when the role is missing or has no font: callers keep the text's current font.</summary>
        public bool TryType(TypeRole role, out TypeEntry entry)
        {
            if (typeMap == null) Rebuild();
            if (typeMap.TryGetValue(role, out entry)) return true;
            WarnOnce("type:" + role, $"Theme '{name}' has no type role {role}; text keeps its current style.");
            return false;
        }

        /// <summary>Null when the slot is empty: callers keep the Image's current sprite (sprites are optional).</summary>
        public Sprite Sprite(SpriteSlot slot)
        {
            if (slot == SpriteSlot.None) return null;
            if (spriteMap == null) Rebuild();
            return spriteMap.TryGetValue(slot, out var s) ? s : null;
        }

        public bool TryButton(ButtonVariant variant, out ButtonStyle style)
        {
            if (buttonMap == null) Rebuild();
            if (buttonMap.TryGetValue(variant, out style)) return true;
            WarnOnce("button:" + variant, $"Theme '{name}' has no button style {variant}; button keeps its current look.");
            return false;
        }

        /// <summary>Static shortcut for code-built UI. Falls back to the default token value when there's no theme asset.</summary>
        public static Color C(ColorToken token, float alpha = 1f)
        {
            var t = Current;
            var c = t ? t.Color(token) : DefaultColor(token);
            c.a *= alpha;
            return c;
        }

        /// <summary>Static shortcut for code-built UI: font for a type role (null = keep your current font).</summary>
        public static TMP_FontAsset Font(TypeRole role)
        {
            var t = Current;
            return t && t.TryType(role, out var e) ? e.font : null;
        }

        // ---------------- validation ----------------

        /// <summary>Everything that's wrong or incomplete with this theme. Errors block builds (PimlrUIThemeBuildCheck).</summary>
        public List<Issue> Validate()
        {
            var issues = new List<Issue>();
            void Add(Severity s, string m) => issues.Add(new Issue { severity = s, message = m });

            void CheckEnum<TEnum, TEntry>(TEntry[] arr, Func<TEntry, TEnum> key, string label, Func<TEnum, bool> required, Severity missingSeverity)
                where TEnum : struct, Enum
            {
                var seen = new Dictionary<TEnum, int>();
                if (arr != null) foreach (var e in arr) { var k = key(e); seen[k] = seen.TryGetValue(k, out var n) ? n + 1 : 1; }
                foreach (var kv in seen) if (kv.Value > 1) Add(Severity.Error, $"{label}: {kv.Key} appears {kv.Value} times (only the first is used).");
                foreach (TEnum v in Enum.GetValues(typeof(TEnum)))
                    if (required(v) && !seen.ContainsKey(v)) Add(missingSeverity, $"{label}: no entry for {v}.");
            }

            CheckEnum(colors, e => e.token, "Colors", t => !IsBuiltIn(t), Severity.Warning);
            CheckEnum(type, e => e.role, "Type roles", r => true, Severity.Error);
            CheckEnum(buttons, e => e.variant, "Buttons", v => true, Severity.Warning);
            CheckEnum(sprites, e => e.slot, "Sprites", s => s != SpriteSlot.None, Severity.Info);

            if (type != null)
                foreach (var t in type)
                {
                    if (!t.font) { Add(Severity.Error, $"Type role {t.role} has no font assigned."); continue; }
                    if (t.size <= 0f) Add(Severity.Warning, $"Type role {t.role} has size {t.size}.");
                    else if (t.size < minTextSize) Add(Severity.Warning, $"Type role {t.role} is {t.size}px, below the {minTextSize}px floor (it will be raised to the floor on screen).");
                    if (t.preset && t.font.atlasTexture && t.preset.mainTexture != t.font.atlasTexture)
                        Add(Severity.Error, $"Type role {t.role}: preset '{t.preset.name}' was made from a different font than '{t.font.name}' (text will render garbled).");
                    if (t.font.atlasPopulationMode == AtlasPopulationMode.Dynamic && !t.font.sourceFontFile)
                        Add(Severity.Error, $"Type role {t.role}: dynamic font '{t.font.name}' has no source font file, so new characters can't be added at runtime.");
                    if (t.font.fallbackFontAssetTable == null || t.font.fallbackFontAssetTable.Count == 0)
                        Add(Severity.Info, $"Type role {t.role}: '{t.font.name}' has no fallback fonts (missing glyphs render as boxes).");
                    if (t.role == TypeRole.PlayerName && t.upperCase)
                        Add(Severity.Warning, "Type role PlayerName is set to upper case. Player names are user data and should keep their casing.");
                }

            if (sprites != null)
                foreach (var s in sprites)
                    if (s.slot != SpriteSlot.None && !s.sprite) Add(Severity.Info, $"Sprite slot {s.slot} is empty (Images keep their own sprite).");

            return issues;
        }
    }
}
