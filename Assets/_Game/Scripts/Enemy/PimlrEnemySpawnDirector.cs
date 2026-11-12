using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PIMLR: enemies arrive instead of popping into existence.
///   1. A spawn spot is picked down the street from the player (28-55 m, off-screen preferred, on the NavMesh,
///      at street level). If the scene has an object named "PIMLR_EnemySpawnPoints", its children are used as
///      the candidate spots instead (place them down side streets / around corners).
///   2. A portal opens there with a sound, the enemy steps out of it facing the player, an electric burst plays.
/// Look & feel lives in Assets/_Game/Resources/PimlrSpawnFX.asset.
///
/// Used by: JUAutoInstantiate (Story zone spawners), InfiniteMode waves, GameExecutionManager.SpawnBoss.
/// </summary>
public class PimlrEnemySpawnDirector : MonoBehaviour
{
    public const string AuthoredPointsRootName = "PIMLR_EnemySpawnPoints";
    /// <summary>PIMLR (post-playtest): Infinite Mode spawn distance vs. the PimlrSpawnFX asset (10-55 m -> 8-41 m).</summary>
    public static float InfiniteDistanceScale = 0.75f;

    static PimlrEnemySpawnDirector runner;
    static PimlrSpawnFXSettings settings;
    static bool settingsLoaded;

    readonly List<(Vector3 pos, float time)> recent = new List<(Vector3, float)>();
    Transform player;
    Camera cam;

    static PimlrSpawnFXSettings Settings
    {
        get
        {
            if (!settingsLoaded)
            {
                settingsLoaded = true;
                settings = Resources.Load<PimlrSpawnFXSettings>("PimlrSpawnFX");
                if (!settings)
                {
                    settings = ScriptableObject.CreateInstance<PimlrSpawnFXSettings>();
                    Debug.LogWarning("PIMLR Spawn: Resources/PimlrSpawnFX.asset missing - spawning without effects.");
                }
            }
            return settings;
        }
    }

