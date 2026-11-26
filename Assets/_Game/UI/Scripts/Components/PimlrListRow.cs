// #UI: makes a list row focusable so a controller/keyboard can move through a list (and the list scrolls with it).
// The row draws its own cursor (a lime edge bar + soft fill), so PimlrMenuNavigator's generic ring is skipped.
// It has no click action of its own; it never changes data.
//
// Unreal note: a UListView entry widget with IsFocusable + a focus brush; UListView scrolls to the focused item.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PIMLR.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PIMLR/UI/List Row (focusable)")]
    public class PimlrListRow : Selectable, IPimlrFocusVisual
    {
        [Tooltip("Shown while the row has the cursor.")]
        public Graphic cursorEdge;
        public Graphic cursorFill;

        public bool DrawsOwnFocus => true;

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            ShowCursor(false);
        }

        public override void OnSelect(BaseEventData e) { base.OnSelect(e); ShowCursor(true); }
        public override void OnDeselect(BaseEventData e) { base.OnDeselect(e); ShowCursor(false); }
        public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); if (EventSystem.current && EventSystem.current.currentSelectedGameObject != gameObject) ShowCursor(true, 0.5f); }
        public override void OnPointerExit(PointerEventData e) { base.OnPointerExit(e); if (EventSystem.current && EventSystem.current.currentSelectedGameObject != gameObject) ShowCursor(false); }

        void ShowCursor(bool on, float strength = 1f)
        {
            if (cursorEdge) { cursorEdge.enabled = on; var c = PimlrUITheme.C(ColorToken.BrandLime); c.a = strength; cursorEdge.color = c; }
            if (cursorFill) { cursorFill.enabled = on; cursorFill.color = PimlrUITheme.C(ColorToken.BrandLime, 0.10f * strength); }
        }
    }
}
