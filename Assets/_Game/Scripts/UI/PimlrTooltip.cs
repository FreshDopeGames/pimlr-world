// PIMLR (Nov 18): short control hints that float over the player's head, e.g.
//   PimlrTooltip.ShowOverPlayer($"Press {PimlrTooltip.Key(PimlrTooltip.NextWeaponLabel)} to equip your water gun", 4f);
//   PimlrTooltip.ShowOverPlayer(text, 30f, until: () => pc.IsItemEquiped);   // stays until the player does it
// One tooltip at a time (a new one replaces the old). Hidden behind menus, the loading screen and the death card.
// Key labels follow the current controller (PimlrInput.Scheme): Xbox / PlayStation names, or keyboard keys.
//
// Unreal note: a UWidgetComponent (Screen space) attached to the pawn's head socket, or a HUD widget projected with
// ProjectWorldLocationToScreen; key names from CommonInput's input action data so they swap with the device.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PimlrTooltip : MonoBehaviour
{
    public static Color KeyColor = new Color(1f, 0.82f, 0.1f, 1f);     // PIMLR yellow
    public static Color PanelColor = new Color(0f, 0f, 0f, 0.72f);
    public static float HeadClearance = 0.45f;                        // metres above the head bone
    public static float FontSize = 30f;

    static PimlrTooltip instance;

    RectTransform panel;
    TextMeshProUGUI label;
    CanvasGroup group;
    Canvas canvas;
    Transform head, playerT;
    float until, shownAt;
    Func<bool> doneWhen;
    bool showing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    // ------------------------------------------------------------------ labels
    /// <summary>"[E]" style highlighted key label.</summary>
    public static string Key(string label) => $"<color=#{ColorUtility.ToHtmlStringRGB(KeyColor)}>[{label}]</color>";

    public static string NextWeaponLabel => PimlrInput.UsingGamepad ? "D-PAD RIGHT" : "E";
    public static string PrevWeaponLabel => PimlrInput.UsingGamepad ? "D-PAD LEFT" : "Q";

    // ------------------------------------------------------------------ API
    public static void ShowOverPlayer(string richText, float seconds, Func<bool> until = null)
    {
        var t = Instance;
        if (!t) return;
        t.Show(richText, seconds, until);
    }

    public static void Hide() { if (instance) instance.showing = false; }

    public static bool IsShowing => instance && instance.showing;

    static PimlrTooltip Instance
    {
        get
        {
            if (instance) return instance;
            var go = new GameObject("PIMLR Tooltip", typeof(RectTransform));
            instance = go.AddComponent<PimlrTooltip>();
            instance.Build();
            return instance;
        }
    }

    void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false; group.blocksRaycasts = false; group.alpha = 0f;

        var bg = new GameObject("Panel", typeof(RectTransform)).AddComponent<Image>();
        bg.transform.SetParent(transform, false);
        bg.color = PanelColor;
        bg.raycastTarget = false;
        panel = bg.rectTransform;
        panel.anchorMin = panel.anchorMax = Vector2.zero;
        panel.pivot = new Vector2(0.5f, 0f);

        label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(panel, false);
        var font = PIMLR.Missions.SaveTheFans.SaveTheFansStyle.Font;
        if (font) label.font = font;
        label.fontSize = FontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(22f, 10f); lrt.offsetMax = new Vector2(-22f, -10f);
    }

    void Show(string text, float seconds, Func<bool> done)
    {
        label.text = text;
        label.ForceMeshUpdate();
        panel.sizeDelta = new Vector2(label.preferredWidth + 44f, label.preferredHeight + 20f);
        until = Time.unscaledTime + Mathf.Max(0.5f, seconds);
        doneWhen = done;
        shownAt = Time.unscaledTime;
        showing = true;
        head = null;
    }

    void LateUpdate()
    {
        if (showing)
        {
            bool done = false;
            if (doneWhen != null) { try { done = doneWhen(); } catch { done = true; } }
            if (done || Time.unscaledTime >= until) showing = false;
        }

        bool blocked = Cursor.visible || PimlrInterstitial.Playing || PimlrDeathSequence.IsPlaying;
        bool visible = showing && !blocked && Place();
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        // A gentle pop when it first appears.
        float pop = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.18f);
        panel.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, 1f - (1f - pop) * (1f - pop));
    }

    // Over the player's head, kept on screen. False if there's no camera / player, or the head is behind the camera.
    bool Place()
    {
        var cam = Camera.main;
        if (!cam) return false;
        if (!playerT)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (!p) return false;
            playerT = p.transform;
            head = null;
        }
        if (!head)
        {
            var a = playerT.GetComponentInChildren<Animator>();
            head = a && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Head) : null;
        }
        Vector3 world = head ? head.position + Vector3.up * HeadClearance : playerT.position + Vector3.up * 2.1f;
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) return false;

        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 pos = new Vector2(sp.x, sp.y) / scale;
        Vector2 screen = new Vector2(Screen.width, Screen.height) / scale;
        float halfW = panel.sizeDelta.x * 0.5f + 12f;
        pos.x = Mathf.Clamp(pos.x, halfW, screen.x - halfW);
        pos.y = Mathf.Clamp(pos.y, 12f, screen.y - panel.sizeDelta.y - 12f);
        panel.anchoredPosition = pos;
        return true;
    }
}
