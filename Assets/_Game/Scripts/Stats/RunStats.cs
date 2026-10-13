using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum RunMode
{
    None,
    Story,
    Infinite
}

public enum RunEndReason
{
    Completed,
    Died,
    Quit,
    Abandoned
}

[Serializable]
public struct RunSnapshot
{
    public RunMode mode;
    public bool active;
    public int wave;
    public int kills;
    public int bossKills;
    public int coinsEarned;
    public float elapsed;
    public Zone startZone;
    public Zone zone;
    public RunEndReason endReason;
}

public static class RunStats
{
    private static RunSnapshot _snapshot = CreateEmptySnapshot();
    private static RunSnapshot _lastSnapshot = CreateEmptySnapshot();
    private static float _startTime;
    private static float _frozenElapsed;

    public static event Action<RunSnapshot> Changed;
    public static event Action<RunSnapshot> Ended;

    public static bool Active => _snapshot.active;
    public static RunMode Mode => _snapshot.mode;

    public static float Elapsed => Active
        ? Time.time - _startTime
        : _frozenElapsed;

    public static RunSnapshot Snapshot
    {
        get
        {
            RunSnapshot snapshot = Active ? _snapshot : _lastSnapshot;
            snapshot.elapsed = Elapsed;
            return snapshot;
        }
    }

    public static void Begin(RunMode mode, Zone startZone)
    {
        if (Active)
            End(RunEndReason.Abandoned);

        _snapshot = new RunSnapshot
        {
            mode = mode,
            active = true,
            wave = mode == RunMode.Infinite ? 1 : 0,
            startZone = startZone,
            zone = startZone
        };
        _lastSnapshot = _snapshot;
        _startTime = Time.time;
        _frozenElapsed = 0f;

        Changed?.Invoke(Snapshot);
    }

    public static void RegisterKill(bool isBoss = false)
    {
        if (!Active)
            return;

        _snapshot.kills++;
        if (isBoss)
            _snapshot.bossKills++;

        Changed?.Invoke(Snapshot);
    }

    public static void AddCoins(int amount)
    {
        if (!Active || amount <= 0)
            return;

        _snapshot.coinsEarned += amount;
        Changed?.Invoke(Snapshot);
    }

    public static void SetWave(int wave)
    {
        if (!Active)
            return;

        _snapshot.wave = wave;
        Changed?.Invoke(Snapshot);
    }

    public static void SetZone(Zone zone)
    {
        if (!Active)
            return;

        _snapshot.zone = zone;
        Changed?.Invoke(Snapshot);
    }

    public static RunSnapshot End(RunEndReason reason)
    {
        if (!Active)
            return _lastSnapshot;

        _frozenElapsed = Elapsed;
        _snapshot.active = false;
        _snapshot.elapsed = _frozenElapsed;
        _snapshot.endReason = reason;
        RunSnapshot finalSnapshot = _snapshot;
        _lastSnapshot = finalSnapshot;

        Ended?.Invoke(finalSnapshot);
        return finalSnapshot;
    }

    // PIMLR (leaderboards): Story runs end only by completing (GameExecutionManager.CompleteStory) or quitting.
    // Dying / restarting a stage reloads YannicksWorld and the run carries on. Leaving YannicksWorld for any other
    // scene (hub, main menu) while a Story run is active = quit.
    public const string StorySceneName = "YannicksWorld";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallStoryRunQuitGuard()
    {
        SceneManager.activeSceneChanged -= StoryRunQuitGuard;
        SceneManager.activeSceneChanged += StoryRunQuitGuard;
    }

    private static void StoryRunQuitGuard(Scene previous, Scene next)
    {
        if (!Active || Mode != RunMode.Story || next.name == StorySceneName)
            return;

        Debug.Log($"RunStats: Story run cancelled - left {StorySceneName} for {next.name} before completing.");
        End(RunEndReason.Quit);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _snapshot = CreateEmptySnapshot();
        _lastSnapshot = CreateEmptySnapshot();
        _startTime = 0f;
        _frozenElapsed = 0f;
        Changed = null;
        Ended = null;
    }

    private static RunSnapshot CreateEmptySnapshot()
    {
        return new RunSnapshot { mode = RunMode.None };
    }
}
