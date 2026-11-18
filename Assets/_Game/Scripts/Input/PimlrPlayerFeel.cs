using JUTPS;
using UnityEngine;

/// <summary>
/// PIMLR: player-only movement/weapon feel. Applied to characters tagged "Player" on each scene load, so enemy
/// AI (which shares JUCharacterController) keeps JUTPS's default timings. Tune here.
/// </summary>
public static class PimlrPlayerFeel
{
    // Turning: body rotation lerp speed (JUTPS default on our player was 3 = slow, arcing turns).
    public const float TurnSpeed = 12f;

    // Weapons
    public const float HandIKTransitionSpeed = 6f;   // JUTPS default 2 (hands took 0.25s+ to reach the gun)
    public const float WeaponSwitchDuration = 0.25f; // JUTPS default 0.5
    public const float MaxWeaponReadyDelay = 0.33f;  // max wait to fire after a reload or switch

    // Haptics (Nov 16): faint tick on every shot the player fires. Low = heavy motor, High = light motor (0..1).
    // Kept well under the balloon throw (0.15 / 0.35) so sustained fire reads as a light buzz, not a shake.
    public static float ShotRumbleLow = 0.03f;
    public static float ShotRumbleHigh = 0.12f;
    public static float ShotRumbleSeconds = 0.06f;

    public static void ApplyToPlayers()
    {
        foreach (var c in Object.FindObjectsByType<JUCharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.CompareTag("Player")) Apply(c);
    }

    public static void Apply(JUCharacterController c)
    {
        c.RotationSpeed = TurnSpeed;
        c.HandIKTransitionSpeed = HandIKTransitionSpeed;
        c.WeaponSwitchDuration = WeaponSwitchDuration;
        c.MaxWeaponReadyDelay = MaxWeaponReadyDelay;
    }
}
