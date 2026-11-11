// PIMLR #STF (Save The Fans) — ammo restock spot near each fan (e.g. a soap-refill cooler).
// Needs a trigger collider. Only works while its fan's heat is live.
// Refills every JUTPS Weapon the player carries: TotalBullets (reserve) back up to what the
// player had when the mission started (or the minimum below), and the magazine in the hand.
//
// Placement rule of thumb: 8-15 m from the fan, NOT on the enemies' spawn side — the player
// should have to briefly leave the fan exposed to restock. That's the tension.
//
// Unreal note: an actor with a USphereComponent overlap that calls an IPimlrAmmoReceiver
// interface on the overlapping pawn's weapons.

using System.Collections.Generic;
using JUTPS.WeaponSystem;
using UnityEngine;
using UnityEngine.Events;

namespace PIMLR.Missions.SaveTheFans
{
    // Needs a Collider (Box/Sphere) set to Is Trigger on the same object.
    [AddComponentMenu("PIMLR/Save The Fans/Ammo Restock Station")]
    public class AmmoRestockStation : MonoBehaviour
    {
        [Header("Refill")]
        [Tooltip("Reserve ammo is topped up to max(this, the amount the weapon had when the mission started).")]
        [Min(0)] public int minimumReserveRefill = 120;
        [Tooltip("Also fill the current magazine.")]
        public bool refillMagazine = true;
        [Tooltip("Seconds before the station can be used again.")]
        [Min(0f)] public float cooldown = 12f;

        [Header("Visuals")]
        [Tooltip("Shown when ready (glow, icon). Hidden while cooling down / inactive.")]
        public GameObject readyVisual;
        public UnityEvent onRestocked;

        public bool IsReady => isActive && Time.time >= readyAt;

        static readonly Dictionary<Weapon, int> baselineReserve = new Dictionary<Weapon, int>();
        bool isActive;
        float readyAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => baselineReserve.Clear();

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c) c.isTrigger = true;
        }

        public void SetActive(bool on)
        {
            isActive = on;
            if (on) readyAt = 0f;
            RefreshVisual();
        }

        void Update()
        {
            if (isActive) RefreshVisual();
        }

        void RefreshVisual()
        {
            if (readyVisual && readyVisual.activeSelf != IsReady) readyVisual.SetActive(IsReady);
        }

        void OnTriggerEnter(Collider other) => TryRestock(other);
        void OnTriggerStay(Collider other) => TryRestock(other);

        void TryRestock(Collider other)
        {
            if (!IsReady || !other.CompareTag("Player")) return;

            var playerCtrl = PlayerControlLock.ResolvePlayer();
            GameObject root = playerCtrl ? playerCtrl.gameObject : other.transform.root.gameObject;

            int refilled = 0;
            foreach (var weapon in root.GetComponentsInChildren<Weapon>(true))
                if (Refill(weapon)) refilled++;

            if (refilled == 0) return; // already full: don't burn the cooldown
            readyAt = Time.time + cooldown;
            RefreshVisual();
            onRestocked?.Invoke();
        }

        bool Refill(Weapon weapon)
        {
            if (!weapon) return false;

            if (!baselineReserve.TryGetValue(weapon, out int baseline))
            {
                baseline = weapon.TotalBullets;
                baselineReserve[weapon] = baseline;
            }

            bool changed = false;
            int target = Mathf.Max(minimumReserveRefill, baseline);
            if (weapon.TotalBullets < target) { weapon.TotalBullets = target; changed = true; }

            if (refillMagazine && weapon.BulletsAmounts < weapon.BulletsPerMagazine)
            {
                weapon.BulletsAmounts = weapon.BulletsPerMagazine;
                changed = true;
            }
            return changed;
        }

        /// Called at mission start so the baseline is the full loadout, not whatever was left mid-fight.
        public static void CaptureBaseline(GameObject player)
        {
            if (!player) return;
            foreach (var weapon in player.GetComponentsInChildren<Weapon>(true))
            {
                if (!weapon) continue;
                int prev = baselineReserve.TryGetValue(weapon, out int p) ? p : 0;
                baselineReserve[weapon] = Mathf.Max(prev, weapon.TotalBullets);
            }
        }
    }
}
