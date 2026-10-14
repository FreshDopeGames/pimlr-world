using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent full-screen black cover that sits above EVERYTHING (loading screen,
/// interstitials, gameplay UI). Survives scene loads so the cover can go up before
/// a reload and only come down once the "Level X Begins" interstitial is ready.
/// Works on Unity 2022.3 and Unity 6. Uses unscaled time (safe with timeScale = 0).
/// Self-creating: no prefab or scene setup needed.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader _instance;
    private CanvasGroup _group;
    private Coroutine _running;

    private static ScreenFader Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[ScreenFader]");
                _instance = go.AddComponent<ScreenFader>();
                _instance.Build();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue; // above every other canvas

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var imgGo = new GameObject("Black");
        imgGo.transform.SetParent(transform, false);
        var img = imgGo.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>Snap to solid black this frame. Call BEFORE hiding the loading screen / reloading.</summary>
    public static void CoverInstant()
    {
        var f = Instance;
        f.StopRunning();
        f._group.alpha = 1f;
        f._group.blocksRaycasts = true;
    }

    /// <summary>Snap fully clear.</summary>
    public static void ClearInstant()
    {
        var f = Instance;
        f.StopRunning();
        f._group.alpha = 0f;
        f._group.blocksRaycasts = false;
    }

    /// <summary>Use with: yield return ScreenFader.FadeFromBlack(0.6f);</summary>
    public static IEnumerator FadeFromBlack(float duration) { return Instance.FadeRoutine(0f, duration); }

    /// <summary>Use with: yield return ScreenFader.FadeToBlack(0.4f);</summary>
    public static IEnumerator FadeToBlack(float duration) { return Instance.FadeRoutine(1f, duration); }

    private IEnumerator FadeRoutine(float target, float duration)
    {
        StopRunning();
        _group.blocksRaycasts = true; // block clicks while fading
        float start = _group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _group.alpha = target;
        _group.blocksRaycasts = target > 0.001f;
    }

    private void StopRunning()
    {
        if (_running != null) { StopCoroutine(_running); _running = null; }
    }
}