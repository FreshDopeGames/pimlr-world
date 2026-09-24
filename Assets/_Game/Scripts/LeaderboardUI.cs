using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// PIMLR Leaderboards UI: three tab buttons re-sort the same Infinite Mode list
// (Wave / Kills / Survival Time); Story Mode is a separate always-by-time list.
// Follows the same "spawn a row prefab into a holder" pattern already used by
// MusicPurchasePanel/MusicPurchaseEntry elsewhere in this project.
//
// EDITOR WIRING (residual step):
//   1. Build a panel with two sections: Infinite Mode (3 tab buttons + a row holder
//      under a ScrollRect content) and Story Mode (row holder only).
//   2. Create a small row prefab: Rank / Name / Value as three TextMeshProUGUI fields,
//      add the LeaderboardRow component below, assign the three text fields.
//   3. Assign infiniteModeRowPrefab / storyModeRowPrefab to that prefab, and the two
//      row holders to their ScrollRect Content transforms.
//   4. Assign the 3 tab buttons; tab highlight Images are optional (simple active-tab dot/underline).
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
    [SerializeField] private PlayerNameEntryUI playerNameEntryUI;

    [Header("Navigation")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private string mainMenuScreenName = "PLMRMainMenuPanel";

    private readonly List<LeaderboardRow> spawnedInfiniteRows = new List<LeaderboardRow>();
    private readonly List<LeaderboardRow> spawnedStoryRows = new List<LeaderboardRow>();

    private void OnEnable()
    {
        if (waveTabButton != null) waveTabButton.onClick.AddListener(() => ShowInfiniteMode(LeaderboardManager.InfiniteModeSortMode.Wave));
        if (killsTabButton != null) killsTabButton.onClick.AddListener(() => ShowInfiniteMode(LeaderboardManager.InfiniteModeSortMode.Kills));
        if (timeTabButton != null) timeTabButton.onClick.AddListener(() => ShowInfiniteMode(LeaderboardManager.InfiniteModeSortMode.SurvivalTime));

        if (playerNameEntryUI == null)
            playerNameEntryUI = FindObjectOfType<PlayerNameEntryUI>(true);

        if (!PlayerProfile.HasSetDisplayName && playerNameEntryUI != null)
        {
            playerNameEntryUI.NameSubmitted += RefreshAfterNameEntry;
            playerNameEntryUI.gameObject.SetActive(true);
            return;
        }

        if (!PlayerProfile.HasSetDisplayName)
        {
            Debug.LogError("LeaderboardUI requires a PlayerNameEntryUI reference or object in the scene.");
            return;
        }

        RefreshLeaderboard();
    }

    private void OnDisable()
    {
        if (waveTabButton != null) waveTabButton.onClick.RemoveAllListeners();
        if (killsTabButton != null) killsTabButton.onClick.RemoveAllListeners();
        if (timeTabButton != null) timeTabButton.onClick.RemoveAllListeners();
        if (playerNameEntryUI != null)
            playerNameEntryUI.NameSubmitted -= RefreshAfterNameEntry;
    }

    private void RefreshAfterNameEntry()
    {
        RefreshLeaderboard();
    }

    private void RefreshLeaderboard()
    {
        ShowInfiniteMode(LeaderboardManager.InfiniteModeSortMode.Wave);
        ShowStoryMode();
    }

    public void OnBackButton()
    {
        gameObject.SetActive(false);

        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            return;
        }

        if (uiManager != null)
            uiManager.ShowMenu(mainMenuScreenName);
    }

    public void ShowInfiniteMode(LeaderboardManager.InfiniteModeSortMode sortMode)
    {
        SetActiveTabHighlight(sortMode);

        List<LeaderboardEntry> entries = LeaderboardManager.Instance.GetInfiniteModeTop(sortMode);
        ClearRows(spawnedInfiniteRows);

        for (int i = 0; i < entries.Count; i++)
        {
            LeaderboardRow row = Instantiate(infiniteModeRowPrefab, infiniteModeRowHolder);
            string valueText = sortMode switch
            {
                LeaderboardManager.InfiniteModeSortMode.Wave => $"Wave {entries[i].wave}",
                LeaderboardManager.InfiniteModeSortMode.Kills => $"{entries[i].kills} kills",
                LeaderboardManager.InfiniteModeSortMode.SurvivalTime => FormatTime(entries[i].survivalTime),
                _ => string.Empty
            };
            row.Set(i + 1, entries[i].playerName, valueText);
            spawnedInfiniteRows.Add(row);
        }
    }

    public void ShowStoryMode()
    {
        List<LeaderboardEntry> entries = LeaderboardManager.Instance.GetStoryModeTop();
        ClearRows(spawnedStoryRows);

        for (int i = 0; i < entries.Count; i++)
        {
            LeaderboardRow row = Instantiate(storyModeRowPrefab, storyModeRowHolder);
            row.Set(i + 1, entries[i].playerName, FormatTime(entries[i].completionTime));
            spawnedStoryRows.Add(row);
        }
    }

    private void SetActiveTabHighlight(LeaderboardManager.InfiniteModeSortMode sortMode)
    {
        if (waveTabHighlight) waveTabHighlight.gameObject.SetActive(sortMode == LeaderboardManager.InfiniteModeSortMode.Wave);
        if (killsTabHighlight) killsTabHighlight.gameObject.SetActive(sortMode == LeaderboardManager.InfiniteModeSortMode.Kills);
        if (timeTabHighlight) timeTabHighlight.gameObject.SetActive(sortMode == LeaderboardManager.InfiniteModeSortMode.SurvivalTime);
    }

    private void ClearRows(List<LeaderboardRow> rows)
    {
        foreach (LeaderboardRow row in rows)
            if (row != null) Destroy(row.gameObject);
        rows.Clear();
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }
}

// Attach to the row prefab; assign the three TMP fields on it.
public class LeaderboardRow : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI valueText;

    public void Set(int rank, string playerName, string value)
    {
        rankText.text = rank.ToString();
        nameText.text = playerName;
        valueText.text = value;
    }
}
