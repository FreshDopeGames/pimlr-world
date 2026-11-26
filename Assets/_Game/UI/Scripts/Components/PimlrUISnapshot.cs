// #UI: stores the original look of components a UI slice themed in place (e.g. Save_Btn's Image/Button, a title's
// TMP text), so that slice's Revert menu can put them back exactly. Values are typed fields (real asset references,
// not instance IDs), so they survive domain reloads, editor restarts and version control.
// Written/read by editor tools only; harmless at runtime. Safe to delete once a slice is accepted.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PIMLR.UI
{
    [AddComponentMenu("")]
    public class PimlrUISnapshot : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string slice;
            public Component target;

            // Image
            public Sprite sprite;
            public Image.Type imageType;
            public Color color = Color.white;
            public float pixelsPerUnitMultiplier = 1f;

            // Button / Selectable
            public Selectable.Transition transition;
            public ColorBlock colors = ColorBlock.defaultColorBlock;
            public Graphic targetGraphic;

            // RectTransform
            public Vector2 rectAnchorMin, rectAnchorMax, rectPivot, rectPos, rectSize;

            // TMP text
            public TMP_FontAsset font;
            public Material fontMaterial;
            public float fontSize;
            public FontStyles fontStyle;
            public float characterSpacing;
            public Color textColor = Color.white;
            public bool autoSize;
            public float fontSizeMin, fontSizeMax;

            // Serialized object-reference fields of a MonoBehaviour (e.g. LeaderboardUI's buttons / highlights / row holder)
            public List<string> fieldNames = new List<string>();
            public List<UnityEngine.Object> fieldValues = new List<UnityEngine.Object>();
            // GameObjects this slice switched off (restored to active on revert)
            public List<GameObject> deactivated = new List<GameObject>();

            [Tooltip("Legacy (pre-typed) snapshot, kept for reference only.")]
            [TextArea] public string json;
        }

        public List<Entry> entries = new List<Entry>();

        public static Entry Capture(string slice, Component c)
        {
            var e = new Entry { slice = slice, target = c };
            switch (c)
            {
                case RectTransform rt:
                    e.rectAnchorMin = rt.anchorMin; e.rectAnchorMax = rt.anchorMax; e.rectPivot = rt.pivot; e.rectPos = rt.anchoredPosition; e.rectSize = rt.sizeDelta;
                    break;
                case Image img:
                    e.sprite = img.sprite; e.imageType = img.type; e.color = img.color; e.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier;
                    break;
                case Selectable s:
                    e.transition = s.transition; e.colors = s.colors; e.targetGraphic = s.targetGraphic;
                    break;
                case TMP_Text t:
                    e.font = t.font; e.fontMaterial = t.fontSharedMaterial; e.fontSize = t.fontSize; e.fontStyle = t.fontStyle;
                    e.characterSpacing = t.characterSpacing; e.textColor = t.color;
                    e.autoSize = t.enableAutoSizing; e.fontSizeMin = t.fontSizeMin; e.fontSizeMax = t.fontSizeMax;
                    break;
            }
            return e;
        }

        public static void Restore(Entry e)
        {
            switch (e.target)
            {
                case RectTransform rt:
                    rt.anchorMin = e.rectAnchorMin; rt.anchorMax = e.rectAnchorMax; rt.pivot = e.rectPivot; rt.anchoredPosition = e.rectPos; rt.sizeDelta = e.rectSize;
                    break;
                case Image img:
                    img.sprite = e.sprite; img.type = e.imageType; img.color = e.color; img.pixelsPerUnitMultiplier = e.pixelsPerUnitMultiplier;
                    break;
                case Selectable s:
                    s.transition = e.transition; s.colors = e.colors; if (e.targetGraphic) s.targetGraphic = e.targetGraphic;
                    break;
                case TMP_Text t:
                    if (e.font) t.font = e.font;
                    if (e.fontMaterial) t.fontSharedMaterial = e.fontMaterial;
                    t.fontSize = e.fontSize; t.fontStyle = e.fontStyle; t.characterSpacing = e.characterSpacing; t.color = e.textColor;
                    t.enableAutoSizing = e.autoSize; if (e.fontSizeMax > 0f) { t.fontSizeMin = e.fontSizeMin; t.fontSizeMax = e.fontSizeMax; }
                    break;
            }
        }
    }
}
