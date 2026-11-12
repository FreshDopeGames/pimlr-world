using JUTPS.AI;
using JUTPS.CameraSystems;
using JUTPS.InventorySystem;
using JUTPS.ItemSystem;
using JUTPS.Utilities;
using JUTPS.VehicleSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;


public class GameExecutionManager : Singleton<GameExecutionManager>
{

     


    bool zone1Finish;
    [SerializeField]
    internal JUInventory playerInventory;
    public PlayerHandler playerHandler;

    public CarController carController;

    [SerializeField]
    GameObject gunUpgradePop_Up;
    [SerializeField]
    GameObject gunUpgradeBTN;
    [Header("Zombies Section")]
    public int zombiesKilled = 4;
    public JUAutoInstantiate zombieSpawner;
    public GameObject zombieBoss;
    public GameObject waterBallonzombie;
    public List<ZombieAI> zombieBossList;
    public bool zone2Start, zone2Finish;
    public Zone currentZoneMode;

    public GameObject ai_Chat_Triger;

    public int money;
    [SerializeField]
    UILevelCompletePopUp uILevelCompletePopUp;

    public GameObject Zone2Zombie;


    public TPSCameraController tpsCameraController;
    public GameObject PlayerCar, AiCar, carCinemation, cameraObj;
    public RCC_AICarController policeCar;

    public InfiniteMode infiniteMode;

    // PIMLR #5: Movement leash. While false the player is clamped to a small starting
    // area (see BoundsRestriction.cs) until the vehicle is unlocked. Other systems
    // (BoundsRestriction) read this; call UnlockVehicle() when the truck becomes drivable.
    public bool vehicleUnlocked = false;

    // PIMLR #5: Raised when the vehicle is unlocked so the boundary restriction can disable itself.
    public System.Action onVehicleUnlocked;

    public void UnlockVehicle()
    {
        if (vehicleUnlocked) return;
        vehicleUnlocked = true;
        onVehicleUnlocked?.Invoke();
        Debug.Log("PIMLR #5: vehicle unlocked - movement leash released.");
    }

