using TMPro;
using UnityEngine;

public class RunStatsHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI modeLabel;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI killsText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI coinsText;

    private const float TimeRefreshInterval = 0.25f;
    private float nextTimeRefresh;

    private void OnEnable()
    {
        RunStats.Changed += OnRunStatsChanged;
        RunStats.Ended += OnRunEnded;
        Refresh();
    }

    private void OnDisable()
    {
        RunStats.Changed -= OnRunStatsChanged;
        RunStats.Ended -= OnRunEnded;
    }

    private void Update()
    {
        if (timeText == null || Time.unscaledTime < nextTimeRefresh)
            return;

        nextTimeRefresh = Time.unscaledTime + TimeRefreshInterval;
        RefreshTime();
    }

    private void OnRunStatsChanged(RunSnapshot snapshot)
    {
        Refresh();
    }

    private void OnRunEnded(RunSnapshot snapshot)
    {
        Refresh();
    }

    private void Refresh()
    {
        RunSnapshot snapshot = RunStats.Snapshot;
        bool hasActiveRun = RunStats.Active;
        bool infiniteRun = hasActiveRun && snapshot.mode == RunMode.Infinite;

        if (modeLabel != null)
            modeLabel.text = hasActiveRun ? snapshot.mode.ToString() : "--";

        if (waveText != null)
        {
            waveText.gameObject.SetActive(infiniteRun);
            waveText.text = infiniteRun ? $"Wave {snapshot.wave}" : "--";
        }

        if (killsText != null)
            killsText.text = hasActiveRun ? snapshot.kills.ToString() : "--";

        if (coinsText != null)
            coinsText.text = hasActiveRun ? snapshot.coinsEarned.ToString() : "--";

        RefreshTime();
    }

    private void RefreshTime()
    {
        if (timeText == null)
            return;

        timeText.text = RunStats.Active ? FormatTime(RunStats.Elapsed) : "--";
    }

    internal static string FormatTime(float elapsed)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsed));
        return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
    }
}
