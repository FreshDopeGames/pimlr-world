using System;
using UnityEngine;

// PIMLR (playtest): lightweight local play session. Name is only required at score-submit time.
public static class PlayerSession
{
    private const string NameKey = "pimlr_session_name";
    private const string IdKey   = "pimlr_session_id";
    public const int MinLength = 2, MaxLength = 12;

    public static event Action NameRequested;       // the overlay listens for this
    public static event Action<string> NameChanged;  // "" means the session was reset
    private static Action _pending;

    public static bool HasName => !string.IsNullOrEmpty(Name);
    public static string Name => PlayerPrefs.GetString(NameKey, "");

    public static string Id
    {
        get
        {
            string id = PlayerPrefs.GetString(IdKey, "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(IdKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    public static string SuggestName() => "Player" + UnityEngine.Random.Range(1000, 10000);

    // Call before submitting a score. Runs onReady now if a name exists, otherwise after the player picks one.
    public static void EnsureName(Action onReady)
    {
        if (HasName) { onReady?.Invoke(); return; }
        _pending += onReady;
        NameRequested?.Invoke();
    }

    public static bool TrySetName(string raw, out string error)
    {
        // Strip < > so a name can't inject TMP rich-text tags into the leaderboard list.
        string clean = (raw ?? "").Replace("<", "").Replace(">", "").Trim();
        if (clean.Length < MinLength) { error = $"Name needs at least {MinLength} characters."; return false; }
        if (clean.Length > MaxLength) clean = clean.Substring(0, MaxLength);

        PlayerPrefs.SetString(NameKey, clean);
        PlayerPrefs.Save();
        error = null;

        NameChanged?.Invoke(clean);
        var cb = _pending; _pending = null;
        cb?.Invoke();
        return true;
    }

    public static void CancelPending() { _pending = null; }

    // "Not you?" for shared playtest machines.
    public static void Reset()
    {
        PlayerPrefs.DeleteKey(NameKey);
        PlayerPrefs.DeleteKey(IdKey);
        PlayerPrefs.Save();
        _pending = null;
        NameChanged?.Invoke("");
    }
}
