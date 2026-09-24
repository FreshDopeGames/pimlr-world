using JUTPS;
using TMPro;
using UnityEngine;

public class PlmrMainMenuPanel : MonoBehaviour
{
    public UIManager _UIManager;
    public UILodingScreen loadingScreen;
    public JUCharacterController characterController;
    public GameObject target;
    [SerializeField] TextMeshProUGUI coinText;
    private void Start()
    {
        var player = GameObject.FindWithTag("Player");
        characterController = player.GetComponent<JUCharacterController>();
        coinText.text = CoinManager.Instance.GetCoins().ToString();
    }

    private void OnEnable()
    {
        if (CoinManager.Instance)
            CoinManager.Instance.coinValueChanged += OnCoinValueChange;
    }
    private void OnDisable()
    {
        if (CoinManager.Instance)
            CoinManager.Instance.coinValueChanged -= OnCoinValueChange;
    }

    public void OnCoinValueChange(int value)
    {
        coinText.text = value.ToString();
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


        switch (levelCount)
        {
            case -1:
                PlayerPrefs.SetString("currentZoneMode", Zone.InfiniteMode.ToString());
                break;

            case 1:
                PlayerPrefs.SetString("currentZoneMode", Zone.ChatWilly.ToString());
                break;

            case 2:
                PlayerPrefs.SetString("currentZoneMode", Zone.ZoneBoss1.ToString());
                break;

            case 3:
                PlayerPrefs.SetString("currentZoneMode", Zone.Zone2.ToString());
                break;

            default:
                PlayerPrefs.SetString("currentZoneMode", Zone.ChatWilly.ToString());
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

        if (target.activeSelf)
        {
            PlayerControllerStop();
        }
        else
        {
            PlayerControllerStart();
        }
    }

    void HandleToggleTarget()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
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
