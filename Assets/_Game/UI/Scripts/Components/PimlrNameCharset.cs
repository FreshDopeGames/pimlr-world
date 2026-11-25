// #UI: decides which characters a player name may contain: only characters that EVERY font used to display names
// can actually draw (unsupported glyphs render as nothing in TMP, so a name could show up blank on a leaderboard).
// Full-width and compatibility forms are folded first (Unicode NFKC: "ＷＩＤＥ" -> "WIDE", "™" -> "TM").
// Emoji and other characters outside the Basic Multilingual Plane are not supported.
//
// Fonts checked: the theme's PlayerName font (with fallbacks) plus whatever the caller passes (the input field's
// own font, the leaderboard row font). When a name font gains coverage (e.g. a Greek fallback), names allow it
// automatically.
//
// Unreal note: same rule against the Composite Font's sub-font ranges (FCompositeFont) when validating
// the name entry UEditableTextBox.

using System.Collections.Generic;
using System.Text;
using TMPro;

namespace PIMLR.UI
{
    public static class PimlrNameCharset
    {
        /// <summary>NFKC fold, so look-alike forms become the plain characters the fonts have.</summary>
        public static string Normalize(string s) => string.IsNullOrEmpty(s) ? s ?? "" : s.Normalize(NormalizationForm.FormKC);

        /// <summary>True if every given font (searching its fallbacks) can draw c. Null fonts are skipped.</summary>
        public static bool CanDraw(char c, IList<TMP_FontAsset> fonts)
        {
            if (char.IsSurrogate(c)) return false;   // emoji / astral planes
            if (char.IsControl(c)) return false;
            if (c == ' ') return true;
            bool any = false;
            foreach (var f in fonts)
            {
                if (!f) continue;
                any = true;
                if (!f.HasCharacter(c, true, true)) return false; // tryAdd: dynamic fonts pull the glyph from their source file
            }
            return any;
        }

        /// <summary>The name-display fonts: theme PlayerName font + extras (duplicates and nulls removed).</summary>
        public static List<TMP_FontAsset> Fonts(params TMP_FontAsset[] extras)
        {
            var list = new List<TMP_FontAsset>();
            var themed = PimlrUITheme.Font(TypeRole.PlayerName);
            if (themed) list.Add(themed);
            if (extras != null) foreach (var f in extras) if (f && !list.Contains(f)) list.Add(f);
            return list;
        }

        /// <summary>Distinct characters in s (after Normalize) that can't be drawn, in order of appearance.</summary>
        public static string Unsupported(string s, IList<TMP_FontAsset> fonts)
        {
            var bad = new StringBuilder();
            foreach (var c in Normalize(s))
                if (!CanDraw(c, fonts) && bad.ToString().IndexOf(c) < 0) bad.Append(c);
            return bad.ToString();
        }
    }
}
