// #UI (Slice 3): lays out one themed leaderboard row from its current width (1080p design units).
//   Wide (>= 900):  [rank badge] [NAME .................. YOU] [VALUE] [detail]
//   Compact:        [rank badge] [NAME  YOU            ] [VALUE]
//                                [detail (caption)     ]
// Podium rows (top 3) are taller. The "YOU" tag sits right after the name, wherever the name ends.
// Pure presentation: LeaderboardRow.Set() still writes all the data.
//
// Unreal note: a WBP_LeaderboardRow with a size-box switch (wide vs compact) driven by the list's width.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PIMLR.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("PIMLR/UI/Leaderboard Row Layout")]
    public class PimlrLeaderboardRowLayout : MonoBehaviour
    {
        public RectTransform badge, rank, nameText, youTag, value, detail;
        public LayoutElement layout;
        public bool podium;
        public float wideMinWidth = 900f;

        bool applying;

        void OnEnable() => Apply();
        void OnRectTransformDimensionsChange() { if (!applying && isActiveAndEnabled) Apply(); }

        public void Apply()
        {
            if (applying) return;
            applying = true;
            try { Layout(); } finally { applying = false; }
        }

        void Layout()
        {
            float W = ((RectTransform)transform).rect.width;
            if (W <= 1f) return;
            bool wide = W >= wideMinWidth;
            float rowH = wide ? (podium ? 80f : 64f) : (podium ? 96f : 84f);
            if (layout) { layout.minHeight = rowH; layout.preferredHeight = rowH; }

            const float left = 20f, badgeSize = 56f, gap = 16f, right = 20f;
            float valueW = wide ? 190f : 150f, detailW = wide ? Mathf.Min(320f, W * 0.28f) : 0f;
            float nameX = left + badgeSize + gap;

            Place(badge, left, 0f, badgeSize, badgeSize, 0.5f);
            Place(rank, left, 0f, badgeSize, badgeSize, 0.5f);

            float nameRight = W - right - valueW - gap - (wide ? detailW + gap : 0f);
            float nameW = Mathf.Max(80f, nameRight - nameX);
            float nameY = wide ? 0f : 14f;
            Place(nameText, nameX, nameY, nameW, 38f, 0.5f);

            if (value) { var t = value.GetComponent<TMP_Text>(); if (t) t.alignment = TextAlignmentOptions.MidlineRight; }
            PlaceRight(value, right + (wide ? detailW + gap : 0f), 0f, valueW, 40f);

            if (detail)
            {
                var t = detail.GetComponent<TMP_Text>();
                if (wide) { if (t) t.alignment = TextAlignmentOptions.MidlineRight; PlaceRight(detail, right, 0f, detailW, 30f); }
                else { if (t) t.alignment = TextAlignmentOptions.MidlineLeft; Place(detail, nameX, -20f, nameW, 28f, 0.5f); }
            }

            if (youTag && nameText)
            {
                var nt = nameText.GetComponent<TMP_Text>();
                float textW = nt ? Mathf.Min(nt.GetPreferredValues(nt.text).x, nameW - 96f) : 0f;
                Place(youTag, nameX + Mathf.Max(0f, textW) + 12f, nameY, 72f, 30f, 0.5f);
            }
        }

        static void Place(RectTransform r, float x, float y, float w, float h, float anchorY)
        {
            if (!r) return;
            r.anchorMin = r.anchorMax = new Vector2(0f, anchorY); r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        }

        static void PlaceRight(RectTransform r, float rightInset, float y, float w, float h)
        {
            if (!r) return;
            r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f); r.pivot = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(-rightInset, y); r.sizeDelta = new Vector2(w, h);
        }
    }
}
