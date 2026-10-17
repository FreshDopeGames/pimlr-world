using JUTPS;
using JUTPS.JUInputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AiChatInteraction : MonoBehaviour
{

    public GameObject ai_BOT_Canvas;
    [SerializeField] internal JUCharacterController characterController; //AUTO ASSIGN

    [Tooltip("PIMLR: world-space 'Talk to Sirihanna' prompt. Hidden while the chat is open. Auto-found by name if empty.")]
    public GameObject chatPrompt;
    private const string ChatPromptName = "Chat CTA Text";

    private void Start()
    {
        if (ai_BOT_Canvas != null)
            ai_BOT_Canvas.SetActive(false);

        if (chatPrompt == null)
            chatPrompt = FindChatPrompt();
    }

    // Looks through this interaction's whole hierarchy (the prompt sits under a sibling object).
    private GameObject FindChatPrompt()
    {
        foreach (Transform t in transform.root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == ChatPromptName)
                return t.gameObject;
        }
        return null;
    }

    private void SetChatPromptVisible(bool visible)
    {
        if (chatPrompt != null)
            chatPrompt.SetActive(visible);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (ai_BOT_Canvas != null)
                ai_BOT_Canvas.SetActive(true);

            SetChatPromptVisible(false); // PIMLR: no floating CTA while you're already talking to her

            characterController = other.GetComponentInParent<JUCharacterController>();

            PlayerControllerStop();
        }
    }
    void PlayerControllerStop()
    {
        if (characterController == null)
        {
            Debug.LogWarning("AiChatInteraction: no JUCharacterController found on the player collider.", this);
            return;
        }

        characterController.BlockHorizontalInput = true;

        characterController.BlockVerticalInput = true;
        characterController.BlockFireModeOnCursorVisible = true;
        characterController.CanMove = false;
        characterController.CanJump = false;
        characterController.CanRotate = false;
        characterController.EnableRoll = false;
        characterController.UseDefaultControllerInput = false;



        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    [System.Obsolete]
    public void OnClosePanel()
    {
        if (SceneManagerScript.Instance != null && SceneManagerScript.Instance.musicSystem != null && SceneManagerScript.Instance.musicSystem.musicSystem != null)
            // PIMLR (playtest): restore the catalog player to full volume. 0.1 left every song nearly silent after the Sirihanna chat. Keep the existing null checks.
            SceneManagerScript.Instance.musicSystem.musicSystem.volume = 1f;

        if (ai_BOT_Canvas != null)
            ai_BOT_Canvas.SetActive(false);

        SetChatPromptVisible(true); // PIMLR: dialog cancelled -> bring the CTA back

        PlayerControllerStart();
        PimlrInput.OnDialogueClosed(); // PIMLR (post-playtest): the A that pressed Next/Close must not also jump


    }
    public void PlayerControllerStart()
    {
        if (characterController == null)
            return;

        characterController.BlockHorizontalInput = false;

        characterController.BlockVerticalInput = false;

        characterController.BlockFireModeOnCursorVisible = false;
        characterController.CanMove = true;
        characterController.CanJump = true;
        characterController.CanRotate = true;
        characterController.EnableRoll = true;
        characterController.UseDefaultControllerInput = true;
        //characterController.enabled = true;
        //JUInput.Instance().EnableBlockStandardInputs();
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    

}
