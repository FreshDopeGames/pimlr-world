using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PIMLR: remembers how the player arrived in the Living Room (SceneStaticEU) so returning players skip the
/// intro exposition. Continuing = coming back from YannicksWorld (story finished, Infinite exited, quit to hub)
/// or pressing Keep Playing / Continue on 00_MainMenu. New Game always plays the intro.
/// </summary>
public static class PimlrSessionFlow
{
    public const string HubScene = "SceneStaticEU";
    public const string GameplayScene = "YannicksWorld";
    public const string MainMenuScene = "00_MainMenu";

    static bool continueRequested;   // Keep Playing / Continue pressed on the main menu
    static string previousScene;

    /// <summary>True for this hub visit when the intro dialogue should be skipped.</summary>
    public static bool SkipHubIntro { get; private set; }

    public static void RequestContinue() => continueRequested = true;
    public static void RequestNewGame() => continueRequested = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        continueRequested = false;
        previousScene = null;
        SkipHubIntro = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    static void OnActiveSceneChanged(Scene from, Scene to)
    {
        // 'from' is often invalid/unnamed on a single-mode load, so track the last real scene ourselves.
        string cameFrom = previousScene;
        previousScene = to.name;

        if (to.name != HubScene) return;

        SkipHubIntro = cameFrom == GameplayScene || (cameFrom == MainMenuScene && continueRequested);
        continueRequested = false;
        Debug.Log($"PIMLR Session: entered {HubScene} from '{cameFrom ?? "(start)"}' - intro dialogue {(SkipHubIntro ? "skipped" : "plays")}.");
    }
}
