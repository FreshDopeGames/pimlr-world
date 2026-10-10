using TMPro;
using UnityEngine;
using UnityEngine.UI;

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