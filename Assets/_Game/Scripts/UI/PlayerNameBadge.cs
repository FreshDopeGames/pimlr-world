using TMPro;
using UnityEngine;

/// <summary>
/// PIMLR (post-playtest): shows whose run this is ("PLAYING AS  CRAIG") so it's clear who the current run's
/// leaderboard entry will go to. Reads PlayerProfile.DisplayName - the same name LeaderboardManager submits.
///
/// Setup (no layout work needed):
///   Add this component to a UI panel (the Living Room's PLMRMainMenuPanel, the pause menu panel).
///   It builds its own TextMeshPro label in the chosen corner. To use a label you've laid out yourself instead,
///   drag that TMP text into "Label". Drag any existing TMP text into "Copy Style From" to match its font.
/// Refreshes whenever the panel opens and when the name changes.
/// </summary>
[DisallowMultipleComponent]
public class PlayerNameBadge : MonoBehaviour
{
    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight, TopCenter }

    [Tooltip("Leave empty to auto-create a label in the corner below.")]
    [SerializeField] TMP_Text label;
    [Tooltip("Optional: an existing TMP text whose font / material the auto-created label should use.")]
    [SerializeField] TMP_Text copyStyleFrom;

    [Header("Auto-created label")]
    [SerializeField] Corner corner = Corner.TopLeft;
    [SerializeField] Vector2 margin = new Vector2(32f, 24f);
    [SerializeField] Vector2 size = new Vector2(520f, 50f);
    [SerializeField] float fontSize = 30f;

    [Header("Text")]
    [SerializeField] string prefix = "PLAYING AS";
    [SerializeField] Color prefixColor = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField] Color nameColor = new Color(0.095f, 0.745f, 0f, 1f); // PIMLR SAVE-button green
    [SerializeField] string noNameText = "PLAYING AS  PLAYER";

    void Awake()
    {
        if (label == null) label = BuildLabel();
    }

    void OnEnable()
    {
        PlayerProfile.DisplayNameChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        PlayerProfile.DisplayNameChanged -= Refresh;
    }

    public void Refresh()
    {
        if (label == null) return;
        string playerName = PlayerProfile.DisplayName;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            label.text = noNameText; // LeaderboardManager submits unnamed runs as "Player"
            return;
        }
        label.text = $"<color=#{ColorUtility.ToHtmlStringRGBA(prefixColor)}>{prefix}</color>  " +
                     $"<color=#{ColorUtility.ToHtmlStringRGBA(nameColor)}><b>{playerName.ToUpperInvariant()}</b></color>";
    }

    TMP_Text BuildLabel()
    {
        var go = new GameObject("PlayerNameBadge_Label", typeof(RectTransform));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);

        Vector2 anchor;
        TextAlignmentOptions align;
        switch (corner)
        {
            case Corner.TopRight: anchor = new Vector2(1, 1); align = TextAlignmentOptions.TopRight; break;
            case Corner.BottomLeft: anchor = new Vector2(0, 0); align = TextAlignmentOptions.BottomLeft; break;
            case Corner.BottomRight: anchor = new Vector2(1, 0); align = TextAlignmentOptions.BottomRight; break;
            case Corner.TopCenter: anchor = new Vector2(0.5f, 1); align = TextAlignmentOptions.Top; break;
            default: anchor = new Vector2(0, 1); align = TextAlignmentOptions.TopLeft; break;
        }
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        float x = anchor.x == 0.5f ? 0f : (anchor.x == 0 ? margin.x : -margin.x);
        float y = anchor.y == 0 ? margin.y : -margin.y;
        rt.anchoredPosition = new Vector2(x, y);

        var text = go.AddComponent<TextMeshProUGUI>();
        if (copyStyleFrom != null)
        {
            text.font = copyStyleFrom.font;
            text.fontSharedMaterial = copyStyleFrom.fontSharedMaterial;
        }
        text.fontSize = fontSize;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = align;
        text.raycastTarget = false; // never steal clicks / controller focus from the panel's buttons
        text.richText = true;
        text.outlineWidth = 0.2f;   // readable over the 3D living room
        text.outlineColor = new Color32(0, 0, 0, 200);
        go.transform.SetAsLastSibling();
        return text;
    }
}
