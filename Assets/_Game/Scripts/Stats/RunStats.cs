using System;
using UnityEngine;

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
