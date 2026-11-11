using System.Text.RegularExpressions;
using JUTPS;
using UnityEngine;

// PIMLR #ANL: called from JUHealth.DoDamage (one line there, tagged PIMLR #ANL) for every hit on any character.
//   - Hit on the player   -> damage taken (player_damaged, batched to at most one event per second).
//   - Hit on anything else while the player fired in the last 1.5 s -> a hit for the player's current weapon.
//   - That hit took the target from alive to 0 -> kill (enemy type, weapon, distance, on foot or in vehicle).
// Shots come from AnalyticsHooks (magazine count dropping), so accuracy = hits / shots per weapon.
namespace PIMLR.Analytics
{
    public static class CombatTelemetry
    {
        const float HitAttributionWindow = 1.5f;
        const float DamageBatchSeconds = 1f;

        internal static string CurrentWeapon = "unarmed";
        internal static float LastShotTime = -10f;
        internal static Transform Player;

        static float pendingDamage;
        static int pendingHits;
        static float pendingSince = -1f;

        static readonly Regex CloneSuffix = new Regex(@"(\s*\(Clone\))+|\s*\(\d+\)$", RegexOptions.Compiled);

        public static string CleanName(string n) => CloneSuffix.Replace(n ?? "", "").Trim();

        public static void OnDamage(JUHealth target, float damage, float healthBefore)
        {
            if (target == null || damage <= 0f) return;

            if (target.CompareTag("Player"))
            {
                if (RunBreakdown.Current != null) RunBreakdown.Current.damageTaken += damage;
                if (pendingSince < 0f) pendingSince = Time.time;
                pendingDamage += damage;
                pendingHits++;
                if (target.Health <= 0f && healthBefore > 0f) FlushPlayerDamage(target);
                return;
            }

            bool byPlayer = Time.time - LastShotTime <= HitAttributionWindow;
            if (!byPlayer) return;

            RunBreakdown.Current?.AddHit(CurrentWeapon, damage);

            if (target.Health <= 0f && healthBefore > 0f)
            {
                string type = CleanName(target.gameObject.name);
                RunBreakdown.Current?.AddKill(type);
                float dist = Player ? Vector3.Distance(Player.position, target.transform.position) : -1f;
                PimlrAnalytics.Log("kill", new AnalyticsData()
                    .Add("enemy", type)
                    .Add("weapon", CurrentWeapon)
                    .Add("dist", dist)
                    .Add("in_vehicle", PimlrInput.PlayerInVehicle));
            }
        }

        /// <summary>Polled by AnalyticsHooks; sends the batched damage once a second.</summary>
        internal static void Tick(JUHealth playerHealth)
        {
            if (pendingSince >= 0f && Time.time - pendingSince >= DamageBatchSeconds) FlushPlayerDamage(playerHealth);
        }

        static void FlushPlayerDamage(JUHealth playerHealth)
        {
            if (pendingSince < 0f) return;
            PimlrAnalytics.Log("player_damaged", new AnalyticsData()
                .Add("amount", pendingDamage)
                .Add("hits", pendingHits)
                .Add("hp_after", playerHealth ? playerHealth.Health : -1f)
                .Add("in_vehicle", PimlrInput.PlayerInVehicle));
            pendingDamage = 0f;
            pendingHits = 0;
            pendingSince = -1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            CurrentWeapon = "unarmed";
            LastShotTime = -10f;
            Player = null;
            pendingDamage = 0f; pendingHits = 0; pendingSince = -1f;
        }
    }
}
