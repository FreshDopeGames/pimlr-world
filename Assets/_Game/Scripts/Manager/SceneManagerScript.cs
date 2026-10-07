using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class SceneManagerScript : Singleton<SceneManagerScript>
{
    // Runtime guard that prevents overlapping scene loads; do not assign in the Inspector.
    private bool loadSceneInProgress;

    // Required: the UIManager whose UIMenus list contains the exact "Loading Screen" entry.
    [Tooltip("Required for scene transitions. Assign the UIManager that registers a menu named exactly 'Loading Screen'.")]
    public UIManager uiManager;

    // Legacy reference currently unused by this script; leave empty unless another scene script reads it.
    [Tooltip("Legacy/unused by SceneManagerScript. Leave empty unless another scene component uses this canvas reference.")]
    public GameObject _canvas;

    // Required for visible scene-load progress; assign the UILodingScreen on the registered Loading Screen.
    [Tooltip("Required for progress display. Assign the UILodingScreen component under the registered Loading Screen; its Slider and Text references must also be assigned.")]
    public UILodingScreen loadingScreen;

    // Optional coin counter; assign the TMP label shown in this scene if coin changes should update it.
    [Tooltip("Optional. Assign this scene's coin-count TextMeshProUGUI if it should update when coins change.")]
    [SerializeField] internal TextMeshProUGUI _coins;

    // Legacy login panel reference; only needed while the old sign-in UI is used.
    [Tooltip("Legacy sign-in UI. Assign only if the login flow is still used.")]
    public UILogin uILogin;

    // Legacy sign-up panel reference; only needed while the old registration UI is used.
    [Tooltip("Legacy sign-up UI. Assign only if the registration flow is still used.")]
    public UISignUp uISignUp;

    // Usually assigned automatically by UI_GoalPanel during its initialization; otherwise assign the active goal panel.
    [Tooltip("Goal panel used by gameplay. UI_GoalPanel normally registers itself at runtime; assign manually only if that component is not present or does not initialize.")]
    public UI_GoalPanel goalPanel;

    // Usually assigned by MusicSystem.Start; required for music-store refresh and scripts that resolve music through this manager.
    [Tooltip("Music system used by this scene. MusicSystem.Start normally assigns itself; assign manually only if runtime registration is unavailable.")]
    public MusicSystem musicSystem;

    // Assign the gameplay minimap blip controller when features such as grenade aiming need its player reference.
    [Tooltip("Required by gameplay code that reads the minimap player/blips (for example GrenadeThrower). Leave empty only when those features are not used.")]
    public MinimapBlipController minimapBlipController;

    // Assign the music purchase panel if the in-game store is available in this scene.
    [Tooltip("Assign the music purchase panel used to show store messages, such as insufficient coins.")]
    public MusicPurchasePanel musicPurchasePanel;

    // Configure one entry per store track; each usable entry needs an AudioClip. Purchase state is loaded from PlayerPrefs at runtime.
    [Tooltip("Music store catalog. Each usable entry needs an AudioClip; purchase state is restored from PlayerPrefs.")]
    public MusicData[] musicData;

    // Optional loading-background VideoPlayer; its URL is set to StreamingAssets/Loadingvideo.mp4 at startup.
    [Tooltip("Optional loading-background VideoPlayer. If assigned, it is pointed at StreamingAssets/Loadingvideo.mp4.")]
    public VideoPlayer loadingVideoPlayer;

    // Optional wave-screen VideoPlayer; its URL is set to StreamingAssets/Loadingvideo.mp4 at startup.
    [Tooltip("Optional wave-screen VideoPlayer. If assigned, it is pointed at StreamingAssets/Loadingvideo.mp4.")]
    public VideoPlayer waveScreen;

    // PIMLR #9: Optional full-motion video (FMV) played between levels during a scene load.
    // OFF by default — nothing runs unless playInterLevelFMV is checked AND fmvVideoPlayer is wired in the Editor.
    [Header("PIMLR #9 - Inter-level FMV")]
    // Leave false unless the FMV player and its rendering target/source are configured below.
    [Tooltip("Enable only after assigning fmvVideoPlayer and configuring its rendering target/source. The current Global UI prefab has no FMV player assigned.")]
    [SerializeField] private bool playInterLevelFMV = false;

    // Required when FMV playback is enabled; configure the VideoPlayer's target texture/camera in the Inspector.
    [Tooltip("PIMLR #9: VideoPlayer used to render the inter-level FMV. Leave a RawImage/RenderTexture target wired in the Editor.")]
    [SerializeField] private VideoPlayer fmvVideoPlayer;

    // Optional explicit source; takes precedence over the StreamingAssets filename below.
    [Tooltip("PIMLR #9: Optional explicit clip. If null, the StreamingAssets file named below is used instead.")]
    [SerializeField] private VideoClip fmvClip;

    // Used only when fmvClip is empty; the named file must exist in StreamingAssets.
    [Tooltip("PIMLR #9: StreamingAssets file name used when no fmvClip is assigned.")]
    [SerializeField] private string fmvStreamingAssetsFileName = "PLMRVideo.mp4";

    // Optional screen/canvas to show while FMV playback is active.
    [Tooltip("PIMLR #9: Optional GameObject (e.g. the FMV RawImage/canvas) toggled on while the FMV plays.")]
    [SerializeField] private GameObject fmvScreenRoot;

    // Maximum FMV wait time before scene loading continues; values below one second are clamped to one.
    [Tooltip("PIMLR #9: Safety cap (seconds) so the load never hangs if the video never reports finished.")]
    [SerializeField] private float fmvMaxWaitSeconds = 60f;

    public override void Awake()
    {
        base.Awake();
        if (Instance != this)
            Debug.LogWarning($"[SceneManagerScript:{GetInstanceID()}] Duplicate manager instance detected; Singleton will destroy this GameObject.");
    }

    private void Update()
    {
        //_coins.text = CoinManager.instance.GetCoins().ToString();


    }

    [Header("PIMLR load flow")]
    [SerializeField] private string loadingMenuName = "Loading Screen";
    [Tooltip("Optional. Menu shown AFTER the new scene is active, only if this manager survives the load. Leave blank to let each scene's LevelInit.showMenuAtStart decide.")]
    [SerializeField] private string menuAfterLoad = "";
    [SerializeField] private float minLoadingScreenSeconds = 1.5f;

    // Load a new scene by string
    public void LoadScene(string sceneName = "")
    {
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (loadSceneInProgress)
        {
            Debug.LogWarning("SceneManagerScript: LoadScene ignored, a load is already in progress.", this);
            return;
        }
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    // Coroutine to handle scene loading with a loading screen
    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        loadSceneInProgress = true;
        try
        {
            // Resolve the target up front so a bad name fails loudly instead of hanging.
            int buildIndex = SceneManager.GetActiveScene().buildIndex + 1;
            if (string.IsNullOrEmpty(sceneName))
            {
                if (buildIndex >= SceneManager.sceneCountInBuildSettings)
                {
                    Debug.LogError("SceneManagerScript: no next scene in Build Settings.", this);
                    yield break;
                }
            }
            else if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"SceneManagerScript: scene '{sceneName}' is not in Build Settings (or the name is wrong).", this);
                yield break;
            }

            if (uiManager != null) uiManager.ShowMenu(loadingMenuName);
            SetLoadingProgress(0f);

            // PIMLR #9: optional inter-level FMV
            if (playInterLevelFMV && fmvVideoPlayer != null)
                yield return StartCoroutine(PlayInterLevelFMVCoroutine());

            AsyncOperation op = string.IsNullOrEmpty(sceneName)
                ? SceneManager.LoadSceneAsync(buildIndex)
                : SceneManager.LoadSceneAsync(sceneName);

            if (op == null)
            {
                Debug.LogError("SceneManagerScript: LoadSceneAsync returned null.", this);
                yield break;
            }

            op.allowSceneActivation = false;
            float started = Time.unscaledTime;

            while (op.progress < 0.9f)
            {
                SetLoadingProgress(Mathf.Clamp01(op.progress / 0.9f));
                yield return null;
            }
            SetLoadingProgress(1f);

            // Keep the loading screen up long enough to read.
            while (Time.unscaledTime - started < minLoadingScreenSeconds)
                yield return null;

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // Only reached if this manager survived the scene change (DontDestroyOnLoad).
            if (uiManager != null)
            {
                uiManager.CloseMenu(loadingMenuName);
                if (!string.IsNullOrEmpty(menuAfterLoad)) uiManager.ShowMenu(menuAfterLoad);
            }
        }
        finally
        {
            loadSceneInProgress = false;   // never leave the loader locked
        }
    }

    private void SetLoadingProgress(float p)
    {
        if (loadingScreen == null) return;
        if (loadingScreen.loadingSlider != null) loadingScreen.loadingSlider.value = p;
        if (loadingScreen.loadingText != null)
            loadingScreen.loadingText.text = "Loading: " + (p * 100f).ToString("F0") + "%";
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
    private void Start()
    {
        if (loadingVideoPlayer != null)
            loadingVideoPlayer.url = Application.streamingAssetsPath + "/Loadingvideo.mp4";
        else
            Debug.LogWarning($"[SceneManagerScript:{GetInstanceID()}] Loading VideoPlayer reference is NULL.");

        if (waveScreen != null)
            waveScreen.url = Application.streamingAssetsPath + "/Loadingvideo.mp4";
        else
            Debug.LogWarning($"[SceneManagerScript:{GetInstanceID()}] Wave Screen VideoPlayer reference is NULL.");

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
        if (_coins != null)
            _coins.text = value.ToString();
    }

   
}

[System.Serializable]
public class MusicData
{
    // Optional artwork displayed beside this track in the music store.
    public Sprite albumIcon;

    // Store description; TMP rich-text tags are supported by the UI text.
    public string songDescription;

    // Required for this catalog entry to be loaded into the music system.
    public AudioClip audioClip;

    // Runtime purchase state; SceneManagerScript restores this from PlayerPrefs using the clip name.
    public bool isPurchased;

    // Catalog setting that makes a track available without a purchase.
    public bool isFree;

    // Coin price charged by the music store when this track is purchased.
    public int musicValue = 50;

    // Short name shown for the track's reward/boost in the store.
    public string boostname = "AIMING ADVISE";

    // Description of the reward/boost associated with this track.
    public string boostInfo = "+50% AIM \n+50% FIRE RATE";

    // Achievement count required to unlock a non-free track.
    public int totalTargetAmount;

    // Achievement counter used to determine whether this track is unlocked.
    public AchievementType achievementType = AchievementType.EnemyWash;

    // Gameplay reward granted by the associated track/boost.
    public AchievementReward achievementReward = AchievementReward.Nothing;

    // Lock-state message shown until the achievement requirement is met.
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