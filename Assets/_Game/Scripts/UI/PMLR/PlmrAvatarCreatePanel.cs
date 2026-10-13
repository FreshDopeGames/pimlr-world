using TMPro;
using UnityEngine;

public class PlmrAvatarCreatePanel : MonoBehaviour
{
    public GameObject sceneStaticEU_Manager;
    public UIManager _UIManager;
    [SerializeField] TextMeshProUGUI coinText;
    // Start is called before the first frame update
    void Start()
    {
        if (CoinManager.Instance)
            coinText.text = CoinManager.Instance.GetCoins().ToString();
    }

    // PIMLR: switch to the customization camera every time the panel opens (was only the first time,
    // via Start) and back to the player camera whenever it closes.
    SceneStaticEU_Manager Manager
    {
        get
        {
            if (sceneStaticEU_Manager == null) sceneStaticEU_Manager = GameObject.Find("SceneManager");
            return sceneStaticEU_Manager ? sceneStaticEU_Manager.GetComponent<SceneStaticEU_Manager>() : null;
        }
    }


    private void OnEnable()
    {
        if (CoinManager.Instance)
            CoinManager.Instance.coinValueChanged += OnCoinValueChange;

        var manager = Manager;
        if (manager) manager.EnterCustomizationView();
    }
    private void OnDisable()
    {
        if (CoinManager.Instance)
            CoinManager.Instance.coinValueChanged -= OnCoinValueChange;

        if (!gameObject.scene.isLoaded) return; // scene unloading: don't touch cameras being destroyed
        var manager = Manager;
        if (manager) manager.ExitCustomizationView();
    }

    public void OnCoinValueChange(int value)
    {
        coinText.text = value.ToString();
    }
    public void OnAvaterDoneClick()
    {
        var manager = Manager;
        if (manager) manager.ExitCustomizationView();
        _UIManager.ShowMenu("PLMRMainMenuPanel");
    }
   
}
