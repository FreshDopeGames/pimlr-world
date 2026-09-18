using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Data.Common;

public class Dialogue : MonoBehaviour
{
    [SerializeField] private GameObject dialogueBox;

    public GameObject DialogueBox => dialogueBox;

    [SerializeField] TMP_Text text;
    [SerializeField] internal DialogueScriptable dialogueScript;


    internal ResponseHandler responseHandler;

    private int dialogueIndex = 0;

    [SerializeField]
    private float writingSpeed;

    [SerializeField] RectTransform contentSizeFitter;
    [SerializeField] GameObject skipBtn;

    private void Start()
    {
        responseHandler = GetComponent<ResponseHandler>();
        //   ShowDialogue(dialogueScript);

    }

    public void ShowDialogue()
    {
        dialogueBox.SetActive(true);
        StartCoroutine(StepThroughDialogue(dialogueScript));
    }

    public void ShowDialogue(DialogueScriptable dialogueScriptable)
    {
        dialogueBox.SetActive(true);
        StartCoroutine(StepThroughDialogue(dialogueScriptable));
    }

    public void ClearDialogue()
    {

        text.text = "";
    }

    [ContextMenu("skip")]
    public void Skip()
    {
        if (isTyping)
            skip = true;
        else
            advanceRequested = true;

        if (skipBtn)
            skipBtn.SetActive(false);
    }

    public void Next()
    {
        advanceRequested = true;
    }

    string newstring = "";
    bool skip = false;
    bool isTyping = false;
    bool advanceRequested = false;

    public IEnumerator StepThroughDialogue(DialogueScriptable dialogueScriptable)
    {
        skip = false;
        advanceRequested = false;
        if(skipBtn)
        {
            skipBtn.SetActive(true);
        }

        for (int i = dialogueIndex; i < dialogueScriptable.Dialogue.Length; i++)
        {

            string dialogue = dialogueScriptable.Dialogue[i];
            //Debug.Log("::::");
            newstring= dialogue;
            skip = false;
            isTyping = true;
            if (i == 1 && skipBtn)
                skipBtn.SetActive(true);
            yield return Type(dialogue, text);
            isTyping = false;

            if (i == dialogueScriptable.Dialogue.Length - 1 && skipBtn)
                skipBtn.SetActive(false);
            else if (i == 1 && skipBtn)
                skipBtn.SetActive(false);

            if (contentSizeFitter)
            {
                yield return new WaitForEndOfFrame();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentSizeFitter);
            }
            //Debug.Log("|||||||");

            if (i == dialogueScriptable.Dialogue.Length - 1 && dialogueScriptable.HasResponses)
                break;

            if (i == 0 && dialogueScriptable.Dialogue.Length > 1)
            {
                if (skipBtn)
                    skipBtn.SetActive(false);
                responseHandler.ShowNextButton();
            }

            yield return new WaitUntil(() => advanceRequested || Input.GetKeyDown(KeyCode.Mouse0));
            advanceRequested = false;
            responseHandler.HideNextButton();


        }

        responseHandler.HideNextButton();

        if (dialogueScriptable.HasResponses && dialogueScriptable.Responses[0].isEvent)
        {
            //Debug.Log("inn");
            try
            {
                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Mouse0));
            }
            finally
            {
                //Debug.Log("Going in");
                foreach (Response responses in dialogueScriptable.Responses)
                {
                    if (responses.ResponseEventVoid != null)
                        responses.ResponseEventVoid.Raise();
                    if (responses.ResponseEventDialogue != null)
                        responses.ResponseEventDialogue.Raise(responses.DialogueScriptableEventPayload);
                    if (responses.ResponseEventVector3 != null)
                        responses.ResponseEventVector3.Raise(responses.Vector3EventPayload);
                }

            }
        }
        else if (dialogueScriptable.HasResponses)
        {
            responseHandler.ShowResponses(dialogueScriptable.Responses, ActiveGame.WalkingAround);
        }
        else if (!dialogueScriptable.giveControlBackToManager)
        {
            //  CloseDialogueBox();
        }

        //Debug.Log("  " + dialogueScriptable.HasResponses);
    }


    public void CloseDialogueBox()
    {
        dialogueBox.SetActive(false);
        text.text = string.Empty;

        if (responseHandler != null)
        {
            responseHandler.ClearPreviousAnswer();
        }
    }

    public void ReplaceAndInitiateNewScriptable(DialogueScriptable newdialogue)
    {
        dialogueScript = newdialogue;
        ShowDialogue();
    }

    public Coroutine Type(string textToWrite, TMP_Text text)
    {
        return StartCoroutine(WriteText(textToWrite, text));
    }

    private IEnumerator WriteText(string textToWrite, TMP_Text text)
    {
        float time = 0;
        int charIndex = 0;
        while (charIndex < textToWrite.Length && !skip)
        {
            time += Time.deltaTime * writingSpeed;
            charIndex = Mathf.FloorToInt(time);
            charIndex = Mathf.Clamp(charIndex, 0, textToWrite.Length);

            text.text = textToWrite.Substring(0, charIndex);


            
            yield return null;
        }

        text.text = textToWrite;
    }

}

