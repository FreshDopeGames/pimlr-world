using System;
using System.Collections.Generic;

// PIMLR Leaderboards: plain-data model, local-only for the October playtest.
// Kept engine-agnostic (no Unity types) so the JSON this serializes to ports
// cleanly if/when this moves to a UGS backend or the future Unreal build.
[Serializable]
public class LeaderboardEntry
{
    public string playerName;
    public string anonymousPlayerId; // PlayerProfile.AnonymousId -- decoupled from playerName on purpose, for future telemetry joins without PII

    // Infinite Mode fields
    public int wave;
    public int kills;
    public float survivalTime; // seconds

    // Story Mode field
    public float completionTime; // seconds

    // Run summary fields
    public int coinsEarned;
    public int bossKills;
    public string startZone;

    public string dateISO;

    public LeaderboardEntry(string playerName)
    {
        this.playerName = string.IsNullOrEmpty(playerName) ? "Player" : playerName;
        dateISO = DateTime.UtcNow.ToString("o");
    }
}

[Serializable]
public class LeaderboardData
{
    public List<LeaderboardEntry> infiniteModeEntries = new List<LeaderboardEntry>();
    public List<LeaderboardEntry> storyModeEntries = new List<LeaderboardEntry>();
}

[Serializable]
public class SessionSummary
{
    public string sessionId;
    public string playerName;
    public int runs;
    public int bestWave;
    public int bestKills;
    public float bestSurvivalTime;
    public float bestStoryTime;
    public string lastPlayedISO;
}
