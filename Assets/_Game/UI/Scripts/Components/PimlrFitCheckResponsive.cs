// #UI (Slice 2, responsive): lays out the themed Fit Check card for the current screen.
//   Landscape (desktop):  card on the right, character unobstructed. 52x36 chips (mouse/controller).
//   Landscape (touch):    same place, 112x112 chips and taller tabs (>= 44 pt tap targets).
//   Portrait (any):       bottom sheet. Character in the upper area, full-width card at the bottom, chips sized
//                         to the width, SAVE as a full-width bar at the very bottom (thumb reach).
// Re-lays out only when the screen size / touch mode changes. Nothing here touches Fit Check data or saving.
// Design units = 1080p units inside the PimlrReferenceFrame; SAVE lives on the Living Room canvas, so its numbers
// are canvas units (the original desktop placement is restored in landscape).
//
// Unreal note: equivalent to a UMG widget with two layout variants switched by aspect ratio / touch input.

using TMPro;
using UnityEngine;

namespace PIMLR.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PIMLR/UI/Fit Check Responsive")]
    public class PimlrFitCheckResponsive : MonoBehaviour
    {
        public PimlrReferenceFrame frame;
        public RectTransform card, tabs, colorCaption, nameLabel, savedLabel, hint;
        public PimlrSwatchGrid grid;
        public RectTransform saveButton;      // on the Living Room canvas (canvas units)
        public TMP_Text hintText;

        // original desktop placement of the SAVE button (captured once)
        [SerializeField, HideInInspector] bool captured;
        [SerializeField, HideInInspector] Vector2 saveAnchorMin, saveAnchorMax, savePivot, savePos, saveSize;

        int signature;
        Vector2 lastFrameSize;

        void OnEnable() { Capture(); signature = 0; }

        void Capture()
        {
            if (captured || !saveButton) return;
            saveAnchorMin = saveButton.anchorMin; saveAnchorMax = saveButton.anchorMax; savePivot = saveButton.pivot;
            savePos = saveButton.anchoredPosition; saveSize = saveButton.sizeDelta; captured = true;
        }

        void OnDisable() { if (captured && saveButton) RestoreSave(saveSize); }

        void LateUpdate()
        {
            var fsize = frame ? ((RectTransform)frame.transform).rect.size : Vector2.zero;
            int sig = PimlrResponsive.Signature();
            if (sig == signature && fsize == lastFrameSize) return;
            signature = sig; lastFrameSize = fsize;
            Apply();
        }

        public void Apply()
        {
            if (!card || !frame) return;
            bool touch = PimlrResponsive.Touch;
            bool portrait = PimlrResponsive.Orientation == LayoutOrientation.Portrait;
            var F = ((RectTransform)frame.transform).rect.size;
            float k = Mathf.Max(0.0001f, frame.transform.localScale.x); // design unit * k = canvas unit

            const float pad = 24f, capH = 26f, nameH = 32f, savedH = 26f, hintH = 26f;
            // Touch sizes come from the real on-screen scale, so 44 pt holds on every phone (a fixed size missed by 3 px).
            var rootCanvas = frame.GetComponentInParent<Canvas>() ? frame.GetComponentInParent<Canvas>().rootCanvas : null;
            float screenScale = k * (rootCanvas ? rootCanvas.scaleFactor : 1f);
            float touchMin = Mathf.Ceil(PimlrResponsive.MinTouchPx / Mathf.Max(0.0001f, screenScale)) + 2f; // design units
            float tabH = touch ? Mathf.Max(130f, touchMin) : 96f;
            // Full 44 pt touch chips: in portrait, PimlrPortraitCameraFraming keeps the whole character (shoes included)
            // above the sheet, so the chips no longer need to shrink (Craig, Nov 25: option 1).
            const float touchChipHeightFactor = 1f;
            Vector2 gap = touch ? new Vector2(12f, 12f) : new Vector2(10f, 14f);
            Vector2 chip;
            float cardW;

            if (portrait)
            {
                float avail = F.x - 64f - 2f * pad;
                float cx = Mathf.Min(touch ? 150f : 110f, (avail - 5f * gap.x) / 6f);
                chip = new Vector2(Mathf.Max(cx, touch ? touchMin : 0f), touch ? touchMin * touchChipHeightFactor : cx * 0.72f);
                cardW = F.x - 64f;
            }
            else
            {
                chip = touch ? new Vector2(touchMin, touchMin * touchChipHeightFactor) : new Vector2(52f, 36f);
                cardW = Mathf.Max(452f, 6f * chip.x + 5f * gap.x + 2f * pad);
            }

            float gridH = 2f * chip.y + gap.y;
            float y = pad;
            float tabsY = y; y += tabH + 16f;
            float capY = y; y += capH + 12f;
            float gridY = y; y += gridH + 16f;
            float nameY = y; y += nameH + 6f;
            float savedY = y; y += savedH + 6f + hintH + 18f;
            float cardH = y;

            // SAVE placement first (portrait sheet sits above it)
            float saveH = touch ? 84f : 68f;
            if (saveButton)
            {
                if (portrait)
                {
                    saveButton.anchorMin = new Vector2(0f, 0f); saveButton.anchorMax = new Vector2(1f, 0f); saveButton.pivot = new Vector2(0.5f, 0f);
                    saveButton.offsetMin = new Vector2(24f, 16f); saveButton.offsetMax = new Vector2(-24f, 16f + saveH);
                }
                else RestoreSave(touch ? new Vector2(Mathf.Max(saveSize.x, 260f), Mathf.Max(saveSize.y, saveH)) : saveSize);
            }

            if (portrait)
            {
                card.anchorMin = new Vector2(0f, 0f); card.anchorMax = new Vector2(1f, 0f); card.pivot = new Vector2(0.5f, 0f);
                float bottom = (16f + saveH + 12f) / k;
                card.offsetMin = new Vector2(32f, bottom); card.offsetMax = new Vector2(-32f, bottom + cardH);
            }
            else
            {
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(1f, 0.5f);
                card.anchoredPosition = new Vector2(touch ? -48f : -96f, touch ? 0f : 30f);
                card.sizeDelta = new Vector2(cardW, cardH);
            }

            TopRow(tabs, tabsY, tabH);
            TopRow(colorCaption, capY, capH);
            TopRow(nameLabel, nameY, nameH);
            TopRow(savedLabel, savedY, savedH);
            if (hint) { hint.anchorMin = new Vector2(0f, 0f); hint.anchorMax = new Vector2(1f, 0f); hint.pivot = new Vector2(0.5f, 0f); hint.anchoredPosition = new Vector2(0f, 18f); hint.sizeDelta = new Vector2(-2f * pad, hintH); }
            if (hintText) hintText.text = touch ? "TAP A PART, THEN A COLOR" : "PICK A PART, THEN A COLOR";

            if (grid)
            {
                var g = (RectTransform)grid.transform;
                g.anchorMin = g.anchorMax = g.pivot = new Vector2(0.5f, 1f);
                g.anchoredPosition = new Vector2(0f, -gridY);
                grid.ApplyLayout(6, chip, gap);
            }

            // Tab internals: label box tall enough for the font (no overflow), bigger icons on touch.
            if (tabs)
                foreach (Transform tab in tabs)
                {
                    var label = tab.Find("Label") as RectTransform;
                    if (label) { label.sizeDelta = new Vector2(0f, 32f); label.anchoredPosition = new Vector2(0f, touch ? 16f : 10f); }
                    var icon = tab.Find("Icon") as RectTransform;
                    if (icon) { float s = touch ? 52f : 40f; icon.sizeDelta = new Vector2(s, s); icon.anchoredPosition = new Vector2(0f, touch ? -16f : -10f); }
                }
        }

        void TopRow(RectTransform r, float y, float h)
        {
            if (!r) return;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -y); r.sizeDelta = new Vector2(-48f, h);
        }

        void RestoreSave(Vector2 size)
        {
            saveButton.anchorMin = saveAnchorMin; saveButton.anchorMax = saveAnchorMax; saveButton.pivot = savePivot;
            saveButton.anchoredPosition = savePos; saveButton.sizeDelta = size;
        }
    }
}
