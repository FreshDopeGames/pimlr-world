// #UI: one chip of a PimlrSwatchGrid (see that file for the cursor-flow rules). Draws its own cursor ring,
// so PimlrMenuNavigator's generic ring is skipped. Fill color is caller data (keepColor), never a theme token.

using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PIMLR.UI
{
    /// <summary>One chip of a PimlrSwatchGrid. Draws its own cursor ring (the navigator's generic ring is skipped).</summary>
    public class PimlrSwatch : MonoBehaviour, IPimlrFocusVisual,
        ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] int paletteIndex;
        [SerializeField] Button button;
        public int PaletteIndex { get => paletteIndex; private set => paletteIndex = value; }
        public Button Button { get => button; private set => button = value; }
        public bool DrawsOwnFocus => true;
        public event Action<bool> FocusChanged;

        [SerializeField] Image fill, ring, keyline, equippedBar;
        [SerializeField] float focusScale = 1.14f;
        bool hovered, selected;
        Tween tween;

        public static PimlrSwatch Create(Transform parent, int paletteIndex, Color color, Vector2 size, float focusScale)
        {
            var go = new GameObject("Swatch " + paletteIndex, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            var s = go.AddComponent<PimlrSwatch>();
            s.PaletteIndex = paletteIndex;
            s.focusScale = focusScale;

            // keyline (dark) + ring (focus color) behind the fill, outset so they read on any chip color
            // Solid plates (not thin outlines) so the cursor reads on textured, busy screens: 3px dark keyline + 5px lime halo.
            s.keyline = Layer(go.transform, "Keyline", SpriteSlot.Button, ColorToken.SurfaceScrim, 1f, 8f);
            s.ring = Layer(go.transform, "Cursor Halo", SpriteSlot.Button, ColorToken.BrandLime, 1f, 5f);
            // Permanent faint edge so near-black (and near-background) colors never vanish on carbon surfaces.
            Layer(go.transform, "Edge", SpriteSlot.Button, ColorToken.TextSecondary, 0.35f, 1.5f);
            s.fill = Layer(go.transform, "Fill", SpriteSlot.Button, ColorToken.White, 1f, 0f);
            s.fill.GetComponent<ThemedImage>().keepColor = true; // the color is palette data, not a theme token
            s.fill.color = color;
            s.fill.raycastTarget = true;
            s.equippedBar = Layer(go.transform, "Equipped Bar", SpriteSlot.None, ColorToken.TextPrimary, 1f, 0f);
            var br = s.equippedBar.rectTransform;
            br.anchorMin = new Vector2(0.15f, 0f); br.anchorMax = new Vector2(0.85f, 0f); br.pivot = new Vector2(0.5f, 1f);
            br.offsetMin = new Vector2(0f, -9f); br.offsetMax = new Vector2(0f, -5f);

            s.Button = go.AddComponent<Button>();
            s.Button.targetGraphic = s.fill;
            s.Button.transition = Selectable.Transition.None; // state is shown by ring/scale/bar, not by tinting the color
            s.SetFocusVisual(false, true);
            s.SetEquipped(false);
            return s;
        }

        static Image Layer(Transform parent, string name, SpriteSlot slot, ColorToken token, float alpha, float outset)
        {
            var g = new GameObject(name, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(parent, false);
            var r = (RectTransform)g.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(-outset, -outset); r.offsetMax = new Vector2(outset, outset);
            var img = g.GetComponent<Image>(); img.raycastTarget = false;
            img.pixelsPerUnitMultiplier = 2.5f; // small chips: shrink the 9-slice corners so the cut shape stays readable
            var t = g.AddComponent<ThemedImage>(); t.color = token; t.alpha = alpha; t.sprite = slot; t.Apply();
            return img;
        }

        public void SetEquipped(bool on) { if (equippedBar) equippedBar.enabled = on; }

        void SetFocusVisual(bool on, bool instant = false)
        {
            var th = PimlrUITheme.Current;
            if (ring && th) { var ti = ring.GetComponent<ThemedImage>(); if (ti && ti.color != th.focusColor) { ti.color = th.focusColor; ti.Apply(); } }
            if (ring) ring.enabled = on;
            if (keyline) keyline.enabled = on;
            float target = on ? focusScale : 1f;
            if (tween != null && tween.IsActive()) tween.Kill();
            if (instant || !Application.isPlaying) { transform.localScale = Vector3.one * target; return; }
            var theme = PimlrUITheme.Current;
            if (on) transform.SetAsLastSibling(); // draw above neighbors while focused
            tween = transform.DOScale(target, theme ? theme.hoverTime : 0.08f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        void Refresh() { bool on = hovered || selected; SetFocusVisual(on); FocusChanged?.Invoke(on); }
        public void OnSelect(BaseEventData e) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }
        public void OnPointerEnter(PointerEventData e) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; Refresh(); }
        void OnDisable() { if (tween != null && tween.IsActive()) tween.Kill(); hovered = selected = false; transform.localScale = Vector3.one; }
    }
}
