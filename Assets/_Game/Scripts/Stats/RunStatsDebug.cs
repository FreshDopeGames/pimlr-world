using UnityEngine;

public class RunStatsDebug : MonoBehaviour
{
    [ContextMenu("Begin Infinite")]
    private void BeginInfinite()
    {
        RunStats.Begin(RunMode.Infinite, Zone.InfiniteMode);
        LogSnapshot(RunStats.Snapshot);
    }

    [ContextMenu("Add 5 Kills")]
    private void AddFiveKills()
    {
        for (int i = 0; i < 5; i++)
            RunStats.RegisterKill();

        LogSnapshot(RunStats.Snapshot);
    }

    [ContextMenu("Add Boss Kill")]
    private void AddBossKill()
    {
        RunStats.RegisterKill(true);
        LogSnapshot(RunStats.Snapshot);
    }

    [ContextMenu("Next Wave")]
    private void NextWave()
    {
        RunStats.SetWave(RunStats.Snapshot.wave + 1);
        LogSnapshot(RunStats.Snapshot);
    }

    [ContextMenu("End Died")]
    private void EndDied()
    {
        LogSnapshot(RunStats.End(RunEndReason.Died));
    }

    [ContextMenu("End Completed")]
    private void EndCompleted()
    {
        LogSnapshot(RunStats.End(RunEndReason.Completed));
    }

    private static void LogSnapshot(RunSnapshot snapshot)
    {
        Debug.Log(
            $"RunStats Snapshot: mode={snapshot.mode}, active={snapshot.active}, wave={snapshot.wave}, " +
            $"kills={snapshot.kills}, bossKills={snapshot.bossKills}, coinsEarned={snapshot.coinsEarned}, " +
            $"elapsed={snapshot.elapsed}, startZone={snapshot.startZone}, zone={snapshot.zone}, endReason={snapshot.endReason}");
    }
}
