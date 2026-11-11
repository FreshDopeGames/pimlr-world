using JUTPS;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteMode : Singleton<InfiniteMode>
{







    [SerializeField] internal int currentWave = 1;
   [SerializeField] private int normalEnemyCount = 5;
    [SerializeField] private int bossCount = 0;

    public JUHealth normalEnemyPrefab, bossEnemyPrefab;

    // PIMLR (post-playtest): bomber haters (grenadiers) take every 3rd wave: 3, then +3 each time.
    // Leave empty to use the Story Level 3 bomber (GameExecutionManager.waterBallonzombie).
    [Tooltip("PIMLR: grenadier (bomber hater) prefab for every 3rd wave. Empty = the Story bomber prefab.")]
    public JUHealth grenadierEnemyPrefab;
    [SerializeField] private int grenadierCount = 3;
    public const int GrenadierCountStep = 3;

    enum WaveKind { Normal, Boss, Grenadier }
    // Wave 3, 6, 9... = grenadiers; otherwise odd = melee haters, even = bosses (as before).
    static WaveKind KindOf(int wave) => wave % 3 == 0 ? WaveKind.Grenadier : (wave % 2 == 1 ? WaveKind.Normal : WaveKind.Boss);
    int TargetCount() => KindOf(currentWave) == WaveKind.Grenadier ? grenadierCount
                       : KindOf(currentWave) == WaveKind.Normal ? normalEnemyCount : bossCount;
    // Each kind grows only on its own later waves (the first wave of each kind uses the starting count).
    bool seenNormal, seenGrenadier;

    public Transform normalEnemiesHolder;
    public Transform bossEnemiesHolder;


    public Vector3 SpawnArea;


   public int currentEnemyCount;

    // PIMLR (playtest): track the player's death event and prevent duplicate run submission.
    private JUHealth playerHealth;
    private bool runEnded;

    void Start()
    {
        // PIMLR (post-playtest): random start spot each run (PIMLR_InfiniteStartPoints in the scene; none = as before).
        PimlrInfiniteStart.Apply(this);
        GrowCountForNewWave(); // PIMLR: registers wave 1's kind (no growth on the first wave of a kind)
        StartCoroutine(SpawnWave());
        SceneManagerScript.Instance.goalPanel.gameObject.SetActive(false);

        // PIMLR (playtest): start and initialize the Infinite Mode run snapshot.
        RunStats.Begin(RunMode.Infinite, Zone.InfiniteMode);
        RunStats.SetWave(currentWave);

        GameExecutionManager gameExecutionManager = GameExecutionManager.Instance;
        if (gameExecutionManager != null &&
            gameExecutionManager.playerHandler != null &&
            gameExecutionManager.playerHandler.jUCharacterController != null)
        {
            playerHealth = gameExecutionManager.playerHandler.jUCharacterController.CharacterHealth;
        }

        if (playerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerHealth = player.GetComponentInChildren<JUHealth>(true);
        }

        if (playerHealth != null)
            playerHealth.OnDeath.AddListener(HandlePlayerDeath);
        else
            Debug.LogWarning("InfiniteMode: could not find the player's JUHealth component.", this);
    }

    // PIMLR (playtest): delay death submission so the existing death presentation can finish.
    private void HandlePlayerDeath()
    {
        if (!runEnded)
            StartCoroutine(EndRunAfterDeath());
    }

    // PIMLR (playtest): use realtime so death cleanup still completes while gameplay is paused.
    private IEnumerator EndRunAfterDeath()
    {
        yield return new WaitForSecondsRealtime(2f);
        // PIMLR: let the "WASHED" death sequence finish before the run summary opens on top of it.
        while (PimlrDeathSequence.IsPlaying)
            yield return null;
        EndRun(RunEndReason.Died);
    }

    // PIMLR (playtest): stop the run once and submit its final snapshot when appropriate.
    public void EndRun(RunEndReason reason)
    {
        if (runEnded)
            return;

        runEnded = true;
        StopAllCoroutines();
        RunSnapshot final = RunStats.End(reason);
        if (reason != RunEndReason.Abandoned)
            LeaderboardManager.TrySubmit(final);
    }

    // PIMLR (playtest): expose a parameterless method for Inspector UnityEvents.
    public void QuitRun()
    {
        EndRun(RunEndReason.Quit);
    }

    // PIMLR (playtest): detach the health listener and mark destroyed active runs abandoned.
    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnDeath.RemoveListener(HandlePlayerDeath);

        if (RunStats.Active && RunStats.Mode == RunMode.Infinite)
            RunStats.End(RunEndReason.Abandoned);
    }

    public void CompleteWave()
    {
        CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() + 50 * currentWave);
        // PIMLR (playtest): keep run stats aligned with the existing wave reward and progression.
        RunStats.AddCoins(50 * currentWave);
        currentEnemyCount = 0;
        currentWave++;
        RunStats.SetWave(currentWave);
        GrowCountForNewWave();

        // PIMLR (post-playtest): "WAVE CLEAR!" slow-mo card (same look as WASHED / HATER FREE!), then a short
        // breather before the next wave's card. Skipped if the player died on the same kill.
        if (PimlrDeathSequence.IsPlaying || runEnded)
        {
            StartCoroutine(SpawnWave());
            return;
        }
        PimlrVictorySequence.Play(WaveClearWord, WaveClearColor, () =>
        {
            if (!runEnded && this != null) StartCoroutine(SpawnWave(WaveClearBreather));
        }, WaveClearHoldSeconds);
    }

    // PIMLR (post-playtest): WAVE CLEAR card tuning.
    public static string WaveClearWord = "WAVE CLEAR!";
    public static Color WaveClearColor = new Color(1f, 0.82f, 0.12f, 1f); // PIMLR yellow
    public static float WaveClearHoldSeconds = 1.5f;  // slow-mo hold (HATER FREE! holds 3 s)
    public static float WaveClearBreather = 1.5f;     // seconds after the card before the next wave starts

    // PIMLR (post-playtest): grow the count for the kind of wave that's starting - melee +5 and grenadiers +3
    // (not on their first wave), bosses +1 every boss wave (unchanged).
    void GrowCountForNewWave()
    {
        switch (KindOf(currentWave))
        {
            case WaveKind.Normal: if (seenNormal) normalEnemyCount += 5; seenNormal = true; break;
            case WaveKind.Boss: bossCount += 1; break; // as before: every boss wave adds one (first = 1)
            case WaveKind.Grenadier: if (seenGrenadier) grenadierCount += GrenadierCountStep; seenGrenadier = true; break;
        }
    }

    JUHealth GrenadierPrefab()
    {
        if (grenadierEnemyPrefab != null) return grenadierEnemyPrefab;
        var gem = GameExecutionManager.Instance;
        return gem != null && gem.waterBallonzombie != null ? gem.waterBallonzombie.GetComponent<JUHealth>() : null;
    }

    IEnumerator SpawnWave(float delay = 3.5f)
    {
        yield return new WaitForSeconds(delay);
        currentEnemyCount = 0;
        Debug.Log(currentWave % 2);
        yield return StartCoroutine(GameExecutionManager.Instance.WaitForscreenfadeOut("0/" + TargetCount()));
     
        SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo(currentEnemyCount.ToString() + "/" + TargetCount());
       
        Vector3 randomPosOnArea = transform.position;
        randomPosOnArea.x += Random.Range(-SpawnArea.x, SpawnArea.x);
        randomPosOnArea.y += Random.Range(-SpawnArea.y, SpawnArea.y);
        randomPosOnArea.z += Random.Range(-SpawnArea.z, SpawnArea.z);

        if (KindOf(currentWave) == WaveKind.Grenadier)
        {
            JUHealth prefab = GrenadierPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("InfiniteMode: no grenadier prefab - skipping the grenadier wave.", this);
                CompleteWave();
                yield break;
            }
            for (int i = 0; i < grenadierCount; i++)
            {
                randomPosOnArea = transform.position;
                randomPosOnArea.x += Random.Range(-SpawnArea.x, SpawnArea.x);
                randomPosOnArea.y += Random.Range(-SpawnArea.y, SpawnArea.y);
                randomPosOnArea.z += Random.Range(-SpawnArea.z, SpawnArea.z);

                JUHealth enemy = Instantiate(prefab, normalEnemiesHolder);
                enemy.transform.position = randomPosOnArea;
                enemy.transform.rotation = Quaternion.Euler(0, Random.Range(-360, 360), 0);
                enemy.Health = enemy.MaxHealth;

                // The Story bomber isn't wired to Infinite Mode's kill counter (Infinite prefabs use BossDead).
                if (enemy.GetComponent<BossDead>() == null) enemy.OnDeath.AddListener(OnKillEnemy);

                PimlrEnemySpawnDirector.Arrive(enemy.gameObject);
                yield return new WaitForSeconds(Random.Range(1.2f, 2.2f));
            }
        }
        else if (KindOf(currentWave) == WaveKind.Normal)
        {

            for (int i = 0; i < normalEnemyCount; i++)
            {
                randomPosOnArea = transform.position;
                randomPosOnArea.x += Random.Range(-SpawnArea.x, SpawnArea.x);
                randomPosOnArea.y += Random.Range(-SpawnArea.y, SpawnArea.y);
                randomPosOnArea.z += Random.Range(-SpawnArea.z, SpawnArea.z);
                JUHealth enemy;

                enemy = Instantiate(normalEnemyPrefab, normalEnemiesHolder);


                enemy.transform.position = randomPosOnArea;
                enemy.transform.rotation = Quaternion.Euler(0, Random.Range(-360, 360), 0);

                enemy.Health = enemy.MaxHealth;

                // PIMLR: trickle in through portals down the street instead of all appearing in the intersection.
                PimlrEnemySpawnDirector.Arrive(enemy.gameObject);

                yield return new WaitForSeconds(Random.Range(0.9f, 1.8f));
            }
        }
        else
        {

            for (int i = 0; i < bossCount; i++)
            {
                randomPosOnArea = transform.position;
                randomPosOnArea.x += Random.Range(-SpawnArea.x, SpawnArea.x);
                randomPosOnArea.y += Random.Range(-SpawnArea.y, SpawnArea.y);
                randomPosOnArea.z += Random.Range(-SpawnArea.z, SpawnArea.z);
                JUHealth enemy;

                enemy = Instantiate(bossEnemyPrefab, bossEnemiesHolder);


                enemy.transform.position = randomPosOnArea;
                enemy.transform.rotation = Quaternion.Euler(0, Random.Range(-360, 360), 0);

                enemy.Health = enemy.MaxHealth;

                // PIMLR: bosses arrive through portals too, a beat apart.
                PimlrEnemySpawnDirector.Arrive(enemy.gameObject);
                yield return new WaitForSeconds(Random.Range(1.5f, 2.5f));
            }
        }
    }


    public void OnKillEnemy()
    {
        currentEnemyCount++;

        // PIMLR: record kills (Infinite Mode no longer tracks a separate boss count).
        RunStats.RegisterKill();
        SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo(currentEnemyCount.ToString() + "/" + TargetCount());
        if (TargetCount() <= currentEnemyCount)
            CompleteWave();
    }

}
