using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunSummaryPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI killsText;
    [SerializeField] private TextMeshProUGUI bossText;
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private Button hubButton;
    [SerializeField] private Button againButton;

    private void Awake()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void OnEnable()
    {
        RunStats.Ended += OnRunEnded;

        if (hubButton != null)
            hubButton.onClick.AddListener(OnHubButtonClicked);
        if (againButton != null)
            againButton.onClick.AddListener(OnAgainButtonClicked);
    }

    private void OnDisable()
    {
        RunStats.Ended -= OnRunEnded;

        if (hubButton != null)
            hubButton.onClick.RemoveListener(OnHubButtonClicked);
        if (againButton != null)
            againButton.onClick.RemoveListener(OnAgainButtonClicked);
    }

    private void OnRunEnded(RunSnapshot snapshot)
    {
        if (snapshot.mode != RunMode.Infinite || snapshot.endReason == RunEndReason.Abandoned)
            return;

        if (titleText != null)
            titleText.text = $"Wave {snapshot.wave}";
        if (killsText != null)
            killsText.text = snapshot.kills.ToString();
        if (bossText != null)
            bossText.text = snapshot.bossKills.ToString();
        if (coinsText != null)
            coinsText.text = snapshot.coinsEarned.ToString();
        if (timeText != null)
            timeText.text = RunStatsHUD.FormatTime(snapshot.elapsed);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (root != null)
            root.SetActive(true);
    }

    private void OnHubButtonClicked()
    {
        SceneManagerScript sceneManager = SceneManagerScript.Instance;
        if (sceneManager == null)
        {
            Debug.LogWarning("RunSummaryPanel cannot load the hub because SceneManagerScript.Instance is missing.", this);
            return;
        }

        sceneManager.LoadScene("SceneStaticEU");
    }

    private void OnAgainButtonClicked()
    {
        ZoneProgress.Current = Zone.InfiniteMode;

        SceneManagerScript sceneManager = SceneManagerScript.Instance;
        if (sceneManager == null)
        {
            Debug.LogWarning("RunSummaryPanel cannot restart because SceneManagerScript.Instance is missing.", this);
            return;
        }

        sceneManager.LoadScene("YannicksWorld");
    }
}
