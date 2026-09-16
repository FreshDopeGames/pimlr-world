using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JUTPS.ItemSystem;
using JUTPS;

public class ChatbotUIController : MonoBehaviour
{
    public TMP_InputField inputField;
    public Button sendButton;
    public TextMeshProUGUI chatText;
    public ScrollRect scrollRect; // Reference to the ScrollRect

    private FlowiseAPI flowiseApi;

    [SerializeField] AiChatInteraction aiChatInteraction;

    int AnswerCountNumber = 0;

    // PIMLR #3: baked scripted dialogue replaces the Flowise/AI-API chat.
    // OWNER: finalize this copy. After the last line is tapped/buttoned through, the water gun unlocks.
    [TextArea] public string[] bakedLines = new string[]
    {
        "I've detected your haters from the BM Hive gathering around this area. You may need to protect yourself with a hydraulic tool.",
        "Take this water gun - it's loaded with soapy water, the one thing these haters can't stand.",
        "Head downtown and wash them off the streets. I'll be tracking your progress - good luck."
    };
    int currentLine = 0;

    // PIMLR #3 (continued): Water Gun unlock. This is the piece that was previously only a
    // comment/intent -- EndDialogBoxAndSpawnZonbies decremented coins and spawned zombies but
    // never actually granted the item. See UnlockAndEquipWaterGun() below.
    [Header("PIMLR #3 - Water Gun Unlock")]
    [Tooltip("Water Gun HoldableItem, already parented under the player's item holder in the " +
             "editor but disabled/locked until this dialogue completes. If your JUTPS item setup " +
             "uses a different unlock pattern (e.g. adding to an inventory list rather than " +
             "enabling a pre-placed object), swap the body of UnlockAndEquipWaterGun() accordingly.")]
    [SerializeField] private GameObject waterGunItemObject;

    [Tooltip("Optional: if ItemSwitchManager selects items by index rather than by reference, " +
             "set the Water Gun's slot index here. Leave -1 if not applicable.")]
    [SerializeField] private int waterGunItemIndex = -1;

    void Start()
    {
        // PIMLR #3: scripted dialogue. Tapping/clicking or pressing the Send/Next button advances
        // lines; after the last line the water gun unlocks.
        chatText.text = "\n";
        if (sendButton != null)
        {
            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener(AdvanceDialogue);
        }
        currentLine = 0;
        ShowCurrentLine();
    }

    void ShowCurrentLine()
    {
        if (currentLine >= 0 && currentLine < bakedLines.Length)
            AppendToChat($"<color=yellow>Sirihanna: {bakedLines[currentLine]} </color>\n");
    }

    // Advance to the next scripted line; unlock the water gun after the last one.
    public void AdvanceDialogue()
    {
        currentLine++;
        if (currentLine < bakedLines.Length)
            ShowCurrentLine();
        else
            EndDialogBoxAndSpawnZonbies();
    }

    [System.Obsolete]
    private void Speak(string text)
    {
        SceneManagerScript.Instance.musicSystem.musicSystem.volume = 0.1f;
        Application.ExternalEval($"speak('{text.Replace("'", "\\'")}');");
    }

    [System.Obsolete]
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
        {
            aiChatInteraction.OnClosePanel();
            return;
        }

        // PIMLR #3: tap-through advance. Any primary click / Space / Enter (or a touch, which
        // registers as GetMouseButtonDown(0) on most platforms) advances the baked dialogue, so
        // players aren't forced to precisely hit the small Send/Next button. Only runs while
        // this dialogue is still in progress -- once the last line is shown, AdvanceDialogue()
        // routes into EndDialogBoxAndSpawnZonbies() and this panel closes on its own.
        if (currentLine < bakedLines.Length &&
            (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            AdvanceDialogue();
        }
    }

    // Legacy Flowise/API send path (no longer wired to the button; kept for reference).
    void SendMessage()
    {
        string userMessage = inputField.text;
        if (flowiseApi != null && !string.IsNullOrWhiteSpace(userMessage))
        {
            flowiseApi.SendQuestion(userMessage);
            AppendToChat($"<color=green>You: {userMessage}</color> \n");
            inputField.text = "";
        }
    }

