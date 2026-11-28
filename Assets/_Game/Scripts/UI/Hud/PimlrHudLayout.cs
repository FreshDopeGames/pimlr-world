// #UI Slice 4b: responsive placement of the gameplay HUD (Global UI > JUTPS Interface).
//   Landscape (desktop, browser, phone on its side): the layout from Craig's Nov 27 concept -
//     music player + Boost panel top-left | coins + pause, one objective, kill count top-right
//     weapon carousel bottom-left         | health + stamina bottom-centre | minimap bottom-right
//     4:3 and other narrow screens show 2 carousel slots so it never runs into the vitals.
//   Portrait (phones held upright, tall browser windows): player across the top (room kept for the Boost panel),
//     then coins, objective, minimap and kill count down the right; carousel above the vitals at the bottom-left.
//   Touch screens: the player switches to its compact form (one >= 44 pt play/pause button).
// The landscape positions are the ones the Slice 4b builder set, captured on this component; portrait is computed from
// them. Re-lays out only when the screen size / touch mode changes (PimlrResponsive.Signature), plus once on enable.
//
// Unreal note: one WBP_GameHUD with two layout branches (a WidgetSwitcher or anchors set from the viewport aspect in
// NativeTick when the size changes), safe-zone handled by a USafeZone wrapper.

