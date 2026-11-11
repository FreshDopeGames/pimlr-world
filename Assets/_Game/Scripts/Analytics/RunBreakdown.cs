using System.Collections.Generic;
using UnityEngine;

// PIMLR #ANL: per-run totals for the post-run breakdown page and career stats.
// Filled by AnalyticsHooks / CombatTelemetry from the same signals that feed the event stream.
// It counts whether or not analytics consent is on, because it's the player's own stats screen, not telemetry.
namespace PIMLR.Analytics
{
    public class WeaponTally
    {
        public int shots;
        public int hits;
        public float damage;
        public float Accuracy => shots > 0 ? Mathf.Clamp01((float)hits / shots) : 0f;
    }

    public class RunBreakdown
    {
        /// <summary>The run in progress (null between runs).</summary>
        public static RunBreakdown Current { get; private set; }
        /// <summary>The last finished run, for RunSummaryPanel's second page.</summary>
        public static RunBreakdown Last { get; private set; }

        public RunMode mode;
        public string artist;
        public readonly Dictionary<string, WeaponTally> weapons = new Dictionary<string, WeaponTally>();
        public readonly Dictionary<string, int> killsByType = new Dictionary<string, int>();
        public float damageDealt;
        public float damageTaken;
        public float secondsOnFoot;
        public float secondsInVehicle;
        public int deaths;
        public int bestWave;
        public RunSnapshot final;

        // reset at each wave/stage boundary for weapon_wave_summary
        public readonly Dictionary<string, WeaponTally> waveWeapons = new Dictionary<string, WeaponTally>();

        public int TotalShots { get { int n = 0; foreach (var w in weapons.Values) n += w.shots; return n; } }
        public int TotalHits { get { int n = 0; foreach (var w in weapons.Values) n += w.hits; return n; } }
        public float Accuracy => TotalShots > 0 ? Mathf.Clamp01((float)TotalHits / TotalShots) : 0f;

        internal static void Begin(RunMode mode)
        {
            Current = new RunBreakdown { mode = mode, artist = PimlrAnalytics.Artist };
        }

        internal static RunBreakdown Finish(RunSnapshot snapshot)
        {
            if (Current == null) return null;
            Current.final = snapshot;
            Current.bestWave = Mathf.Max(Current.bestWave, snapshot.wave);
            Last = Current;
            Current = null;
            return Last;
        }

        internal static WeaponTally Tally(Dictionary<string, WeaponTally> map, string weapon)
        {
            if (!map.TryGetValue(weapon, out var t)) map[weapon] = t = new WeaponTally();
            return t;
        }

        internal void AddShots(string weapon, int n)
        {
            Tally(weapons, weapon).shots += n;
            Tally(waveWeapons, weapon).shots += n;
        }

        internal void AddHit(string weapon, float damage)
        {
            var a = Tally(weapons, weapon); a.hits++; a.damage += damage;
            var b = Tally(waveWeapons, weapon); b.hits++; b.damage += damage;
            damageDealt += damage;
        }

        internal void AddKill(string enemyType)
        {
            killsByType.TryGetValue(enemyType, out int n);
            killsByType[enemyType] = n + 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Last = null; }
    }
}