    static PimlrEnemySpawnDirector Runner
    {
        get
        {
            if (!runner) runner = new GameObject("PIMLR_EnemySpawnDirector").AddComponent<PimlrEnemySpawnDirector>();
            return runner;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { runner = null; settings = null; settingsLoaded = false; recentSfx.Clear(); cutsceneRequest = null; arriving.Clear(); }

    // ---------- PIMLR: Story "first enemy" cutscene ----------
    static string cutsceneRequest;

    /// <summary>The next enemy to arrive (outside Save The Fans, which has its own intro) gets a short cutscene: the
    /// camera flies to its portal, it steps out, the camera flies back and the player gets control again.
    /// Other arrivals wait until the cutscene is over. stage = a label for the log.</summary>
    public static void RequestFirstEnemyCutscene(string stage) { cutsceneRequest = stage; }
    public static void CancelFirstEnemyCutscene() { cutsceneRequest = null; }

    static bool TakeCutsceneRequest()
    {
        if (cutsceneRequest == null) return false;
        var stf = PIMLR.Missions.SaveTheFans.SaveTheFansMission.Instance;
        if (stf && stf.IsRunning) return false; // STF enemies come in through the heat intro instead
        cutsceneRequest = null;
        return true;
    }

    /// <summary>
    /// Bring an already-instantiated enemy in through a portal. The enemy is deactivated until it steps out.
    /// relocate = move it to a spawn spot down the street (false = arrive exactly where it already is).
    /// </summary>
    public static void Arrive(GameObject enemy, bool relocate = true)
    {
        if (!enemy) return;
        PimlrRobotFX.Attach(enemy); // PIMLR: robots - glowing red eyes + electric short-circuit when they die
        enemy.SetActive(false);
        arriving.Add(enemy);
        Runner.StartCoroutine(Runner.ArriveRoutine(enemy, relocate));
    }

    /// <summary>True for things JUAutoInstantiate etc. spawn that should arrive through a portal (AI characters).</summary>
    public static bool IsEnemy(GameObject go)
    {
        return go && !go.CompareTag("Player") && go.GetComponent<JUTPS.CharacterBrain.JUCharacterBrain>() != null;
    }

    IEnumerator ArriveRoutine(GameObject enemy, bool relocate)
    {
        var s = Settings;

        // PIMLR: never arrive behind a loading screen, death sequence, zone card or fade. The enemy stays switched
        // off until the interstitial has played all the way through.
        if (PimlrInterstitial.Playing)
        {
            yield return PimlrInterstitial.WaitUntilClear(0.4f);
            if (!enemy) yield break;
        }
        ResolvePlayer();

        Vector3 spot = enemy.transform.position;
        if (relocate && TryPickSpot(out Vector3 picked)) spot = picked;

        Vector3 toPlayer = player ? player.position - spot : enemy.transform.forward;
        toPlayer.y = 0f;
        Quaternion facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : enemy.transform.rotation;

        // PIMLR: Story stage's first enemy -> camera goes to the portal before it opens.
        PIMLR.Missions.SaveTheFans.MissionCameraDirector cutscene = null;
        bool cutsceneHold = false;
        if (player && TakeCutsceneRequest())
        {
            cutscene = PIMLR.Missions.SaveTheFans.MissionCameraDirector.Shared;
            bool reached = false;
            PimlrInterstitial.Begin(); // everyone else waits for the cutscene
            cutsceneHold = true;
            Runner.StartCoroutine(cutscene.PlayEnemyArrival(spot, player, () => reached = true));
            while (!reached && cutscene.IsPlaying) yield return null;
        }

        try
        {
        // portal + sound
        if (!Spawn(s.portalPrefab, spot + Vector3.up * s.portalHeight, facing, s.portalScale, s.portalLifetime, s))
            PlaySfx(s.portalOpenSfx, spot, s);

        yield return new WaitForSeconds(s.stepThroughDelay);
        if (!enemy) yield break;

        // step out of the portal towards the player
        spot = FindClearSpot(spot, enemy); // PIMLR: never step out inside another enemy (physics pops one onto the other's head)
        enemy.transform.SetPositionAndRotation(spot, facing);
        if (enemy.TryGetComponent(out NavMeshAgent agent) && agent.enabled) agent.Warp(spot);
        arriving.Remove(enemy);
        enemy.SetActive(true);

        // Infinite Mode: every arrival comes for the player.
        // Story: spawned away from the action -> head for the intersection if the player isn't found in 10 s.
        bool infinite = (InfiniteMode.Instance != null && InfiniteMode.Instance.isActiveAndEnabled)
                        || (RunStats.Active && RunStats.Mode == RunMode.Infinite);
        if (PimlrGunnerAI.Qualifies(enemy))
        {
            // Gunners (Big Boss): keep ~2.5 m when fighting, patrol the area otherwise. Story: patrol around the
            // intersection where the action is (or where it arrived). Infinite: patrol around the player's area.
            Vector3 center = TryGetIntersection(out Vector3 gHub) ? gHub : spot;
            PimlrGunnerAI.Attach(enemy, center, infinite);
        }
        else if (infinite)
            PimlrHuntPlayer.Attach(enemy);
        else if (relocate && TryGetIntersection(out Vector3 hub) && (Flat(spot) - Flat(hub)).sqrMagnitude > 20f * 20f)
            PimlrSeekIntersection.Attach(enemy, hub);

        // The burst prefab may carry its own sound; only add ours if it doesn't.
        if (!Spawn(s.arrivalBurstPrefab, spot + Vector3.up * 0.1f, facing, s.burstScale, s.burstLifetime, s))
            PlaySfx(s.arrivalSfx, spot, s);

        // PIMLR: release the other arrivals once the camera is back with the player.
        while (cutscene && cutscene.IsPlaying) yield return null;
        }
        finally
        {
            arriving.Remove(enemy);
            if (cutsceneHold) PimlrInterstitial.End();
        }
    }

    /// <summary>
    /// PIMLR: if another character is standing on (or arriving at) this spot, slide out to the nearest free
    /// NavMesh position on a small ring. Two capsules spawned on the same point get depenetrated straight up,
    /// leaving one enemy standing on the other's head.
    /// </summary>
    public static Vector3 FindClearSpot(Vector3 spot, GameObject self, float clearRadius = 1.1f)
    {
        if (IsClear(spot, self, clearRadius)) return spot;
        float startAng = Random.Range(0f, 360f);
        for (float ring = 1.5f; ring <= 4.5f; ring += 1.5f)
            for (int i = 0; i < 8; i++)
            {
                float a = (startAng + i * 45f) * Mathf.Deg2Rad;
                Vector3 c = spot + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ring;
                if (!NavMesh.SamplePosition(c, out var hit, 1f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - spot.y) > 1f) continue; // stay on the same level (no rooftops/ledges)
                if (IsClear(hit.position, self, clearRadius)) return hit.position;
            }
        return spot;
    }

    static readonly HashSet<GameObject> arriving = new HashSet<GameObject>(); // switched off, waiting to step out

    static readonly Collider[] clearHits = new Collider[16];
    static bool IsClear(Vector3 spot, GameObject self, float radius)
    {
        int n = Physics.OverlapCapsuleNonAlloc(spot + Vector3.up * 0.5f, spot + Vector3.up * 1.4f, radius, clearHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = clearHits[i];
            if (!c || (self && c.transform.IsChildOf(self.transform))) continue;
            if (c.GetComponentInParent<JUTPS.CharacterBrain.JUCharacterBrain>()) return false;
        }
        // Enemies still inside their portal are switched off (no colliders) - check the STF/active roster by position too.
        foreach (var other in arriving)
            if (other && other != self && (Flat(other.transform.position) - Flat(spot)).sqrMagnitude < radius * radius) return false;
        return true;
    }

    // The main intersection = where Sirihanna stands (cached: her object is switched off after the chat),
    // falling back to the Story zombie spawner / Infinite Mode spawner.
    bool hasIntersection; Vector3 intersectionPos;
    bool TryGetIntersection(out Vector3 pos)
    {
        if (!hasIntersection)
        {
            var chat = FindObjectsByType<AiChatInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (chat.Length > 0) { intersectionPos = chat[0].transform.position; hasIntersection = true; }
            else if (GameExecutionManager.Instance && GameExecutionManager.Instance.zombieSpawner)
            { intersectionPos = GameExecutionManager.Instance.zombieSpawner.transform.position; hasIntersection = true; }
            else if (InfiniteMode.Instance) { intersectionPos = InfiniteMode.Instance.transform.position; hasIntersection = true; }
        }
        pos = intersectionPos;
        return hasIntersection;
    }

    void ResolvePlayer()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (!cam || !cam.isActiveAndEnabled) cam = Camera.main;
    }

    // ---------- choosing where they come from ----------

    bool TryPickSpot(out Vector3 spot)
    {
        spot = default;
        if (!player) return false;
        var s = Settings;
        recent.RemoveAll(r => Time.time - r.time > 2.5f);

        var candidates = new List<Vector3>();
        // PIMLR (post-playtest): Infinite Mode spawns a little closer than Story (same settings asset, scaled).
        bool infiniteRun = (InfiniteMode.Instance != null && InfiniteMode.Instance.isActiveAndEnabled)
                           || (RunStats.Active && RunStats.Mode == RunMode.Infinite);
        float distScale = infiniteRun ? InfiniteDistanceScale : 1f;
        float minD = s.minDistanceFromPlayer * distScale, maxD = s.maxDistanceFromPlayer * distScale;
        var authored = GameObject.Find(AuthoredPointsRootName);
        if (authored)
        {
            foreach (Transform t in authored.transform)
                if (t != authored.transform && t.gameObject.activeInHierarchy) candidates.Add(t.position);
        }
        else
        {
            // Ring around the player, snapped to the NavMesh.
            for (int i = 0; i < 28; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(minD, maxD);
                Vector3 p = player.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas)) candidates.Add(hit.position);
            }
        }

