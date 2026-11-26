// #UI (Slice 3): explicit cursor flow for the themed leaderboard. LeaderboardUI rebuilds rows and shows/hides the
// Infinite sort chips on its own, so this re-links everything whenever that changes (cheap check each frame).
//
//   BACK  ->right STORY ->right INFINITE (row holds at the ends)
//   mode buttons  -> down: active sort chip (Infinite) or first row (Story)
//   sort chips    -> left/right along the row (holds at ends); up: active mode button; down: first row
//   rows          -> up/down through the list (holds at the last row); first row up: active sort chip or mode
//   B / Esc       -> BACK (PimlrNavHint.isCancel)
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
        public GameObject storyActive, infiniteActive;          // LeaderboardUI's mode highlight objects
        public Selectable[] sorts = new Selectable[3];          // Waves, Washes, Time
        public GameObject[] sortActive = new GameObject[3];     // LeaderboardUI's sort highlight objects
        public Transform rowHolder;
        public ScrollRect scroll;

        int signature = -1;
        Transform lastFirstRow;
        readonly List<Selectable> rows = new List<Selectable>();

        void LateUpdate()
        {
            int sig = Signature();
            var first = rowHolder && rowHolder.childCount > 0 ? rowHolder.GetChild(0) : null; // rows rebuilt -> new first row object
            if (sig != signature || first != lastFirstRow) { signature = sig; lastFirstRow = first; Relink(); }
            KeepFocusedRowVisible();
        }

        int Signature()
        {
            unchecked
            {
                int h = rowHolder ? rowHolder.childCount * 31 : 0;
                h = h * 7 + (infiniteActive && infiniteActive.activeInHierarchy ? 1 : 0);
                for (int i = 0; i < sortActive.Length; i++) h = h * 5 + (sortActive[i] && sortActive[i].activeInHierarchy ? i + 1 : 0);
                return h;
            }
        }

        Selectable ActiveMode() => infiniteActive && infiniteActive.activeInHierarchy ? infinite : story;

        Selectable ActiveSort()
        {
            for (int i = 0; i < sorts.Length; i++)
                if (sorts[i] && sorts[i].gameObject.activeInHierarchy && sortActive[i] && sortActive[i].activeInHierarchy) return sorts[i];
            foreach (var s in sorts) if (s && s.gameObject.activeInHierarchy) return s;
            return null;
        }

        public void Relink()
        {
            rows.Clear();
            if (rowHolder) foreach (Transform t in rowHolder) { var s = t.GetComponent<Selectable>(); if (s && t.gameObject.activeSelf) rows.Add(s); }
            var firstRow = rows.Count > 0 ? rows[0] : null;
            var sortNow = ActiveSort();
            var modeNow = ActiveMode();
            var belowModes = sortNow ? sortNow : firstRow;

            Link(back, up: back, down: belowModes, left: back, right: story);
            Link(story, up: story, down: belowModes, left: back, right: infinite);
            Link(infinite, up: infinite, down: belowModes, left: story, right: infinite);

            var visibleSorts = new List<Selectable>();
            foreach (var s in sorts) if (s && s.gameObject.activeInHierarchy) visibleSorts.Add(s);
            for (int i = 0; i < visibleSorts.Count; i++)
                Link(visibleSorts[i], up: modeNow, down: firstRow ? firstRow : visibleSorts[i],
                     left: i > 0 ? visibleSorts[i - 1] : visibleSorts[i], right: i < visibleSorts.Count - 1 ? visibleSorts[i + 1] : visibleSorts[i]);

            var aboveRows = sortNow ? sortNow : modeNow;
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
