using System;
using UnityEngine;

// PIMLR Player Profile: local identity used for leaderboards now, and as the
// anchor point for future analytics/telemetry correlation.
//
// Two separate identifiers ON PURPOSE:
//   - DisplayName: whatever the player types. Shown on leaderboards. Treat as
//     public/self-chosen, never as a reliable real-world identifier.
//   - AnonymousId: a random GUID generated once per install. Use THIS as the
//     join key for any analytics/telemetry export. Never key an export off
//     DisplayName -- players sometimes type their real name into a name field.
//
// This class does not send anything anywhere. It just gives the rest of the
// game a single place to read/write these two values consistently.
public static class PlayerProfile
{
    private const string DisplayNameKey = "PIMLR_PlayerDisplayName";
    private const string AnonymousIdKey = "PIMLR_AnonymousId";
    private const int MaxNameLength = 16;

    public static string DisplayName
    {
        get => PlayerPrefs.GetString(DisplayNameKey, string.Empty);
        private set => PlayerPrefs.SetString(DisplayNameKey, value);
    }

    public static string AnonymousId
    {
        get
        {
            string id = PlayerPrefs.GetString(AnonymousIdKey, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(AnonymousIdKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    public static bool HasSetDisplayName => !string.IsNullOrEmpty(DisplayName);

    public static void StartNewPlayer()
    {
        PlayerPrefs.DeleteKey(DisplayNameKey);
        PlayerPrefs.SetString(AnonymousIdKey, Guid.NewGuid().ToString("N"));
        PlayerPrefs.Save();
    }

    /// <summary>Cleans/clamps input and stores it; returns the name actually saved.</summary>
    public static string SetDisplayName(string rawInput)
    {
        string cleaned = string.IsNullOrWhiteSpace(rawInput) ? "Player" : rawInput.Trim();
        if (cleaned.Length > MaxNameLength) cleaned = cleaned.Substring(0, MaxNameLength);
        DisplayName = cleaned;
        PlayerPrefs.Save();
        return cleaned;
    }
}
