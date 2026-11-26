// #UI (Slice 3): explicit cursor flow for the themed leaderboard. LeaderboardUI rebuilds rows and shows/hides the
// Infinite sort chips on its own, so this re-links whenever that changes (cheap check each frame).
//
//   BACK                          down: active mode
//   STORY | INFINITE              left/right (hold at ends); up: BACK; down: active period
//                                 (landscape: INFINITE right -> TODAY, the next control on the same visual row)
//   TODAY | WEEK | ALL TIME       left/right (hold); up: active mode; down: active sort (Infinite) or first row
//   WAVES | WASHES | TIME         left/right (hold); up: active period; down: first row
//   rows                          up/down through the list (hold at the last row); first row up: sort or period
//   B / Esc                       BACK (PimlrNavHint.isCancel)
// The focused row is scrolled into view. Gamepad (PimlrNavHint) and keyboard (Unity Navigation) get the same links.
//
// Unreal note: CommonUI explicit navigation rules on the board widget; UListView::ScrollIndexIntoView for rows.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PIMLR.UI
{
    [AddComponentMenu("PIMLR/UI/Leaderboard Nav")]
    public class PimlrLeaderboardNav : MonoBehaviour
    {
        public Selectable back, story, infinite;
        public GameObject storyActive, infiniteActive;
        public Selectable[] periods = new Selectable[3];        // Today, Week, All time
        public GameObject[] periodActive = new GameObject[3];
        public Selectable[] sorts = new Selectable[3];          // Waves, Washes, Time
        public GameObject[] sortActive = new GameObject[3];
        public Transform rowHolder;
        public ScrollRect scroll;
        [Tooltip("Set by the responsive layout: modes and periods share one visual row (landscape).")]
        public bool modesAndPeriodsOnOneRow = true;

        int signature = -1;
        Transform lastFirstRow;
        readonly List<Selectable> rows = new List<Selectable>();

        void LateUpdate()
        {
            int sig = Signature();
            var first = rowHolder && rowHolder.childCount > 0 ? rowHolder.GetChild(0) : null;
            if (sig != signature || first != lastFirstRow) { signature = sig; lastFirstRow = first; Relink(); }
            KeepFocusedRowVisible();
        }

        int Signature()
        {
            unchecked
            {
                int h = rowHolder ? rowHolder.childCount * 31 : 0;
                h = h * 7 + (infiniteActive && infiniteActive.activeInHierarchy ? 1 : 0);
                h = h * 3 + (modesAndPeriodsOnOneRow ? 1 : 0);
                for (int i = 0; i < sortActive.Length; i++) h = h * 5 + (sortActive[i] && sortActive[i].activeInHierarchy ? i + 1 : 0);
                for (int i = 0; i < periodActive.Length; i++) h = h * 5 + (periodActive[i] && periodActive[i].activeInHierarchy ? i + 1 : 0);
                for (int i = 0; i < sorts.Length; i++) h = h * 2 + (sorts[i] && sorts[i].gameObject.activeInHierarchy ? 1 : 0);
                return h;
            }
        }

        static Selectable Active(Selectable[] items, GameObject[] markers)
        {
            for (int i = 0; i < items.Length; i++)
                if (items[i] && items[i].gameObject.activeInHierarchy && markers[i] && markers[i].activeInHierarchy) return items[i];
            foreach (var s in items) if (s && s.gameObject.activeInHierarchy) return s;
            return null;
        }

        static List<Selectable> Visible(Selectable[] items)
        {
            var list = new List<Selectable>();
            foreach (var s in items) if (s && s.gameObject.activeInHierarchy) list.Add(s);
            return list;
        }

        public void Relink()
        {
            rows.Clear();
            if (rowHolder) foreach (Transform t in rowHolder) { var s = t.GetComponent<Selectable>(); if (s && t.gameObject.activeSelf) rows.Add(s); }
            var firstRow = rows.Count > 0 ? rows[0] : null;
            var modeNow = infiniteActive && infiniteActive.activeInHierarchy ? infinite : story;
            var periodNow = Active(periods, periodActive);
            var sortNow = Active(sorts, sortActive);
            var vPeriods = Visible(periods);
            var vSorts = Visible(sorts);

            Link(back, up: back, down: modeNow, left: back, right: modesAndPeriodsOnOneRow ? back : back);
            Link(story, up: back, down: periodNow ? periodNow : (sortNow ? sortNow : firstRow), left: story, right: infinite);
            Link(infinite, up: back, down: periodNow ? periodNow : (sortNow ? sortNow : firstRow), left: story,
                 right: modesAndPeriodsOnOneRow && vPeriods.Count > 0 ? vPeriods[0] : infinite);

            var belowPeriods = sortNow ? sortNow : firstRow;
            for (int i = 0; i < vPeriods.Count; i++)
                Link(vPeriods[i], up: modesAndPeriodsOnOneRow ? back : modeNow, down: belowPeriods ? belowPeriods : vPeriods[i],
                     left: i > 0 ? vPeriods[i - 1] : (modesAndPeriodsOnOneRow ? infinite : vPeriods[i]),
                     right: i < vPeriods.Count - 1 ? vPeriods[i + 1] : vPeriods[i]);

            for (int i = 0; i < vSorts.Count; i++)
                Link(vSorts[i], up: periodNow ? periodNow : modeNow, down: firstRow ? firstRow : vSorts[i],
                     left: i > 0 ? vSorts[i - 1] : vSorts[i], right: i < vSorts.Count - 1 ? vSorts[i + 1] : vSorts[i]);

            var aboveRows = sortNow ? sortNow : (periodNow ? periodNow : modeNow);
            for (int i = 0; i < rows.Count; i++)
                Link(rows[i], up: i > 0 ? rows[i - 1] : aboveRows, down: i < rows.Count - 1 ? rows[i + 1] : rows[i], left: rows[i], right: rows[i]);

            if (back) PimlrNavHint.On(back).isCancel = true;
        }

        static void Link(Selectable s, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            if (!s) return;
            var h = PimlrNavHint.On(s);
            h.up = up; h.down = down; h.left = left; h.right = right;
            s.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
        }

        void KeepFocusedRowVisible()
        {
            if (!scroll || !scroll.content || !EventSystem.current) return;
            var go = EventSystem.current.currentSelectedGameObject;
            if (!go || go.transform.parent != rowHolder) return;
            var viewport = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
            var row = (RectTransform)go.transform;
            Vector3[] r = new Vector3[4], v = new Vector3[4];
            row.GetWorldCorners(r); viewport.GetWorldCorners(v);
            float overTop = r[1].y - v[1].y, underBottom = v[0].y - r[0].y;
            if (overTop <= 0f && underBottom <= 0f) return;
            float scale = scroll.content.lossyScale.y;
            float delta = (overTop > 0f ? -overTop : underBottom) / Mathf.Max(0.0001f, scale);
            var p = scroll.content.anchoredPosition;
            scroll.content.anchoredPosition = new Vector2(p.x, p.y + delta);
            scroll.velocity = Vector2.zero;
        }
    }
}
