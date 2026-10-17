using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

/// <summary>
/// PIMLR (post-playtest): ending FMV after "Shut Down Police Cruiser".
///
/// The old version only left this screen through VideoPlayer.loopPointReached. If the browser never played the
/// video (itch.io: PLMRVideo.mp4 had its index at the END of the file, and the clip has audio, which browsers may
/// refuse to autoplay inside itch's iframe) that event never fired and the game sat on a black screen forever.
///
/// Now every outcome returns to the Living Room: normal end, skip, VideoPlayer error, prepare timeout, autoplay
/// refusal (retries muted first), stalled playback, or a hard time cap.
/// Lives on the "VideoScreen" UI panel; playback starts when UIManager.ShowMenu("VideoScreen") enables it.
/// </summary>
public class UIVideoScreen : MonoBehaviour
{
    public Video[] _videoClips; // legacy, kept so the existing inspector data still deserializes

    public VideoPlayer videoPlayer;

    [Header("PIMLR: ending FMV")]
    [SerializeField] string fileName = "PLMRVideo.mp4";
    [SerializeField] string returnScene = "SceneStaticEU";
    [Tooltip("Real seconds to wait for the browser to load the video before giving up.")]
    [SerializeField] float prepareTimeout = 15f;
    [Tooltip("Real seconds to wait for the first frame after Play() before retrying muted / giving up.")]
    [SerializeField] float startTimeout = 4f;
    [Tooltip("Real seconds of frozen playback before giving up.")]
    [SerializeField] float stallTimeout = 4f;
    [Tooltip("Players can skip after this many real seconds (any face button, Start, Space, Enter, Esc, click).")]
    [SerializeField] float skipAfter = 1.5f;

    bool finished;

    void Awake()
    {
        if (videoPlayer == null) videoPlayer = GetComponentInChildren<VideoPlayer>(true);
        if (videoPlayer != null) videoPlayer.playOnAwake = false; // this script owns playback now
    }

    void OnEnable()
    {
        finished = false;
        StopAllCoroutines();
        StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        // The HATER FREE! cinematic or a pause could leave time slowed; the FMV and the scene load must not depend on it.
        Time.timeScale = 1f;

        if (videoPlayer == null)
        {
            Debug.LogWarning("[UIVideoScreen] No VideoPlayer assigned - skipping the ending FMV.");
            Finish("no player");
            yield break;
        }

        videoPlayer.errorReceived -= OnVideoError;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.loopPointReached -= OnVideoFinished;
        videoPlayer.loopPointReached += OnVideoFinished;

        videoPlayer.Stop();
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = Application.streamingAssetsPath + "/" + fileName;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;
        Debug.Log("[UIVideoScreen] Preparing " + videoPlayer.url);

        // 1) Load
        videoPlayer.Prepare();
        float deadline = Time.realtimeSinceStartup + prepareTimeout;
        while (!videoPlayer.isPrepared && !finished)
        {
            if (Time.realtimeSinceStartup > deadline) { Finish("prepare timeout"); yield break; }
            yield return null;
        }
        if (finished) yield break;

        // 2) Start - if the browser blocks autoplay with sound, retry muted (muted autoplay is always allowed).
        float startedAt = Time.realtimeSinceStartup;
        yield return StartPlayback();
        if (!finished && !HasStarted())
        {
            Debug.LogWarning("[UIVideoScreen] Video didn't start - retrying muted (browser autoplay policy?).");
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.Stop();
            videoPlayer.Prepare();
            deadline = Time.realtimeSinceStartup + prepareTimeout;
            while (!videoPlayer.isPrepared && !finished && Time.realtimeSinceStartup < deadline) yield return null;
            if (!finished) yield return StartPlayback();
            if (!finished && !HasStarted()) { Finish("never started"); yield break; }
        }
        if (finished) yield break;

        // 3) Watch it play
        double length = videoPlayer.length > 0 ? videoPlayer.length : 60.0;
        float hardCap = Time.realtimeSinceStartup + (float)length + 5f;
        double lastTime = videoPlayer.time;
        float lastProgressAt = Time.realtimeSinceStartup;

        while (!finished)
        {
            float now = Time.realtimeSinceStartup;

            if (now - startedAt > skipAfter && SkipPressed()) { Finish("skipped"); yield break; }
            if (now > hardCap) { Finish("time cap"); yield break; }

            if (videoPlayer.time > lastTime + 0.01) { lastTime = videoPlayer.time; lastProgressAt = now; }
            else if (now - lastProgressAt > stallTimeout) { Finish("stalled"); yield break; }

            // Reached the end without loopPointReached firing (seen on some browsers).
            if (videoPlayer.isPrepared && !videoPlayer.isPlaying && videoPlayer.time >= length - 0.25) { Finish("ended"); yield break; }

            yield return null;
        }
    }

    // Returns after the first frame shows or startTimeout passes.
    IEnumerator StartPlayback()
    {
        videoPlayer.Play();
        float deadline = Time.realtimeSinceStartup + startTimeout;
        while (!finished && !HasStarted() && Time.realtimeSinceStartup < deadline) yield return null;
    }

    bool HasStarted() => videoPlayer.isPlaying && (videoPlayer.frame > 0 || videoPlayer.time > 0.05);

    static bool SkipPressed()
    {
        var pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
            return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            return true;
        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogWarning("[UIVideoScreen] VideoPlayer error: " + message);
        Finish("error");
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log("Video Finished Once!");
        Finish("ended");
    }

    void Finish(string reason)
    {
        if (finished) return;
        finished = true;
        Debug.Log("[UIVideoScreen] Ending FMV done (" + reason + ") - loading " + returnScene);

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.Stop();
        }

        Time.timeScale = 1f;
        if (SceneManagerScript.Instance != null) SceneManagerScript.Instance.LoadScene(returnScene);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(returnScene);
    }

    void OnDisable()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
        }
    }
}

[System.Serializable]
public class Video
{
    public VideoClip _videoClip;
}
