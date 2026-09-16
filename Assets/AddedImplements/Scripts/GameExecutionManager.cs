using Unity.Cinemachine;
using JUTPS;
using JUTPS.AI;
using JUTPS.CameraSystems;
using JUTPS.InventorySystem;
using JUTPS.ItemSystem;
using JUTPS.Utilities;
using JUTPS.VehicleSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


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
        if (Enum.TryParse(PlayerPrefs.GetString("currentZoneMode"), out Zone parsedZone))
        {
            currentZoneMode = parsedZone;
        }
        else
        {
            currentZoneMode = Zone.ChatWilly;
        }


        Debug.Log(":::::>>>>>>>>>>" + currentZoneMode);

        //var _uiMAnager = GameObject.FindObjectOfType<UIManager>();
        uILevelCompletePopUp = SceneManagerScript.Instance.uiManager.UIMenus[5].UI_Gameobject.GetComponent<UILevelCompletePopUp>();

        PlayerCar.SetActive(false);
        AiCar.SetActive(false);
        ai_Chat_Triger.SetActive(false);

        if (currentZoneMode == Zone.ChatWilly)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = false;
            currentZoneMode = Zone.Zone1;
            PlayerCar.SetActive(false);
            AiCar.SetActive(false);
            ai_Chat_Triger.SetActive(true);
        }
        else if (currentZoneMode == Zone.Zone1)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            StartCoroutine(WaitForscreenfadeOut("0/10"));

            Invoke("Zone1Start", 3);

        }
        else if (currentZoneMode == Zone.ZoneBoss1)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            StartCoroutine(WaitForscreenfadeOut("0/1"));
            Invoke("SpawnBoss", 3);

        }
        else if (currentZoneMode == Zone.Zone2)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            StartCoroutine(WaitForscreenfadeOut("0/10"));
            StartCoroutine(Zone2Start());
        }
        else if (currentZoneMode == Zone.ZoneBoss2)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            StartCoroutine(Zone2Boss());
        }
        else if(currentZoneMode==Zone.InfiniteMode)
        {
            playerInventory.GetComponent<ItemSwitchManager>().IsPlayer = true;
            infiniteMode.gameObject.SetActive(true);
        }


    }

    // Update is called once per frame
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
                PlayerPrefs.SetString("currentZoneMode", currentZoneMode.ToString());
                SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.Zone1_Enemy_Battle);
                //SpawnBoss();
                StartCoroutine(WaitForscreenfadeOut("0/1"));
                Invoke("SpawnBoss", 10f);
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

                PlayerPrefs.SetString("currentZoneMode", currentZoneMode.ToString());
                //SceneManagerScript.Instance.goalPanel.
            }
        }

        if (zone1Finish)
        {
            Debug.Log("  --------zone 1 finish------   ");

            SceneManagerScript.Instance.goalPanel.OnCompleteGoal(GoalList.Zone1_Boss_Battle);

            currentZoneMode = Zone.Zone2;
            PlayerPrefs.SetString("currentZoneMode", currentZoneMode.ToString());
            SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo("1/ 1");
            StartCoroutine(WaitForscreenfadeOut("0/10"));
            zone1Finish = false;

            StartCoroutine(Zone2Start());
        }
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
    IEnumerator MoveToTargetAndBack()
    {
        tpsCameraController.enabled = false;
        originalPosition = cameraObj.transform.position;
        originalRotation = cameraObj.transform.rotation;
        // Move and rotate to target
        yield return StartCoroutine(MoveAndRotate(
           cameraObj.transform.position, carCinemation.transform.position,
           cameraObj.transform.rotation, carCinemation.transform.rotation,
            moveDuration
        ));

        // Wait at target
        yield return new WaitForSeconds(waitAtTarget);

        // Move and rotate back to original
        yield return StartCoroutine(MoveAndRotate(
           cameraObj.transform.position, originalPosition,
           cameraObj.transform.rotation, originalRotation,
            moveDuration
        ));

        tpsCameraController.enabled = true;
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

        SceneManagerScript.Instance.goalPanel.SetCurrentKillInfo(zombiesKilled.ToString() + " / 10");

        if (zone2Start)
        {
            //PersistentAudioManager.Instance.musicPlayer.shopPointsInt += 10;
            CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() + 10);

        }
        else
        {
            //PersistentAudioManager.Instance.musicPlayer.shopPointsInt += 5;
            CoinManager.Instance.SetCoins(CoinManager.Instance.GetCoins() + 5);
        }
    }


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
        zombieBoss.SetActive(true);
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
}