    [System.Obsolete]
    // Legacy Flowise response handler (no longer subscribed).
    private void HandleResponse(string response)
    {
        AppendToChat($"<color=yellow>Sirihanna:{response} </color>\n");
        Speak(response);
        AnswerCountNumber++;
        if (AnswerCountNumber == 4)
        {
            EndDialogBoxAndSpawnZonbies();
        }
    }

    public void AppendToChat(string message)
    {
        chatText.text += $"{message}\n";
        StartCoroutine(ScrollToBottom());
    }

    private IEnumerator ScrollToBottom()
    {
        // Wait for end of frame so that the UI elements can update their positions
        yield return new WaitForEndOfFrame();
        scrollRect.normalizedPosition = new Vector2(0, 0);
    }

    [System.Obsolete]
    void OnDestroy()
    {
        if (flowiseApi != null)
        {
            flowiseApi.OnResponseReceived -= HandleResponse;
        }
    }

    // PIMLR #3: grants and equips the Water Gun. Called once, right before the zombie-spawn /
    // zone-transition flow in EndDialogBoxAndSpawnZonbies() below.
    //
    // CONFIRM BEFORE SHIPPING: this assumes the Water Gun is a HoldableItem GameObject already
    // parented under the player's item holder and simply disabled until now (the same pattern
    // JUTPS commonly uses for starter/unlockable items). If ItemSwitchManager in this project's
    // JUCharacterControllerCore instead expects items added to a runtime inventory list, swap
    // the SetActive(true) + switch call below for that API. (JUCharacterControllerCore.cs is on
    // the missing-files list -- once it's available this method should be double-checked against
    // its real inventory/equip API rather than left as a best guess.)
    private void UnlockAndEquipWaterGun()
    {
        if (waterGunItemObject != null)
        {
            waterGunItemObject.SetActive(true);
        }

        var switchManager = aiChatInteraction.characterController.GetComponent<ItemSwitchManager>();
        if (switchManager != null)
        {
            // TODO: replace with the real JUTPS call once confirmed, e.g. one of:
            //   switchManager.SwitchToItem(waterGunItemIndex);
            //   switchManager.SwitchToItem(waterGunItemObject.GetComponent<HoldableItem>());
            Debug.Log("PIMLR #3: Water Gun unlocked -- confirm ItemSwitchManager equip call.");
        }
    }

    UILevelCompletePopUp uILevelCompletePopUp;

    [System.Obsolete]
    //[ContextMenu("EndDialogBoxAndSpawnZonbies")]
    public void EndDialogBoxAndSpawnZonbies()
    {
        Debug.Log(":::>>>>EndDialogBoxAndSpawnZonbies");

        // PIMLR #3: grant the Water Gun as the very first thing that happens once dialogue ends,
        // so it's equipped and ready before the player is dropped back into gameplay.
        UnlockAndEquipWaterGun();

        SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.ChatWithSirihanna);

        //DialogueManager.instance.EndDialogue();
        if (uILevelCompletePopUp == null)
        {

            uILevelCompletePopUp = SceneManagerScript.Instance.uiManager.UIMenus[5].UI_Gameobject.GetComponent<UILevelCompletePopUp>();
        }

        if (uILevelCompletePopUp != null)
        {

            GameExecutionManager.Instance.currentZoneMode = Zone.Zone1;
            PlayerPrefs.SetString("currentZoneMode", GameExecutionManager.Instance.currentZoneMode.ToString());

            JUGameManager.InstancedPlayer.StartCoroutine(WaitForscreenfadeOut());
        }

        aiChatInteraction.OnClosePanel();

        aiChatInteraction.transform.parent.gameObject.SetActive(false);

        aiChatInteraction.characterController.GetComponent<ItemSwitchManager>().IsPlayer = true;

        GameExecutionManager.Instance.Zone1Start();


        //gameme

        if (CoinManager.Instance)
            CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() - 40);

    }
    private IEnumerator WaitForscreenfadeOut()
    {
        //fadeOut
        UIFader fader = GameObject.FindObjectOfType<UIFader>();
        if (fader != null) fader.Fade(UIFader.FADE.FadeOut, 0.4f, 0.4f);
        yield return new WaitForSeconds(1f);
        SceneManagerScript.Instance.uiManager.ShowMenu("Zone Panel");
        yield return new WaitForSeconds(3f);
        SceneManagerScript.Instance.uiManager.ShowMenu("JUTPS Interface");
    }

}
