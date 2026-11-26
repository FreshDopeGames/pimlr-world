// #UI: compact, controller-friendly color picker (Fit Check palette and anything else that picks from a small set).
//
// Cursor flow is explicit, never guessed:
// - Inside the grid: left/right move along the row and STOP at the row ends (no wrap-around surprises);
//   up/down keep the column.
// - Top row up -> exitUp (e.g. the active body-part tab). Bottom row down -> exitDown (e.g. Save).
// - EntryPoint is the EQUIPPED chip, so whatever leads into the grid (a tab's "down") lands on what the player
//   is wearing, not on chip #1. Call LinkEntry(...) after SetEquipped to keep those links current.
// Links are written to BOTH PimlrNavHint (gamepad, PimlrMenuNavigator) and Unity Navigation (keyboard arrows).
//
// States are visually distinct and don't depend on color:
// - Cursor (hover / controller focus): thick theme-focus ring with a dark keyline, scale-up, name label.
// - Equipped: a TextPrimary bar under the chip. A chip can be both.
// Colors come from the caller (e.g. PimlrPalette); this component never changes palette data or saves anything.
//
// Unreal note: a UUniformGridPanel of WBP_Swatch (UCommonButtonBase) with explicit navigation rules
// (EUINavigationRule::Explicit / Stop at row ends), focus brush from the style asset.

