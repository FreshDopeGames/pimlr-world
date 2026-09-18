using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class SceneManagerScript : Singleton<SceneManagerScript>
{
    private bool loadSceneInProgress;
    public UIManager uiManager;
    public GameObject _canvas;
    public UILodingScreen loadingScreen;
    [SerializeField] internal TextMeshProUGUI _coins;
    public UILogin uILogin;
    public UISignUp uISignUp;
    public UI_GoalPanel goalPanel;
    public MusicSystem musicSystem;
    public MinimapBlipController minimapBlipController;

    public MusicPurchasePanel musicPurchasePanel;

    public MusicData[] musicData;

    public VideoPlayer loadingVideoPlayer, waveScreen;

    // PIMLR #9: Optional full-motion video (FMV) played between levels during a scene load.
    // OFF by default — nothing runs unless playInterLevelFMV is checked AND fmvVideoPlayer is wired in the Editor.
    [Header("PIMLR #9 - Inter-level FMV")]
    [Tooltip("PIMLR #9: When true, an FMV clip is played before the loaded scene activates.")]
    [SerializeField] private bool playInterLevelFMV = false;
    [Tooltip("PIMLR #9: VideoPlayer used to render the inter-level FMV. Leave a RawImage/RenderTexture target wired in the Editor.")]
    [SerializeField] private VideoPlayer fmvVideoPlayer;
    [Tooltip("PIMLR #9: Optional explicit clip. If null, the StreamingAssets file named below is used instead.")]
    [SerializeField] private VideoClip fmvClip;
    [Tooltip("PIMLR #9: StreamingAssets file name used when no fmvClip is assigned.")]
    [SerializeField] private string fmvStreamingAssetsFileName = "PLMRVideo.mp4";
    [Tooltip("PIMLR #9: Optional GameObject (e.g. the FMV RawImage/canvas) toggled on while the FMV plays.")]
    [SerializeField] private GameObject fmvScreenRoot;
    [Tooltip("PIMLR #9: Safety cap (seconds) so the load never hangs if the video never reports finished.")]
    [SerializeField] private float fmvMaxWaitSeconds = 60f;





    private void Update()
    {
        //_coins.text = CoinManager.instance.GetCoins().ToString();


    }

    // Load a new scene by string
    public void LoadScene(string sceneName = "")
    {

        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!loadSceneInProgress)
        {
            StartCoroutine(LoadSceneCoroutine(sceneName));
            StartCoroutine(WaitForScreenFadeOut());
        }
    }

    // Coroutine to handle scene loading with a loading screen
    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        //Debug.Log("Load a Scene =" + sceneName);
        //UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null) uiManager.ShowMenu("Loading Screen");

        loadSceneInProgress = true;

        // PIMLR #9: Optionally play an inter-level full-motion video before continuing the load.
        // Gated by playInterLevelFMV + a wired fmvVideoPlayer, so default behavior is unchanged.
        if (playInterLevelFMV && fmvVideoPlayer != null)
        {
            yield return StartCoroutine(PlayInterLevelFMVCoroutine());
        }


        AsyncOperation asyncOperation;
        int startIndex = 1;
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        //Scene levels = new Scene[sceneCount - startIndex];
        for (var i = startIndex; i < sceneCount; i++)
        {
            Scene currentScene = SceneManager.GetSceneByBuildIndex(i);
            //  Debug.Log("::::::>>>>>>>" + currentScene.name);
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            asyncOperation = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex + 1);
        }
        else
            asyncOperation = SceneManager.LoadSceneAsync(sceneName);

        // Disable scene activation to allow loading to complete
        asyncOperation.allowSceneActivation = false;

        while (!asyncOperation.isDone)
        {
            // Update the loading slider value based on the loading progress
            float progress = Mathf.Clamp01(asyncOperation.progress / 0.9f); // 0.9 is the max value for progress
                                                                            //Debug.Log("Load scene =" + progress);

            //UILodingScreen loadingScreen = FindObjectOfType<UILodingScreen>();
            if (loadingScreen != null && loadingScreen.loadingSlider != null)
                loadingScreen.loadingSlider.value = progress;

            // Update loading text
            if (loadingScreen != null && loadingScreen.loadingText != null)
                loadingScreen.loadingText.text = "Loading: " + (progress * 100f).ToString("F0") + "%";
            //Debug.Log(loadingScreen.loadingText.text);
            // Check if the loading is almost complete (progress >= 0.9)
            if (asyncOperation.progress >= 0.9f)
            {
                // Enable scene activation to complete the loading
                asyncOperation.allowSceneActivation = true;
                yield return new WaitForSeconds(1.5f);

                if (uiManager != null)
                {
                    yield return new WaitForSeconds(1f);
                    uiManager.CloseMenu("LoadingScreen");
                    //SocketUIManager.Instance.AllScreenActiveOff();

                }
                //SocketUIManager.Instance.AllScreenActiveOff();
                loadSceneInProgress = false;

            }

            yield return null;

        }

        // Destroy the SceneLoader if the loaded scene is CharacterSelection
        if (sceneName == "CharacterSelection")
        {
            Destroy(gameObject);
        }
    }


    // PIMLR #9: Plays the inter-level FMV on fmvVideoPlayer and waits until it finishes (loopPointReached / !isPlaying)
    // or until fmvMaxWaitSeconds elapses, so a stuck video can never block the scene load.
    private IEnumerator PlayInterLevelFMVCoroutine()
    {
        if (fmvVideoPlayer == null) yield break;

        if (fmvScreenRoot != null) fmvScreenRoot.SetActive(true);

        // Choose source: explicit clip wins, otherwise StreamingAssets URL.
        if (fmvClip != null)
        {
            fmvVideoPlayer.source = VideoSource.VideoClip;
            fmvVideoPlayer.clip = fmvClip;
        }
        else
        {
            fmvVideoPlayer.source = VideoSource.Url;
            fmvVideoPlayer.url = Application.streamingAssetsPath + "/" + fmvStreamingAssetsFileName;
        }

        fmvVideoPlayer.isLooping = false;

        bool finished = false;
        VideoPlayer.EventHandler onFinished = (vp) => { finished = true; };
        fmvVideoPlayer.loopPointReached += onFinished;

        // Prepare so we get an accurate frame count / duration before playing.
        fmvVideoPlayer.Prepare();
        float prepTimeout = Time.realtimeSinceStartup + 5f;
        while (!fmvVideoPlayer.isPrepared && Time.realtimeSinceStartup < prepTimeout)
            yield return null;

        fmvVideoPlayer.Play();

        // Wait for the clip to finish (loopPointReached) or the safety cap.
        float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, fmvMaxWaitSeconds);
        while (!finished && Time.realtimeSinceStartup < deadline)
        {
            // Once it has started, isPlaying going false also means it finished/was stopped.
            if (fmvVideoPlayer.isPrepared && !fmvVideoPlayer.isPlaying && fmvVideoPlayer.frame > 0)
                break;

            yield return null;
        }

        fmvVideoPlayer.loopPointReached -= onFinished;
        fmvVideoPlayer.Stop();

        if (fmvScreenRoot != null) fmvScreenRoot.SetActive(false);
    }


    public bool IsPointerOverUIObject()
    {
        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, raycastResults);
        return raycastResults.Count > 0;
    }
    // Coroutine to wait for screen fade out
    private IEnumerator WaitForScreenFadeOut()
    {
        // Fade out



        if (uiManager && uiManager.UI_fader != null)
            uiManager.UI_fader.Fade(UIFader.FADE.FadeOut, 0.4f, 0.4f);
        yield return new WaitForSeconds(2f);
        if (uiManager != null) uiManager.ShowMenu("JUTPS Interface");
    }


    private void Start()
    {
        loadingVideoPlayer.url = Application.streamingAssetsPath + "/Loadingvideo.mp4";
        waveScreen.url = Application.streamingAssetsPath + "/Loadingvideo.mp4";

        if (_coins != null && CoinManager.Instance)
        {
            _coins.text = CoinManager.Instance.GetCoins().ToString();
        }

        if (!SceneManager.GetActiveScene().name.Equals("03_LevelSelection"))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    private void OnEnable()
    {


        FetchMusicData();


        if (CoinManager.Instance)
            CoinManager.Instance.coinValueChanged += OnCoinValueChange;
    }

    public void FetchMusicData()
    {
        if (musicData == null)
        {
            Debug.LogWarning("SceneManagerScript: musicData is not assigned.");
            return;
        }

        for (int i = 0; i < musicData.Length; i++)
        {
            if (musicData[i] == null || musicData[i].audioClip == null)
                continue;

            musicData[i].isPurchased = PlayerPrefs.GetInt(musicData[i].audioClip.name) != 0;
            if (!musicData[i].isPurchased && musicData[i].isFree)
            {
                musicData[i].isPurchased = true;
            }
        }

        if (musicSystem != null)
            musicSystem.RefreshList();
    }
    private void OnDisable()
    {
        if (CoinManager.Instance)
            CoinManager.Instance.coinValueChanged -= OnCoinValueChange;
    }

    public void OnCoinValueChange(int value)
    {
        _coins.text = value.ToString();
    }

   
}

[System.Serializable]
public class MusicData
{
    public Sprite albumIcon;
    public string songDescription;
    public AudioClip audioClip;
    public bool isPurchased;
    public bool isFree;
    public int musicValue = 50;
    public string boostname = "AIMING ADVISE";
    public string boostInfo = "+50% AIM \n+50% FIRE RATE";
    public int totalTargetAmount;
    public AchievementType achievementType = AchievementType.EnemyWash;
    public AchievementReward achievementReward = AchievementReward.Nothing;
    public string lockedBtnMsg;
}


public enum AchievementType
{
    EnemyWash,
    BossWash,
    PoliceCar 
}

public enum AchievementReward
{
    Nothing,
    MovementSpeed,
    AmmoSize,
    VehicleSpeed,
    Defense,
    MaxHP,
    FreezeRay

}