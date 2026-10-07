using System.Collections;
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

    // PIMLR (playtest): resolve menus by name and activate inactive ancestors consistently.
    public void ShowMenu(string targetname, bool disableAllScreens)
    {
        if (disableAllScreens) DisableAllScreens();

        GameObject menuObject = GetMenu(targetname);
        if (menuObject != null)
        {
            SetScreenAndParentsActive(menuObject);
        }
        else
        {
            Debug.LogWarning($"UIManager: no menu named '{targetname}' (or its GameObject is null).", this);
        }

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

    // PIMLR (playtest): expose safe menu lookup for callers that need a menu or component.
    public GameObject GetMenu(string name)
    {
        if (UIMenus == null)
            return null;

        foreach (UI_Screen UI in UIMenus)
        {
            if (UI != null && UI.UI_Name == name && UI.UI_Gameobject != null)
                return UI.UI_Gameobject;
        }

        return null;
    }

    // PIMLR (playtest): return the requested component without requiring callers to index menus.
    public T GetMenu<T>(string name) where T : Component
    {
        GameObject menuObject = GetMenu(name);
        return menuObject != null ? menuObject.GetComponent<T>() : null;
    }

    // PIMLR (playtest): fade out before showing a menu while tolerating a missing fader.
    public void FadeThenShow(string menuName, float delay = 2f, float fadeDuration = 0.4f)
    {
        StartCoroutine(FadeThenShowCoroutine(menuName, delay, fadeDuration));
    }

    private IEnumerator FadeThenShowCoroutine(string menuName, float delay, float fadeDuration)
    {
        if (UI_fader != null)
        {
            UI_fader.gameObject.SetActive(true);
            UI_fader.Fade(UIFader.FADE.FadeOut, fadeDuration, 0f);
        }

        yield return new WaitForSeconds(delay);
        ShowMenu(menuName);
    }

    // PIMLR (playtest): close the same named menu resolved by GetMenu.
    public void CloseMenu(string name)
    {
        GameObject menuObject = GetMenu(name);
        if (menuObject != null)
        {
            menuObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"UIManager: CloseMenu('{name}') matched nothing.", this);
        }
    }

    // PIMLR (playtest): activate inactive parents so nested menus become visible.
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
