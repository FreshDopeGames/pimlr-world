using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;

// PIMLR Name Entry: shown once (first launch, before the Living Room) so the
// leaderboard has a display name to attach to runs. Follows the same
// panel pattern as UILogin/UISignUp elsewhere in this project.
//
// EDITOR WIRING (residual step):
//   1. Build a simple panel: TMP_InputField + a Continue button + optional warning text.
//   2. Assign the fields below.
//   3. Show this panel at boot if PlayerProfile.HasSetDisplayName is false -- e.g.
//      check it in AuthManager.Start() before DummyLogin() routes into the Living
//      Room, or add it as another "showMenuAtStart" case alongside LevelInit's UI panels.
public class PlayerNameEntryUI : MonoBehaviour
{
    public event Action NameSubmitted;

    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI warningText;

    private void Start()
    {
        submitButton.onClick.AddListener(OnSubmit);

        // Pre-fill if a name already exists (e.g. reopened from a settings menu).
        if (PlayerProfile.HasSetDisplayName)
            nameInputField.text = PlayerProfile.DisplayName;

        if (warningText != null) warningText.gameObject.SetActive(false);
    }

    private void OnSubmit()
    {
        if (string.IsNullOrWhiteSpace(nameInputField.text))
        {
            ShowWarning("Enter a name to continue.");
            return;
        }

        string stored = PlayerProfile.SetDisplayName(nameInputField.text);
        nameInputField.text = stored;

        gameObject.SetActive(false);
        NameSubmitted?.Invoke();
    }

    private void ShowWarning(string msg)
    {
        if (warningText == null) return;
        warningText.text = msg;
        warningText.gameObject.SetActive(true);
    }
}
