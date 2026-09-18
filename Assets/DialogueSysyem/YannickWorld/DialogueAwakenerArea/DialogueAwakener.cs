using System.Collections;
using UnityEngine;
using JUTPS;
using JUTPS.InventorySystem;
using JUTPS.ItemSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class DialogueAwakener : MonoBehaviour
{
    public DialogueScriptable dialogueScript;

    public int RequiredCorrectAnswers;

    public int PlayerPoints;

    [SerializeField] JUCharacterController characterController; //AUTO ASSIGN

    public bool isInteracted = false;

    [SerializeField]
    UILevelCompletePopUp uILevelCompletePopUp;

    [SerializeField]
    SceneStaticEU_Manager sceneStaticEUManager;

    UIManager uIManager;

    private void Start()
    {
        var _uiMAnager = SceneManagerScript.Instance.uiManager;
        uILevelCompletePopUp = _uiMAnager.UIMenus[5].UI_Gameobject.GetComponent<UILevelCompletePopUp>();
        //Debug.Log(PlayerPrefs.GetString("currentZoneMode"));

        if (SceneManager.GetActiveScene().name != "SceneStaticEU")
        {
            if (PlayerPrefs.GetString("currentZoneMode") == Zone.Zone1.ToString())
            {
                //Debug.Log("In PP");
                if (uILevelCompletePopUp != null)
                {
                    //Debug.Log("re-start-mode");
                    EndDialogBoxAndSpawnZonbies();
                }
            }
            else if (PlayerPrefs.GetString("currentZoneMode") == Zone.Zone2.ToString())
            {
                GameExecutionManager.Instance.BossDead();
            }
        }
    }

    private void Update()
    {

    }

    private void OnTriggerEnter (Collider other) 
    {
        

        if (other.CompareTag("Player") && !isInteracted)
        {
            if (DialogueManager.instance == null)
            {
                Debug.LogWarning("DialogueAwakener: no DialogueManager exists in the scene.", this);
                return;
            }

            if (DialogueManager.instance.ScreenDialogue == null)
            {
                Debug.LogWarning("DialogueAwakener: DialogueManager.ScreenDialogue is not assigned.", this);
                return;
            }

            if (dialogueScript == null)
            {
                Debug.LogWarning("DialogueAwakener: dialogueScript is not assigned.", this);
                return;
            }

            isInteracted = true;

            PlayerPoints = 0;

            characterController = other.gameObject.GetComponent<JUCharacterController>();

            PlayerControllerStop();

            DialogueManager.instance.StartDialogue(dialogueScript);

             uIManager = GameObject.FindObjectOfType<UIManager>();
        }
    }

    void PlayerControllerStop() 
    {
        characterController.BlockHorizontalInput = true;

        characterController.BlockVerticalInput = true;
        characterController.BlockFireModeOnCursorVisible = true;
        characterController.CanMove = false;
        characterController.CanJump = false;
        characterController.CanRotate = false;
        characterController.EnableRoll = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void PlayerControllerStart () 
    {
        characterController.BlockHorizontalInput = false;

        characterController.BlockVerticalInput = false;

        characterController.BlockFireModeOnCursorVisible = false;
        characterController.CanMove = true;
        characterController.CanJump = true;
        characterController.CanRotate = true;
        characterController.EnableRoll = true;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void IncreasePoints() 
    {
        PlayerPoints++;
    }

    public void IncreasePointsAndEnd () 
    {
        PlayerPoints++;

        DialogueManager.instance.EndDialogue();

        PlayerControllerStart();

        if(PlayerPoints == RequiredCorrectAnswers)
        {
            //ENABLE THE WATER GUN HERE!!!!!

            Debug.Log("All Answers Answered Correctly");
        }
    }

    public void WrongAnswerAndEnd () 
    {
        DialogueManager.instance.EndDialogue();

        PlayerControllerStart();

    }


    public void EndDialogBox()
    {
        DialogueManager.instance.EndDialogue();
        uIManager.ShowMenu("PLMRMainMenuPanel");
    }
    [ContextMenu("EndDialogBoxAndSpawnZonbies")]
    public void EndDialogBoxAndSpawnZonbies()
    {
        
        DialogueManager.instance.EndDialogue();
        if (uILevelCompletePopUp == null)
        {
          
            uILevelCompletePopUp = SceneManagerScript.Instance.uiManager.UIMenus[5].UI_Gameobject.GetComponent<UILevelCompletePopUp>();
        }

        if (uILevelCompletePopUp != null)
        {
          
            if (GameExecutionManager.Instance)
            {
                GameExecutionManager.Instance.currentZoneMode = Zone.Zone1;
                PlayerPrefs.SetString("currentZoneMode", GameExecutionManager.Instance.currentZoneMode.ToString());
            }
            StartCoroutine(WaitForscreenfadeOut());
        }

        PlayerControllerStart();

        characterController.GetComponent<ItemSwitchManager>().IsPlayer = true;  

        GameExecutionManager.Instance.zombieSpawner.gameObject.SetActive(true);
        CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() - 40);
        
    }

    private IEnumerator WaitForscreenfadeOut()
    {
        //fadeOut
        UIFader fader = GameObject.FindObjectOfType<UIFader>();
        if (fader != null) fader.Fade(UIFader.FADE.FadeOut, 0.4f, 0.4f);
        yield return new WaitForSeconds(1f);
        GameObject.FindObjectOfType<UIManager>().ShowMenu("Zone Panel");
        yield return new WaitForSeconds(3f);
        GameObject.FindObjectOfType<UIManager>().ShowMenu("JUTPS Interface");
    }
}

