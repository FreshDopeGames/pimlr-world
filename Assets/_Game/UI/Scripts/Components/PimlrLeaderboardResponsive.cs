// #UI (Slice 3): lays out the themed leaderboard for the current screen (1080p design units in a PimlrReferenceFrame).
//   Landscape:  [BACK] LEADERBOARDS ........................ your rank
//               [STORY][INFINITE]                 [TODAY][WEEK][ALL TIME]
//               [WAVES][WASHES][TIME]             header (mode · period)
//               ─ list ───────────────────────────────────────────────────
//   Portrait:   [BACK] LEADERBOARDS / your rank / [STORY | INFINITE] / [TODAY | WEEK | ALL TIME] /
//               [WAVES | WASHES | TIME] (Infinite only) / header / list fills the rest
// Touch layouts size every control from the real on-screen scale (>= 44 pt). Re-lays out on screen changes and
// when LeaderboardUI shows/hides the Infinite sort chips. Presentation only.
//
// Unreal note: a UMG board with two layout variants (horizontal boxes vs stacked) switched by aspect / touch.

using UnityEngine;
using UnityEngine.UI;

namespace PIMLR.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PIMLR/UI/Leaderboard Responsive")]
    public class PimlrLeaderboardResponsive : MonoBehaviour
    {
        public PimlrReferenceFrame frame;
        public RectTransform card, back, title, yourStats, story, infinite, header, list, status;
        public RectTransform[] periods = new RectTransform[3];
        public RectTransform[] sorts = new RectTransform[3];
        public PimlrLeaderboardNav nav;

        int signature;
        Vector2 lastFrame;
        bool lastSortsVisible;

        void LateUpdate()
        {
            var f = frame ? ((RectTransform)frame.transform).rect.size : Vector2.zero;
            bool sortsVisible = sorts.Length > 0 && sorts[0] && sorts[0].gameObject.activeSelf;
            int sig = PimlrResponsive.Signature();
            if (sig == signature && f == lastFrame && sortsVisible == lastSortsVisible) return;
            signature = sig; lastFrame = f; lastSortsVisible = sortsVisible;
            Apply(sortsVisible);
        }

        public void Apply(bool sortsVisible)
        {
            if (!frame || !card) return;
            bool touch = PimlrResponsive.Touch;
            bool portrait = PimlrResponsive.Orientation == LayoutOrientation.Portrait;
            var F = ((RectTransform)frame.transform).rect.size;
            float k = Mathf.Max(0.0001f, frame.transform.localScale.x);
            var rootCanvas = frame.GetComponentInParent<Canvas>() ? frame.GetComponentInParent<Canvas>().rootCanvas : null;
            float touchMin = Mathf.Ceil(PimlrResponsive.MinTouchPx / Mathf.Max(0.0001f, k * (rootCanvas ? rootCanvas.scaleFactor : 1f))) + 2f;

            float ctrlH = touch ? Mathf.Max(64f, touchMin) : 64f;
            float chipH = touch ? Mathf.Max(52f, touchMin) : 52f;
            const float margin = 24f, pad = 28f, gapY = 16f, gapX = 8f;

            Stretch(card, margin);
            float W = F.x - 2f * margin - 2f * pad;   // inner width
            float y = pad;

            if (!portrait)
            {
                Top(back, pad, y, 150f, ctrlH);
                Top(title, pad + 150f + 24f, y, 640f, ctrlH);
                TopRight(yourStats, pad, y, Mathf.Min(460f, W - 840f), ctrlH);
                y += ctrlH + gapY;

                float modeW = 280f;
                Top(story, pad, y, modeW, ctrlH);
                Top(infinite, pad + modeW + gapX, y, modeW, ctrlH);
                float pW = 170f;
                for (int i = 0; i < periods.Length; i++)
                    TopRight(periods[periods.Length - 1 - i], pad + i * (pW + gapX), y, pW, ctrlH);
                y += ctrlH + gapY;

                float sW = 170f;
                for (int i = 0; i < sorts.Length; i++) Top(sorts[i], pad + i * (sW + gapX), y, sW, chipH);
                float headerX = sortsVisible ? pad + 3f * (sW + gapX) + 16f : pad;
                Top(header, headerX, y, W + pad - headerX, chipH, true);
                y += chipH + gapY;
                if (nav) nav.modesAndPeriodsOnOneRow = true;
            }
            else
            {
                Top(back, pad, y, 150f, ctrlH);
                Top(title, pad + 150f + 24f, y, W - 150f - 24f, ctrlH);
                y += ctrlH + 8f;
                Top(yourStats, pad, y, W, 36f, true);
                y += 36f + gapY;

                float half = (W - gapX) / 2f;
                Top(story, pad, y, half, ctrlH);
                Top(infinite, pad + half + gapX, y, half, ctrlH);
                y += ctrlH + gapY;

                float third = (W - 2f * gapX) / 3f;
                for (int i = 0; i < periods.Length; i++) Top(periods[i], pad + i * (third + gapX), y, third, ctrlH);
                y += ctrlH + gapY;

                if (sortsVisible)
                {
                    for (int i = 0; i < sorts.Length; i++) Top(sorts[i], pad + i * (third + gapX), y, third, chipH);
                    y += chipH + gapY;
                }
                Top(header, pad, y, W, 40f, true);
                y += 40f + 8f;
                if (nav) nav.modesAndPeriodsOnOneRow = false;
            }

            // Touch: the list is swiped, so the scrollbar is a thin display-only indicator (not a tiny grab handle).
            var sr = list ? list.GetComponent<ScrollRect>() : null;
            if (sr && sr.verticalScrollbar) sr.verticalScrollbar.interactable = !touch;

            if (list)
            {
                list.anchorMin = new Vector2(0f, 0f); list.anchorMax = new Vector2(1f, 1f); list.pivot = new Vector2(0.5f, 0.5f);
                list.offsetMin = new Vector2(pad, pad); list.offsetMax = new Vector2(-pad, -y);
            }
            if (status) { status.anchorMin = status.anchorMax = new Vector2(0.5f, 0.5f); status.pivot = new Vector2(0.5f, 0.5f); status.anchoredPosition = new Vector2(0f, -y / 2f + pad / 2f); status.sizeDelta = new Vector2(W, 140f); }
        }

        static void Stretch(RectTransform r, float m)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(m, m); r.offsetMax = new Vector2(-m, -m);
        }

        static void Top(RectTransform r, float x, float y, float w, float h, bool textBox = false)
        {
            if (!r) return;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(Mathf.Max(1f, w), h);
        }

        static void TopRight(RectTransform r, float rightInset, float y, float w, float h)
        {
            if (!r) return;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(-rightInset, -y); r.sizeDelta = new Vector2(Mathf.Max(1f, w), h);
        }
    }
}
