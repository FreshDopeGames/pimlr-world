using UnityEngine;

/// <summary>
/// PIMLR #GREN: tuning for the player's water balloons. The live copy is Assets/_Game/Resources/PIMLR/PlayerGrenades.asset.
/// Damage / splash radius live on the balloon's burst effect (Water_Explosion_Player, WaterSplashDamage).
/// Balloons are the player's secondary weapon: picked up only (no shop, no auto-equip), selected with D-pad Left/Right
/// (Q / E on keyboard), aimed with LT (right mouse), distance set with the left stick (W / S), thrown with RT (left mouse).
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

    [Header("Throwing")]
    [Tooltip("Seconds between throws (on top of the throw animation).")]
    [Min(0.1f)] public float cooldown = 0.4f;
    [Tooltip("Longest throw (metres). Keep it 3 m past the bombers' Max Throw Distance (GrenadeThrower on PIMLR_Bomber, 30 m).")]
    [Min(3f)] public float maxRange = 33f;
    [Tooltip("Shortest throw the left stick can dial in (metres).")]
    [Min(1f)] public float minRange = 4f;
    [Tooltip("Throw distance when you first equip balloons (metres). After that the last distance you set is kept.")]
    [Min(1f)] public float startRange = 15f;
    [Tooltip("How fast the left stick (W / S) changes the throw distance, metres per second at full tilt.")]
    [Min(1f)] public float rangeAdjustSpeed = 14f;
    [Tooltip("Horizontal speed of the arc (m/s). Flight time = distance / this, clamped below. Same idea as the bombers.")]
    [Min(1f)] public float arcHorizontalSpeed = 16f;
    public float minFlightTime = 0.35f;
    public float maxFlightTime = 1.7f;
    [Tooltip("Which throw clip plays. ByDistance = Goalie Throw up close, Throw Object (the lunge) for long throws.")]
    public PimlrThrowVariantMode throwVariant = PimlrThrowVariantMode.ByDistance;
    [Tooltip("If the Animator never reaches a throw state, let go after this many seconds anyway.")]
    [Min(0f)] public float fallbackReleaseDelay = 0.45f;

    [Header("Trajectory arrow (while aiming)")]
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
}
