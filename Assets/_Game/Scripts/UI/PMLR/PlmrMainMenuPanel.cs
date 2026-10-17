using JUTPS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlmrMainMenuPanel : MonoBehaviour
{
    public UIManager _UIManager;
    public UILodingScreen loadingScreen;
    public JUCharacterController characterController;
    public GameObject target;
    [SerializeField] TextMeshProUGUI coinText;
    // PIMLR: this panel's own background. The panel GameObject must stay active (Update listens for Tab),
    // so its background is shown/hidden alongside `target` instead. Auto-filled from this object if empty.
    [SerializeField] Graphic panelBackground;

    private void Awake()
    {
        if (panelBackground == null)
            panelBackground = GetComponent<Graphic>();
    }

    private void Start()
    {
        // PIMLR: null-safe - the splash scene (00_MainMenu) carries this panel too but has no player.
        var player = GameObject.FindWithTag("Player");
        if (player != null) characterController = player.GetComponent<JUCharacterController>();
        if (coinText != null && CoinManager.Instance) coinText.text = CoinManager.Instance.GetCoins().ToString();
    }

    // PIMLR (post-playtest): B / Circle closes this menu like Start / Options does (called by PimlrMenuNavigator
    // when the open menu has no Close / Back button of its own).
    static readonly System.Collections.Generic.List<PlmrMainMenuPanel> instances = new System.Collections.Generic.List<PlmrMainMenuPanel>();

    public static bool TryCloseFromController()
    {
        foreach (var p in instances)
        {
            // Only in the Living Room (a player to hand control back to) - never the 00_MainMenu splash buttons.
            if (p != null && p.isActiveAndEnabled && p.characterController != null && p.target != null && p.target.activeSelf)
            {
                p.target.SetActive(false);
                return true;
            }
        }
        return false;
    }

    private void OnEnable()
    {
        if (!instances.Contains(this)) instances.Add(this);
        if (CoinManager.Instance)
            CoinManager.Instance.coinValueChanged += OnCoinValueChange;
    }
    private void OnDisable()
    {
        instances.Remove(this);
        if (CoinManager.Instance)
            CoinManager.Instance.coinValueChanged -= OnCoinValueChange;
    }

    public void OnCoinValueChange(int value)
    {
        if (coinText != null) coinText.text = value.ToString();
    }

    public void LoadScene(string sceneName)
    {
        if (SceneManagerScript.Instance.uiManager && SceneManagerScript.Instance.uiManager.UI_fader != null)
            SceneManagerScript.Instance.uiManager.UI_fader.Fade(UIFader.FADE.FadeOut, 10f, 0f);
        SceneManagerScript.Instance.LoadScene(sceneName);
        this.gameObject.SetActive(false);
    }

    public void OnAvatrCreateButton()
    {
        _UIManager.ShowMenu("CharaterCreatePanel");
    }

    public void OnLeaderboardButton()
    {
        LeaderboardUI leaderboard = FindObjectOfType<LeaderboardUI>(true);
        if (leaderboard == null)
        {
            Debug.LogWarning("PlmrMainMenuPanel could not find a LeaderboardUI in the current scene.");
            return;
        }

        gameObject.SetActive(false);
        leaderboard.gameObject.SetActive(true);
    }

    public void OnHelixGamePlayClick()
    {
        // SceneManager.LoadScene("FirstScene");

        //Application.OpenURL(Constant.Helixlink);


        AuthManager.Instance.currentGameMode = GameMode.Helix;

        LoadScene(Constant.Helix_Scene_Name);



    }
    public void OnHumnanityRocksGamePlayClick()
    {
        // SceneManager.LoadScene("SpaceshipMainLoader");
        //Application.OpenURL(Constant.Humanitylink);

        AuthManager.Instance.currentGameMode = GameMode.HumanityRocks;

        LoadScene(Constant.Humanity_Scene_Name);
    }


    public void OnSelectLevel(int levelCount)
    {
        // PIMLR (playtest): persist the selected level through the typed zone progress owner.
        switch (levelCount)
        {
            case -1:
                ZoneProgress.Current = Zone.InfiniteMode;
                break;

            case 1:
                ZoneProgress.Current = Zone.ChatWilly;
                break;

            case 2:
                ZoneProgress.Current = Zone.ZoneBoss1;
                break;

            case 3:
                ZoneProgress.Current = Zone.Zone2;
                break;

            default:
                ZoneProgress.Current = Zone.ChatWilly;
                break;


        }

        PlayerPrefs.Save();
        LoadScene("YannicksWorld");
    }

    /* private void Update()
     {
         if (Input.GetKeyDown(KeyCode.Tab))
             target.SetActive(!target.activeSelf);
         if (target.activeSelf)
         {
             PlayerControllerStop();
         }
         else
         {
             PlayerControllerStart();
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
     public void PlayerControllerStart()
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
     }*/
    private void Update()
    {
        HandleToggleTarget();

        if (panelBackground)
            panelBackground.enabled = target.activeSelf;

        if (target.activeSelf)
        {
            PlayerControllerStop();
        }
        else
        {
            PlayerControllerStart();
        }

        // PIMLR (post-playtest): menu just closed -> brief button pause so the closing press doesn't jump.
        if (targetWasActive && !target.activeSelf) PimlrInput.BlockGameplayInputFor(PimlrInput.UnpauseButtonPause, blockMovement: false);
        targetWasActive = target.activeSelf;
    }

    bool targetWasActive;

    void HandleToggleTarget()
    {
        // PIMLR: Start / Options toggles the menu on controllers (Tab on keyboard). While the menu is open the
        // cursor is unlocked, which is what freezes the JUTPS camera, so controllers need a way to close it.
        if (Input.GetKeyDown(KeyCode.Tab) || PimlrInput.MenuButtonDown)
        {
            ToggleTarget();
        }
    }

    void ToggleTarget()
    {
        target.SetActive(!target.activeSelf);
    }

    void PlayerControllerStop()
    {
        SetPlayerControllerState(true);
        SetCursorState(true);
    }

    public void PlayerControllerStart()
    {
        SetPlayerControllerState(false);
        SetCursorState(false);
    }

    void SetPlayerControllerState(bool state)
    {
        characterController.BlockHorizontalInput = state;
        characterController.BlockVerticalInput = state;
        characterController.BlockFireModeOnCursorVisible = state;
        characterController.CanMove = !state;
        characterController.CanJump = !state;
        characterController.CanRotate = !state;
        characterController.EnableRoll = !state;
    }

    void SetCursorState(bool state)
    {
        Cursor.visible = state;

        if (state)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

}
