// PIMLR #STF (Save The Fans) — wave composition for one fan's heat.
// One asset per fan (e.g. STF_Waves_Fan1, STF_Waves_Fan2...). Each asset is a list of
// increasingly numerous waves. Tune numbers here, never in code.
//
// Unreal note: a UPrimaryDataAsset with a TArray<FFanWave> (USTRUCT), or a Data Table
// per fan. Prefab references become TSubclassOf<ACharacter> / soft class pointers.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PIMLR.Missions.SaveTheFans
{
    [Serializable]
    public class FanSpawnEntry
    {
        [Tooltip("Optional label for your own reference, e.g. 'Thug'.")]
        public string label = "Thug";

        [Tooltip("Enemy prefab with JUHealth + a JUTPS AI brain on its root: PIMLR_Melee, PIMLR_Gunner or PIMLR_Bomber from Assets/_Game/Prefabs/Enemies. No variants needed.")]
        public GameObject prefab;

        [Min(0)] public int count = 3;

        [Tooltip("Targeting rules for this enemy type. If empty, the mission's default profile is used.")]
        public FanAggroProfile aggroProfile;
    }

    [Serializable]
    public class FanWave
    {
        public string name = "Wave";

        [Tooltip("Seconds of breathing room before this wave starts spawning (restock time).")]
        [Min(0f)] public float preWaveDelay = 4f;

        [Tooltip("Seconds between individual spawns.")]
        [Min(0.05f)] public float spawnInterval = 0.75f;

        [Tooltip("Never more than this many of this wave alive at once. Keeps WebGL frame rate sane and gives the player a fighting chance.")]
        [Min(1)] public int maxAlive = 6;

        [Tooltip("Mix enemy types as they spawn (Thug, Shooter, Thug, Shooter...) instead of spawning all of one type first.")]
        public bool interleaveTypes = true;

        public List<FanSpawnEntry> entries = new List<FanSpawnEntry>();

        public int TotalCount
        {
            get
            {
                int n = 0;
                foreach (var e in entries) if (e != null && e.prefab) n += Mathf.Max(0, e.count);
                return n;
            }
        }
    }

    [CreateAssetMenu(fileName = "STF_Waves_", menuName = "PIMLR/Save The Fans/Wave Set")]
    public class FanWaveSet : ScriptableObject
    {
        public List<FanWave> waves = new List<FanWave>();

        [Header("Heat tuning")]
        [Tooltip("Multiplies every enemy's wash rate in this heat. >1 makes later fans more fragile.")]
        [Min(0f)] public float washRateMultiplier = 1f;

        [Tooltip("PIMLR Coins granted when this fan is saved.")]
        [Min(0)] public int coinReward = 100;

        [Tooltip("Fan recovers this many wash points between waves (a little relief, not a full reset).")]
        [Min(0f)] public float recoveryBetweenWaves = 25f;

        /// Builds the spawn order for a wave (interleaved or grouped).
        public static List<FanSpawnEntry> BuildSpawnQueue(FanWave wave)
        {
            var queue = new List<FanSpawnEntry>();
            if (wave == null) return queue;

            if (!wave.interleaveTypes)
            {
                foreach (var e in wave.entries)
                    if (e != null && e.prefab) for (int i = 0; i < e.count; i++) queue.Add(e);
                return queue;
            }

            var remaining = new List<int>();
            foreach (var e in wave.entries) remaining.Add(e != null && e.prefab ? Mathf.Max(0, e.count) : 0);

            bool any = true;
            while (any)
            {
                any = false;
                for (int i = 0; i < wave.entries.Count; i++)
                {
                    if (remaining[i] <= 0) continue;
                    queue.Add(wave.entries[i]);
                    remaining[i]--;
                    any = true;
                }
            }
            return queue;
        }
    }
}