using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PIMLR.UI
{
    [AddComponentMenu("PIMLR/UI/Swatch Grid")]
    public class PimlrSwatchGrid : MonoBehaviour
    {
        [Header("Layout (px at 1920x1080)")]
        [Min(1)] public int columns = 6;
        public Vector2 chipSize = new Vector2(52f, 36f);
        public Vector2 spacing = new Vector2(10f, 14f);
        [Tooltip("Cursor scale-up. Keep spacing >= chipSize * (focusScale - 1) so a focused chip never overlaps its neighbors.")]
        public float focusScale = 1.14f;

        [Header("Links out of the grid")]
        public Selectable exitUp;
        public Selectable exitDown;

        [Tooltip("Optional: shows the name of the chip under the cursor (falls back to the equipped one).")]
        public TMP_Text nameLabel;

        public event Action<int> Picked;   // value = palette index
        public event Action<int> Focused;

        readonly List<PimlrSwatch> chips = new List<PimlrSwatch>();
        string[] names;
        int equipped = -1;

        public IReadOnlyList<PimlrSwatch> Chips => chips;
        public Selectable EntryPoint => equipped >= 0 && Find(equipped) ? Find(equipped).Button : chips.Count > 0 ? chips[0].Button : null;

        /// <summary>(Re)build the chips. order = display order of palette indices (e.g. PimlrPalette.DisplayOrder).</summary>
        public void Build(IList<Color> colors, IList<string> colorNames, IList<int> order = null)
        {
            foreach (var c in chips) if (c) DestroySafe(c.gameObject);
            chips.Clear();
            names = new string[colors.Count];
            for (int i = 0; i < colors.Count; i++) names[i] = colorNames != null && i < colorNames.Count ? colorNames[i] : "";

            int count = order != null ? order.Count : colors.Count;
            var rt = (RectTransform)transform;
            int rows = Mathf.CeilToInt(count / (float)columns);
            rt.sizeDelta = new Vector2(columns * chipSize.x + (columns - 1) * spacing.x, rows * chipSize.y + (rows - 1) * spacing.y);

            for (int n = 0; n < count; n++)
            {
                int index = order != null ? order[n] : n;
                var chip = PimlrSwatch.Create(transform, index, colors[index], chipSize, focusScale);
                int col = n % columns, row = n / columns;
                var crt = (RectTransform)chip.transform;
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 1f);
                crt.anchoredPosition = new Vector2(col * (chipSize.x + spacing.x), -row * (chipSize.y + spacing.y));
                // pivot at center for scaling
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition += new Vector2(chipSize.x / 2f, -chipSize.y / 2f);
                chip.Button.onClick.AddListener(() => { SetEquipped(index); Picked?.Invoke(index); });
                chip.FocusChanged += on => OnChipFocus(index, on);
                chips.Add(chip);
            }
            Relink();
            UpdateLabel(-1);
        }

        public void SetEquipped(int paletteIndex)
        {
            equipped = paletteIndex;
            foreach (var c in chips) c.SetEquipped(c.PaletteIndex == paletteIndex);
            UpdateLabel(-1);
        }

        /// <summary>Point `from`'s given direction at the equipped chip (e.g. a tab's down). Call after SetEquipped.</summary>
        public void LinkEntry(Selectable from, Vector2Int dir)
        {
            if (!from) return;
            var target = EntryPoint;
            var hint = PimlrNavHint.On(from);
            if (dir == Vector2Int.down) hint.down = target; else if (dir == Vector2Int.up) hint.up = target;
            else if (dir == Vector2Int.left) hint.left = target; else hint.right = target;
            var nav = from.navigation;
            nav.mode = Navigation.Mode.Explicit;
            if (dir == Vector2Int.down) nav.selectOnDown = target; else if (dir == Vector2Int.up) nav.selectOnUp = target;
            else if (dir == Vector2Int.left) nav.selectOnLeft = target; else nav.selectOnRight = target;
            from.navigation = nav;
        }

        /// <summary>Re-lay out existing chips (responsive: bigger touch targets, different columns) and re-link.</summary>
        public void ApplyLayout(int newColumns, Vector2 newChipSize, Vector2 newSpacing)
        {
            columns = Mathf.Max(1, newColumns); chipSize = newChipSize; spacing = newSpacing;
            int count = chips.Count, rows = Mathf.CeilToInt(count / (float)columns);
            ((RectTransform)transform).sizeDelta = new Vector2(columns * chipSize.x + (columns - 1) * spacing.x, rows * chipSize.y + (rows - 1) * spacing.y);
            for (int n = 0; n < count; n++)
            {
                int col = n % columns, row = n / columns;
                var crt = (RectTransform)chips[n].transform;
                crt.sizeDelta = chipSize;
                crt.anchoredPosition = new Vector2(col * (chipSize.x + spacing.x) + chipSize.x / 2f, -row * (chipSize.y + spacing.y) - chipSize.y / 2f);
            }
            Relink();
        }

        /// <summary>Rewrite every chip's links (call if exitUp / exitDown change).</summary>
        public void Relink()
        {
            int count = chips.Count, rows = Mathf.CeilToInt(count / (float)columns);
            for (int n = 0; n < count; n++)
            {
                int col = n % columns, row = n / columns;
                Selectable At(int r, int c) { int i = r * columns + c; return r >= 0 && r < rows && c >= 0 && c < columns && i < count ? chips[i].Button : null; }
                var left = col > 0 ? At(row, col - 1) : null;               // stop at row ends
                var right = col < columns - 1 ? At(row, col + 1) : null;
                var up = row > 0 ? At(row - 1, col) : exitUp;
                var down = row < rows - 1 ? (At(row + 1, col) ?? At(row + 1, Mathf.Min(col, (count - 1) % columns))) : exitDown;

                var b = chips[n].Button;
                var hint = PimlrNavHint.On(b);
                hint.up = up; hint.down = down; hint.left = left; hint.right = right;
                // Dead ends point at itself so the automatic navigator can't jump to some other panel.
                if (!hint.left) hint.left = b;
                if (!hint.right) hint.right = b;
                b.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
            }
        }

        PimlrSwatch Find(int paletteIndex) { foreach (var c in chips) if (c.PaletteIndex == paletteIndex) return c; return null; }

        void OnChipFocus(int index, bool on)
        {
            if (on) Focused?.Invoke(index);
            UpdateLabel(on ? index : -1);
        }

        void UpdateLabel(int focusedIndex)
        {
            if (!nameLabel || names == null) return;
            int i = focusedIndex >= 0 ? focusedIndex : equipped;
            string n = i >= 0 && i < names.Length ? names[i] : "";
            nameLabel.text = focusedIndex >= 0 && focusedIndex != equipped ? n : (n.Length > 0 ? n + "  ·  EQUIPPED" : "");
        }

        static void DestroySafe(GameObject g) { if (Application.isPlaying) Destroy(g); else DestroyImmediate(g); }
    }
}
