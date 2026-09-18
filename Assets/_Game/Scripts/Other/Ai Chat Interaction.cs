using JUTPS;
using JUTPS.JUInputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AiChatInteraction : MonoBehaviour
{

    public GameObject ai_BOT_Canvas;
    [SerializeField] internal JUCharacterController characterController; //AUTO ASSIGN
    private void Start()
    {
        if (ai_BOT_Canvas != null)
            ai_BOT_Canvas.SetActive(false);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (ai_BOT_Canvas != null)
                ai_BOT_Canvas.SetActive(true);

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
            SceneManagerScript.Instance.musicSystem.musicSystem.volume = 0.1f;

        if (ai_BOT_Canvas != null)
            ai_BOT_Canvas.SetActive(false);

        if (Application.platform == RuntimePlatform.WebGLPlayer)
            Application.ExternalCall("stopSpeaking");
        PlayerControllerStart();


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
