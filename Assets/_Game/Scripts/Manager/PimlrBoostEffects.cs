// PIMLR (Nov 27): live values for Track Boosts that aren't player stats (those go through PlayerStatBlock).
// Set by AuthManager.PowerChange, cleared by AuthManager.ResetPowerups; gameplay reads them where the effect applies.
//   CoinMultiplier  "2x PIMLR Coins" (Thurz - BIG YAPER ft. Mike & Keys): EnemyCoinDrop multiplies each enemy's payout.
//
// Unreal note: fields on the boost subsystem (UPimlrBoostSubsystem), read by the coin-drop component.

public static class PimlrBoostEffects
{
    /// <summary>Multiplier on coins dropped by defeated enemies (1 = no boost).</summary>
    public static float CoinMultiplier = 1f;

    public const float CoinBoostFactor = 2f;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { CoinMultiplier = 1f; }
}
