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
    // OWNER: finalize this copy. After the last line is buttoned through, the water gun unlocks.
    [TextArea] public string[] bakedLines = new string[]
    {
        "I've detected your haters from the BM Hive gathering around this area. You may need to protect yourself with a hydraulic tool.",
        "Take this water gun - it's loaded with soapy water, the one thing these haters can't stand.",
        "Head downtown and wash them off the streets. I'll be tracking your progress - good luck."
    };
    int currentLine = 0;

    private void OnEnable()
    {
        ConfigureDialogueButton();
    }

    void Start()
    {
        // PIMLR #3: scripted dialogue. The Send/Next button advances lines; after the last line the water gun unlocks.
        if (chatText != null)
            chatText.text = "\n";
        ConfigureDialogueButton();
        currentLine = 0;
        ShowCurrentLine();
    }

    private void ConfigureDialogueButton()
    {
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(AdvanceDialogue);
            sendButton.onClick.AddListener(AdvanceDialogue);
        }
    }

    void ShowCurrentLine()
    {
        if (bakedLines != null && currentLine >= 0 && currentLine < bakedLines.Length)
            AppendToChat($"<color=yellow>Sirihanna: {bakedLines[currentLine]} </color>\n");
    }

    // Advance to the next scripted line; unlock the water gun after the last one.
    public void AdvanceDialogue()
    {
        if (bakedLines == null || bakedLines.Length == 0)
        {
            EndDialogBoxAndSpawnZonbies();
            return;
        }

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
        if (chatText == null)
            return;

        chatText.text += $"{message}\n";
        if (scrollRect != null)
            StartCoroutine(ScrollToBottom());
    }

    private IEnumerator ScrollToBottom()
    {
        // Wait for end of frame so that the UI elements can update their positions
        yield return new WaitForEndOfFrame();
        if (scrollRect != null)
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



    UILevelCompletePopUp uILevelCompletePopUp;

    [System.Obsolete]
    //[ContextMenu("EndDialogBoxAndSpawnZonbies")]
    public void EndDialogBoxAndSpawnZonbies()
    {
        Debug.Log(":::>>>>EndDialogBoxAndSpawnZonbies");

        // Close first so a missing optional progression dependency cannot trap the player in the chat.
        if (aiChatInteraction != null)
            aiChatInteraction.OnClosePanel();

        if (SceneManagerScript.Instance != null && SceneManagerScript.Instance.goalPanel != null)
            SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.ChatWithSirihanna);

        //DialogueManager.instance.EndDialogue();
        if (uILevelCompletePopUp == null)
        {

            uILevelCompletePopUp = SceneManagerScript.Instance.uiManager.UIMenus[5].UI_Gameobject.GetComponent<UILevelCompletePopUp>();
        }

        if (uILevelCompletePopUp == null && SceneManagerScript.Instance != null && SceneManagerScript.Instance.uiManager != null && SceneManagerScript.Instance.uiManager.UIMenus != null && SceneManagerScript.Instance.uiManager.UIMenus.Length > 5 && SceneManagerScript.Instance.uiManager.UIMenus[5] != null && SceneManagerScript.Instance.uiManager.UIMenus[5].UI_Gameobject != null)
        {

            if (GameExecutionManager.Instance != null)
            {
                GameExecutionManager.Instance.currentZoneMode = Zone.Zone1;
                PlayerPrefs.SetString("currentZoneMode", GameExecutionManager.Instance.currentZoneMode.ToString());
            }

            if (JUGameManager.InstancedPlayer != null)
                JUGameManager.InstancedPlayer.StartCoroutine(WaitForscreenfadeOut());
        }

        if (aiChatInteraction != null)
        {
            if (aiChatInteraction.transform.parent != null)
                aiChatInteraction.transform.parent.gameObject.SetActive(false);

            if (aiChatInteraction.characterController != null)
            {
                ItemSwitchManager itemSwitchManager = aiChatInteraction.characterController.GetComponent<ItemSwitchManager>();
                if (itemSwitchManager != null)
                    itemSwitchManager.IsPlayer = true;
            }
        }

        if (GameExecutionManager.Instance != null)
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
