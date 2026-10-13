using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PIMLR: the pause menu's "Home Button" (LIVING ROOM) and the inventory "Home" button had their OnClick target
/// lost (SceneManagerScript reference = None), so they did nothing. Installed on every scene load by
/// PimlrInputRuntime: any button with those names gets a working "go to the Living Room" action.
/// </summary>
public class PimlrHomeButtons : MonoBehaviour
{
    public const string HubScene = "SceneStaticEU";
    static readonly string[] Names = { "Home Button", "Home" };

    public static void InstallAll()
    {
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (System.Array.IndexOf(Names, b.name) < 0) continue;
            if (b.GetComponent<PimlrHomeButtons>()) continue;
            b.gameObject.AddComponent<PimlrHomeButtons>();
            b.onClick.AddListener(GoHome);
        }
    }

    static void GoHome()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == HubScene) return;
        if (SceneManagerScript.Instance == null)
        {
            Debug.LogWarning("PIMLR Home: no SceneManagerScript to load the Living Room.");
            return;
        }
        Time.timeScale = 1f;               // the pause menu freezes time; don't arrive frozen
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManagerScript.Instance.LoadScene(HubScene);
    }
}
