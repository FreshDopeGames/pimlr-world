using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingFlowDiagnostics : MonoBehaviour
{
    [SerializeField] private string targetScene = "SceneStaticEU";

    private void Start() { Run(); }

    [ContextMenu("Run Loading Flow Diagnostics")]
    public void Run()
    {
        Debug.Log($"[LFD] '{targetScene}' loadable (in Build Settings): {Application.CanStreamedLevelBeLoaded(targetScene)}");

        var sms = FindObjectsByType<SceneManagerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[LFD] SceneManagerScript instances: {sms.Length}");
        foreach (var s in sms)
            Debug.Log($"[LFD]   '{s.name}' scene='{s.gameObject.scene.name}' (DontDestroyOnLoad means persistent) " +
                      $"isInstance={s == SceneManagerScript.Instance} parent='{(s.transform.parent ? s.transform.parent.name : "ROOT")}'", s);

        var sm = SceneManagerScript.Instance;
        if (sm == null) { Debug.LogError("[LFD] SceneManagerScript.Instance is NULL"); return; }

        Debug.Log($"[LFD] uiManager={(sm.uiManager != null)} loadingScreen={(sm.loadingScreen != null)} " +
                  $"loadingVideoPlayer={(sm.loadingVideoPlayer != null)} waveScreen={(sm.waveScreen != null)} " +
                  $"musicSystem={(sm.musicSystem != null)} uILogin={(sm.uILogin != null)}");

        if (sm.loadingScreen != null)
            Debug.Log($"[LFD] loadingSlider={(sm.loadingScreen.loadingSlider != null)} loadingText={(sm.loadingScreen.loadingText != null)} " +
                      $"loadingBarDatas={(sm.loadingScreen.loadingBarDatas != null ? sm.loadingScreen.loadingBarDatas.Length : -1)}");

        var uis = FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[LFD] UIManager instances: {uis.Length}");
        foreach (var u in uis)
        {
            Debug.Log($"[LFD]   '{u.name}' scene='{u.gameObject.scene.name}' UI_fader={(u.UI_fader != null)} menus={u.UIMenus.Length}", u);
            foreach (var m in u.UIMenus)
                Debug.Log($"[LFD]     \"{m.UI_Name}\" -> {(m.UI_Gameobject != null ? m.UI_Gameobject.name : "NULL GAMEOBJECT")}");
        }
    }
}