using System;
using UnityEngine;

// PIMLR (playtest): lightweight local play session. Name is only required at score-submit time.
public static class PlayerSession
{
    // PIMLR (playtest): preserve the old limits as aliases while PlayerProfile owns them.
    public const int MinLength = PlayerProfile.MinNameLength;
    public const int MaxLength = PlayerProfile.MaxNameLength;

    public static event Action NameRequested;       // the overlay listens for this
    public static event Action<string> NameChanged;  // "" means the session was reset
    // PIMLR (playtest): let listeners react when a pending name request is dismissed.
    public static event Action NameCancelled;
    private static Action _pending;

    // PIMLR (playtest): expose the shared player identity instead of maintaining session-local prefs.
    public static bool HasName => PlayerProfile.HasSetDisplayName;
    public static string Name => PlayerProfile.DisplayName;
    public static string Id => PlayerProfile.AnonymousId;

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
        // PIMLR (playtest): validate the same sanitized text that PlayerProfile stores.
        string clean = (raw ?? "").Replace("<", "").Replace(">", "").Trim();
        if (clean.Length < PlayerProfile.MinNameLength)
        {
            error = $"Name needs at least {PlayerProfile.MinNameLength} characters.";
            return false;
        }
        if (clean.Length > PlayerProfile.MaxNameLength)
        {
            error = $"Name cannot exceed {PlayerProfile.MaxNameLength} characters.";
            return false;
        }

        clean = PlayerProfile.SetDisplayName(clean);
        error = null;

        NameChanged?.Invoke(clean);
        var cb = _pending; _pending = null;
        cb?.Invoke();
        return true;
    }

    // PIMLR (playtest): notify subscribers that an outstanding name prompt was cancelled.
    public static void CancelPending()
    {
        _pending = null;
        NameCancelled?.Invoke();
    }

    // "Not you?" for shared playtest machines.
    public static void Reset()
    {
        // PIMLR (playtest): start a new identity through the single profile source of truth.
        PlayerProfile.StartNewPlayer();
        _pending = null;
        NameChanged?.Invoke("");
    }
}
