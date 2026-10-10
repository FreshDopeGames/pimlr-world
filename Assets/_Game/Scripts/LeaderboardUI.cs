using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardUI : MonoBehaviour
{
    [Header("Infinite Mode")]
    public Transform infiniteModeRowHolder;
    public LeaderboardRow infiniteModeRowPrefab;
    public Button waveTabButton, killsTabButton, timeTabButton;
    public Image waveTabHighlight, killsTabHighlight, timeTabHighlight; // optional active-tab indicators

    [Header("Story Mode")]
    public Transform storyModeRowHolder;
    public LeaderboardRow storyModeRowPrefab;

    [Header("Name Entry")]
    // PIMLR (playtest): retained for existing Inspector references; opening the board never requests a name.
    [SerializeField] private PlayerNameEntryUI playerNameEntryUI;

    [Header("Navigation")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private string mainMenuScreenName = "PLMRMainMenuPanel";

    // PIMLR (playtest): optional shared controls and display fields for the single-board layout.
    [Header("Board")]
    [SerializeField] private Button infiniteModeButton;
    [SerializeField] private Button storyModeButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Image infiniteModeHighlight;
    [SerializeField] private Image storyModeHighlight;
    [SerializeField] private Transform rowHolder;
    [SerializeField] private LeaderboardRow rowPrefab;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI yourStatsText;

    // PIMLR (playtest): keep one list because both modes reuse the same visible row area.
    private readonly List<LeaderboardRow> spawnedRows = new List<LeaderboardRow>();
    private LeaderboardManager subscribedManager;

    // PIMLR (playtest): the board defaults to Infinite Mode and its Wave sort whenever it opens.
    private enum BoardMode { Infinite, Story }
    private BoardMode currentMode;
    private LeaderboardManager.InfiniteModeSortMode currentSort;

    private void OnEnable()
    {
        // PIMLR (playtest): named listeners make enable/disable cycles safe without removing prefab listeners.
        if (waveTabButton != null) waveTabButton.onClick.AddListener(OnWaveTabClicked);
        if (killsTabButton != null) killsTabButton.onClick.AddListener(OnKillsTabClicked);
        if (timeTabButton != null) timeTabButton.onClick.AddListener(OnTimeTabClicked);
        if (infiniteModeButton != null) infiniteModeButton.onClick.AddListener(OnInfiniteModeClicked);
        if (storyModeButton != null) storyModeButton.onClick.AddListener(OnStoryModeClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackButton);

        currentMode = BoardMode.Infinite;
        currentSort = LeaderboardManager.InfiniteModeSortMode.Wave;

        subscribedManager = LeaderboardManager.Instance;
        if (subscribedManager != null)
            subscribedManager.Changed += OnLeaderboardChanged;

        Refresh();
    }

    private void OnDisable()
    {
        // PIMLR (playtest): remove only this component's named listeners, preserving Inspector callbacks.
        if (waveTabButton != null) waveTabButton.onClick.RemoveListener(OnWaveTabClicked);
        if (killsTabButton != null) killsTabButton.onClick.RemoveListener(OnKillsTabClicked);
        if (timeTabButton != null) timeTabButton.onClick.RemoveListener(OnTimeTabClicked);
        if (infiniteModeButton != null) infiniteModeButton.onClick.RemoveListener(OnInfiniteModeClicked);
        if (storyModeButton != null) storyModeButton.onClick.RemoveListener(OnStoryModeClicked);
        if (backButton != null) backButton.onClick.RemoveListener(OnBackButton);

        if (subscribedManager != null)
            subscribedManager.Changed -= OnLeaderboardChanged;
        subscribedManager = null;
    }

    // PIMLR (playtest): all board changes converge here so the two modes cannot leave stale rows behind.
    public void Refresh()
    {
        ClearRows();

        LeaderboardManager manager = LeaderboardManager.Instance;
        if (manager == null)
        {
            if (statusText != null)
            {
                statusText.text = "Leaderboards unavailable";
                statusText.gameObject.SetActive(true);
            }
            return;
        }

        if (headerText != null)
            headerText.text = GetHeaderText();

        bool isInfiniteMode = currentMode == BoardMode.Infinite;
        if (waveTabButton != null) waveTabButton.gameObject.SetActive(isInfiniteMode);
        if (killsTabButton != null) killsTabButton.gameObject.SetActive(isInfiniteMode);
        if (timeTabButton != null) timeTabButton.gameObject.SetActive(isInfiniteMode);

        SetActiveTabHighlight(currentSort);
        if (infiniteModeHighlight != null)
            infiniteModeHighlight.gameObject.SetActive(isInfiniteMode);
        if (storyModeHighlight != null)
            storyModeHighlight.gameObject.SetActive(!isInfiniteMode);

        List<LeaderboardEntry> entries = isInfiniteMode
            ? manager.GetInfiniteModeTop(currentSort, 50)
            : manager.GetStoryModeTop(50);
        Transform activeRowHolder = rowHolder != null
            ? rowHolder
            : isInfiniteMode ? infiniteModeRowHolder : storyModeRowHolder;
        LeaderboardRow activeRowPrefab = rowPrefab != null
            ? rowPrefab
            : isInfiniteMode ? infiniteModeRowPrefab : storyModeRowPrefab;

        for (int i = 0; i < entries.Count; i++)
        {
            if (activeRowHolder == null || activeRowPrefab == null)
                continue;

            LeaderboardEntry entry = entries[i];
            LeaderboardRow row = Instantiate(activeRowPrefab, activeRowHolder);
            string value = isInfiniteMode
                ? GetInfiniteValue(entry)
                : FormatTime(entry.completionTime);
            string detail = isInfiniteMode
                ? $"{entry.kills} washes, {entry.bossKills} bosses, {entry.coinsEarned} coins"
                : $"{entry.kills} washes, {entry.coinsEarned} coins";

            row.Set(i + 1, entry.playerName, value,
                entry.anonymousPlayerId == PlayerProfile.AnonymousId, detail);
            spawnedRows.Add(row);
        }

        if (statusText != null)
        {
            bool isEmpty = entries.Count == 0;
            statusText.text = isEmpty ? "No runs yet" : string.Empty;
            statusText.gameObject.SetActive(isEmpty);
        }

        RefreshYourStats(manager);
    }

    // PIMLR (playtest): preserve the existing public navigation entry point.
    public void OnBackButton()
    {
        gameObject.SetActive(false);

        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            return;
        }

        PlmrMainMenuPanel menuPanel = FindObjectOfType<PlmrMainMenuPanel>(true);
        if (menuPanel != null)
        {
            menuPanel.gameObject.SetActive(true);
            return;
        }

        if (uiManager != null)
            uiManager.ShowMenu(mainMenuScreenName);
    }

    // PIMLR (playtest): retain the existing public mode-switch API for external callers.
    public void ShowInfiniteMode(LeaderboardManager.InfiniteModeSortMode sortMode)
    {
        currentMode = BoardMode.Infinite;
        currentSort = sortMode;
        Refresh();
    }

    // PIMLR (playtest): retain the existing public mode-switch API for external callers.
    public void ShowStoryMode()
    {
        currentMode = BoardMode.Story;
        Refresh();
    }

    // PIMLR (playtest): named button handlers keep serialized UI callbacks independent of anonymous delegates.
    private void OnWaveTabClicked() => ShowInfiniteMode(LeaderboardManager.InfiniteModeSortMode.Wave);
    private void OnKillsTabClicked() => ShowInfiniteMode(LeaderboardManager.InfiniteModeSortMode.Kills);
    private void OnTimeTabClicked() => ShowInfiniteMode(LeaderboardManager.InfiniteModeSortMode.SurvivalTime);
    private void OnInfiniteModeClicked() => ShowInfiniteMode(currentSort);
    private void OnStoryModeClicked() => ShowStoryMode();
    private void OnLeaderboardChanged() => Refresh();

    // PIMLR (playtest): keep tab indicators synchronized with the selected Infinite sort.
    private void SetActiveTabHighlight(LeaderboardManager.InfiniteModeSortMode sortMode)
    {
        bool showSortHighlights = currentMode == BoardMode.Infinite;
        if (waveTabHighlight != null)
            waveTabHighlight.gameObject.SetActive(showSortHighlights && sortMode == LeaderboardManager.InfiniteModeSortMode.Wave);
        if (killsTabHighlight != null)
            killsTabHighlight.gameObject.SetActive(showSortHighlights && sortMode == LeaderboardManager.InfiniteModeSortMode.Kills);
        if (timeTabHighlight != null)
            timeTabHighlight.gameObject.SetActive(showSortHighlights && sortMode == LeaderboardManager.InfiniteModeSortMode.SurvivalTime);
    }

    // PIMLR (playtest): clear all generated rows before any refresh result, including unavailable-manager state.
    private void ClearRows()
    {
        foreach (LeaderboardRow row in spawnedRows)
        {
            if (row != null)
                Destroy(row.gameObject);
        }
        spawnedRows.Clear();
    }

    // PIMLR (playtest): keep visible labels consistent with each supported board mode and sort.
    private string GetHeaderText()
    {
        if (currentMode == BoardMode.Story)
            return "Story Mode: Fastest runs";

        return currentSort switch
        {
            LeaderboardManager.InfiniteModeSortMode.Kills => "Infinite Mode: Most washes",
            LeaderboardManager.InfiniteModeSortMode.SurvivalTime => "Infinite Mode: Longest run",
            _ => "Infinite Mode: Most waves"
        };
    }

    // PIMLR (playtest): Infinite values use player-facing washes terminology for kills.
    private string GetInfiniteValue(LeaderboardEntry entry)
    {
        return currentSort switch
        {
            LeaderboardManager.InfiniteModeSortMode.Kills => $"{entry.kills} washes",
            LeaderboardManager.InfiniteModeSortMode.SurvivalTime => FormatTime(entry.survivalTime),
            _ => $"Wave {entry.wave}"
        };
    }

    // PIMLR (playtest): rank and total are read from the same session-aware leaderboard API used by the board.
    private void RefreshYourStats(LeaderboardManager manager)
    {
        if (yourStatsText == null)
            return;

        string sessionId = PlayerProfile.AnonymousId;
        int rank = currentMode == BoardMode.Infinite
            ? manager.GetInfiniteRank(currentSort, sessionId)
            : manager.GetStoryRank(sessionId);
        int count = currentMode == BoardMode.Infinite
            ? manager.GetInfiniteCount()
            : manager.GetStoryCount();
        yourStatsText.text = rank == 0 ? "No runs yet" : $"Your rank: #{rank} of {count}";
    }

    // PIMLR (playtest): one shared clock format for Infinite and Story rows.
    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
    }
}

// PIMLR (playtest): optional visual row details let the shared prefab show mode-specific run information.
public class LeaderboardRow : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI valueText;
    // PIMLR (playtest): optional highlight and detail label for the current player's row.
    [SerializeField] private Image highlight;
    [SerializeField] private TextMeshProUGUI detailText;

    // PIMLR (playtest): defaults preserve compatibility with existing three-argument row setup calls.
    public void Set(int rank, string playerName, string value, bool isYou = false, string detail = "")
    {
        if (rankText != null)
            rankText.text = rank.ToString();
        if (nameText != null)
            nameText.text = isYou ? $"{playerName} (you)" : playerName;
        if (valueText != null)
            valueText.text = value;
        if (highlight != null)
            highlight.gameObject.SetActive(isYou);
        if (detailText != null)
            detailText.text = detail;
    }
}
