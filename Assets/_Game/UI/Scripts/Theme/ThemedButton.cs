// #UI: styles an existing uGUI Button from a PIMLR button variant: fill states, label color, focus ring, motion.
//
// What it NEVER changes: onClick and its persistent Inspector listeners, navigation (Automatic/Explicit/None and
// explicit targets), interactable, other components on the button, or tweens other scripts run on this transform.
// Transition: a button that already uses Sprite Swap or Animation keeps it ("Keep Existing" is chosen automatically
// when the component is added); colors are only written in "Theme Color Tint" mode.
// Motion: per button (Theme / Compact / None), so big menu buttons pop while slots and icons stay calm.
//
// Unreal note: a WBP_PimlrButton (UCommonButtonBase) with a UCommonButtonStyle per variant; hover/press become
// widget animations with the same durations from the theme.

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PIMLR.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("PIMLR/UI/Themed Button")]
    public class ThemedButton : ThemedBehaviour, IPimlrFocusVisual,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public enum TransitionMode { ThemeColorTint = 0, KeepExisting = 1 }

        public ButtonVariant variant = ButtonVariant.Primary;

        [Tooltip("Theme Color Tint: fills come from the variant. Keep Existing: leave the button's own transition (Sprite Swap / Animation) alone.")]
        public TransitionMode transitionMode = TransitionMode.ThemeColorTint;

        [Tooltip("Apply the theme's Button sprite to the target graphic (ignored in Keep Existing mode).")]
        public bool useThemeSprite = true;

        [Tooltip("Optional label; found automatically in children if empty.")]
        public TMP_Text label;

        [Tooltip("Optional outline shown while hovered or selected (controller focus).")]
        public Graphic focusRing;

        public ButtonMotion motion = ButtonMotion.Theme;

        /// <summary>With a focus ring assigned, this button draws its own cursor and PimlrMenuNavigator skips its generic ring.</summary>
        public bool DrawsOwnFocus => focusRing != null && isActiveAndEnabled;

        Button button;
        bool hovered, selected, pressed;
        Tween scaleTween;          // only OUR tween is ever killed
        Vector3 restScale = Vector3.one;
        bool scaledByUs;

        [SerializeField, HideInInspector] bool initialized;

        /// <summary>First apply on a newly added component: respect a button that already uses Sprite Swap / Animation.
        /// (Done here, not in Reset(), because Unity calls OnEnable before Reset when a component is added.)</summary>
        void InitializeFromExisting()
        {
            if (initialized) return;
            initialized = true;
            if (!button) return;
            if (button.transition == Selectable.Transition.SpriteSwap || button.transition == Selectable.Transition.Animation)
                transitionMode = TransitionMode.KeepExisting;
            if (button.GetComponent<Animator>()) motion = ButtonMotion.None;
        }

        protected override void OnEnable()
        {
            hovered = selected = pressed = false;
            base.OnEnable();
            // Already selected when enabled (e.g. a menu re-opened with this as first selection).
            if (Application.isPlaying && EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject)
            { selected = true; Refresh(); }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            KillOurTween();
            if (Application.isPlaying && scaledByUs) transform.localScale = restScale; // undo only what we did
            scaledByUs = false;
            if (focusRing) focusRing.enabled = false;
        }

        protected override void ApplyTheme(PimlrUITheme theme)
        {
            if (!button) button = GetComponent<Button>();
            if (!button) return;
            InitializeFromExisting();
            if (!label) label = GetComponentInChildren<TMP_Text>(true);

            if (transitionMode == TransitionMode.ThemeColorTint && theme.TryButton(variant, out var st))
            {
                if (button.targetGraphic is Image img)
                {
                    if (useThemeSprite)
                    {
                        var s = theme.Sprite(SpriteSlot.Button);
                        if (s)
                        {
                            if (img.sprite != s) img.sprite = s;
                            var t = s.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                            if (img.type != t) img.type = t;
                        }
                    }
                    if (!Same(img.color, Color.white)) img.color = Color.white; // states below are absolute colors (white x tint)
                }

                var fill = theme.Color(st.fill, st.fillAlpha);
                var cb = button.colors;
                cb.normalColor = fill;
                cb.highlightedColor = Lift(fill, st.hoverLift);
                cb.selectedColor = Lift(fill, st.hoverLift);
                cb.pressedColor = Drop(fill, st.pressDrop);
                cb.disabledColor = theme.Color(ColorToken.StateDisabled, 0.6f);
                cb.colorMultiplier = 1f;
                cb.fadeDuration = theme.hoverTime;
                if (!cb.Equals(button.colors)) button.colors = cb;
                if (button.transition != Selectable.Transition.ColorTint) button.transition = Selectable.Transition.ColorTint;

                if (label)
                {
                    var lc = theme.Color(st.label);
                    if (!button.interactable) lc.a *= 0.5f;
                    if (!Same(label.color, lc)) label.color = lc;
                }
            }

            if (focusRing)
            {
                var fc = theme.Color(theme.focusColor);
                if (!Same(focusRing.color, fc)) focusRing.color = fc;
                if (focusRing is Image ring)
                {
                    var rs = theme.Sprite(SpriteSlot.FocusRing);
                    if (rs && ring.sprite != rs) { ring.sprite = rs; ring.type = rs.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple; }
                }
                focusRing.raycastTarget = false;
                bool on = Application.isPlaying && (hovered || selected) && button.interactable;
                if (focusRing.enabled != on) focusRing.enabled = on;
            }
        }

        static Color Lift(Color c, float t) => new Color(Mathf.Lerp(c.r, 1f, t), Mathf.Lerp(c.g, 1f, t), Mathf.Lerp(c.b, 1f, t), Mathf.Max(c.a, t));
        static Color Drop(Color c, float t) => new Color(c.r * (1f - t), c.g * (1f - t), c.b * (1f - t), Mathf.Max(c.a, t));

        float TargetScale(PimlrUITheme theme)
        {
            if (motion == ButtonMotion.None || !theme) return 1f;
            float hover = motion == ButtonMotion.Compact ? theme.compactHoverScale : theme.hoverScale;
            if (pressed) return 0.97f;
            return hovered || selected ? hover : 1f;
        }

        void KillOurTween()
        {
            if (scaleTween != null && scaleTween.IsActive()) scaleTween.Kill();
            scaleTween = null;
        }

        void Refresh()
        {
            if (!button) button = GetComponent<Button>();
            bool active = (hovered || selected) && button && button.interactable;
            if (focusRing) focusRing.enabled = active;
            if (!Application.isPlaying || motion == ButtonMotion.None) return;

            var theme = Theme;
            float target = active || pressed ? TargetScale(theme) : 1f;
            if (!scaledByUs)
            {
                if (Mathf.Approximately(target, 1f)) return;
                restScale = transform.localScale; // capture whatever the scale is NOW (other scripts may have set it)
            }
            KillOurTween();
            float time = theme ? (pressed ? theme.pressTime : theme.hoverTime) : 0.08f;
            scaledByUs = true;
            scaleTween = transform.DOScale(restScale * target, time)
                .SetEase(pressed ? Ease.OutQuad : Ease.OutBack).SetUpdate(true)
                .OnComplete(() => { if (Mathf.Approximately(target, 1f)) scaledByUs = false; });
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; Refresh(); }
        public void OnSelect(BaseEventData e) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }
        public void OnPointerDown(PointerEventData e) { if (button && button.interactable) { pressed = true; Refresh(); } }
        public void OnPointerUp(PointerEventData e) { pressed = false; Refresh(); }
    }
}
