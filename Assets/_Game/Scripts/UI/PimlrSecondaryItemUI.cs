// PIMLR #GREN (Nov 16): shows the water balloons (the player's secondary weapon) in the existing JUTPS UI.
//
//   HUD  - Global UI > ... > Character_Info/Weapons (JUTPS UIItemInformation). JUTPS only knows its own items, and the
//          balloons aren't one, so with balloons out it showed the empty-hands "Hand" sprite and a stale ammo count.
//          While balloons are equipped this overrides it each LateUpdate (after UIItemInformation.Update): balloon
//          icon (PlayerGrenades.asset > HUD Icon), "count/max" in the ammo text, the water-bullet icon hidden.
//   Pause - Global UI > Pause Screen > ... > "Secondary Weapon Slot (2)". It was a JUTPS sequential slot ("second")
//          that never had a JUTPS item, so it sat greyed out forever. Its JUTPS InventorySlotUI is switched off (its
//          Equip/Drop options can't act on balloons) and this lights it up while the player carries any: full-colour
//          icon + the green->red fill bar the primary slot uses, set to count/max. Greyed out again at zero.
//
// Added to the player by PimlrPlayerGrenades, so no prefab edits are needed. Global UI loads from Resources at runtime,
// so the UI pieces are looked up lazily (and re-looked-up if destroyed).
//
// Unreal note: the HUD weapon widget binds to the inventory component's "ActiveSlotChanged" delegate and reads an
// FInventorySlotView {Icon, Count, Max} from whichever slot is active (gun or balloons) - one code path, no override.
// The pause slot is the same widget class bound to slot index 1.

using System.Collections.Generic;
using JUTPS;
using JUTPS.InventorySystem;
using JUTPS.InventorySystem.UI;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public class PimlrSecondaryItemUI : MonoBehaviour
{
    PimlrPlayerGrenades grenades;
    JUCharacterController pc;

    // HUD
    UIItemInformation hud;
    bool hudOverriding, hudPreserveAspectBefore, hudBulletLabelBefore;
    Sprite hudSpriteBefore;
    string hudNameBefore;

    // Pause slot(s)
    class Slot
    {
        public InventorySlotUI ui;
        public Image icon;
        public Image bar;
        public Color iconOff;
        public bool barOffWasActive;
    }
    readonly List<Slot> pauseSlots = new List<Slot>();
    float nextLookup;

    void Awake()
    {
        grenades = GetComponent<PimlrPlayerGrenades>();
        pc = GetComponent<JUCharacterController>();
    }

    void OnDisable()
    {
        RestoreHud();
        foreach (var s in pauseSlots) ApplySlot(s, 0, 1);
    }

    void LateUpdate()
    {
        if (!grenades) return;
        var S = PimlrPlayerGrenades.S;
        if (Time.unscaledTime >= nextLookup && (!hud || pauseSlots.Count == 0 || pauseSlots.Exists(s => !s.ui)))
        {
            nextLookup = Time.unscaledTime + 1f;
            Lookup();
        }

        // ---------------- HUD weapon panel
        if (hud)
        {
            if (grenades.IsEquipped) OverrideHud(S);
            else if (hudOverriding) RestoreHud();
        }

        // ---------------- pause menu Secondary slot
        foreach (var s in pauseSlots) ApplySlot(s, grenades.Count, grenades.MaxCount);
    }

    void Lookup()
    {
        if (!hud)
        {
            // The gameplay HUD's one: it has the TMP ammo text. (The Mobile Rig copy has none.)
            foreach (var u in FindObjectsByType<UIItemInformation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (u && u.BulletQuantity) { hud = u; hudOverriding = false; break; }
        }

        pauseSlots.RemoveAll(s => !s.ui);
        if (pauseSlots.Count > 0) return;
        foreach (var ui in FindObjectsByType<InventorySlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!ui || !ui.DrawSequentialItem || ui.SequentialToDraw != JUInventory.SequentialSlotsEnum.second) continue;
            if (!InPauseScreen(ui.transform)) continue;      // not the old JUTPS inventory screen
            var s = new Slot { ui = ui, icon = ui.SlotItemImage, bar = ui.SlotHealthBar };
            s.iconOff = s.icon ? s.icon.color : Color.gray;
            ui.enabled = false;                              // its Equip / Drop options can't act on balloons
            if (s.icon && PimlrPlayerGrenades.S && PimlrPlayerGrenades.S.hudIcon) s.icon.sprite = PimlrPlayerGrenades.S.hudIcon;
            pauseSlots.Add(s);
        }
    }

    static bool InPauseScreen(Transform t)
    {
        for (; t; t = t.parent) if (t.name == "Pause Screen") return true;
        return false;
    }

    void OverrideHud(PimlrPlayerGrenadeSettings S)
    {
        if (!hudOverriding)
        {
            hudOverriding = true;
            if (hud.Icon) { hudSpriteBefore = hud.Icon.sprite; hudPreserveAspectBefore = hud.Icon.preserveAspect; }
            if (hud.BulletLabel) hudBulletLabelBefore = hud.BulletLabel.activeSelf;
            if (hud.ItemName) hudNameBefore = hud.ItemName.text;
        }
        if (hud.Icon && S && S.hudIcon)
        {
            hud.Icon.sprite = S.hudIcon;
            hud.Icon.preserveAspect = true;   // the panel's icon box is sized for the (wide) water gun
        }
        if (hud.BulletQuantity) hud.BulletQuantity.text = $"{grenades.Count}/{grenades.MaxCount}";
        if (hud.BulletLabel && hud.BulletLabel.activeSelf) hud.BulletLabel.SetActive(false); // water-bullet icon: gun ammo only
        if (hud.ItemName && S) hud.ItemName.text = S.hudName;
        if (hud.ItemQuantity) hud.ItemQuantity.text = "";
        if (hud.ItemHealth) hud.ItemHealth.fillAmount = grenades.MaxCount > 0 ? (float)grenades.Count / grenades.MaxCount : 0f;
    }

    void RestoreHud()
    {
        if (!hudOverriding || !hud) { hudOverriding = false; return; }
        hudOverriding = false;
        if (hud.Icon) hud.Icon.preserveAspect = hudPreserveAspectBefore;
        // UIItemInformation rewrites icon / name / ammo itself next frame when a gun is out. Empty hands don't
        // touch the ammo text, so clear the balloon count rather than leave it showing under the hand icon.
        bool emptyHands = !pc || !pc.HoldableItemInUseRightHand;
        if (emptyHands)
        {
            if (hud.BulletQuantity) hud.BulletQuantity.text = "";
            if (hud.Icon && hud.EmptySprite) hud.Icon.sprite = hud.EmptySprite;
        }
        else if (hud.BulletLabel) hud.BulletLabel.SetActive(hudBulletLabelBefore);
    }

    static void ApplySlot(Slot s, int count, int max)
    {
        bool on = count > 0;
        if (s.icon) s.icon.color = on ? Color.white : s.iconOff;
        if (s.bar)
        {
            if (s.bar.gameObject.activeSelf != on) s.bar.gameObject.SetActive(on);
            if (on)
            {
                float k = max > 0 ? Mathf.Clamp01((float)count / max) : 0f;
                s.bar.fillAmount = k;
                s.bar.color = Color.Lerp(Color.red, Color.green, k);   // same colours JUTPS uses on the primary slot
            }
        }
    }
}
