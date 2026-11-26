// #UI: applies a PIMLR type role (font, size, case, tracking, color, material preset) to a TextMeshPro text.
// Screens reference roles, never font files, so swapping Russo One for PHATT later is one field on the theme asset.
// Casing: only roles marked upperCase (Display, Heading, Caption) uppercase; PlayerName / Body / Utility keep the
// text's own casing. A per-text "Text Case" override can force Original or Upper.
//
// Unreal note: a UTextBlock (or UCommonTextBlock) whose style comes from a UCommonTextStyle per role.

using TMPro;
using UnityEngine;

namespace PIMLR.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("PIMLR/UI/Themed Text")]
    public class ThemedText : ThemedBehaviour
    {
        public TypeRole role = TypeRole.Body;

        [Tooltip("Override the role's casing. Use Original for names, messages and descriptions.")]
        public TextCase textCase = TextCase.FromRole;

        [Tooltip("Scale the role's size (e.g. 1.5 for a hero headline). 0 = leave the text's current size alone.")]
        [Min(0f)] public float sizeScale = 1f;

        [Tooltip("Use this color instead of the role's default color.")]
        public bool overrideColor;
        public ColorToken color = ColorToken.TextPrimary;
        [Range(0f, 1f)] public float alpha = 1f;

        [Tooltip("Leave the text color alone (a script colors it, e.g. the current player's name).")]
        public bool keepColor;

        [Tooltip("Use the role's material preset (outline / hard shadow) when it has one.")]
        public bool usePreset = true;

        TMP_Text text;

        /// <summary>1 inside a PimlrReferenceFrame or on a 1080p canvas; e.g. 0.667 directly on a 1280x720 canvas.</summary>
        float DesignToCanvas(PimlrUITheme theme)
        {
            if (GetComponentInParent<PimlrReferenceFrame>(true)) return 1f;
            var canvas = GetComponentInParent<Canvas>(true);
            var scaler = canvas ? canvas.rootCanvas.GetComponent<UnityEngine.UI.CanvasScaler>() : null;
            if (!scaler || scaler.uiScaleMode != UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize) return 1f;
            var r = scaler.referenceResolution; var d = theme.referenceResolution;
            float m = scaler.matchWidthOrHeight;
            return Mathf.Pow(r.x / d.x, 1f - m) * Mathf.Pow(r.y / d.y, m);
        }

        protected override void ApplyTheme(PimlrUITheme theme)
        {
            if (!text) text = GetComponent<TMP_Text>();
            if (!text || !theme.TryType(role, out var e)) return;

            if (e.font) // missing font = keep the current one (Validate reports it as an error)
            {
                if (text.font != e.font) text.font = e.font;
                var mat = usePreset && e.preset ? e.preset : e.font.material;
                if (mat && text.fontSharedMaterial != mat) text.fontSharedMaterial = mat;
            }
            if (sizeScale > 0f && e.size > 0f)
            {
                // Readability floor: never below theme.minTextSize (14 px at 720p by default), whatever the scale.
                // Sizes are 1080p design units; on an older canvas (e.g. 1280x720) they're converted to canvas units.
                float k = DesignToCanvas(theme);
                float size = Mathf.Max(e.size * sizeScale, theme.minTextSize) * k;
                if (text.enableAutoSizing)
                {
                    // Auto-size may shrink a long label to fit, but never below the readability floor.
                    float floor = Mathf.Min(size, theme.minTextSize * k);
                    if (!Mathf.Approximately(text.fontSizeMax, size)) text.fontSizeMax = size;
                    if (!Mathf.Approximately(text.fontSizeMin, floor)) text.fontSizeMin = floor;
                }
                else if (!Mathf.Approximately(text.fontSize, size)) text.fontSize = size;
            }

            bool upper = textCase == TextCase.Upper || (textCase == TextCase.FromRole && e.upperCase);
            var style = upper ? text.fontStyle | FontStyles.UpperCase : text.fontStyle & ~FontStyles.UpperCase;
            if (text.fontStyle != style) text.fontStyle = style;
            if (!Mathf.Approximately(text.characterSpacing, e.tracking)) text.characterSpacing = e.tracking;

            // A ThemedButton owns its label's color (it depends on variant + interactable), so don't fight it.
            var owner = GetComponentInParent<ThemedButton>(true);
            if (owner && owner.label == text) { owner.Apply(); return; }
            if (keepColor) return;
            var c = theme.Color(overrideColor ? color : e.color, alpha);
            if (!Same(text.color, c)) text.color = c;
        }
    }
}
