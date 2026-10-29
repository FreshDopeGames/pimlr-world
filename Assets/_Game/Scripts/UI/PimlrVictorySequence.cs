using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PIMLR: the victory twin of <see cref="PimlrDeathSequence"/> ("WASHED"). Same look - slow-motion ramp, letterbox
/// bars, dim, big bold word with outline + underlay - but in a custom word/colour, and instead of fading to black and
/// reloading it ramps time back up to 1x, opens the bars and hands control back through a callback.
///
/// Used by PoliceSirenController for "HATER FREE!" when the police cruiser is shut down.
/// Built entirely in code: no prefab or scene setup needed. Timings are in real seconds.
/// </summary>
public class PimlrVictorySequence : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    // Tuning (matches WASHED where it makes sense)
    public static float SlowDownSeconds = 1.4f;    // 1x -> SlowMotionScale
    public static float SlowMotionScale = 0.15f;
    public static float TextFadeInDelay = 0.3f;
    public static float TextFadeInSeconds = 0.8f;
    public static float HoldSeconds = 3f;
    public static float SpeedUpSeconds = 1.0f;     // SlowMotionScale -> 1x, word and bars clear
    public static float DimDuringSlowMo = 0.25f;
    public static float LetterboxHeight = 0.11f;   // share of the screen height, per bar

    static PimlrVictorySequence active;

    string word;
    Color color;
    Action onComplete;
    float hold = -1f; // PIMLR: per-call hold length; < 0 = HoldSeconds
    CanvasGroup textGroup;
    RectTransform textRect;
    Image dim, barTop, barBottom;
    float baseFixedDelta;
    bool finished;

    /// <summary>Plays the sequence, then invokes <paramref name="onComplete"/> with time back at 1x.</summary>
    public static void Play(string word, Color color, Action onComplete = null, float holdSeconds = -1f)
    {
        if (active) { onComplete?.Invoke(); return; }
        var go = new GameObject("PIMLR_VictorySequence");
        active = go.AddComponent<PimlrVictorySequence>();
        active.word = word;
        active.color = color;
        active.onComplete = onComplete;
        active.hold = holdSeconds;
        active.BuildUI();
    }

    void Awake()
    {
        IsPlaying = true;
        baseFixedDelta = Time.fixedDeltaTime;
        // JUTPS's slow-motion helper pushes timeScale back toward 1 every frame while enabled; keep it out of the way.
        if (JUTPS.FX.JUSlowmotion.Instance != null) JUTPS.FX.JUSlowmotion.Instance.EnableSlowmotion = false;
    }

    void Start() => StartCoroutine(Run());

    void OnDestroy()
    {
        if (active == this) active = null;
        IsPlaying = false;
        // Never leave the game stuck in slow motion (scene change, early destroy).
        if (!finished && !PimlrDeathSequence.IsPlaying) Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDelta;
    }

    IEnumerator Run()
    {
        // 1) Slow-motion ramp, dim, letterbox, and the word punches in.
        float t = 0f;
        float total = Mathf.Max(SlowDownSeconds, TextFadeInDelay + TextFadeInSeconds);
        while (t < total)
        {
            if (Bail()) yield break;
            if (!PausedNow()) t += Time.unscaledDeltaTime;
            float k = EaseOutCubic(Mathf.Clamp01(t / SlowDownSeconds));
            SetTimeScale(Mathf.Lerp(1f, SlowMotionScale, k));
            SetAlpha(dim, DimDuringSlowMo * k);
            SetLetterbox(k);

            float tk = Mathf.Clamp01((t - TextFadeInDelay) / TextFadeInSeconds);
            textGroup.alpha = tk;
            textRect.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, EaseOutBack(tk));
            yield return null;
        }
        textGroup.alpha = 1f;
        textRect.localScale = Vector3.one;

        // 2) Hold with a slow push-in and a light victory pulse.
        t = 0f;
        float holdFor = hold >= 0f ? hold : HoldSeconds;
        while (t < holdFor)
        {
            if (Bail()) yield break;
            if (!PausedNow()) t += Time.unscaledDeltaTime;
            SetTimeScale(SlowMotionScale);
            float push = Mathf.Lerp(1f, 1.06f, holdFor > 0f ? t / holdFor : 1f);
            float pulse = 1f + Mathf.Sin(t * 5f) * 0.015f;
            textRect.localScale = Vector3.one * push * pulse;
            yield return null;
        }

        // 3) Ramp the speed back up to 1x while the word, dim and bars clear.
        t = 0f;
        Vector3 startScale = textRect.localScale;
        while (t < SpeedUpSeconds)
        {
            if (Bail()) yield break;
            if (!PausedNow()) t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / SpeedUpSeconds);
            float e = k * k; // ease-in: speeds up toward the end
            SetTimeScale(Mathf.Lerp(SlowMotionScale, 1f, e));
            textGroup.alpha = 1f - k;
            textRect.localScale = startScale * Mathf.Lerp(1f, 1.15f, k);
            SetAlpha(dim, DimDuringSlowMo * (1f - k));
            SetLetterbox(1f - k);
            yield return null;
        }

        if (!PausedNow()) Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDelta;
        finished = true;

        Action done = onComplete;
        onComplete = null;
        Destroy(gameObject);
        done?.Invoke();
    }

    // If the player gets WASHED mid-sequence, step aside and let the death sequence own time.
    bool Bail()
    {
        if (!PimlrDeathSequence.IsPlaying) return false;
        finished = true;
        Destroy(gameObject);
        return true;
    }

    static bool PausedNow() => JUTPS.JUPauseGame.Paused;

    void SetTimeScale(float scale)
    {
        if (PausedNow()) return; // let the pause menu own time while it's open
        Time.timeScale = scale;
        Time.fixedDeltaTime = baseFixedDelta * Mathf.Max(scale, 0.01f); // smooth physics in slow motion
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    static void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }

    void SetLetterbox(float k)
    {
        float h = LetterboxHeight * EaseOutCubic(Mathf.Clamp01(k));
        barTop.rectTransform.anchorMin = new Vector2(0f, 1f - h);
        barBottom.rectTransform.anchorMax = new Vector2(1f, h);
    }

    // ---------- UI built in code (mirrors PimlrDeathSequence) ----------

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31990; // above HUD; just under WASHED so a death always wins
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        dim = MakeImage("Dim", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
        barTop = MakeImage("LetterboxTop", new Vector2(0f, 1f), Vector2.one, Color.black);
        barBottom = MakeImage("LetterboxBottom", Vector2.zero, new Vector2(1f, 0f), Color.black);
        SetLetterbox(0f);

        var textGO = new GameObject("VictoryWord", typeof(RectTransform), typeof(CanvasGroup));
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
        tmp.text = word;
        tmp.fontSize = 210f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 70f;
        tmp.fontSizeMax = 210f;
        tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tmp.characterSpacing = 10f;
        tmp.alignment = TextAlignmentOptions.Center;
#if UNITY_2023_2_OR_NEWER
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
#else
        tmp.enableWordWrapping = false;
#endif
        tmp.color = color;
        tmp.raycastTarget = false;
        try
        {
            // Own material instance: thick dark-green outline + soft shadow so the word reads over any scene.
            Material m = tmp.fontMaterial;
            tmp.outlineWidth = 0.18f;
            tmp.outlineColor = new Color32(4, 30, 8, 255);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.75f));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.5f);
        }
        catch { /* shader without outline/underlay support: plain bold text still works */ }
    }

    Image MakeImage(string name, Vector2 anchorMin, Vector2 anchorMax, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
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