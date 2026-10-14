using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PIMLR: the "WASHED" death sequence. When the player dies, the game slides into slow motion, letterbox bars
/// close in, "WASHED" fades in large and bold, holds for 3 seconds, then everything fades to black and the level
/// restarts (Story keeps its run - same reload as before, just presented).
/// In Infinite mode the run summary (Wave / kills / coins with Again + Hub) is revealed instead of an automatic
/// restart, because that's where the run's score is shown and submitted; Again restarts the level.
///
/// Started by PlayerHandler.OnLoadScene, which the player's JUHealth.OnDeath event calls in YannicksWorld.
/// Built entirely in code: no prefab or scene setup needed. Timings are in real seconds.
/// </summary>
public class PimlrDeathSequence : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    // Tuning
    public static string Word = "WASHED";
    public static float SlowDownSeconds = 1.4f;   // 1x -> SlowMotionScale
    public static float SlowMotionScale = 0.15f;
    public static float TextFadeInDelay = 0.3f;
    public static float TextFadeInSeconds = 0.8f;
    public static float HoldSeconds = 3f;
    public static float FadeOutSeconds = 1.0f;
    public static float RevealSeconds = 0.6f;
    public static float DimDuringSlowMo = 0.35f;
    public static float LetterboxHeight = 0.11f; // share of the screen height, per bar

    static PimlrDeathSequence active;

    string sceneToReload;
    CanvasGroup textGroup;
    RectTransform textRect;
    Image dim, barTop, barBottom;
    float baseFixedDelta;
    float baseListenerVolume;

    /// <summary>Plays the sequence and then reloads <paramref name="sceneName"/> (or shows the Infinite run summary).</summary>
    public static void Play(string sceneName)
    {
        if (active) return; // already running; ignore duplicate death events
        var go = new GameObject("PIMLR_DeathSequence");
        DontDestroyOnLoad(go);
        active = go.AddComponent<PimlrDeathSequence>();
        active.sceneToReload = string.IsNullOrEmpty(sceneName) ? SceneManager.GetActiveScene().name : sceneName;
    }

    void Awake()
    {
        IsPlaying = true;
        baseFixedDelta = Time.fixedDeltaTime;
        baseListenerVolume = AudioListener.volume;
        // JUTPS's slow-motion helper pushes timeScale back toward 1 every frame while enabled; keep it out of the way.
        if (JUTPS.FX.JUSlowmotion.Instance != null) JUTPS.FX.JUSlowmotion.Instance.EnableSlowmotion = false;
        BuildUI();
    }

    void Start() => StartCoroutine(Run());

    void OnDestroy()
    {
        if (active == this) active = null;
        IsPlaying = false;
        Time.fixedDeltaTime = baseFixedDelta;
        AudioListener.volume = baseListenerVolume;
    }

    IEnumerator Run()
    {
        // 1) Slow-motion ramp, dim, letterbox, and the word starts to rise in.
        float t = 0f;
        float total = Mathf.Max(SlowDownSeconds, TextFadeInDelay + TextFadeInSeconds);
        while (t < total)
        {
            if (!PausedNow()) t += Time.unscaledDeltaTime;
            float k = EaseOutCubic(Mathf.Clamp01(t / SlowDownSeconds));
            SetTimeScale(Mathf.Lerp(1f, SlowMotionScale, k));
            SetAlpha(dim, DimDuringSlowMo * k);
            SetLetterbox(k);

            float tk = Mathf.Clamp01((t - TextFadeInDelay) / TextFadeInSeconds);
            textGroup.alpha = tk;
            textRect.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, EaseOutCubic(tk));
            yield return null;
        }
        textGroup.alpha = 1f;
        textRect.localScale = Vector3.one;

        // 2) Hold. A slow push-in keeps the frame alive.
        t = 0f;
        while (t < HoldSeconds)
        {
            if (!PausedNow()) t += Time.unscaledDeltaTime;
            SetTimeScale(SlowMotionScale);
            textRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.06f, t / HoldSeconds);
            yield return null;
        }

        // 3) Fade the word and the world to black, and the sound with it.
        t = 0f;
        while (t < FadeOutSeconds)
        {
            if (!PausedNow()) t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FadeOutSeconds);
            SetTimeScale(SlowMotionScale);
            textGroup.alpha = 1f - k;
            SetAlpha(dim, Mathf.Lerp(DimDuringSlowMo, 1f, k));
            AudioListener.volume = Mathf.Lerp(baseListenerVolume, 0f, k);
            yield return null;
        }
        SetAlpha(dim, 1f);
        Time.fixedDeltaTime = baseFixedDelta;

        bool showSummary = RunStats.Active && RunStats.Mode == RunMode.Infinite &&
                           InfiniteMode.Instance != null && FindObjectOfType<RunSummaryPanel>(true) != null;

        if (showSummary)
        {
            // Freeze the arena behind the summary; Again / Hub both reload with time back at 1x.
            Time.timeScale = 0f;
            IsPlaying = false;                   // lets InfiniteMode end the run, which opens the summary panel
            yield return null;
        }
        else
        {
            Time.timeScale = 1f;
            if (SceneManagerScript.Instance != null) SceneManagerScript.Instance.LoadScene(sceneToReload);
            else SceneManager.LoadScene(sceneToReload);
            yield return null;
            yield return null;                   // loading screen is up underneath us now
        }

        // 4) Lift the black off whatever is underneath (loading screen or run summary).
        SetLetterbox(0f);
        t = 0f;
        while (t < RevealSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / RevealSeconds);
            SetAlpha(dim, 1f - k);
            AudioListener.volume = Mathf.Lerp(0f, baseListenerVolume, k);
            yield return null;
        }
        Destroy(gameObject);
    }

    static bool PausedNow() => JUTPS.JUPauseGame.Paused;

    void SetTimeScale(float scale)
    {
        if (PausedNow()) return; // let the pause menu own time while it's open
        Time.timeScale = scale;
        Time.fixedDeltaTime = baseFixedDelta * Mathf.Max(scale, 0.01f); // smooth physics/ragdoll in slow motion
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    static void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }

    void SetLetterbox(float k)
    {
        float h = LetterboxHeight * EaseOutCubic(k);
        barTop.rectTransform.anchorMin = new Vector2(0f, 1f - h);
        barBottom.rectTransform.anchorMax = new Vector2(1f, h);
    }

    // ---------- UI built in code ----------

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // above HUD, pause and loading screens
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        dim = MakeImage("Dim", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
        barTop = MakeImage("LetterboxTop", new Vector2(0f, 1f), Vector2.one, Color.black);
        barBottom = MakeImage("LetterboxBottom", Vector2.zero, new Vector2(1f, 0f), Color.black);

        var textGO = new GameObject("Washed", typeof(RectTransform), typeof(CanvasGroup));
        textGO.transform.SetParent(transform, false);
        textRect = (RectTransform)textGO.transform;
        textRect.anchorMin = new Vector2(0f, 0.3f);
        textRect.anchorMax = new Vector2(1f, 0.7f);
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        textGroup = textGO.GetComponent<CanvasGroup>();
        textGroup.alpha = 0f;
        textGroup.blocksRaycasts = false;
        textGroup.interactable = false;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = PickFont();
        if (font != null) tmp.font = font;
        tmp.text = Word;
        tmp.fontSize = 230f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 80f;
        tmp.fontSizeMax = 230f;
        tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tmp.characterSpacing = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.color = new Color32(6, 250, 255, 255);
        tmp.raycastTarget = false;
        try
        {
            // Own material instance: thick dark outline + soft shadow so the word reads over any scene.
            Material m = tmp.fontMaterial;
            tmp.outlineWidth = 0.18f;
            tmp.outlineColor = new Color32(10, 10, 20, 255);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.75f));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.5f);
        }
        catch { /* shader without outline/underlay support: plain bold text still works */ }
    }

    Image MakeImage(string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false; // never blocks the run summary's buttons
        return img;
    }

    /// <summary>Prefer a heavy face already used in the game's UI; fall back to TMP's default font.</summary>
    static TMP_FontAsset PickFont()
    {
        string[] heavy = { "black", "heavy", "extrabold", "bold" };
        foreach (string key in heavy)
            foreach (TMP_Text t in FindObjectsOfType<TMP_Text>(true))
                if (t.font != null && t.font.name.ToLowerInvariant().Contains(key)) return t.font;
        return TMP_Settings.defaultFontAsset;
    }
}
