using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// PIMLR: the Living Room menu's (PLMRMainMenuPanel) "Exit Button" returns to the 00_MainMenu splash screen.
/// Its Inspector OnClick still pointed at the retired 03_LevelSelection scene; this replaces that at runtime, so
/// no prefab edit is needed. Installed on every scene load by PimlrInputRuntime.
/// AuthManager resets the splash (New Game / Keep Playing) when 00_MainMenu loads again - see
/// AuthManager.OnSceneLoadedPimlr.
/// </summary>
public class PimlrMainMenuReturn : MonoBehaviour
{
    public const string MainMenuScene = "00_MainMenu";
    const string PanelName = "PLMRMainMenuPanel";
    const string ButtonName = "Exit Button";

    public static void InstallAll()
    {
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.name != ButtonName || b.GetComponent<PimlrMainMenuReturn>() || !UnderPanel(b.transform)) continue;
            b.gameObject.AddComponent<PimlrMainMenuReturn>();

            // Switch off the old Inspector call (LoadScene("03_LevelSelection")) and use ours instead.
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                b.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            b.onClick.AddListener(GoToMainMenu);
        }
    }

    static bool UnderPanel(Transform t)
    {
        for (; t != null; t = t.parent)
            if (t.name == PanelName) return true;
        return false;
    }

    public static void GoToMainMenu()
    {
        if (SceneManagerScript.Instance == null)
        {
            Debug.LogWarning("PIMLR Exit: no SceneManagerScript to load the main menu.");
            return;
        }
        // Any Story / Infinite run is closed by RunStats / SceneManagerScript when the scene changes.
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PimlrSessionFlow.RequestNewGame(); // nothing pending: the splash decides (New Game / Keep Playing)
        Debug.Log("PIMLR Exit: returning to " + MainMenuScene);
        SceneManagerScript.Instance.LoadScene(MainMenuScene);
    }
}