    void Start()
    {
        // PIMLR (playtest): load the persisted zone through its typed owner.
        Zone startZone = ZoneProgress.Current;
        currentZoneMode = startZone;

        if (startZone == Zone.ChatWilly ||
            startZone == Zone.Zone1 ||
            startZone == Zone.ZoneBoss1 ||
            startZone == Zone.Zone2 ||
            startZone == Zone.ZoneBoss2 ||
            startZone == Zone.SaveTheFans) // PIMLR #STF
        {
            // PIMLR (leaderboards): a Story run survives deaths / stage restarts (YannicksWorld reloading).
            // Keep the original run, start zone and timer; only quitting out of YannicksWorld cancels it
            // (see RunStats.StoryRunQuitGuard).
            if (RunStats.Active && RunStats.Mode == RunMode.Story)
            {
                RunStats.SetZone(startZone);
                Debug.Log($"PIMLR Leaderboards: Story run continues after stage restart (started at {RunStats.Snapshot.startZone}, now {startZone}).");
            }
            else
            {
                RunStats.Begin(RunMode.Story, startZone);
            }
        }

        Debug.Log(":::::>>>>>>>>>>" + currentZoneMode);

        // PIMLR (playtest): retrieve the popup by its registered name instead of menu-array position.
        UIManager uiManager = SceneManagerScript.Instance != null ? SceneManagerScript.Instance.uiManager : null;
        uILevelCompletePopUp = uiManager != null ? uiManager.GetMenu<UILevelCompletePopUp>("Zone Panel") : null;
        if (uILevelCompletePopUp == null)
            Debug.LogWarning("GameExecutionManager: UIManager could not find UILevelCompletePopUp on 'Zone Panel'.", this);

        PlayerCar.SetActive(false);
        AiCar.SetActive(false);
        ai_Chat_Triger.SetActive(false);

        if (currentZoneMode == Zone.ChatWilly)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = false;
            currentZoneMode = Zone.Zone1;
            RunStats.SetZone(currentZoneMode);
            PlayerCar.SetActive(false);
            AiCar.SetActive(false);
            ai_Chat_Triger.SetActive(true);
        }
        else if (currentZoneMode == Zone.Zone1)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            // PIMLR: the zone card plays all the way through (after the loading screen) before the spawner starts.
            StartCoroutine(AfterZoneCard("0/10", Zone1Start));

        }
        else if (currentZoneMode == Zone.ZoneBoss1)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            StartCoroutine(AfterZoneCard("0/1", SpawnBoss)); // PIMLR: boss arrives once the zone card has closed

        }
        else if (currentZoneMode == Zone.Zone2)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            StartCoroutine(AfterZoneCard("0/10", () => StartCoroutine(Zone2Start()))); // PIMLR: card first
        }
        else if (currentZoneMode == Zone.ZoneBoss2)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            StartCoroutine(Zone2Boss());
        }
        else if (currentZoneMode == Zone.SaveTheFans)
        {
            // PIMLR #STF: restarting the stage after a death resumes at the fan the player was on.
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            StartCoroutine(SaveTheFansStart());
        }
        else if(currentZoneMode==Zone.InfiniteMode)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            infiniteMode.gameObject.SetActive(true);
        }


    }

    // Update is called once per frame
    float gamepadFreeCursorTime; // PIMLR: see controller cursor re-lock in Update

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {

            if (Cursor.lockState == CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                //Debug.Log("lock");
            }
            else if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                //Debug.Log("unlock");
            }
        }
        // PIMLR: controller equivalent of "click to re-lock the cursor". If a controller is in use, the cursor is free
        // and no menu buttons have been on screen for 0.4s (PimlrMenuNavigator idle - the grace period lets it pick
        // up a popup that just opened), lock it so the camera and A/jump work again.
        if (PimlrInput.UsingGamepad && Cursor.lockState == CursorLockMode.None && !PimlrMenuNavigator.IsNavigating)
        {
            gamepadFreeCursorTime += Time.unscaledDeltaTime;
            if (gamepadFreeCursorTime > 0.4f)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                gamepadFreeCursorTime = 0f;
            }
        }
        else
        {
            gamepadFreeCursorTime = 0f;
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None && SceneManagerScript.Instance && !SceneManagerScript.Instance.IsPointerOverUIObject())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (zombiesKilled >= 10)
        {

            zombiesKilled = 0;
            zombieSpawner.GetComponent<JUAutoInstantiate>().EmptyInstantiatePorcentage = 100;
            for (int i = 0; i < zombieSpawner.Spawneds.Count; i++)
            {
                Destroy(zombieSpawner.Spawneds[i].gameObject);
            }
            zombieSpawner.Spawneds.Clear();
            if (!zone2Start)
            {

                Debug.LogError(":::::::::>>>>>>>>>>>>>");
                //gunUpgradePop_Up.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                //gunUpgradeBTN.SetActive(true);


                currentZoneMode = Zone.ZoneBoss1;
                // PIMLR (playtest): persist zone progression through its typed owner.
                ZoneProgress.Current = currentZoneMode;
                RunStats.SetZone(currentZoneMode);
                SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.Zone1_Enemy_Battle);
                //SpawnBoss();
                // PIMLR: the boss arrives as soon as the "0/1" zone card has finished (was a fixed 4.5 s timer).
                StartCoroutine(AfterZoneCard("0/1", SpawnBoss));
            }
            if (zone2Start)
            {
                zone2Finish = true;
                Debug.Log("zone2Finish::" + zone2Finish);
                SceneManagerScript.Instance.minimapBlipController.policecar = AiCar.transform;
                SceneManagerScript.Instance.minimapBlipController.playercar = PlayerCar.transform;
                SceneManagerScript.Instance.minimapBlipController.playerCarBlip.gameObject.SetActive(true);
                SceneManagerScript.Instance.minimapBlipController.policeBlip.gameObject.SetActive(true);
                PlayerCar.SetActive(true);
                AiCar.SetActive(true);
                StartCoroutine(MoveToTargetAndBack());
                SceneManagerScript.Instance.goalPanel.enemyInfoHolder.SetActive(false);
                SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.Zone2_Enemy_Battle);
                currentZoneMode = Zone.ZoneBoss2;

                // PIMLR (playtest): persist zone progression through its typed owner.
                ZoneProgress.Current = currentZoneMode;
                RunStats.SetZone(currentZoneMode);
                //SceneManagerScript.Instance.goalPanel.
            }
        }

        if (zone1Finish)
        {
            Debug.Log("  --------zone 1 finish------   ");

            SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.Zone1_Boss_Battle);

            SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo("1/ 1");
            zone1Finish = false;

            // PIMLR #STF: Boss 1 is down. Save The Fans runs next, then hands over to Zone 2.
            // If the mission isn't in the scene the Story goes straight to Zone 2, exactly as before.
            if (PIMLR.Missions.SaveTheFans.SaveTheFansMission.Instance != null)
            {
                currentZoneMode = Zone.SaveTheFans;
                ZoneProgress.Current = currentZoneMode;
                RunStats.SetZone(currentZoneMode);
                PIMLR.Missions.SaveTheFans.SaveTheFansMission.ClearSavedProgress(); // a fresh visit starts at fan 1
                StartCoroutine(SaveTheFansStart());
            }
            else
            {
                BeginZone2();
            }
        }
    }

    // PIMLR #STF: stage card, then the mission takes over.
    IEnumerator SaveTheFansStart()
    {
        yield return PimlrInterstitial.WaitUntilClear(0f); // PIMLR: after the loading screen
        yield return StartCoroutine(WaitForscreenfadeOut(""));
        playerInventory.EquipItem(playerInventory.AllHoldableItems[0].ItemSwitchID); // PIMLR #STF: equip like every other stage
        SceneManagerScript.Instance.goalPanel.enemyInfoHolder.SetActive(false);
        var mission = PIMLR.Missions.SaveTheFans.SaveTheFansMission.Instance;
        if (mission != null) mission.BeginMission();
        else BeginZone2(); // mission removed from the scene: don't strand the Story on this zone
    }

    // PIMLR #STF: called by SaveTheFansMission (SendMessage) when the last fan is saved.
    public void OnSaveTheFansComplete()
    {
        if (currentZoneMode != Zone.SaveTheFans) return; // the mission was run on its own for testing
        BeginZone2();
    }

    // PIMLR #STF: the original "Boss 1 finished -> Zone 2" hand-off, unchanged, moved into a method.
    void BeginZone2()
    {
        currentZoneMode = Zone.Zone2;
        // PIMLR (playtest): persist zone progression through its typed owner.
        ZoneProgress.Current = currentZoneMode;
        RunStats.SetZone(currentZoneMode);
        StartCoroutine(AfterZoneCard("0/10", () => StartCoroutine(Zone2Start()))); // PIMLR: card first, then spawns
    }

    // PIMLR: wait out any loading screen, play the zone card in full, then start the zone.
    IEnumerator AfterZoneCard(string infodata, System.Action then)
    {
        yield return PimlrInterstitial.WaitUntilClear(0f);
        yield return StartCoroutine(WaitForscreenfadeOut(infodata));
        yield return PimlrInterstitial.WaitUntilClear();
        then?.Invoke();
    }

    [ContextMenu("aaaaa")]
    public void MoveCameraDummy()
    {
        StartCoroutine(MoveToTargetAndBack());

    }

    public float moveDuration = 1.5f;
    public float waitAtTarget = 1f;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    // PIMLR #CAM: the car reveal used to fly in a straight line to 'carCinemation' (a camera parked 10 m behind the
    // Car) and back, cutting through buildings and often framing the side of the truck against a wall. Now the shot
    // is picked on the most open side of the truck (the street side, preferring its flanks) and both moves are
    // routed over rooftops by PimlrCinematicCamera. carCinemation is only the fallback if the Car is missing.
    [Header("Car reveal camera (PIMLR #CAM)")]
    public float carRevealDistance = 8f;
    public float carRevealHeight = 2.2f;
    IEnumerator MoveToTargetAndBack()
    {
        tpsCameraController.enabled = false;
        PIMLR.Missions.SaveTheFans.PlayerControlLock.FreezeAim(); // PIMLR: the gun must not follow the camera to the car
        // PIMLR #CAM: cameraObj is the rig pivot; the lens sits 3 m behind it. Plan and move the lens itself.
        var lensRig = new PimlrCinematicCamera.LensRig(cameraObj.transform);
        originalPosition = lensRig.HomePosition;
        originalRotation = lensRig.HomeRotation;

        Vector3 shotPos;
        Quaternion shotRot;
        if (PlayerCar != null)
        {
            Vector3 focus = PlayerCar.transform.position + Vector3.up * 1.2f;
            shotPos = PimlrCinematicCamera.FindOpenShot(focus, carRevealDistance, carRevealHeight, PlayerCar.transform.forward);
            shotRot = Quaternion.LookRotation((focus - shotPos).normalized, Vector3.up);
        }
        else
        {
            shotPos = carCinemation.transform.position;
            shotRot = carCinemation.transform.rotation;
        }

        // Move and rotate to target
        yield return StartCoroutine(PimlrCinematicCamera.Fly(cameraObj.transform, shotPos, shotRot, moveDuration));

        // Wait at target
        yield return new WaitForSeconds(waitAtTarget);

        // Move and rotate back to original
        yield return StartCoroutine(PimlrCinematicCamera.Fly(cameraObj.transform, originalPosition, originalRotation, moveDuration));

        lensRig.Restore();
        tpsCameraController.enabled = true;
        PIMLR.Missions.SaveTheFans.PlayerControlLock.UnfreezeAim();
    }

    IEnumerator MoveAndRotate(Vector3 startPos, Vector3 endPos, Quaternion startRot, Quaternion endRot, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            cameraObj.transform.position = Vector3.Lerp(startPos, endPos, t);
            cameraObj.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cameraObj.transform.position = endPos;
        cameraObj.transform.rotation = endRot;
    }

    public void OnKillEnemy()
    {
        zombiesKilled += 1;
        RunStats.RegisterKill();

        SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo(zombiesKilled.ToString() + " / 10");

        if (zone2Start)
        {
            //PersistentAudioManager.Instance.musicPlayer.shopPointsInt += 10;
            CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() + 10);
            RunStats.AddCoins(10);

        }
        else
        {
            //PersistentAudioManager.Instance.musicPlayer.shopPointsInt += 5;
            CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() + 5);
            RunStats.AddCoins(5);
        }
    }

    // PIMLR (leaderboards): ends the Story run as Completed and submits it. Called from JUHealth.OnVideoPlay when the
    // final objective ("Shut Down Police Cruiser") is met, just before the ending FMV and the return to the Living Room.
    // Previously nothing called this, so finished runs were only ever ended as Abandoned and never reached the board.
    public void CompleteStory()
    {
        if (!RunStats.Active || RunStats.Mode != RunMode.Story)
            return;


        RunSnapshot final = RunStats.End(RunEndReason.Completed);
        LeaderboardManager.TrySubmit(final);
        ZoneProgress.Current = Zone.ChatWilly;
    }

    // PIMLR (leaderboards): no longer ends the Story run here - this also ran when YannicksWorld reloaded after a
    // death, which cancelled the run. Quitting is handled by RunStats.StoryRunQuitGuard.

    public void BossDead()
    {
        zone1Finish = true; zone2Finish = false;
    }
    public void SpawnBoss()
    {
        SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.Zone1_Enemy_Battle);
        playerInventory.EquipItem(playerInventory.AllHoldableItems[0].ItemSwitchID);
        SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo("0 / 1");
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        // PIMLR: Zombie Boss must be the boss object placed (inactive) in this scene, e.g. "Big Boss" - not a prefab asset
        // from the Project window. Arrive() only switches an existing object on, so an asset here means no boss ever appears.
        if (zombieBoss == null || !zombieBoss.scene.IsValid())
        {
            Debug.LogError("GameExecutionManager: 'Zombie Boss' must reference the boss object in the scene (e.g. 'Big Boss'), not a prefab asset. No boss spawned.", this);
            return;
        }
        // PIMLR: the boss steps out of a portal where it's placed instead of popping in, on camera.
        PimlrEnemySpawnDirector.RequestFirstEnemyCutscene("Boss 1");
        PimlrEnemySpawnDirector.Arrive(zombieBoss, relocate: false);
    }

    public void UpgradeGun()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        playerInventory.WeaponInUseInRightHand.BulletsPerMagazine = 50;
        //CoinManager.instance.SetCoins(CoinManager.instance.GetCoins() - 50);
    }

    public void AddWaterGun()
    {
        Debug.Log(":::>>>>");
        for (int i = 0; i < playerInventory.AllHoldableItems.Length; i++)
        {
            playerInventory.HoldableItensRightHand[i].AddItem();

        }
    }

    public void Zone1Start()
    {
        Debug.Log("::::>>>>>" + playerInventory.AllHoldableItems.Length);
        SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.ChatWithSirihanna);
        playerInventory.EquipItem(playerInventory.AllHoldableItems[0].ItemSwitchID);
        PimlrEnemySpawnDirector.RequestFirstEnemyCutscene("Zone 1"); // PIMLR: show the first enemy arriving
        zombieSpawner.gameObject.SetActive(true);
        SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo(zombiesKilled.ToString() + " / 10");
    }

    IEnumerator Zone2Start()
    {
        Debug.Log(" zone 2 starts");
        yield return new WaitForSeconds(0.5f);
        SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.Zone1_Boss_Battle);
        playerInventory.EquipItem(playerInventory.AllHoldableItems[0].ItemSwitchID);
        yield return new WaitForSeconds(0.5f);
        yield return new WaitForSeconds(1f);
        zombieSpawner.PrefabsToInstantiate[0] = waterBallonzombie;
        zombieSpawner.EmptyInstantiatePorcentage = 0;
        // PIMLR: Level 3 needs 10 washes - spawn exactly 10 bombers in total, not an endless stream.
        zombieSpawner.ResetTotalSpawnCap(10);
        PimlrEnemySpawnDirector.RequestFirstEnemyCutscene("Zone 2"); // PIMLR: show the first bomber arriving
        zombieSpawner.gameObject.SetActive(true);
        zone2Start = true;
    }


    IEnumerator Zone2Boss()
    {
        yield return new WaitForSeconds(0.5f);
        SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.Zone2_Enemy_Battle);
        playerInventory.EquipItem(playerInventory.AllHoldableItems[0].ItemSwitchID);
        zone2Finish = true;
        PlayerCar.SetActive(true);
        AiCar.SetActive(true);
    }


    public IEnumerator WaitForscreenfadeOut(string infodata)
    {
        PimlrInterstitial.Begin(); // PIMLR: enemy arrivals wait until the zone card is done
        try
        {
            //fadeOut
            UIFader fader = SceneManagerScript.Instance.uiManager.UI_fader;
            if (fader != null) fader.Fade(UIFader.FADE.FadeOut, 0.4f, 0.4f);
            yield return new WaitForSeconds(1f);
            SceneManagerScript.Instance.uiManager.ShowMenu("Zone Panel",false);
            yield return new WaitForSeconds(3f);
            SceneManagerScript.Instance.uiManager.CloseMenu("Zone Panel");
            SceneManagerScript.Instance.uiManager.ShowMenu("JUTPS Interface",false);
            SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo(infodata);
        }
        finally { PimlrInterstitial.End(); }
    }
}
