using UnityEngine;

/// <summary>
/// PIMLR #GREN: tuning for the player's water balloons. The live copy is Assets/_Game/Resources/PIMLR/PlayerGrenades.asset.
/// Damage / splash radius live on the balloon's burst effect (Water_Explosion_Player, WaterSplashDamage).
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
    [Tooltip("Seconds between throws.")]
    [Min(0.1f)] public float cooldown = 0.9f;
    [Tooltip("The balloon lands where the crosshair points, up to this far (metres).")]
    [Min(3f)] public float maxRange = 28f;
    [Tooltip("Horizontal speed of the arc (m/s). Flight time = distance / this, clamped below.")]
    [Min(1f)] public float arcHorizontalSpeed = 16f;
    public float minFlightTime = 0.35f;
    public float maxFlightTime = 1.4f;
    [Tooltip("Play the player Animator's 'Throw' trigger.")]
    public bool useThrowAnimation = true;
    [Tooltip("Seconds from the button press to the balloon leaving the hand.")]
    [Min(0f)] public float releaseDelay = 0.3f;

    [Header("Buttons")]
    public UnityEngine.InputSystem.Key keyboardKey = UnityEngine.InputSystem.Key.G;
    [Tooltip("D-pad Down is free in gameplay (Left/Right = weapons, Up = Fan Hype in Save The Fans).")]
    public UnityEngine.InputSystem.LowLevel.GamepadButton gamepadButton = UnityEngine.InputSystem.LowLevel.GamepadButton.DpadDown;
}
