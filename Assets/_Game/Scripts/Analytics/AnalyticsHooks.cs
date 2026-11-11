using System.Collections.Generic;
using JUTPS;
using JUTPS.WeaponSystem; // PIMLR #STATS #ANL
using PIMLR.Missions.SaveTheFans;
using UnityEngine;
using UnityEngine.SceneManagement;

// PIMLR #ANL: turns existing game signals into analytics events, so gameplay code needs almost no new calls.
//   RunStats.Changed / Ended     -> run_start, run_end, wave_start, wave_end, weapon_wave_summary, stage_start, stage_end
//   CoinManager.coinValueChanged -> coin_earn, coin_spend (by balance delta)
//   The player (found by tag)    -> shots per weapon (magazine count dropping), player_death, pos every 2 s,
//                                   foot vs vehicle time
//   SaveTheFansMission events    -> fan_heat_start, fan_heat_end, fan_travel, fan_hype_used, wave_start, wave_end
//   PimlrInput.SchemeChanged     -> input_scheme_change
// Lives on the PIMLR_Analytics object that AnalyticsWriter creates.
namespace PIMLR.Analytics
{
    public class AnalyticsHooks : MonoBehaviour
    {
        const float PosInterval = 2f;

        // run state
        bool runOpen;
        RunSnapshot prev;

        // coins
        CoinManager boundCoins;
        int lastCoins = int.MinValue;

        // player
        JUCharacterController player;
        JUHealth playerHealth;
        Weapon trackedWeapon;
        int trackedAmmo;
        float nextPos;

        // Save The Fans
        SaveTheFansMission boundStf;
        float stfIntermissionAt = -1f;
        bool stfTravelUsedVehicle;
        float stfHeatStartAt = -1f;
        float stfPeakWash;
        int stfHeatIndex = -1;

        void OnEnable()
        {
            RunStats.Changed += OnRunChanged;
            RunStats.Ended += OnRunEnded;
            PimlrInput.SchemeChanged += OnSchemeChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            RunStats.Changed -= OnRunChanged;
            RunStats.Ended -= OnRunEnded;
            PimlrInput.SchemeChanged -= OnSchemeChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindStf();
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (m != LoadSceneMode.Single) return;
            player = null;
            playerHealth = null;
            trackedWeapon = null;
        }

        // ------------------------------------------------------------------ runs

        void OnRunChanged(RunSnapshot s)
        {
            if (!s.active) return;

            if (!runOpen)
            {
                runOpen = true;
                PimlrAnalytics.BeginRun();
                RunBreakdown.Begin(s.mode);
                PimlrAnalytics.Log("run_start", new AnalyticsData().Add("run_mode", s.mode).Add("start_zone", s.startZone));
                if (s.mode == RunMode.Infinite) PimlrAnalytics.Log("wave_start", AnalyticsData.With("wave", s.wave));
                else PimlrAnalytics.Log("stage_start", AnalyticsData.With("zone", s.zone));
                prev = s;
                return;
            }

            if (s.mode == RunMode.Infinite && s.wave != prev.wave)
            {
                EndWave(prev.wave, prev);
                PimlrAnalytics.Log("wave_start", AnalyticsData.With("wave", s.wave));
                if (RunBreakdown.Current != null) RunBreakdown.Current.bestWave = Mathf.Max(RunBreakdown.Current.bestWave, s.wave);
            }

            if (s.mode == RunMode.Story && s.zone != prev.zone)
            {
                WeaponWaveSummary("stage", prev.zone.ToString());
                PimlrAnalytics.Log("stage_end", new AnalyticsData().Add("zone", prev.zone).Add("elapsed", s.elapsed));
                PimlrAnalytics.Log("stage_start", AnalyticsData.With("zone", s.zone));
            }

            prev = s;
        }

        void EndWave(int wave, RunSnapshot s)
        {
            WeaponWaveSummary("wave", wave.ToString());
            PimlrAnalytics.Log("wave_end", new AnalyticsData().Add("wave", wave).Add("kills_total", s.kills).Add("elapsed", s.elapsed));
        }

        void WeaponWaveSummary(string kind, string id)
        {
            var r = RunBreakdown.Current;
            if (r == null) return;
            foreach (var kv in r.waveWeapons)
            {
                if (kv.Value.shots == 0 && kv.Value.hits == 0) continue;
                PimlrAnalytics.Log("weapon_wave_summary", new AnalyticsData()
                    .Add(kind, id)
                    .Add("weapon", kv.Key)
                    .Add("shots", kv.Value.shots)
                    .Add("hits", kv.Value.hits)
                    .Add("accuracy", kv.Value.Accuracy)
                    .Add("damage", kv.Value.damage));
            }
            r.waveWeapons.Clear();
        }

