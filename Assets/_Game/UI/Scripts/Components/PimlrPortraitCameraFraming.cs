// #UI (Fit Check, portrait): keeps the whole character visible ABOVE the bottom sheet on tall screens.
// It never moves the camera or changes its Inspector settings (position, rotation, FOV stay exactly as authored).
// Instead, in portrait only, it sets a projection matrix with a lens shift + slight zoom-out so the character's
// bounds land in the band between the top of the sheet and the top of the screen. In landscape, or when Fit Check
// closes, it calls ResetProjectionMatrix() and the camera is back to normal.
//
// Unreal note: the same idea as a CineCamera "Filmback" sensor offset / UCameraComponent::OffCenterProjectionOffset
// driven by the viewport's aspect.

using UnityEngine;

namespace PIMLR.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PIMLR/UI/Portrait Camera Framing")]
    public class PimlrPortraitCameraFraming : MonoBehaviour
    {
        public Camera targetCamera;
        [Tooltip("Root holding the character renderers (the visible ones are framed).")]
        public Transform characterRoot;
        [Tooltip("The bottom sheet; the character is framed above its top edge.")]
        public RectTransform sheet;
        [Range(0f, 0.2f)] public float topMargin = 0.09f;     // room for the title / coins
        [Range(0f, 0.1f)] public float gapAboveSheet = 0.02f;

        int signature;
        Rect lastSheet;
        bool applied;

        void OnDisable() => Clear();

        void LateUpdate()
        {
            if (!targetCamera) return;
            bool portrait = PimlrResponsive.Orientation == LayoutOrientation.Portrait;
            if (!portrait) { Clear(); return; }

            var sheetRect = SheetViewport();
            int sig = PimlrResponsive.Signature();
            if (applied && sig == signature && sheetRect == lastSheet) return;
            signature = sig; lastSheet = sheetRect;
            Frame(sheetRect.yMax);
        }

        Rect SheetViewport()
        {
            if (!sheet) return new Rect(0f, 0f, 1f, 0.3f);
            var c = new Vector3[4]; sheet.GetWorldCorners(c); // overlay canvas: world = screen pixels
            float top = Mathf.Max(c[1].y, c[2].y) / Mathf.Max(1f, Screen.height);
            return new Rect(0f, 0f, 1f, Mathf.Clamp01(top));
        }

        void Frame(float sheetTop)
        {
            targetCamera.ResetProjectionMatrix();
            if (!TryCharacterBounds(out var b)) { applied = false; return; }

            // character vertical extent in viewport space with the normal projection
            float yMin = 1f, yMax = 0f;
            var e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                var p = b.center + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var v = targetCamera.WorldToViewportPoint(p);
                if (v.z <= 0f) continue;
                yMin = Mathf.Min(yMin, v.y); yMax = Mathf.Max(yMax, v.y);
            }
            if (yMax <= yMin) { applied = false; return; }

            float bandMin = sheetTop + gapAboveSheet, bandMax = 1f - topMargin;
            float band = Mathf.Max(0.05f, bandMax - bandMin);
            float h = yMax - yMin;
            float s = Mathf.Max(1f, h / band);                       // zoom out only if needed (never zoom in)
            // after scaling about the screen center, the character's center moves to 0.5 + (c - 0.5) / s
            float cNow = 0.5f + ((yMin + yMax) * 0.5f - 0.5f) / s;
            float cWant = (bandMin + bandMax) * 0.5f;
            float shiftNdc = 2f * (cWant - cNow);

            var m = targetCamera.projectionMatrix;
            m[0, 0] /= s; m[1, 1] /= s;                              // wider view
            m[1, 2] -= shiftNdc;                                     // vertical lens shift (perspective: ndc.y += -m12)
            targetCamera.projectionMatrix = m;
            applied = true;
        }

        bool TryCharacterBounds(out Bounds b)
        {
            b = default;
            if (!characterRoot) return false;
            bool any = false;
            foreach (var r in characterRoot.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        void Clear()
        {
            if (!applied || !targetCamera) { applied = false; return; }
            targetCamera.ResetProjectionMatrix();
            applied = false;
        }
    }
}
