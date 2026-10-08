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

    public Transform normalEnemiesHolder;
    public Transform bossEnemiesHolder;


    public Vector3 SpawnArea;


   public int currentEnemyCount;

    // PIMLR (playtest): track the player's death event and prevent duplicate run submission.
    private JUHealth playerHealth;
    private bool runEnded;

    void Start()
    {
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
        if (currentWave % 2 == 1)
            normalEnemyCount += 5;
        else
            bossCount += 1;

        StartCoroutine(SpawnWave());
    }

    IEnumerator SpawnWave()
    {
        yield return new WaitForSeconds(3.5f);
        currentEnemyCount = 0;
        Debug.Log(currentWave % 2);
        yield return StartCoroutine(GameExecutionManager.Instance.WaitForscreenfadeOut("0/" + (currentWave % 2 == 1 ? normalEnemyCount : bossCount)));
     
        SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo(currentEnemyCount.ToString() + "/" + (currentWave % 2 == 1 ? normalEnemyCount : bossCount));
       
        Vector3 randomPosOnArea = transform.position;
        randomPosOnArea.x += Random.Range(-SpawnArea.x, SpawnArea.x);
        randomPosOnArea.y += Random.Range(-SpawnArea.y, SpawnArea.y);
        randomPosOnArea.z += Random.Range(-SpawnArea.z, SpawnArea.z);

        if (currentWave % 2 == 1)
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

                enemy.gameObject.SetActive(true);

                yield return new WaitForSeconds(0.3f);
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

                enemy.gameObject.SetActive(true);

            }
        }
    }


    public void OnKillEnemy()
    {
        currentEnemyCount++;

        // PIMLR (playtest): record kills and classify kills on even waves as boss kills.
        RunStats.RegisterKill(isBoss: currentWave % 2 == 0);
        SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo(currentEnemyCount.ToString() + "/" + (currentWave % 2 == 1 ? normalEnemyCount : bossCount));
        if (currentWave % 2 == 1)
        {
            if (normalEnemyCount <= currentEnemyCount)
            {
                CompleteWave();
                return;
            }
        }
        else
        {
            if (bossCount <= currentEnemyCount)
            {
                CompleteWave();
            }
        }
    }

}