        void OnRunEnded(RunSnapshot s)
        {
            if (!runOpen) return;
            runOpen = false;

            if (s.mode == RunMode.Infinite) EndWave(s.wave, s);
            else WeaponWaveSummary("stage", s.zone.ToString());

            var r = RunBreakdown.Finish(s);
            var d = new AnalyticsData()
                .Add("run_mode", s.mode)
                .Add("reason", s.endReason)
                .Add("duration", s.elapsed)
                .Add("zone", s.zone)
                .Add("wave", s.wave)
                .Add("kills", s.kills)
                .Add("boss_kills", s.bossKills)
                .Add("coins", s.coinsEarned);
            if (r != null)
            {
                d.Add("shots", r.TotalShots).Add("hits", r.TotalHits).Add("accuracy", r.Accuracy)
                 .Add("damage_dealt", r.damageDealt).Add("damage_taken", r.damageTaken)
                 .Add("foot_s", r.secondsOnFoot).Add("vehicle_s", r.secondsInVehicle)
                 .Add("deaths", r.deaths).Add("kills_by_type", r.killsByType);
                CareerStats.Record(r);
            }
            PimlrAnalytics.Log("run_end", d);
            PimlrAnalytics.EndRun();
            AnalyticsWriter.FlushNow();
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            BindCoins();
            BindPlayer();
            BindStf();

            if (player != null)
            {
                TrackShots();
                TrackMovement();
                CombatTelemetry.Tick(playerHealth);
            }

            if (boundStf != null) TrackStf();
        }

        void BindCoins()
        {
            var cm = CoinManager.Instance;
            if (cm == boundCoins) return;
            if (boundCoins != null) boundCoins.coinValueChanged -= OnCoins;
            boundCoins = cm;
            if (cm != null)
            {
                cm.coinValueChanged += OnCoins;
                lastCoins = cm.GetCoins();
            }
        }

        void OnCoins(int balance)
        {
            if (lastCoins == int.MinValue) { lastCoins = balance; return; }
            int delta = balance - lastCoins;
            lastCoins = balance;
            if (delta == 0) return;
            // A Reset Progress (to 0) shows as a spend with source "reset" so it can be filtered out.
            string ev = delta > 0 ? "coin_earn" : "coin_spend";
            string source = delta > 0 ? (RunStats.Active ? "run" : "other") : (balance == 0 && !RunStats.Active ? "reset_or_spend" : "spend");
            PimlrAnalytics.Log(ev, new AnalyticsData().Add("amount", Mathf.Abs(delta)).Add("source", source).Add("balance", balance));
        }

        void BindPlayer()
        {
            if (player != null) return;
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            player = go.GetComponent<JUCharacterController>();
            if (player == null) return;
            playerHealth = go.GetComponent<JUHealth>();
            if (playerHealth != null) playerHealth.OnDeath.AddListener(OnPlayerDeath);
            CombatTelemetry.Player = go.transform;
            trackedWeapon = null;
            nextPos = Time.time;
        }

        void TrackShots()
        {
            Weapon w = player.WeaponInUseRightHand;
            if (w != trackedWeapon)
            {
                trackedWeapon = w;
                trackedAmmo = w != null ? w.BulletsAmounts : 0;
                CombatTelemetry.CurrentWeapon = w != null ? WeaponName(w) : "unarmed";
                return;
            }
            if (w == null) return;

            int ammo = w.BulletsAmounts;
            if (ammo < trackedAmmo)
            {
                int shots = trackedAmmo - ammo;
                CombatTelemetry.LastShotTime = Time.time;
                RunBreakdown.Current?.AddShots(CombatTelemetry.CurrentWeapon, shots);
            }
            trackedAmmo = ammo; // a reload raises it; that's not a shot
        }

        static string WeaponName(Weapon w) =>
            !string.IsNullOrEmpty(w.ItemName) ? w.ItemName : CombatTelemetry.CleanName(w.gameObject.name);

        void TrackMovement()
        {
            bool inVehicle = player.IsDriving || PimlrInput.PlayerInVehicle;
            var r = RunBreakdown.Current;
            if (r != null && !player.IsDead)
            {
                if (inVehicle) r.secondsInVehicle += Time.deltaTime;
                else r.secondsOnFoot += Time.deltaTime;
            }

            if (Time.time >= nextPos && (RunStats.Active || boundStf != null))
            {
                nextPos = Time.time + PosInterval;
                Vector3 p = player.transform.position;
                PimlrAnalytics.Log("pos", new AnalyticsData()
                    .Add("x", Mathf.Round(p.x * 2f) / 2f)
                    .Add("z", Mathf.Round(p.z * 2f) / 2f)
                    .Add("in_vehicle", inVehicle));
            }
        }

        void OnPlayerDeath()
        {
            if (RunBreakdown.Current != null) RunBreakdown.Current.deaths++;
            Vector3 p = player != null ? player.transform.position : Vector3.zero;
            PimlrAnalytics.Log("player_death", new AnalyticsData()
                .Add("x", Mathf.Round(p.x * 2f) / 2f)
                .Add("z", Mathf.Round(p.z * 2f) / 2f)
                .Add("weapon", CombatTelemetry.CurrentWeapon)
                .Add("in_vehicle", PimlrInput.PlayerInVehicle));
            AnalyticsWriter.FlushNow();
        }