        Vector3 best = default; float bestScore = float.MinValue; bool found = false;
        foreach (var c in candidates)
        {
            float d = Vector3.Distance(Flat(c), Flat(player.position));
            if (Mathf.Abs(c.y - player.position.y) > s.maxHeightDifference) continue;
            if (authored && (d < minD * 0.6f || d > maxD * 2f)) continue;
            if (!Reachable(c)) continue;

            float score = Random.Range(0f, 1f);                         // variety
            if (s.preferOffScreen && !VisibleToCamera(c)) score += 2f;  // around the corner / behind you
            foreach (var r in recent) if (Vector3.Distance(r.pos, c) < s.spreadRadius) score -= 3f; // spread out
            if (authored) score -= Mathf.Abs(d - (minD + maxD) * 0.5f) * 0.02f;

            if (score > bestScore) { bestScore = score; best = c; found = true; }
        }

        if (!found) return false;
        recent.Add((best, Time.time));
        spot = best;
        return true;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    NavMeshPath pathCache;
    bool Reachable(Vector3 p)
    {
        if (!NavMesh.SamplePosition(player.position, out var from, 4f, NavMesh.AllAreas)) return true; // can't tell, allow
        pathCache ??= new NavMeshPath();
        return NavMesh.CalculatePath(from.position, p, NavMesh.AllAreas, pathCache) && pathCache.status == NavMeshPathStatus.PathComplete;
    }

