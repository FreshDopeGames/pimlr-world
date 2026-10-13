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

    private bool wired;

    private void Awake()
    {
        // PIMLR: a stray, unwired copy of this component (e.g. on SceneStaticEU's Living Room UI) must not
        // subscribe to NameRequested - its Show() would throw and break the real overlay's name flow.
        wired = root && input && confirmButton;
        if (!wired)
        {
            Debug.LogWarning($"[PlayerNameEntryController] '{name}' is missing root/input/confirmButton - disabled. " +
                             "Remove this component or wire its references.", this);
            enabled = false;
            return;
        }

        PlayerSession.NameRequested += Show;
        confirmButton.onClick.AddListener(Confirm);
        input.onSubmit.AddListener(_ => Confirm());
        if (newSessionButton) newSessionButton.onClick.AddListener(StartNewSession);
        if (randomizeButton) randomizeButton.onClick.AddListener(() => input.text = PlayerSession.SuggestName());
        if (cancelButton) cancelButton.onClick.AddListener(Cancel);
        input.characterLimit = PlayerSession.MaxLength;
        root.SetActive(false);
    }

    // PIMLR (playtest): abandon the current run and clear local identity/progress before requesting a fresh name.
    private void StartNewSession()
    {
        if (RunStats.Active)
            RunStats.End(RunEndReason.Abandoned);

        PlayerSession.Reset();
        SessionState.ResetProgress();
        PlayerSession.EnsureName(null);
    }

    [ContextMenu("Test EnsureName")]
    private void TestEnsureName() => PlayerSession.EnsureName(() => Debug.Log("Name ready: " + PlayerSession.Name));

    [ContextMenu("Reset Session")]
    private void TestReset() => PlayerSession.Reset();

    private void OnDestroy() { if (wired) PlayerSession.NameRequested -= Show; }

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