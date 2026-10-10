using System;
using UnityEngine;

// PIMLR (playtest): centralize locally persisted session and progress keys.
public static class SessionState
{
    public const string CoinsKey = "PIMLR_Coins";

    // PIMLR (playtest): clear local progression as one saved reset operation.
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(CoinsKey);
        PlayerPrefs.DeleteKey(ZoneProgress.ZoneKey);
        PlayerPrefs.Save();

        // PIMLR (playtest): clear the live balance so its next update cannot restore the previous session's coins.
        if (CoinManager.Instance != null)
            CoinManager.Instance.SetCoins(0);
    }
}

// PIMLR (playtest): provide a typed owner for persisted zone progression.
public static class ZoneProgress
{
    internal const string ZoneKey = "currentZoneMode";

    public static Zone Current
    {
        get
        {
            string storedZone = PlayerPrefs.GetString(ZoneKey, string.Empty);
            return Enum.TryParse(storedZone, out Zone zone) ? zone : Zone.ChatWilly;
        }
        set => PlayerPrefs.SetString(ZoneKey, value.ToString());
    }
}
