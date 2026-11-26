// #UI: writes theme colors into another component's Color fields (e.g. JUTPS UIHealhBar's FullHPColor / EmptyHPColor,
// UIStaminaBar's normalColor). The other script keeps all of its behavior (lerping, healing / damage flashes,
// exhausted state); only its palette comes from the theme. Runs in Play mode only, so the prefab's own values are
// never changed (removing this component restores the original colors).
//
// Unreal note: equivalent to binding the widget's color properties to the style asset instead of literals.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PIMLR.UI
{
    [AddComponentMenu("PIMLR/UI/Themed Color Fields")]
    public class PimlrThemedColorFields : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public Component target;
            [Tooltip("Name of a Color field on the target (public or [SerializeField] private).")]
            public string field;
            public ColorToken token;
            [Range(0f, 1f)] public float alpha;
        }

        public List<Entry> entries = new List<Entry>();

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            PimlrUITheme.Changed -= Apply;
            PimlrUITheme.Changed += Apply;
            Apply();
        }

        void OnDisable() => PimlrUITheme.Changed -= Apply;

        public void Apply()
        {
            if (!this || !Application.isPlaying) return;
            foreach (var e in entries)
            {
                if (!e.target || string.IsNullOrEmpty(e.field)) continue;
                var f = FindField(e.target.GetType(), e.field);
                if (f == null || f.FieldType != typeof(Color)) { Debug.LogWarning($"[PIMLR UI] {e.target.GetType().Name} has no Color field '{e.field}'", this); continue; }
                f.SetValue(e.target, PimlrUITheme.C(e.token, e.alpha <= 0f ? 1f : e.alpha));
            }
        }

        static FieldInfo FindField(Type t, string name)
        {
            for (; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f;
            }
            return null;
        }
    }
}
