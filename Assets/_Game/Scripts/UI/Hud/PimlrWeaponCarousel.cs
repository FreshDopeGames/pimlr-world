// #UI Slice 4b: weapon carousel, bottom-left of the HUD (Craig, Nov 27). Replaces the wide JUTPS "Weapons" card.
// One slot per thing the player can hold: every unlocked right-hand JUTPS item, plus the water balloons
// (PimlrPlayerGrenades) while the player has any. Each slot is the item's icon stacked over its count
// (gun: magazine/total, balloons: count/max, melee: icon only). The held item is always the first, larger slot with
// the lime frame; the others follow in inventory order and wrap around, so switching weapons scrolls the strip
// sideways. Hands empty = no slot highlighted.
// Reads only (never switches weapons itself): JUTPS / PimlrPlayerGrenades keep owning the inputs.
//
// Unreal note: WBP_WeaponCarousel bound to the inventory component's ActiveSlotChanged / SlotsChanged delegates;
// slot widgets from an FInventorySlotView {Icon, Count, Max}; the scroll is a widget animation on the strip.

using System.Collections.Generic;
using DG.Tweening;
using JUTPS;
using JUTPS.ItemSystem;
using JUTPS.WeaponSystem;
using PIMLR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PimlrWeaponCarousel : MonoBehaviour
{
    [Tooltip("Most slots shown at once. PimlrHudLayout lowers it to 2 on narrow screens (4:3).")]
    public int maxSlots = 3;

    public const float Pad = 8f, Gap = 6f, SelectedW = 112f, OtherW = 84f, SlotH = 92f, Count = 3;
    public float Height => SlotH + Pad * 2f;
    public float Width { get; private set; }

    [SerializeField, HideInInspector] RectTransform strip;
    [SerializeField, HideInInspector] CanvasGroup group, stripGroup;
    [SerializeField, HideInInspector] List<RectTransform> slots = new List<RectTransform>();

    struct Entry { public Object key; public Sprite icon; public string count; }
    readonly List<Entry> entries = new List<Entry>();
    Object lastSelected;
    int lastIndex = -1, laidOutSlots = -1;

    void OnEnable() { if (!strip) Build(); }
    void OnDisable() { if (strip) strip.DOKill(); if (stripGroup) stripGroup.DOKill(); }

    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
        }
        group = GetComponent<CanvasGroup>(); if (!group) group = gameObject.AddComponent<CanvasGroup>(); // (no ?? on Unity objects)
        group.interactable = false; group.blocksRaycasts = false;

        var bg = PimlrHudKit.Img("Background", transform, ColorToken.SurfaceBase, 0.85f);
        PimlrHudKit.Stretch(bg.rectTransform);
        PimlrHudKit.Edges("Edge", transform, ColorToken.HealthGreen, 3f, top: false, bottom: false, right: false);

        strip = PimlrHudKit.Rect("Strip", transform);
        PimlrHudKit.Stretch(strip);
        stripGroup = strip.gameObject.AddComponent<CanvasGroup>();

        slots.Clear();
        for (int i = 0; i < Count; i++)
        {
            var slot = PimlrHudKit.Img("Slot " + (i + 1), strip, ColorToken.SurfaceRaised, 0.9f).rectTransform;
            var frame = PimlrHudKit.Img("Frame (sprite slot)", slot, ColorToken.BrandLime, 1f, SpriteSlot.WeaponSlot);
            PimlrHudKit.Stretch(frame.rectTransform);
            PimlrHudKit.Edges("Frame (edges)", slot, ColorToken.BrandLime, 2f);
            var icon = PimlrHudKit.Icon("Icon", slot, null);
            icon.color = Color.white;
            PimlrHudKit.Txt("Count", slot, TypeRole.Utility, 25.5f, ColorToken.TextPrimary, TextCase.Original, TextAlignmentOptions.Center);
            slots.Add(slot);
        }
        laidOutSlots = -1;
        if (!Application.isPlaying) Preview();
    }

    // Editor preview so the prefab shows the carousel's footprint (3 empty slots).
    void Preview()
    {
        entries.Clear();
        for (int i = 0; i < maxSlots; i++) entries.Add(new Entry { count = i == 0 ? "35/150" : "4/10" });
        Show(0, true);
    }

    void Update()
    {
        if (!strip || !Application.isPlaying) return;
        Collect(out int selectedIndex);
        group.alpha = entries.Count > 0 ? 1f : 0f;
        if (entries.Count == 0) return;

        var selKey = selectedIndex >= 0 ? entries[selectedIndex].key : null;
        if (selKey != lastSelected && lastSelected != null && selKey != null && Application.isPlaying)
        {
            // Scroll in from the side the new item sits on.
            float dir = selectedIndex >= lastIndex ? 1f : -1f;
            strip.DOKill(); stripGroup.DOKill();
            strip.anchoredPosition = new Vector2(dir * 36f, 0f);
            strip.DOAnchorPos(Vector2.zero, 0.22f).SetEase(Ease.OutCubic).SetUpdate(true);
            stripGroup.alpha = 0.35f;
            stripGroup.DOFade(1f, 0.18f).SetUpdate(true);
        }
        lastSelected = selKey; lastIndex = selectedIndex;
        Show(selectedIndex, selectedIndex >= 0);
    }

    void Collect(out int selectedIndex)
    {
        entries.Clear();
        selectedIndex = -1;
        var pc = JUGameManager.InstancedPlayer;
        var inv = pc ? pc.Inventory : null;
        var gren = PimlrPlayerGrenades.Instance;
        Object held = gren && gren.IsEquipped ? (Object)gren : (pc ? pc.HoldableItemInUseRightHand : null);
        string sec = PimlrHudKit.Hex(ColorToken.TextSecondary);

        if (inv && inv.HoldableItensRightHand != null)
            foreach (var h in inv.HoldableItensRightHand)
            {
                if (!h || !h.Unlocked) continue;
                string count;
                if (h is Weapon w) count = $"{w.BulletsAmounts}<color={sec}>/{w.TotalBullets}</color>";
                else if (h is MeleeWeapon) count = "";
                else count = $"{h.ItemQuantity}<color={sec}>/{h.MaxItemQuantity}</color>";
                if (h == held) selectedIndex = entries.Count;
                entries.Add(new Entry { key = h, icon = h.ItemIcon, count = count });
            }

        if (gren && (gren.Count > 0 || gren.IsEquipped))
        {
            if (gren == held) selectedIndex = entries.Count;
            var S = PimlrPlayerGrenades.S;
            entries.Add(new Entry { key = gren, icon = S ? S.hudIcon : null, count = $"{gren.Count}<color={sec}>/{gren.MaxCount}</color>" });
        }
    }

    /// <summary>Lays out the slots: the selected entry first, the rest after it in order (wrapping).</summary>
    void Show(int selectedIndex, bool highlight)
    {
        int n = Mathf.Min(entries.Count, Mathf.Clamp(maxSlots, 1, (int)Count));
        int start = Mathf.Max(0, selectedIndex);
        float x = Pad;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            bool on = i < n;
            if (slot.gameObject.activeSelf != on) slot.gameObject.SetActive(on);
            if (!on) continue;
            var e = entries[(start + i) % entries.Count];
            bool sel = highlight && i == 0;
            float w = sel ? SelectedW : OtherW;
            PimlrHudKit.Place(slot, new Vector2(0f, 0f), new Vector2(x, Pad), new Vector2(w, SlotH));
            x += w + Gap;

            var bg = slot.GetComponent<Image>();
            bg.enabled = sel;
            var frameImg = slot.Find("Frame (sprite slot)").GetComponent<Image>();
            bool hasFrame = frameImg.sprite;
            frameImg.enabled = sel && hasFrame; // stays active so the theme can still hand it a sprite
            slot.Find("Frame (edges)").gameObject.SetActive(sel && !hasFrame);

            var icon = slot.Find("Icon").GetComponent<Image>();
            icon.sprite = e.icon;
            icon.enabled = e.icon;
            icon.color = new Color(1f, 1f, 1f, sel ? 1f : 0.55f);
            PimlrHudKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(w - 14f, 46f));

            var count = slot.Find("Count").GetComponent<TMP_Text>();
            if (count.text != e.count) count.text = e.count;
            count.alpha = sel ? 1f : 0.75f;
            PimlrHudKit.Place(count.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(w - 4f, 30f));
        }
        Width = x - Gap + Pad;
        if (n == 0) Width = 0f;
        var self = (RectTransform)transform;
        var size = new Vector2(Width, Height);
        if (self.sizeDelta != size) self.sizeDelta = size;
    }
}
