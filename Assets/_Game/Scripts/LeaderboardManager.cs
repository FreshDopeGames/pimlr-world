using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// PIMLR Leaderboards: local-only, per-device top scores (October playtest scope).
// Uses the project's existing Singleton<T> base (see Assets/_Game/Scripts/Manager/Singleton.cs)
// and persists as a single JSON blob in PlayerPrefs, same storage style as everything
// else in this project (zone mode, coins, purchases).
//
// Deliberately NOT wired to idea-labs.xyz or Unity Gaming Services -- if a real backend
// gets added later, only Save()/Load() need to change; SubmitX/GetX stays the same so
// InfiniteMode.cs / GameExecutionManager.cs never need to change again.
//
// EDITOR WIRING (residual step):
//   1. Create a persistent GameObject (or add to an existing manager prefab, e.g. "AuthManager"
//      or a new "Managers" prefab loaded once at boot).
//   2. Add this component, tick 'dontdestry' (inherited from Singleton<T>) if it should
//      survive scene loads.
public class LeaderboardManager : Singleton<LeaderboardManager>
{
    private const string SaveKey = "PIMLR_Leaderboards_v1";
    private const int MaxEntriesPerList = 100; // generous local cap; oldest trimmed first

    private LeaderboardData data;

    public enum InfiniteModeSortMode { Wave, Kills, SurvivalTime }

    public override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // duplicate destroyed by Singleton<T>.Awake()
        Load();
    }

    #region Submit
    /// <summary>Call when an Infinite Mode run ends (player death or quit).</summary>
    public void SubmitInfiniteModeRun(string playerName, int waveReached, int enemiesKilled, float survivalTimeSeconds)
    {
        data.infiniteModeEntries.Add(new LeaderboardEntry(playerName)
        {
            anonymousPlayerId = PlayerProfile.AnonymousId,
            wave = waveReached,
            kills = enemiesKilled,
            survivalTime = survivalTimeSeconds
        });
        Save();
    }

    /// <summary>Call when Story Mode is completed (final zone/boss cleared).</summary>
    public void SubmitStoryModeRun(string playerName, float completionTimeSeconds)
    {
        data.storyModeEntries.Add(new LeaderboardEntry(playerName)
        {
            anonymousPlayerId = PlayerProfile.AnonymousId,
            completionTime = completionTimeSeconds
        });
        Save();
    }
    #endregion

    #region Query
    public List<LeaderboardEntry> GetInfiniteModeTop(InfiniteModeSortMode sortMode, int count = 10)
    {
        IEnumerable<LeaderboardEntry> sorted = sortMode switch
        {
            InfiniteModeSortMode.Wave => data.infiniteModeEntries.OrderByDescending(e => e.wave),
            InfiniteModeSortMode.Kills => data.infiniteModeEntries.OrderByDescending(e => e.kills),
            InfiniteModeSortMode.SurvivalTime => data.infiniteModeEntries.OrderByDescending(e => e.survivalTime),
            _ => data.infiniteModeEntries.OrderByDescending(e => e.wave)
        };
        return sorted.Take(count).ToList();
    }

    /// <summary>Story Mode ranks fastest completion first.</summary>
    public List<LeaderboardEntry> GetStoryModeTop(int count = 10)
    {
        return data.storyModeEntries.OrderBy(e => e.completionTime).Take(count).ToList();
    }
    #endregion

    #region Persistence
    private void Save()
    {
        TrimOldest(data.infiniteModeEntries);
        TrimOldest(data.storyModeEntries);
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    // Trims by date rather than any single metric, so pruning never biases
    // which entries survive to be shown under a different sort tab later.
    private void TrimOldest(List<LeaderboardEntry> list)
    {
        if (list.Count <= MaxEntriesPerList) return;
        var kept = list.OrderByDescending(e => e.dateISO).Take(MaxEntriesPerList).ToList();
        list.Clear();
        list.AddRange(kept);
    }

    private void Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        try
        {
            data = string.IsNullOrEmpty(json) ? new LeaderboardData() : JsonUtility.FromJson<LeaderboardData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LeaderboardManager: failed to parse saved data, resetting. {e}");
            data = new LeaderboardData();
        }
        data ??= new LeaderboardData();
        data.infiniteModeEntries ??= new List<LeaderboardEntry>();
        data.storyModeEntries ??= new List<LeaderboardEntry>();
    }

    [ContextMenu("Clear All Leaderboard Data")]
    public void ClearAll()
    {
        data = new LeaderboardData();
        Save();
    }
    #endregion
}
