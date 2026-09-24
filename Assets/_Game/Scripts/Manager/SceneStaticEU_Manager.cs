using JUTPS;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneStaticEU_Manager : MonoBehaviour
{


    [SerializeField] private GameObject mainPanel;
    [SerializeField] GameObject customizationCanvas;
    [SerializeField] GameObject customizationCamera;
    //[SerializeField] string sceneToLoad;
    [SerializeField]
    JUCharacterController characterController;
    [SerializeField]
    GameObject characterCameraController;

    [SerializeField]
    GameObject dialogueBox;
    [SerializeField]
    TextMeshProUGUI coins;

    public GameObject gO_ScriptChangeColor;



    private void Start()
    {
        characterController.BlockHorizontalInput = true;
        if (CoinManager.Instance && coins!=null)
        {

            //Debug.Log(CoinManager.Instance.GetCoins().ToString());
          //  CoinManager.instance?.SetCoins(CoinManager.instance.GetCoins() + 50);
            coins.text = CoinManager.Instance.GetCoins().ToString();
        }
    }

    private void OnEnable()
    {
        if (CoinManager.Instance)
            CoinManager.Instance.coinValueChanged += OnCoinValueChange;


        if(SceneManager.GetActiveScene().name==Constant.PLMR_Scene_Name)
        {
            if(SocketNetworkManager.Instance!=null)
            {
                Destroy(SocketNetworkManager.Instance.gameObject);
            }

            if (TS.Generics.AllScenes.instance!=null)
            {
                Destroy(TS.Generics.AllScenes.instance.gameObject);
            }
        }
    }
    private void OnDisable()
    {
        if (CoinManager.Instance)
            CoinManager.Instance.coinValueChanged -= OnCoinValueChange;
    }

    public void OnCoinValueChange(int value)
    {
        coins.text = value.ToString();
    }


    #region Coroutine
    public IEnumerator Panel_Execution()
    {
        yield return new WaitForSeconds(1f);

        Debug.Log("Panel_Execution");
        // current changes
        //customizationCanvas.SetActive(true);
        customizationCamera.SetActive(true);
        dialogueBox.SetActive(false);
        characterCameraController.SetActive(false);
    }

    #endregion

    public void OpenMainMenu()
    {
        customizationCanvas.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void SceneLoad(string sceneToLoad)
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        mainPanel.SetActive(false);
        customizationCanvas.SetActive(false);

        PlayerControllerStart();
        SceneManagerScript.Instance.LoadScene(sceneToLoad);
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

    void PlayerControllerStart()
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

}