        void OnSchemeChanged() =>
            PimlrAnalytics.Log("input_scheme_change", AnalyticsData.With("scheme", PimlrInput.UsingGamepad ? "Gamepad" : "KeyboardMouse"));

        // ------------------------------------------------------------------ Save The Fans

        void BindStf()
        {
            var m = SaveTheFansMission.Instance;
            if (m == boundStf) return;
            UnbindStf();
            boundStf = m;
            if (m == null) return;
            m.onMissionStarted.AddListener(StfMissionStarted);
            m.onIntermissionStarted.AddListener(StfIntermission);
            m.onHeatStarted.AddListener(StfHeatStarted);
            m.onWaveStarted.AddListener(StfWaveStarted);
            m.onWaveCleared.AddListener(StfWaveCleared);
            m.onHeatSaved.AddListener(StfHeatSaved);
            m.onHeatFailed.AddListener(StfHeatFailed);
            m.onMissionCompleted.AddListener(StfMissionCompleted);
            m.onBoostStarted.AddListener(StfBoost);
        }

        void UnbindStf()
        {
            if (boundStf == null) return;
            boundStf.onMissionStarted.RemoveListener(StfMissionStarted);
            boundStf.onIntermissionStarted.RemoveListener(StfIntermission);
            boundStf.onHeatStarted.RemoveListener(StfHeatStarted);
            boundStf.onWaveStarted.RemoveListener(StfWaveStarted);
            boundStf.onWaveCleared.RemoveListener(StfWaveCleared);
            boundStf.onHeatSaved.RemoveListener(StfHeatSaved);
            boundStf.onHeatFailed.RemoveListener(StfHeatFailed);
            boundStf.onMissionCompleted.RemoveListener(StfMissionCompleted);
            boundStf.onBoostStarted.RemoveListener(StfBoost);
            boundStf = null;
        }

        string FanName(int heat)
        {
            var fan = boundStf != null ? boundStf.CurrentFan : null;
            return fan != null ? CombatTelemetry.CleanName(fan.name) : "heat_" + heat;
        }

        void TrackStf()
        {
            if (stfIntermissionAt >= 0f && PimlrInput.PlayerInVehicle) stfTravelUsedVehicle = true;
            if (stfHeatStartAt >= 0f && boundStf.CurrentFan != null)
                stfPeakWash = Mathf.Max(stfPeakWash, boundStf.CurrentFan.Wash01);
        }

        void StfMissionStarted() => PimlrAnalytics.Log("mission_start", AnalyticsData.With("mission", "save_the_fans"));
        void StfMissionCompleted() => PimlrAnalytics.Log("mission_end", new AnalyticsData().Add("mission", "save_the_fans").Add("result", "completed"));

        void StfIntermission(int heat)
        {
            stfIntermissionAt = Time.time;
            stfTravelUsedVehicle = PimlrInput.PlayerInVehicle;
        }

        void StfHeatStarted(int heat)
        {
            if (stfIntermissionAt >= 0f)
            {
                PimlrAnalytics.Log("fan_travel", new AnalyticsData()
                    .Add("heat", heat)
                    .Add("fan", FanName(heat))
                    .Add("travel_s", Time.time - stfIntermissionAt)
                    .Add("vehicle_used", stfTravelUsedVehicle));
                stfIntermissionAt = -1f;
            }
            stfHeatIndex = heat;
            stfHeatStartAt = Time.time;
            stfPeakWash = 0f;
            PimlrAnalytics.Log("fan_heat_start", new AnalyticsData().Add("heat", heat).Add("fan", FanName(heat)));
        }

        void StfWaveStarted(int heat, int wave) =>
            PimlrAnalytics.Log("wave_start", new AnalyticsData().Add("heat", heat).Add("wave", wave));

        void StfWaveCleared(int heat, int wave)
        {
            WeaponWaveSummary("wave", heat + "." + wave);
            PimlrAnalytics.Log("wave_end", new AnalyticsData().Add("heat", heat).Add("wave", wave));
        }

        void StfHeatSaved(int heat) => StfHeatEnd(heat, "saved");
        void StfHeatFailed(int heat) => StfHeatEnd(heat, "washed");

        void StfHeatEnd(int heat, string result)
        {
            PimlrAnalytics.Log("fan_heat_end", new AnalyticsData()
                .Add("heat", heat)
                .Add("fan", FanName(heat))
                .Add("result", result)
                .Add("peak_wash", stfPeakWash)
                .Add("duration", stfHeatStartAt >= 0f ? Time.time - stfHeatStartAt : 0f));
            stfHeatStartAt = -1f;
            AnalyticsWriter.FlushNow();
        }

        void StfBoost() =>
            PimlrAnalytics.Log("fan_hype_used", new AnalyticsData().Add("heat", stfHeatIndex).Add("fan", FanName(stfHeatIndex)));
    }
}
