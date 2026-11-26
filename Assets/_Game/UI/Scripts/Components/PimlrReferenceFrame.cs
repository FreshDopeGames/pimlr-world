// #UI: hosts UI authored at the design-system resolution (1920x1080) inside a canvas that still uses an older
// reference resolution (e.g. the Living Room's 1280x720), at the correct scale. Children are laid out in 1080p
// units, so theme sizes (text roles, the 21px text floor, chip sizes) mean the same thing on every screen.
// When that canvas is standardized to 1920x1080 / match 0.5, the frame's scale becomes 1 and nothing else changes.
//
// Unreal note: not needed; UMG uses one DPI scale curve for the whole project.

using UnityEngine;
using UnityEngine.UI;

namespace PIMLR.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("PIMLR/UI/Reference Frame (1080p)")]
    public class PimlrReferenceFrame : MonoBehaviour
    {
        public Vector2 designResolution = new Vector2(1920f, 1080f);

        [Tooltip("Never let the canvas shrink this UI below its 1080p physical size (relative to the screen's short side). Fixes narrow ratios like 4:3, where the canvas fits by width and text would drop under the 14 px floor.")]
        public bool keepShortSideScale = true;

        Vector2 lastParentSize = new Vector2(-1f, -1f);
        float lastScale = -1f;

        /// <summary>Current canvas-units -> design-units scale (1 when the canvas is already 1080p).</summary>
        public float Scale => lastScale > 0f ? lastScale : CanvasToDesignScale();

        void OnEnable() => Fit(true);
        void LateUpdate() => Fit(false);

        /// <summary>Scale between the canvas reference and the design resolution, using the scaler's width/height match.</summary>
        public float CanvasToDesignScale()
        {
            var scaler = GetComponentInParent<CanvasScaler>();
            if (!scaler || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return 1f;
            var r = scaler.referenceResolution;
            float kw = r.x / designResolution.x, kh = r.y / designResolution.y;
            float m = scaler.matchWidthOrHeight;
            return Mathf.Pow(kw, 1f - m) * Mathf.Pow(kh, m); // same log blend CanvasScaler uses
        }

        public void Fit(bool force)
        {
            var parent = transform.parent as RectTransform;
            if (!parent) return;
            float k = CanvasToDesignScale();
            if (keepShortSideScale)
            {
                var root = GetComponentInParent<Canvas>() ? GetComponentInParent<Canvas>().rootCanvas : null;
                float sf = root ? root.scaleFactor : 1f;
                if (sf > 0f) k = Mathf.Max(k, PimlrResponsive.ShortSideScale / sf);
            }
            var size = parent.rect.size;
            if (!force && size == lastParentSize && Mathf.Approximately(k, lastScale)) return;
            lastParentSize = size; lastScale = k;

            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size / Mathf.Max(0.0001f, k);
            rt.localScale = Vector3.one * k;
        }
    }
}
