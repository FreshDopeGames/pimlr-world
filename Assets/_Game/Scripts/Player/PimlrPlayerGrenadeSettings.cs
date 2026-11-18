using UnityEngine;

/// <summary>
/// PIMLR #GREN: tuning for the player's water balloons. The live copy is Assets/_Game/Resources/PIMLR/PlayerGrenades.asset.
/// Damage / splash radius live on the balloon's burst effect (Water_Explosion_Player, WaterSplashDamage).
/// Balloons are the player's secondary weapon: picked up only (no shop, no auto-equip), selected with D-pad Left/Right
/// (Q / E on keyboard). Throwing is "cook to throw" (Nov 16): hold RT / R2 / left mouse to wind up (the pose holds at
/// the top of the wind-up), release to throw. The longer the hold, the farther it goes (minRange -> maxRange over
/// fullCookTime). LT / right mouse still plants the player and faces the camera to line the throw up.
/// </summary>
[CreateAssetMenu(menuName = "PIMLR/Player Grenade Settings", fileName = "PlayerGrenades")]
public class PimlrPlayerGrenadeSettings : ScriptableObject
{
    [Header("Balloon")]
    [Tooltip("The balloon the player throws (Grenade with Player Owned ticked).")]
    public GameObject balloonPrefab;

    [Header("Carrying")]
    [Min(0)] public int startCount = 0;
    [Min(1)] public int maxCount = 5;

    [Header("HUD / pause menu")]
    [Tooltip("Icon shown in the HUD weapon panel while balloons are equipped (and in the pause menu's Secondary slot).")]
    public Sprite hudIcon;
    [Tooltip("Name shown in the HUD weapon panel's name label, if that panel has one.")]
    public string hudName = "Water Balloons";

    [Header("Throwing")]
    [Tooltip("Seconds between throws (on top of the throw animation).")]
    [Min(0.1f)] public float cooldown = 0.4f;
    [Tooltip("Longest throw (metres), reached after a full cook. Keep it 3 m past the bombers' Max Throw Distance (GrenadeThrower on PIMLR_Bomber, 30 m).")]
    [Min(3f)] public float maxRange = 33f;
    [Tooltip("Shortest throw (metres): a quick tap of the throw button.")]
    [Min(1f)] public float minRange = 4f;
    [Tooltip("Horizontal speed of the arc (m/s). Flight time = distance / this, clamped below. Same idea as the bombers.")]
    [Min(1f)] public float arcHorizontalSpeed = 16f;
    public float minFlightTime = 0.35f;
    public float maxFlightTime = 1.7f;
    [Tooltip("If the Animator never reaches a throw state, let go after this many seconds anyway.")]
    [Min(0f)] public float fallbackReleaseDelay = 0.45f;

    [Header("Cook to throw")]
    [Tooltip("Seconds of holding the throw button (counted from the press) to reach Max Range.")]
    [Min(0.1f)] public float fullCookTime = 1.2f;
    [Tooltip("Hold time (0-1 of Full Cook Time, x axis) -> distance (0 = Min Range, 1 = Max Range, y axis). Linear by default; bow it upward for a faster early ramp.")]
    public AnimationCurve cookResponse = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("Throw clip used for the cooked throw. Throw Object has the clearest wind-up.")]
    public PimlrThrowVariant cookedThrowVariant = PimlrThrowVariant.ThrowObject;
    [Tooltip("Where the wind-up pose holds, as a share of the clip's release time (0.7 = 70% of the way to the release frame). " +
             "Raise it if the arm isn't fully cocked back while holding; lower it if the throw has visibly started.")]
    [Range(0.2f, 0.98f)] public float windupHoldShare = 0.7f;
    [Tooltip("Rumble pulse every this many seconds while cooking (0 = off) - a subtle 'charging' cue on gamepad.")]
    [Min(0f)] public float cookRumbleInterval = 0.3f;

    [Header("Trajectory arrow (while cooking / aiming)")]
    public Material arcFillMaterial;
    public Material arcOutlineMaterial;
    [Tooltip("Share of the flight path the arrow covers, from the hand (0.5 = the first half).")]
    [Range(0.1f, 1f)] public float arrowLengthFraction = 0.5f;
    [Tooltip("Shaft width (metres).")]
    [Min(0.02f)] public float arrowWidth = 0.16f;
    [Min(0.05f)] public float arrowHeadWidth = 0.44f;
    [Min(0.05f)] public float arrowHeadLength = 0.55f;
    [Tooltip("White border width (metres), like the objective arrow's outline.")]
    [Min(0f)] public float arrowOutlineWidth = 0.035f;

    // ---- Legacy (pre-Nov 16 left-stick distance aiming). Kept so the existing asset's values survive; unused.
    [HideInInspector] public float startRange = 15f;
    [HideInInspector] public float rangeAdjustSpeed = 14f;
    [HideInInspector] public PimlrThrowVariantMode throwVariant = PimlrThrowVariantMode.ByDistance;
}
