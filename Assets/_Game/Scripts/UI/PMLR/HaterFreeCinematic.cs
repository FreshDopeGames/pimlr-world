using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Letterbox bars + green "HATER FREE!" text + time-scale ramp.
/// Call HaterFreeCinematic.Play(onComplete).
public class HaterFreeCinematic : MonoBehaviour
{
    const float BarHeightFraction = 0.13f;
    static readonly Color HaterGreen = new Color(0.15f, 1f, 0.25f, 1f);

    float slowScale = 0.25f;    // set >1 for a speed-up instead
    float rampTime = 0.6f;
    float holdTime = 2.5f;
    float originalFixedDelta;
    bool restored;

    RectTransform topBar, bottomBar;
    TextMeshProUGUI label;

    public static void Play(Action onComplete = null)
    {
        var go = new GameObject("HaterFreeCinematic");
        DontDestroyOnLoad(go);
        go.AddComponent<HaterFreeCinematic>().StartCoroutine(go.GetComponent<HaterFreeCinematic>().Run(onComplete));
    }

    IEnumerator Run(Action onComplete)
    {
        originalFixedDelta = Time.fixedDeltaTime;
        BuildUI();

        float startScale = Time.timeScale;

        // Ramp in: bars slide, time slows, text pops
        yield return Animate(rampTime, t =>
        {
            float e = Mathf.SmoothStep(0, 1, t);
            SetBars(e * BarHeightFraction * Screen.height);
            SetTimeScale(Mathf.Lerp(startScale, slowScale, e));
            label.alpha = e;
            label.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, e);
        });

        // Hold with a gentle pulse
        float held = 0f;
        while (held < holdTime)
        {
            held += Time.unscaledDeltaTime;
            float pulse = 1f + Mathf.Sin(held * 6f) * 0.03f;
            label.rectTransform.localScale = Vector3.one * pulse;
            yield return null;
        }

        // Ramp out
        yield return Animate(rampTime, t =>
        {
            float e = Mathf.SmoothStep(0, 1, t);
            SetBars((1 - e) * BarHeightFraction * Screen.height);
            SetTimeScale(Mathf.Lerp(slowScale, 1f, e));
            label.alpha = 1 - e;
        });

        RestoreTime();
        onComplete?.Invoke();
        Destroy(gameObject);
    }

    IEnumerator Animate(float duration, Action<float> step)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
            step(Mathf.Clamp01(t));
            yield return null;
        }
    }

    void SetTimeScale(float s)
    {
        Time.timeScale = s;
        Time.fixedDeltaTime = originalFixedDelta * s; // keep physics smooth in slow-mo
    }

    void RestoreTime()
    {
        if (restored) return;
        restored = true;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = originalFixedDelta;
    }

    void OnDestroy() { RestoreTime(); } // safety: never leave the game stuck in slow-mo

    void SetBars(float h)
    {
        topBar.sizeDelta = new Vector2(0, h);
        bottomBar.sizeDelta = new Vector2(0, h);
    }

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        topBar = MakeBar("TopBar", new Vector2(0, 1));
        bottomBar = MakeBar("BottomBar", new Vector2(0, 0));

        var textGO = new GameObject("HaterFreeText", typeof(RectTransform));
        textGO.transform.SetParent(transform, false);
        label = textGO.AddComponent<TextMeshProUGUI>();
        label.text = "HATER FREE!";
        label.color = HaterGreen;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 120;
        label.alignment = TextAlignmentOptions.Center;
        label.outlineColor = new Color32(0, 40, 0, 255);
        label.outlineWidth = 0.25f;
        label.alpha = 0f;
        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1400, 250);
        SetBars(0);
    }

    RectTransform MakeBar(string name, Vector2 anchorY)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        go.GetComponent<Image>().color = Color.black;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0, anchorY.y);
        rt.anchorMax = new Vector2(1, anchorY.y);
        rt.pivot = new Vector2(0.5f, anchorY.y);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }
}