    bool VisibleToCamera(Vector3 p)
    {
        if (!cam) return true;
        Vector3 chest = p + Vector3.up * 1.2f;
        Vector3 vp = cam.WorldToViewportPoint(chest);
        if (vp.z <= 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f) return false;
        // in the frustum - is a building in the way?
        return !Physics.Linecast(cam.transform.position, chest, 1 << 0, QueryTriggerInteraction.Ignore);
    }

    // ---------- fx ----------

    /// <summary>Spawns an effect; returns true if it brought its own (now 3D) sound.</summary>
    static bool Spawn(GameObject prefab, Vector3 pos, Quaternion yaw, float scale, float lifetime, PimlrSpawnFXSettings s)
    {
        if (!prefab) return false;
        var fx = Instantiate(prefab, pos, yaw * prefab.transform.rotation);
        fx.transform.localScale = prefab.transform.localScale * scale;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        Runner.StartCoroutine(StopAndDestroy(fx, lifetime));

        // ETFX effects ship with 2D AudioSources (heard "on the camera" at full volume from anywhere).
        // Make them positional so they fall off with distance like everything else in the street.
        var sources = fx.GetComponentsInChildren<AudioSource>(true);
        foreach (var src in sources) Make3D(src, s);
        return sources.Length > 0;
    }

    static readonly List<float> recentSfx = new List<float>();

    // Positional, and quieter when several enemies arrive at once so a wave doesn't stack into a wall of sound.
    static void Make3D(AudioSource src, PimlrSpawnFXSettings s)
    {
        recentSfx.RemoveAll(t => Time.time - t > 0.4f);
        float crowd = 1f / (1f + recentSfx.Count * 0.6f);
        recentSfx.Add(Time.time);

        src.spatialBlend = 1f;
        // PIMLR: full volume within sfxFullVolumeRadius (~5 m), then a steep drop, so a portal down the street sounds
        // down the street (the old linear 4-60 m rolloff was still at ~55% volume 30 m away).
        src.rolloffMode = AudioRolloffMode.Custom;
        src.minDistance = 1f;
        src.maxDistance = s.sfxMaxDistance;
        src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, RolloffCurve(s));
        src.dopplerLevel = 0f;
        src.spread = 0f;
        src.volume *= s.sfxVolume * crowd;
        if (src.isPlaying) { src.Stop(); src.Play(); } // played on Awake as 2D - restart with the new settings
    }

    static AnimationCurve rolloffCurve;
    static float rolloffKey = -1f;

    /// <summary>Volume vs. distance (normalised to maxDistance): 1 inside the full-volume radius, then (r/d)^power,
    /// fading to 0 at maxDistance.</summary>
    static AnimationCurve RolloffCurve(PimlrSpawnFXSettings s)
    {
        float key = s.sfxFullVolumeRadius * 1000f + s.sfxFalloffPower + s.sfxMaxDistance * 0.001f;
        if (rolloffCurve != null && Mathf.Approximately(key, rolloffKey)) return rolloffCurve;
        rolloffKey = key;
        float max = Mathf.Max(1f, s.sfxMaxDistance), r = Mathf.Clamp(s.sfxFullVolumeRadius, 0.5f, max);
        var keys = new System.Collections.Generic.List<Keyframe> { new Keyframe(0f, 1f), new Keyframe(r / max, 1f) };
        for (int i = 1; i <= 16; i++)
        {
            float d = Mathf.Lerp(r, max, i / 16f);
            float v = Mathf.Pow(r / d, s.sfxFalloffPower);
            v *= Mathf.Clamp01((max - d) / (max * 0.15f)); // ease to silence over the last 15%
            keys.Add(new Keyframe(d / max, v));
        }
        rolloffCurve = new AnimationCurve(keys.ToArray());
        for (int i = 2; i < rolloffCurve.length; i++) rolloffCurve.SmoothTangents(i, 0f);
        // Flat (no overshoot above 1) across the full-volume radius.
        for (int i = 0; i < 2; i++) { var k = rolloffCurve[i]; k.inTangent = k.outTangent = 0f; rolloffCurve.MoveKey(i, k); }
        return rolloffCurve;
    }

    static IEnumerator StopAndDestroy(GameObject fx, float lifetime)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, lifetime - 0.8f));
        if (!fx) yield break;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (var l in fx.GetComponentsInChildren<Light>()) l.enabled = false;
        Destroy(fx, 0.8f);
    }

    static void PlaySfx(AudioClip clip, Vector3 pos, PimlrSpawnFXSettings s)
    {
        if (!clip) return;
        var go = new GameObject("PIMLR_SpawnSfx");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = 1f;
        Make3D(src, s);
        src.Play();
        Destroy(go, clip.length + 0.2f);
    }
}
