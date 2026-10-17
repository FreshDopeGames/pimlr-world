using JUTPS;
using UnityEngine;
using UnityEngine.UI;

// PIMLR #11: Stamina bar UI.
// JUTPS already implements a sprint taper internally (SprintSpeedDecrease in
// JUCharacterControllerCore: ramps 0 -> MaxSprintSpeedDecrease while sprinting,
// then decays back to 0). The meter just wasn't shown. This drives an Image.fillAmount
// from the player's NormalizedSprintStamina (exposed read-only on the core).
//
// EDITOR WIRING (residual step):
//   1. Create a UI Image (Image Type = Filled) for the stamina bar on the HUD Canvas
//      (e.g. inside the existing "JUTPS Interface" menu).
//   2. Add this component anywhere on that Canvas and assign 'fillImage' to that Image.
//   3. Leave 'player' null to auto-resolve from GameExecutionManager at runtime, or
//      assign the player's JUCharacterController explicitly.
[AddComponentMenu("PIMLR/UI Stamina Bar")]
public class UIStaminaBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Filled Image whose fillAmount represents remaining sprint stamina.")]
    [SerializeField] private Image fillImage; // ASSIGN IN EDITOR

    [Tooltip("Unused since the post-playtest stamina rework (the bar reads PimlrStamina). Kept so scene data still loads.")]
    [SerializeField] private JUCharacterController player;

    [Header("Options")]
    [Tooltip("How fast the bar catches up (fill per second). 0 = snap instantly.")]
    [SerializeField] private float lerpSpeed = 3f;

    [Tooltip("Hide the bar entirely when stamina is full (idle).")]
    [SerializeField] private bool hideWhenFull = false;

    [Tooltip("Tint while exhausted (sprinting locked until it recovers). Multiplies your sprite's colour.")]
    [SerializeField] private Color depletedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color normalColor = Color.white;

    private void Update()
    {
        if (fillImage == null) return;

        // PIMLR (post-playtest): reads the real stamina pool (PimlrStamina on the player), which only drains while
        // sprinting / punching. The old source (JUTPS NormalizedSprintStamina) is a sprint-speed ramp that rises
        // whenever the player runs, which made the bar fill up from 0 on normal running.
        PimlrStamina st = PimlrStamina.Resolve();
        if (st == null) return;

        float target = st.Normalized;
        fillImage.fillAmount = lerpSpeed <= 0f ? target : Mathf.MoveTowards(fillImage.fillAmount, target, lerpSpeed * Time.deltaTime);

        // Darker while exhausted (sprinting locked until it recovers).
        fillImage.color = st.Exhausted ? depletedColor : normalColor;

        if (hideWhenFull)
        {
            bool show = target < 0.999f;
            if (fillImage.enabled != show) fillImage.enabled = show;
        }
    }
}
