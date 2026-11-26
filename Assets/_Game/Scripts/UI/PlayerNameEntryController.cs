using System.Collections.Generic;
using PIMLR.UI;
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

    // PIMLR (online leaderboards): optional extra fields. Handle = private contact for weekly prizes;
    // PIN = lets the same name be used on another device (shared playtest PCs, phone + laptop).
    [Header("Online (optional)")]
    [SerializeField] private TMP_InputField handleInput;
    [SerializeField] private TMP_InputField pinInput;

    // #UI: names may only use characters every name-display font can draw (otherwise they render blank).
    [Header("Name characters (#UI)")]
    [Tooltip("Fonts that display player names elsewhere (leaderboard rows, hub badge). The input field's font and the theme's PlayerName font are always checked.")]
    [SerializeField] private TMP_FontAsset[] nameDisplayFonts;

    private bool wired;
    private bool busy;
    private List<TMP_FontAsset> nameFonts;

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
        // PIMLR (online leaderboards): Enter moves name -> handle -> PIN -> confirm, so keyboard players never need the mouse.
        input.onSubmit.AddListener(_ => NextOrConfirm(handleInput ? handleInput : pinInput));
        if (handleInput)
        {
            handleInput.characterLimit = 60;
            handleInput.onSubmit.AddListener(_ => NextOrConfirm(pinInput));
        }
        if (pinInput)
        {
            pinInput.characterLimit = 4;
            pinInput.contentType = TMP_InputField.ContentType.Pin;
            pinInput.onSubmit.AddListener(_ => Confirm());
        }
        if (newSessionButton) newSessionButton.onClick.AddListener(StartNewSession);
        if (randomizeButton) randomizeButton.onClick.AddListener(() => input.text = PlayerSession.SuggestName());
        if (cancelButton) cancelButton.onClick.AddListener(Cancel);
        input.characterLimit = PlayerSession.MaxLength;
        // #UI: block characters the fonts can't draw as they're typed or pasted (TMP runs this per character).
        input.onValidateInput = ValidateNameChar;
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
        ShowMessage("");
        input.text = PlayerSession.SuggestName(); // one-click path
        if (handleInput) handleInput.text = "";
        if (pinInput) pinInput.text = "";
        SetBusy(false);
        root.SetActive(true);
        input.Select();
        input.caretPosition = input.text.Length;
    }

    // #UI: built lazily so the theme and fonts are loaded; covers typing and paste.
    private List<TMP_FontAsset> NameFonts()
    {
        if (nameFonts != null) return nameFonts;
        var extras = new List<TMP_FontAsset>();
        if (input && input.textComponent) extras.Add(input.textComponent.font);
        if (nameDisplayFonts != null) extras.AddRange(nameDisplayFonts);
        nameFonts = PimlrNameCharset.Fonts(extras.ToArray());
        return nameFonts;
    }

    private char ValidateNameChar(string text, int charIndex, char added)
    {
        if (added == '<' || added == '>') return '\0';
        string folded = PimlrNameCharset.Normalize(added.ToString());
        if (folded.Length == 1 && PimlrNameCharset.CanDraw(folded[0], NameFonts()))
        {
            if (errorText && errorText.text.StartsWith("Can't use")) ShowMessage("");
            return folded[0];
        }
        ShowMessage(char.IsSurrogate(added) ? "Can't use emoji in names yet." : $"Can't use \"{added}\" in names yet. Letters, numbers and common symbols work.");
        return '\0';
    }

    private void NextOrConfirm(TMP_InputField next)
    {
        if (next) { next.Select(); next.ActivateInputField(); }
        else Confirm();
    }

    private void Confirm()
    {
        if (busy) return;

        // Same cleaning/limits PlayerSession.TrySetName applies, checked before asking the server.
        string clean = PimlrNameCharset.Normalize(input.text ?? "").Replace("<", "").Replace(">", "").Trim();
        // #UI: last line of defense (text set from code, IME composition): every character must be drawable.
        string unsupported = PimlrNameCharset.Unsupported(clean, NameFonts());
        if (unsupported.Length > 0) { ShowMessage($"Can't use \"{unsupported}\" in names yet. Letters, numbers and common symbols work."); input.Select(); return; }
        if (clean.Length < PlayerProfile.MinNameLength) { ShowMessage($"Name needs at least {PlayerProfile.MinNameLength} characters."); return; }
        if (clean.Length > PlayerProfile.MaxNameLength) { ShowMessage($"Name cannot exceed {PlayerProfile.MaxNameLength} characters."); return; }

        string handle = handleInput ? handleInput.text.Trim() : "";
        string pin = pinInput ? pinInput.text.Trim() : "";
        if (pin.Length > 0 && (pin.Length != 4 || !int.TryParse(pin, out _))) { ShowMessage("PIN must be 4 digits (or leave it blank)."); return; }

        SetBusy(true);
        ShowMessage("Saving your name...");
        PimlrOnline.Register(clean, handle, pin, result =>
        {
            SetBusy(false);
            if (!result.ok) { ShowMessage(result.message); input.Select(); return; }

            if (PlayerSession.TrySetName(clean, out string error))
            {
                if (result.offline) Debug.Log("PlayerNameEntry: offline - name saved on this device; online leaderboard will sync when connected.");
                root.SetActive(false);
            }
            else ShowMessage(error);
        });
    }

    private void Cancel()
    {
        if (busy) return;
        PlayerSession.CancelPending();
        root.SetActive(false);
    }

    private void SetBusy(bool value)
    {
        busy = value;
        confirmButton.interactable = !value;
    }

    private void ShowMessage(string msg)
    {
        if (errorText) errorText.text = msg;
        else if (!string.IsNullOrEmpty(msg)) Debug.Log("PlayerNameEntry: " + msg);
    }
}
