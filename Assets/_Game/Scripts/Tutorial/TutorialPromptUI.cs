// PIMLR #TUT: on-screen prompt for the controls warm-up. Builds its own overlay canvas at runtime (same
// self-bootstrapping approach as PimlrObjectiveArrow), so no scene or prefab edits are needed.
// Visibility is CanvasGroup alpha only - the object stays active so fades never fail.
//
// Unreal mapping: WBP_TutorialPrompt (UUserWidget) - TextBlock prompt, TextBlock progress, ProgressBar skip,
// a fade-in/out UMG Animation, added to the viewport by the tutorial subsystem.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialPromptUI : MonoBehaviour
{
    CanvasGroup group;
    TMP_Text promptText, progressText, skipText;
    Image skipFill, panel;
    Coroutine fade;
    float fadeSeconds = 0.2f;

    static readonly Color PanelColor = new Color(0.05f, 0.05f, 0.08f, 0.78f);
    static readonly Color DoneColor = new Color(0.20f, 0.75f, 0.35f, 0.85f);
    static readonly Color Accent = new Color(1f, 0.82f, 0.15f, 1f); // matches the yellow objective arrow

    public static TutorialPromptUI Create()
    {
        var root = new GameObject("PIMLR_TutorialPrompt");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var ui = root.AddComponent<TutorialPromptUI>();
        ui.Build();
        return ui;
    }

    void Build()
    {
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;

        // Panel: top-centre, below where the goal panel / minimap sit.
        var panelRt = NewRect("Panel", transform);
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0f, -150f);
        panelRt.sizeDelta = new Vector2(860f, 120f);
        panel = panelRt.gameObject.AddComponent<Image>();
        panel.color = PanelColor;
        panel.raycastTarget = false;

        // Accent bar on the left edge.
        var bar = NewRect("Accent", panelRt);
        bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(0f, 1f);
        bar.pivot = new Vector2(0f, 0.5f); bar.sizeDelta = new Vector2(8f, 0f);
        var barImg = bar.gameObject.AddComponent<Image>(); barImg.color = Accent; barImg.raycastTarget = false;

        promptText = NewText("Prompt", panelRt, 38f, FontStyles.Bold, TextAlignmentOptions.Center);
        var pr = promptText.rectTransform;
        pr.anchorMin = new Vector2(0f, 0.3f); pr.anchorMax = new Vector2(1f, 1f);
        pr.offsetMin = new Vector2(30f, 0f); pr.offsetMax = new Vector2(-30f, -8f);

        progressText = NewText("Progress", panelRt, 26f, FontStyles.Normal, TextAlignmentOptions.Center);
        progressText.color = Accent;
        var pg = progressText.rectTransform;
        pg.anchorMin = new Vector2(0f, 0f); pg.anchorMax = new Vector2(1f, 0.35f);
        pg.offsetMin = new Vector2(30f, 6f); pg.offsetMax = new Vector2(-30f, 0f);

        // Skip hint + hold-to-skip fill under the panel.
        skipText = NewText("SkipHint", transform, 20f, FontStyles.Normal, TextAlignmentOptions.Center);
        skipText.color = new Color(1f, 1f, 1f, 0.7f);
        var sk = skipText.rectTransform;
        sk.anchorMin = sk.anchorMax = new Vector2(0.5f, 1f); sk.pivot = new Vector2(0.5f, 1f);
        sk.anchoredPosition = new Vector2(0f, -276f); sk.sizeDelta = new Vector2(600f, 30f);

        var fillRt = NewRect("SkipFill", transform);
        fillRt.anchorMin = fillRt.anchorMax = new Vector2(0.5f, 1f); fillRt.pivot = new Vector2(0.5f, 1f);
        fillRt.anchoredPosition = new Vector2(0f, -270f); fillRt.sizeDelta = new Vector2(860f, 4f);
        skipFill = fillRt.gameObject.AddComponent<Image>();
        skipFill.color = Accent; skipFill.raycastTarget = false;
        skipFill.type = Image.Type.Filled; skipFill.fillMethod = Image.FillMethod.Horizontal; skipFill.fillAmount = 0f;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static TMP_Text NewText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
    {
        var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.fontStyle = style; t.alignment = align;
        t.color = Color.white; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    // ------------------------------------------------------------------ API used by TutorialDirector

    public void Show(string prompt)
    {
        promptText.text = prompt;
        progressText.text = "";
        panel.color = PanelColor;
        FadeTo(1f);
    }

    public void SetPrompt(string prompt) => promptText.text = prompt; // refresh labels after a keyboard<->pad switch
    public void SetProgress(string text) => progressText.text = text;
    public void SetSkipHint(string text) => skipText.text = text;
    public void FlashComplete() { panel.color = DoneColor; progressText.text = "Done!"; }
    public void Hide() => FadeTo(0f);
    public void SetSkipFill(float t) => skipFill.fillAmount = Mathf.Clamp01(t);

    void FadeTo(float target)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        float start = group.alpha, t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, t / fadeSeconds);
            yield return null;
        }
        group.alpha = target;
    }
}
