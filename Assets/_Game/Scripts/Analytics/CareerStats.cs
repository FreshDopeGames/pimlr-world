using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// PIMLR #ANL: lifetime totals per mode and per artist, in persistentDataPath/career.json.
// Updated once at each run_end from RunBreakdown (never by re-reading the .jsonl logs).
// Not gated by analytics consent: this is the player's own stats screen and never leaves the machine.
//
// Read it with CareerStats.Load(), then .Total(), .ForMode(RunMode.Infinite) or .ForArtist("thurz").
namespace PIMLR.Analytics
{
    [Serializable]
    public class CareerRow
    {
        public string mode;
        public string artist;
        public int runs;
        public int completed;
        public int kills;
        public int bossKills;
        public int coins;
        public int deaths;
        public int shots;
        public int hits;
        public float damageDealt;
        public float damageTaken;
        public float secondsPlayed;
        public float secondsInVehicle;
        public int bestWave;
        public float bestCompletedTime; // 0 = none yet

        public float Accuracy => shots > 0 ? Mathf.Clamp01((float)hits / shots) : 0f;

        public void Add(CareerRow o)
        {
            runs += o.runs; completed += o.completed; kills += o.kills; bossKills += o.bossKills; coins += o.coins;
            deaths += o.deaths; shots += o.shots; hits += o.hits; damageDealt += o.damageDealt; damageTaken += o.damageTaken;
            secondsPlayed += o.secondsPlayed; secondsInVehicle += o.secondsInVehicle;
            bestWave = Mathf.Max(bestWave, o.bestWave);
            if (o.bestCompletedTime > 0f && (bestCompletedTime <= 0f || o.bestCompletedTime < bestCompletedTime))
                bestCompletedTime = o.bestCompletedTime;
        }
    }

    [Serializable]
    public class CareerStats
    {
        public int version = 1;
        public List<CareerRow> rows = new List<CareerRow>();

        public static string FilePath => Path.Combine(Application.persistentDataPath, "career.json");
        public static event Action<CareerStats> Updated;

        public static CareerStats Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<CareerStats>(File.ReadAllText(FilePath)) ?? new CareerStats();
            }
            catch (Exception e) { Debug.LogWarning("PIMLR Career: could not read career.json: " + e.Message); }
            return new CareerStats();
        }

        public void Save()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(this, true)); }
            catch (Exception e) { Debug.LogWarning("PIMLR Career: could not write career.json: " + e.Message); }
        }

        /// <summary>Deletes career.json. Call from Reset Progress.</summary>
        public static void Wipe()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
            Updated?.Invoke(new CareerStats());
        }

        public CareerRow Total() => Sum(r => true);
        public CareerRow ForMode(RunMode mode) => Sum(r => r.mode == mode.ToString());
        public CareerRow ForArtist(string artist) => Sum(r => r.artist == artist);

        CareerRow Sum(Predicate<CareerRow> match)
        {
            var total = new CareerRow { mode = "*", artist = "*" };
            foreach (var r in rows) if (match(r)) total.Add(r);
            return total;
        }

        internal static void Record(RunBreakdown b)
        {
            if (b == null || b.final.endReason == RunEndReason.Abandoned) return;

            var career = Load();
            string mode = b.mode.ToString();
            var row = career.rows.Find(r => r.mode == mode && r.artist == b.artist);
            if (row == null) career.rows.Add(row = new CareerRow { mode = mode, artist = b.artist });

            bool done = b.final.endReason == RunEndReason.Completed;
            row.Add(new CareerRow
            {
                runs = 1,
                completed = done ? 1 : 0,
                kills = b.final.kills,
                bossKills = b.final.bossKills,
                coins = b.final.coinsEarned,
                deaths = b.deaths,
                shots = b.TotalShots,
                hits = b.TotalHits,
                damageDealt = b.damageDealt,
                damageTaken = b.damageTaken,
                secondsPlayed = b.final.elapsed,
                secondsInVehicle = b.secondsInVehicle,
                bestWave = b.bestWave,
                bestCompletedTime = done ? b.final.elapsed : 0f
            });
            career.Save();
            Updated?.Invoke(career);
        }
    }
}
