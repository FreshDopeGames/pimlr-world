// #UI (Slice 4): one simple rule for the health bar color, decided with Craig (Nov 25):
//   above the low-health threshold -> HealthGreen (the green of the plus-sign health pickup)
//   at or below the threshold      -> StateDanger (red)
// No blending and no heal / damage flashes. JUTPS's UIHealhBar still drives the fill amount (and its smooth drain);
// this runs after it each frame and only sets the colors.
//
// Unreal note: a single binding on the health bar's FillColorAndOpacity: Percent <= Threshold ? Danger : HealthGreen.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PIMLR.UI
{
    [AddComponentMenu("PIMLR/UI/Health Bar Color")]
    public class PimlrHealthBarColor : MonoBehaviour
    {
        public Image fill;
        public TMP_Text label;
        [Range(0f, 1f)] public float lowThreshold = 0.25f;
        public ColorToken healthy = ColorToken.HealthGreen;
        public ColorToken low = ColorToken.StateDanger;

        void LateUpdate()
        {
            if (!fill) return;
            var c = PimlrUITheme.C(fill.fillAmount <= lowThreshold ? low : healthy);
            fill.color = c;
            // The label sits at the bar's center: dark text while the fill covers it, light text over the empty track.
            if (label) label.color = fill.fillAmount >= 0.55f ? Color.Lerp(c, Color.black, 0.6f) : PimlrUITheme.C(ColorToken.TextPrimary);
        }
    }
}
