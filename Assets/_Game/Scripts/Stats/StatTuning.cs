using System;
using UnityEngine;

// PIMLR #STATS: maps 1-10 ratings to the real JUTPS / PIMLR values. Ratings 1, 5 and 10 are set points with straight
// lines between them. Rating 5 must equal today's tuning, so Thurz (all 5s) plays exactly as before.
//
// Optional asset: Create > PIMLR > Stat Tuning, saved as Resources/PIMLR/StatTuning to override these defaults.
// Without the asset the code defaults below are used.
//
// Unreal: one UCurveFloat per row (keys at 1, 5, 10).
[CreateAssetMenu(menuName = "PIMLR/Stat Tuning", fileName = "StatTuning")]
public class StatTuning : ScriptableObject
{
    [Serializable]
    public struct SetPoints
    {
        public float at1, at5, at10;
        public SetPoints(float a1, float a5, float a10) { at1 = a1; at5 = a5; at10 = a10; }

        public float Evaluate(float rating)
        {
            rating = Mathf.Clamp(rating, 1f, 10f);
            return rating <= 5f
                ? Mathf.Lerp(at1, at5, (rating - 1f) / 4f)
                : Mathf.Lerp(at5, at10, (rating - 5f) / 5f);
        }
    }

    [Header("Speed")]
    [Tooltip("JUCharacterControllerCore.Speed")]
    public SetPoints runSpeed = new SetPoints(2.4f, 3.0f, 3.8f);

    [Header("Agility")]
    [Tooltip("Multiplier on PimlrPlayerFeel.TurnSpeed (12), which is what the player actually turns at today.")]
    public SetPoints turnSpeedMultiplier = new SetPoints(0.75f, 1.0f, 1.35f);
    [Tooltip("JUCharacterControllerCore.AirInfluenceControll")]
    public SetPoints airControl = new SetPoints(0.35f, 0.5f, 0.7f);

    [Header("Accuracy (multiplies the equipped weapon's own values)")]
    [Tooltip("x Weapon.Precision. In JUTPS this is how fast the spread recovers between shots: higher = tighter.")]
    public SetPoints precisionMultiplier = new SetPoints(0.8f, 1.0f, 1.25f);
    [Tooltip("x Weapon.LossOfAccuracyPerShot: how much each shot widens the spread. Lower = tighter.")]
    public SetPoints spreadPerShotMultiplier = new SetPoints(1.25f, 1.0f, 0.75f);

    [Header("Health")]
    [Tooltip("JUHealth.MaxHealth")]
    public SetPoints maxHealth = new SetPoints(80f, 100f, 130f);

    [Header("Stamina")]
    [Tooltip("PimlrStamina.Max")]
    public SetPoints staminaMax = new SetPoints(80f, 100f, 130f);
    [Tooltip("PimlrStamina.RegenPerSecond")]
    public SetPoints staminaRegen = new SetPoints(25f, 30f, 37f);

    [Header("Jump height")]
    [Tooltip("JUCharacterControllerCore.JumpForce")]
    public SetPoints jumpForce = new SetPoints(2.6f, 3.0f, 3.5f);

    static StatTuning active;

    /// <summary>Resources/PIMLR/StatTuning if present, otherwise the defaults above.</summary>
    public static StatTuning Active
    {
        get
        {
            if (active == null)
            {
                active = Resources.Load<StatTuning>("PIMLR/StatTuning");
                if (active == null)
                {
                    active = CreateInstance<StatTuning>();
                    active.hideFlags = HideFlags.DontSave;
                }
            }
            return active;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => active = null;
}