using System;
using PIMLR.UI;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PimlrHudLayout : MonoBehaviour
{
    [Serializable]
    public struct RectState
    {
        public Vector2 anchorMin, anchorMax, pivot, pos, size;
        public static RectState Of(RectTransform r) => r ? new RectState { anchorMin = r.anchorMin, anchorMax = r.anchorMax, pivot = r.pivot, pos = r.anchoredPosition, size = r.sizeDelta } : default;
        public void ApplyTo(RectTransform r)
        {
            if (!r) return;
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
        }
    }

    public PimlrHudMusicPlayer player;
    public PimlrWeaponCarousel carousel;
    public RectTransform coins, objective, minimapRing, minimap, killCount;
    [Tooltip("The coin card's button; grown to the 44 pt touch minimum on touch screens.")]
    public RectTransform coinButton;
    public Vector2 coinButtonSize = new Vector2(62.84f, 60f);

    [Header("Landscape positions (captured by the Slice 4b builder)")]
    public RectState coinsLandscape;
    public RectState objectiveLandscape, ringLandscape, minimapLandscape, killLandscape;

    [Header("Spacing (HUD canvas units)")]
    public float margin = 24f;
    public float portraitMargin = 12f;
    public float gap = 10f;
    [Tooltip("Canvas width below which the carousel shows 2 slots instead of 3.")]
    public float narrowWidth = 1200f;

    int signature;

    void OnEnable() { signature = 0; }

    void LateUpdate()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (!canvas) return;
        var size = ((RectTransform)canvas.rootCanvas.transform).rect.size;
        int sig;
        unchecked { sig = PimlrResponsive.Signature() ^ ((int)size.x * 92821) ^ ((int)size.y * 68917); }
        if (sig == signature) return;
        signature = sig;
        Apply(size);
    }

    public void Apply(Vector2 canvasSize)
    {
        float upp = PimlrHudKit.UnitsPerPixel(this);
        var sa = Screen.safeArea;
        float top = (Screen.height - sa.yMax) * upp, bottom = sa.yMin * upp, left = sa.xMin * upp, right = (Screen.width - sa.xMax) * upp;
        bool portrait = PimlrResponsive.Orientation == LayoutOrientation.Portrait;

        if (coinButton)
        {
            // lossyScale = canvas scale x any parent scale (the coin card is scaled), so this lands on exactly 44 pt.
            float minTouch = PimlrResponsive.MinTouchPx / Mathf.Max(0.0001f, Mathf.Abs(coinButton.lossyScale.y));
            coinButton.sizeDelta = PimlrResponsive.Touch ? Vector2.Max(coinButtonSize, new Vector2(minTouch, minTouch)) : coinButtonSize;
            var img = coinButton.GetComponent<Image>();
            if (img) img.preserveAspect = true;
        }

        if (player)
        {
            player.compact = PimlrResponsive.Touch;
            player.compactPlaySize = Mathf.Max(64f, PimlrResponsive.MinTouchPx * upp);
        }

        if (!portrait)
        {
            coinsLandscape.ApplyTo(coins); Shift(coins, -right, -top);
            objectiveLandscape.ApplyTo(objective); Shift(objective, -right, -top);
            ringLandscape.ApplyTo(minimapRing); Shift(minimapRing, -right, bottom);
            minimapLandscape.ApplyTo(minimap); Shift(minimap, -right, bottom);
            if (objective && killCount)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(objective);
                PlaceKill(-objective.anchoredPosition.y + objective.rect.height + gap, -(Mathf.Abs(objectiveLandscape.pos.x) + right));
            }
            if (player)
            {
                player.width = 440f;
                PimlrHudKit.Place((RectTransform)player.transform, new Vector2(0f, 1f), new Vector2(margin + left, -(20f + top)), ((RectTransform)player.transform).sizeDelta);
                player.Layout();
            }
            if (carousel)
            {
                carousel.maxSlots = canvasSize.x < narrowWidth ? 2 : 3;
                PlaceCorner((RectTransform)carousel.transform, new Vector2(0f, 0f), new Vector2(margin + left, margin + bottom));
            }
            return;
        }

        // ---------------- portrait
        float m = portraitMargin;
        float y = m + top; // running distance from the top edge
        if (player)
        {
            player.width = canvasSize.x - m * 2f - left - right;
            PimlrHudKit.Place((RectTransform)player.transform, new Vector2(0f, 1f), new Vector2(m + left, -y), ((RectTransform)player.transform).sizeDelta);
            player.Layout();
            y += player.BodyHeight + PimlrHudMusicPlayer.BoostHeight + gap; // room for the Boost panel, so nothing jumps
        }

        float rightEdge = -(Mathf.Abs(objectiveLandscape.pos.x) + right); // keep the landscape right margin
        if (coins)
        {
            // The coin card sits 20 units below the "Menu" anchor; move the anchor so the card's top lands on y.
            coinsLandscape.ApplyTo(coins);
            coins.anchoredPosition = coinsLandscape.pos + new Vector2(-right, -(y - 20f));
            y += 80f + gap;
        }
        if (objective)
        {
            objectiveLandscape.ApplyTo(objective);
            objective.anchoredPosition = new Vector2(rightEdge, -y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(objective);
            y += objective.rect.height + gap;
        }
        if (minimap)
        {
            float d = Mathf.Max(minimapRing ? minimapRing.sizeDelta.x : 0f, minimap.sizeDelta.x);
            var center = new Vector2(rightEdge - d * 0.5f, -(y + d * 0.5f));
            foreach (var r in new[] { minimapRing, minimap })
            {
                if (!r) continue;
                r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
                r.anchoredPosition = center + new Vector2((r.pivot.x - 0.5f) * r.sizeDelta.x, (r.pivot.y - 0.5f) * r.sizeDelta.y);
            }
            y += d + gap;
        }
        if (killCount) PlaceKill(y, rightEdge);
        if (carousel)
        {
            carousel.maxSlots = 3;
            PlaceCorner((RectTransform)carousel.transform, new Vector2(0f, 0f), new Vector2(m + left, 80f + bottom));
        }
    }

    // The kill counter's original anchors stretch across the screen with a fixed -1080 width offset, so it was 200 wide
    // only on a 16:9 canvas (and 28 wide on 4:3). It now sits under the objective at the objective's width.
    void PlaceKill(float topFromEdge, float rightEdge)
    {
        killCount.anchorMin = killCount.anchorMax = killCount.pivot = new Vector2(1f, 1f);
        killCount.sizeDelta = new Vector2(objective ? objective.sizeDelta.x : 293f, killLandscape.size.y);
        killCount.anchoredPosition = new Vector2(rightEdge, -topFromEdge);
    }

    static void Shift(RectTransform r, float dx, float dy) { if (r) r.anchoredPosition += new Vector2(dx, dy); }

    static void PlaceCorner(RectTransform r, Vector2 corner, Vector2 pos)
    {
        r.anchorMin = r.anchorMax = r.pivot = corner;
        r.anchoredPosition = pos;
    }
}
