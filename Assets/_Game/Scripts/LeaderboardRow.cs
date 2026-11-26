using PIMLR.UI;
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

    // #UI (Slice 3): optional name-first styling, used only when rankBadge is assigned (LeaderboardRow_Themed.prefab).
    // The original LeaderboardRow.prefab keeps its exact behavior. Data and ranking are never touched here.
    [Header("Themed row (#UI, optional)")]
    [SerializeField] private Image rankBadge;                  // gold / silver / bronze burst for the top 3
    [SerializeField] private GameObject youTag;                // "YOU" tag on the current player's row
    [SerializeField] private PimlrLeaderboardRowLayout rowLayout;

    // PIMLR (playtest): defaults preserve compatibility with existing three-argument row setup calls.
    public void Set(int rank, string playerName, string value, bool isYou = false, string detail = "")
    {
        bool themed = rankBadge != null;
        if (rankText != null)
            rankText.text = rank.ToString();
        if (nameText != null)
            nameText.text = isYou && !themed ? $"{playerName} (you)" : playerName; // #UI: themed rows show a YOU tag
        if (valueText != null)
            valueText.text = value;
        if (highlight != null)
            highlight.gameObject.SetActive(isYou);
        if (detailText != null)
            detailText.text = detail;

        if (themed) ApplyThemedRank(rank, isYou);
    }

    // #UI: podium colors + current-player styling; the row's layout component handles sizes and placement.
    void ApplyThemedRank(int rank, bool isYou)
    {
        bool podium = rank >= 1 && rank <= 3;
        var badgeToken = rank == 1 ? ColorToken.AccentGold : rank == 2 ? ColorToken.TextSecondary : ColorToken.AccentWarn;
        rankBadge.gameObject.SetActive(podium);
        if (podium) rankBadge.color = PimlrUITheme.C(badgeToken);
        if (rankText) rankText.color = PimlrUITheme.C(podium ? ColorToken.TextOnLime : isYou ? ColorToken.BrandLime : ColorToken.TextSecondary);
        if (nameText) nameText.color = PimlrUITheme.C(isYou ? ColorToken.BrandLime : ColorToken.TextPrimary);
        if (youTag) youTag.SetActive(isYou);
        if (rowLayout) { rowLayout.podium = podium; rowLayout.Apply(); }
    }
}
