using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{

    public UIFader UI_fader;
    public UI_Screen[] UIMenus;
    public GameObject Preloader;
    public bool openLoginScreen;

    void Awake()
    {
        Debug.Log($"[UIManager:{GetInstanceID()}] Awake in scene '{gameObject.scene.name}'. Registered menu count={(UIMenus != null ? UIMenus.Length : 0)}, fader={(UI_fader != null ? UI_fader.name : "NULL")}.");
        DisableAllScreens();

        //don't destroy
        //DontDestroyOnLoad(gameObject);

        if (openLoginScreen)
            ShowMenu("Login_Panel", true);

    }

    // PIMLR (load-flow fix): fader null-guard, explicit not-found warning
    public void ShowMenu(string targetname, bool disableAllScreens)
    {
        if (disableAllScreens) DisableAllScreens();

        bool found = false;
        foreach (UI_Screen UI in UIMenus)
        {
            if (UI.UI_Name == targetname && UI.UI_Gameobject != null)
            {
                UI.UI_Gameobject.SetActive(true);
                found = true;
                break;
            }
        }
        if (!found)
            Debug.LogWarning($"UIManager: no menu named '{targetname}' (or its GameObject is null).", this);

        if (UI_fader != null)
        {
            UI_fader.gameObject.SetActive(true);
            UI_fader.Fade(UIFader.FADE.FadeIn, .5f, .3f);
        }
    }

    public void ShowMenu(string name)
    {
        ShowMenu(name, true);
    }

    //close a menu by name
    public void CloseMenu(string name)
    {
        bool found = false;
        foreach (UI_Screen UI in UIMenus)
        {
            if (UI.UI_Name == name && UI.UI_Gameobject != null)
            {
                UI.UI_Gameobject.SetActive(false);
                found = true;
            }
        }
        if (!found)
            Debug.LogWarning($"UIManager: CloseMenu('{name}') matched nothing.", this);
    }

    private void SetScreenAndParentsActive(GameObject screen)
    {
        Transform current = screen.transform;
        while (current != null)
        {
            current.gameObject.SetActive(true);
            current = current.parent;
        }
    }

    //disable all the menus
    public void DisableAllScreens()
    {
        if (UIMenus == null)
        {
            Debug.LogError($"[UIManager:{GetInstanceID()}] UIMenus is NULL; cannot disable screens.");
            return;
        }

        foreach (UI_Screen UI in UIMenus)
        {
            if (UI.UI_Gameobject != null)
                UI.UI_Gameobject.SetActive(false);
            else
                Debug.Log("Null ref found in UI with name: " + UI.UI_Name);
        }
    }

    //show or hide touch screen controls
    void SetTouchScreenControls(UI_Screen UI)
    {
        if (UI.UI_Name == "TouchScreenControls") return;
        //InputManager inputManager = GameObject.FindObjectOfType<InputManager>();
        //if(inputManager != null && inputManager.inputType == INPUTTYPE.TOUCHSCREEN){
        if (UI.showTouchControls)
        {
            ShowMenu("TouchScreenControls", false);
        }
        else
        {
            CloseMenu("TouchScreenControls");
        }
    }



}

[System.Serializable]
public class UI_Screen
{
    public string UI_Name;
    public GameObject UI_Gameobject;
    [HideInInspector]
    public bool showTouchControls;
}
