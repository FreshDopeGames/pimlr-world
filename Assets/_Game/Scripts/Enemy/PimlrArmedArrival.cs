// PIMLR (Nov 18): armed enemies (the Gunner) step out of their portal already holding their gun properly.
//
// Measured before this fix: for the first ~0.2 s after stepping out the Gunner aimed empty-handed, then its gun was
// equipped with the full JUTPS weapon-switch animation, which resets the arm IK to 0 - so its left hand hung ~0.6 m off
// the gun until ~1.3 s after arrival (the "janky arms").
//
// Now, on the first frame after the enemy is switched on (after every Start has run), its first unlocked right-hand
// weapon is equipped with no switch animation: the switch state is cleared, the arm IK goes straight to full weight and
// the hands are snapped onto the gun's grips. Added by PimlrEnemySpawnDirector.Arrive; only acts on characters with
// an enabled JUInventory holding a weapon (melee robots and bombers are left alone).
//
// Unreal note: on the spawned AI pawn, attach the weapon actor to the hand socket in BeginPlay (no equip montage) and
// set the AnimBP's "ArmsIKAlpha" to 1 for the first frame instead of blending it in.

using System.Collections;
using System.Reflection;
using JUTPS;
using UnityEngine;

public class PimlrArmedArrival : MonoBehaviour
{
    static readonly BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
    static FieldInfo armsIK, bothArms, switchTime;

    JUCharacterController c;
    Animator anim;

    public static void Attach(GameObject enemy)
    {
        if (!enemy || enemy.CompareTag("Player") || enemy.GetComponent<PimlrArmedArrival>()) return;
        var inv = enemy.GetComponent<JUTPS.InventorySystem.JUInventory>();
        if (!inv || !inv.enabled) return;
        enemy.AddComponent<PimlrArmedArrival>();
    }

    void OnEnable()
    {
        c = GetComponent<JUCharacterController>();
        anim = GetComponent<Animator>();
        if (armsIK == null)
        {
            for (var t = typeof(JUCharacterController); t != null; t = t.BaseType)
            {
                armsIK ??= t.GetField("ArmsWeightIK", F);
                bothArms ??= t.GetField("BothArmsLayerWeight", F);
                switchTime ??= t.GetField("WeaponSwitchingCurrentTime", F);
            }
        }
        StartCoroutine(EquipNow());
    }

    IEnumerator EquipNow()
    {
        yield return null; // every Start (inventory setup) has run
        if (!c || c.IsDead || c.Inventory == null) { Destroy(this); yield break; }

        // JUTPS's ItemSwitchManager equips its 'Item To Equip On Start' on a 0.2 s timer, which would replay the draw
        // animation over ours: cancel it and equip that same item now.
        var sw = GetComponent<JUTPS.ItemSystem.ItemSwitchManager>();
        int startId = sw ? sw.ItemToEquipOnStart : -1;
        if (sw) sw.CancelInvoke();

        if (!c.HoldableItemInUseRightHand)
        {
            int id = startId;
            if (c.Inventory.HoldableItensRightHand != null)
                foreach (var item in c.Inventory.HoldableItensRightHand)
                    if (id < 0 && item && item.Unlocked) { id = item.ItemSwitchID; break; }
            if (id < 0) { Destroy(this); yield break; }
            c.SwitchToItem(id);
        }

        // Skip the draw animation: hands straight onto the gun, for a few frames so nothing blends them back out.
        for (int i = 0; i < 3 && c && c.HoldableItemInUseRightHand; i++)
        {
            Snap();
            yield return null;
        }
        Destroy(this);
    }

    void Snap()
    {
        c.IsWeaponSwitching = false;
        switchTime?.SetValue(c, 10f);
        armsIK?.SetValue(c, 1f);
        bothArms?.SetValue(c, 1f);
        if (anim)
        {
            int layer = anim.GetLayerIndex("Weapon Switch Layer");
            if (layer >= 0)
            {
                int empty = Animator.StringToHash("Empty");
                if (anim.HasState(layer, empty)) anim.Play(empty, layer, 0f);
            }
        }
        c.SmoothRightHandPosition(1000f);
        c.SmoothLeftHandPosition(1000f);
        c.DoHandPositioningNoSmoothing();
    }
}
