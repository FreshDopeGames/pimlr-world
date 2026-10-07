using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerNameEntryController : MonoBehaviour
{
    [SerializeField] private GameObject root;            // the PlayerNameEntryOverlay panel
    [SerializeField] private TMP_InputField input;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button randomizeButton;     // optional
    [SerializeField] private Button cancelButton;        // optional
    [SerializeField] private TextMeshProUGUI errorText;  // optional
    [SerializeField] private Button newSessionButton;   // "Not you? New session" in the Leaderboards panel

    private void Awake()
    {
        PlayerSession.NameRequested += Show;
        confirmButton.onClick.AddListener(Confirm);
        input.onSubmit.AddListener(_ => Confirm());
        if (newSessionButton) newSessionButton.onClick.AddListener(PlayerSession.Reset);
        if (randomizeButton) randomizeButton.onClick.AddListener(() => input.text = PlayerSession.SuggestName());
        if (cancelButton) cancelButton.onClick.AddListener(Cancel);
        input.characterLimit = PlayerSession.MaxLength;
        root.SetActive(false);
    }

    [ContextMenu("Test EnsureName")]
    private void TestEnsureName() => PlayerSession.EnsureName(() => Debug.Log("Name ready: " + PlayerSession.Name));

    [ContextMenu("Reset Session")]
    private void TestReset() => PlayerSession.Reset();

    private void OnDestroy() { PlayerSession.NameRequested -= Show; }

    private void Show()
    {
        Cursor.lockState = CursorLockMode.None; // run-end screens may still have the cursor locked
        Cursor.visible = true;
        if (errorText) errorText.text = "";
        input.text = PlayerSession.SuggestName(); // one-click path
        root.SetActive(true);
        input.Select();
        input.caretPosition = input.text.Length;
    }

    private void Confirm()
    {
        if (PlayerSession.TrySetName(input.text, out string error)) root.SetActive(false);
        else if (errorText) errorText.text = error;
    }

    private void Cancel() { PlayerSession.CancelPending(); root.SetActive(false); }
}