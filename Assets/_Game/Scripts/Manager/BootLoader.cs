using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PIMLR (boot flow): 00_MainMenu is a pass-through boot scene. Replaces AuthManager.Start's DummyLogin auto-skip.
public class BootLoader : MonoBehaviour
{
    [SerializeField] private string firstScene = Constant.PLMR_Scene_Name; // "SceneStaticEU"
    [SerializeField] private float extraDelay = 0.1f;

    private IEnumerator Start()
    {
        yield return null; // let every Awake/Start (AuthManager, CoinManager, SceneManagerScript) finish first
        if (extraDelay > 0f) yield return new WaitForSecondsRealtime(extraDelay);

        if (AuthManager.Instance != null)
            AuthManager.Instance.currentGameMode = GameMode.Pimlr; // loading-screen copy uses this

        SceneManagerScript.Instance.LoadScene(firstScene);
    }
}
