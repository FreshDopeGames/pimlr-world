// PIMLR #STF (Save The Fans) — health bar floating over a fan's head, shown only while that fan's heat is on
// (intro, waves, the breather between waves, and the moment it's saved or washed). Built from plain Unity UI so
// there's nothing to set up; FanHeat creates one automatically. Swap the colours or replace the Images with your
// own art later. Health = FanHeat.maxWash minus the wash meter.
//
// Unreal note: a UWidgetComponent (Screen space) on the fan actor, bound to the fan's health delegate.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PIMLR.Missions.SaveTheFans
{
    [AddComponentMenu("PIMLR/Save The Fans/Fan Health Bar")]
    public class FanHealthBar : MonoBehaviour
    {
        public FanHeat fan;
        public RectTransform fill;
        public Image fillImage;
        public TMP_Text label;
        public GameObject visualRoot;

        public Color healthyColor = new Color(0.2f, 0.85f, 0.3f);
        public Color dangerColor = new Color(0.95f, 0.2f, 0.15f);

        const float WidthPx = 140f, HeightPx = 16f, PxToMetres = 0.01f;

        public static FanHealthBar Create(FanHeat fan)
        {
            var go = new GameObject("FanHealthBar", typeof(RectTransform));
            go.transform.SetParent(fan.transform, false);
            var bar = go.AddComponent<FanHealthBar>();
            bar.fan = fan;
            bar.Build();
            return bar;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(WidthPx, HeightPx + 22f);
            rt.localScale = Vector3.one * PxToMetres;

            visualRoot = new GameObject("Visual", typeof(RectTransform));
            visualRoot.transform.SetParent(transform, false);
            Stretch((RectTransform)visualRoot.transform);

            var back = Box("Back", visualRoot.transform, new Color(0f, 0f, 0f, 0.65f));
            var brt = back.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(0f, HeightPx); brt.anchoredPosition = Vector2.zero;

            fillImage = Box("Fill", back.transform, healthyColor);
            fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(2f, 2f); fill.offsetMax = new Vector2(-2f, -2f);

            var t = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(visualRoot.transform, false);
            label = t;
            var font = SaveTheFansStyle.Font; // PIMLR #STF: Barlow-Bold SDF
            if (font) t.font = font;
            t.fontSize = 14f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
            var lrt = t.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot = new Vector2(0.5f, 0f);
            lrt.sizeDelta = new Vector2(0f, 20f); lrt.anchoredPosition = new Vector2(0f, HeightPx + 1f);
        }

        void LateUpdate()
        {
            if (!fan) { Destroy(gameObject); return; }

            bool show = ShouldShow();
            if (visualRoot.activeSelf != show) visualRoot.SetActive(show);
            if (!show) return;

            transform.position = fan.FocusPoint + Vector3.up * fan.healthBarHeight;
            var cam = Camera.main;
            if (cam) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);

            float h01 = fan.Health01;
            // Shrink from the right: the anchor's right edge follows health.
            fill.anchorMax = new Vector2(h01, 1f);
            fillImage.color = Color.Lerp(dangerColor, healthyColor, Mathf.InverseLerp(0.15f, 0.7f, h01));
            label.text = $"{fan.fanDisplayName.ToUpper()}  {Mathf.CeilToInt(fan.Health)}/{Mathf.RoundToInt(fan.MaxHealth)}";
        }

        bool ShouldShow()
        {
            var m = SaveTheFansMission.Instance;
            if (!m || m.CurrentFan != fan) return false;
            switch (m.CurrentState)
            {
                case SaveTheFansMission.State.Cinematic:
                case SaveTheFansMission.State.BetweenWaves:
                case SaveTheFansMission.State.Wave:
                case SaveTheFansMission.State.HeatSaved:
                case SaveTheFansMission.State.HeatFailed:
                    return true;
                default:
                    return false;
            }
        }

        static Image Box(string name, Transform parent, Color c)
        {
            var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
    }
}
