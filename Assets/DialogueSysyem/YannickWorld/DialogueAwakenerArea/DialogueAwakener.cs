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
        // PIMLR (playtest): retrieve the popup by its registered name instead of menu-array position.
        UIManager uiManager = SceneManagerScript.Instance != null ? SceneManagerScript.Instance.uiManager : null;
        uILevelCompletePopUp = uiManager != null ? uiManager.GetMenu<UILevelCompletePopUp>("Zone Panel") : null;
        if (uILevelCompletePopUp == null)
            Debug.LogWarning("DialogueAwakener: UIManager could not find UILevelCompletePopUp on 'Zone Panel'.", this);

        // PIMLR: continuing players (back from YannicksWorld, or Keep Playing on the main menu) already know the
        // exposition - skip the intro dialogue and bring the main menu up 1 s after the scene fades in.
        if (SceneManager.GetActiveScene().name == PimlrSessionFlow.HubScene && PimlrSessionFlow.SkipHubIntro)
        {
            isInteracted = true; // the player spawns inside this trigger - don't start the dialogue
            StartCoroutine(ShowHubMenuAfterFadeIn());
            return;
        }

        if (SceneManager.GetActiveScene().name != "SceneStaticEU")
        {
            // PIMLR (playtest): compare the typed persisted zone instead of string values.
            if (ZoneProgress.Current == Zone.Zone1)
            {
                //Debug.Log("In PP");
                if (uILevelCompletePopUp != null)
                {
                    //Debug.Log("re-start-mode");
                    EndDialogBoxAndSpawnZonbies();
                }
            }
            else if (ZoneProgress.Current == Zone.Zone2)
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
            // PIMLR (playtest): retrieve the popup by its registered name instead of menu-array position.
            UIManager uiManager = SceneManagerScript.Instance != null ? SceneManagerScript.Instance.uiManager : null;
            uILevelCompletePopUp = uiManager != null ? uiManager.GetMenu<UILevelCompletePopUp>("Zone Panel") : null;
            if (uILevelCompletePopUp == null)
                Debug.LogWarning("DialogueAwakener: UIManager could not find UILevelCompletePopUp on 'Zone Panel'.", this);
        }

        if (uILevelCompletePopUp != null)
        {
          
            if (GameExecutionManager.Instance)
            {
                GameExecutionManager.Instance.currentZoneMode = Zone.Zone1;
                // PIMLR (playtest): persist zone progression through its typed owner.
                ZoneProgress.Current = GameExecutionManager.Instance.currentZoneMode;
            }
            StartCoroutine(WaitForscreenfadeOut());
        }

        PlayerControllerStart();

        characterController.GetComponent<ItemSwitchManager>().IsPlayer = true;  

        GameExecutionManager.Instance.zombieSpawner.gameObject.SetActive(true);
        CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() - 40);
        
    }

    // PIMLR: same end state as finishing the intro (EndDialogBox), minus the dialogue.
    private IEnumerator ShowHubMenuAfterFadeIn()
    {
        // Wait for the loading screen to close (the loader finishes activating this scene first), then 1 s.
        yield return null;
        yield return new WaitForSecondsRealtime(1.0f);

        UIManager ui = SceneManagerScript.Instance != null && SceneManagerScript.Instance.uiManager != null
            ? SceneManagerScript.Instance.uiManager
            : FindObjectOfType<UIManager>();
        // Same player state the intro leaves behind (frozen, cursor free) so the menu behaves identically.
        if (characterController == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) characterController = player.GetComponent<JUCharacterController>();
        }
        if (characterController != null) PlayerControllerStop();

        if (ui != null) ui.ShowMenu("PLMRMainMenuPanel");
        else Debug.LogWarning("DialogueAwakener: no UIManager to show PLMRMainMenuPanel.", this);
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
