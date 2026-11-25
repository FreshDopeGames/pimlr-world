// PIMLR #TUT: session-scoped flag for the controls warm-up.
// "Once per user session" = once per app launch / WebGL page load, or per new player name if the
// name-entry flow calls BeginSession(). Deliberately NOT saved to PlayerPrefs.
//
// Unreal mapping: a bool on a UGameInstanceSubsystem (UTutorialSubsystem). The GameInstance lives exactly
// as long as this static does, so the behaviour carries over 1:1.

using UnityEngine;

public static class TutorialSession
{
    public static bool CompletedThisSession { get; private set; }
    public static string SessionPlayerName { get; private set; }

    public static void MarkCompleted() => CompletedThisSession = true;

    /// <summary>Call from the name-entry / leaderboard session init. A different name = a new session,
    /// so the next player on the same machine gets the warm-up too.</summary>
    public static void BeginSession(string playerName)
    {
        if (playerName == SessionPlayerName) return;
        SessionPlayerName = playerName;
        CompletedThisSession = false;
    }

    // Keeps the flag correct when Enter Play Mode Options has domain reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        CompletedThisSession = false;
        SessionPlayerName = null;
    }
}
