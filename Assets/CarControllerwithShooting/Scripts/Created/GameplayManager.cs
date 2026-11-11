using CarControllerwithShooting;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;
using UnityEngine.Networking;

public class GameplayManager : Singleton<GameplayManager>
{


    public string playerVehicleURL = "https://www.idea-labs.xyz/api/helix/vehicles/1";
    public PlayerInformation currentPlayerInformation;



    public CinemachineVirtualCamera virtualCamera;

    public PlayerID playerHammer, playerHero, playerHype;
    public PlayerID otherPlayerHammer, otherPlayerHero, otherPlayerHype;
    public PlayerID helix_AI_Bot;
    public Transform playerParent;


    


    public BulletScript Bullet_Machinegun_Player;
    //public BulletScript Bullet_Machinegun_Client;


    public MissileScript Bullet_Missile_Player;
    public MissileScript Bullet_Missile_Client;

    public Transform[] spawnPoints;
    Transform pos;
    public List<PlayerID> playerList;
    public CarController[] objectPrefabs;
    public GameCanvas gameCanvas;

    [Header("Timer")]
    //float startTime = 600; // Initial time in seconds
    float startTime = 120; // Initial time in seconds
    public float countdownTime; // Remaining time in seconds
    float t;
    public float respawnTime = 4f;


    [Header("TeamScoreManager")]
    public int teamAScore = 0;
    public int teamBScore = 0;


    public Dictionary<string, PowerUp> allPowerUps = new Dictionary<string, PowerUp>();



    public Dictionary<string, MissileScript> allmissiles = new Dictionary<string, MissileScript>();



    // Add a public delegate and event for missile firing
    public delegate void MissileFiredHandler(Transform missileTransform, string playerId);
    public static event MissileFiredHandler OnMissileFired;



    public static event Action OnMissileDestroyed;



    public Transform gunTarget;


    public void OnRaiseMissileDestroyHandler()
    {
        //Debug.Log("OnRaiseMissileDestroyHandlerP:::::::::::::k");

        //if (OnMissileDestroyed == null)
        //{
        //    Debug.Log("$$$$$$$$$$$$$$$$$$$$");
        //}
        //else
        OnMissileDestroyed?.Invoke();
    }
    public void OnRaiseMissileFiredHandler(Transform missileTransform, string playerId)
    {
        if (OnMissileFired != null)
            OnMissileFired(missileTransform, playerId);
        //else
        //    Debug.Log(":::::::>>>>>Event Not Assign");
    }

    #region UNITY-CALLBACKS

    IEnumerator Start()
    {

        using (UnityWebRequest request = UnityWebRequest.Get(playerVehicleURL))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.Log(request.error);
            }
            else
            {
                Debug.Log("Car Garage Data:::>>>" + request.downloadHandler.text);

                JsonUtility.FromJsonOverwrite(request.downloadHandler.text, currentPlayerInformation);

                JsonUtility.FromJsonOverwrite(currentPlayerInformation.attributes, currentPlayerInformation.playerAttributes);

                #region ARRAY OF OBJECTS CONVERSION
                JSONObject newJsonObject = new JSONObject(currentPlayerInformation.weapons);

                currentPlayerInformation.playerWeapons.Clear();

                //if (newJsonObject.Count > 1)
                //{
                //    for (int j = 0; j < newJsonObject.Count; j++)
                //    {
                //        PlayerWeapons currentPlayerWeapons = new();

                //        JsonUtility.FromJsonOverwrite(newJsonObject[j].ToString(), currentPlayerWeapons);

                //        currentPlayerInformation.playerWeapons.Add(currentPlayerWeapons);
                //    }
                //}
                //else if (newJsonObject.Count == 1)
                //{
                //    PlayerWeapons currentPlayerWeapons = new();

                //    JsonUtility.FromJsonOverwrite(newJsonObject[0].ToString(), currentPlayerWeapons);

                //    currentPlayerInformation.playerWeapons.Add(currentPlayerWeapons);
                //}

                #endregion
            }
        }
        try
        {

#if !UNITY_EDITOR
            startTime = 180;
#endif
            int i = 0;
            //Debug.Log(SocketPlayerManager.Instance.helixPlayerInfoList.Count);
            foreach (HelixPlayerInfo p in SocketPlayerManager.Instance.helixPlayerInfoList.Values)
            {
                PlayerID g;
                if (!CheckNPCPlayer(p.userID))
                {
                    if (p.userID == SocketPlayerManager.Instance.player_Id)
                    {

                        switch (currentPlayerInformation.title)
                        {
                            case "Hype":
                                g = Instantiate(playerHype, spawnPoints[i].position, Quaternion.identity, playerParent);
                                break;

                            case "Hero":
                                g = Instantiate(playerHero, spawnPoints[i].position, Quaternion.identity, playerParent);
                                break;

                            case "Hammer":
                                g = Instantiate(playerHammer, spawnPoints[i].position, Quaternion.identity, playerParent);
                                break;

                            default:
                                g = Instantiate(playerHammer, spawnPoints[i].position, Quaternion.identity, playerParent);
                                break;
                        }

                        SocketPlayerManager.Instance.MyPlayer = g;

                        g.name = p.userName;
                        g.tag = "Player";
                        if (SocketPlayerManager.Instance.allPlayers.ContainsKey(p.userID))
                        {
                            SocketPlayerManager.Instance.allPlayers[p.userID] = g;
                        }
                        else
                        {
                            SocketPlayerManager.Instance.allPlayers.Add(p.userID, g);
                        }
                        CarColorAssigner(g);
                        if (g.controller == null)
                            g.controller = g.GetComponent<CarController>();
                        if (g.gunController == null)
                            g.gunController = g.GetComponent<GunController>();

                        //player.controller = g.controller;
                        //player.gunController = g.gunController;
                        //player.controller.setplayerID(g);
                        //player.gunController.setplayerID(g);
                        gameCanvas.speedometerScript.controller = g.controller;
                        gameCanvas.helixHealthBar.controller = g.controller;
                        gameCanvas.helixHealthBar.helixPlayerHealth = SocketPlayerManager.Instance.allPlayers[p.userID].helixPlayerHealth;
                        g.helixPlayerInfo = SocketPlayerManager.Instance.helixPlayerInfo;
                        g.isLocalPlayer = true;

                      
                        SocketPlayerManager.Instance.helixPlayerInfo.isLocalUser = true;

                        //Debug.Log("SPWAN-PLAYER");
                    }
                    else
                    {
                        //if(SocketPlayerManager.Instance.helixPlayerInfoList[p.userID].)
                        switch (currentPlayerInformation.title)
                        {
                            case "Hype":
                                g = Instantiate(otherPlayerHype, spawnPoints[i].position, Quaternion.identity, playerParent);
                                break;

                            case "Hero":
                                g = Instantiate(otherPlayerHero, spawnPoints[i].position, Quaternion.identity, playerParent);
                                break;

                            case "Hammer":
                                g = Instantiate(otherPlayerHammer, spawnPoints[i].position, Quaternion.identity, playerParent);
                                break;

                            default:
                                g = Instantiate(otherPlayerHammer, spawnPoints[i].position, Quaternion.identity, playerParent);
                                break;
                        }
                        //g = Instantiate(otherPlayer, spawnPoints[i].position, Quaternion.identity, playerParent);
                        g.name = p.userName;
                        g.isLocalPlayer = false;
                        g.controller.setplayerID(g);
                        g.gunController.setplayerID(g);
                        g.tag = "Enemy";
                        g.helixPlayerInfo = SocketPlayerManager.Instance.helixPlayerInfoList[p.userID];
                        g.SetTeamColor();
                        SocketPlayerManager.Instance.allPlayers.Add(p.userID, g);
                        //Debug.Log($"User ID: {SocketPlayerManager.Instance.helixPlayerInfoList[i].userID} || TeamA: {g.helixPlayerInfo.teamA} || TeamB: {g.helixPlayerInfo.teamB}");
                        CarColorAssigner(g);

                    }



                    g.playerID = p.userID;
                    playerList.Add(g);
                    i++;
                }
            }

            StartCoroutine(ActiveNpcCar());

            countdownTime = startTime;
        }

        catch (Exception e)
        {
            Debug.Log($"GameplayManager ---> Start ---> {e.ToString()}");
        }
    }

    public bool CheckNPCPlayer(string newPlayerID)
    {

        for (int i = 0; i < objectPrefabs.Length; i++)
        {
            if (objectPrefabs[i].playerid.playerID == newPlayerID)
                return true;
        }

        return false;
    }
    //   int count = 10;
    public IEnumerator ActiveNpcCar()
    {
        yield return new WaitUntil(() => SocketPlayerManager.Instance.MyPlayer != null);

        int teamAplayers = 0;
        foreach (PlayerID item in SocketPlayerManager.Instance.allPlayers.Values)
        {
            if (item.helixPlayerInfo.teamA)
            {
                teamAplayers++;
            }
        }



        string teamSelect;
        //Debug
        int value = SocketPlayerManager.Instance.MyPlayer.helixPlayerInfo.roomMaxPlayer - SocketPlayerManager.Instance.MyPlayer.helixPlayerInfo.roomCurrentPlayer;

        //value = 2;
        if (!SocketPlayerManager.Instance.MyPlayer.helixPlayerInfo.isRoomHostUser)
            yield return new WaitForSeconds(1f);


        for (int i = 0; i < value; i++)
        {
            if (objectPrefabs[i] != null)
            {
                if (SocketPlayerManager.Instance.MyPlayer.helixPlayerInfo.isRoomHostUser)
                {
                    if ((SocketPlayerManager.Instance.MyPlayer.helixPlayerInfo.roomMaxPlayer / 2 - teamAplayers) > i)
                    {

                        teamSelect = "Team A";
                        objectPrefabs[i].playerid.helixPlayerInfo.teamA = true;
                        objectPrefabs[i].playerid.helixPlayerInfo.teamB = false;
                        SocketNetworkManager.Instance.Team_Select(objectPrefabs[i].playerid.helixPlayerInfo.userID, teamSelect, true, false);

                    }
                    else
                    {
                        teamSelect = "Team B";
                        objectPrefabs[i].playerid.helixPlayerInfo.teamA = false;
                        objectPrefabs[i].playerid.helixPlayerInfo.teamB = true;
                        SocketNetworkManager.Instance.Team_Select(objectPrefabs[i].playerid.helixPlayerInfo.userID, teamSelect, false, true);

                    }

                    objectPrefabs[i].playerid.helixPlayerInfo.userName = HelixNpcCarManager.Instance.GenerateRandomName();
                    //Debug.Log("PlayyerName:::::" + objectPrefabs[i].playerid.helixPlayerInfo.userName + "TEAMNAME::::" + teamSelect+ "objectPrefabs[i].playerid.helixPlayerInfo.userID"+ objectPrefabs[i].playerid.helixPlayerInfo.userID);
                }
                objectPrefabs[i].playerid.isAIPlayer = true;
                if (!SocketPlayerManager.Instance.helixPlayerInfo.isRoomHostUser)
                {
                    objectPrefabs[i]._rigidbody.mass = 1;
                    objectPrefabs[i]._rigidbody.isKinematic = true;
                }
                SocketNetworkManager.Instance.OnSaveNPCData(objectPrefabs[i].playerid.helixPlayerInfo);
                if (SocketPlayerManager.Instance.allPlayers.ContainsKey(objectPrefabs[i].playerid.playerID))
                    SocketPlayerManager.Instance.allPlayers[objectPrefabs[i].playerid.playerID] = objectPrefabs[i].playerid;
                else
                    SocketPlayerManager.Instance.allPlayers.Add(objectPrefabs[i].playerid.playerID, objectPrefabs[i].playerid);


                if (SocketPlayerManager.Instance.helixPlayerInfoList.ContainsKey(objectPrefabs[i].playerid.helixPlayerInfo.userID))
                {
                    objectPrefabs[i].playerid.helixPlayerInfo = SocketPlayerManager.Instance.helixPlayerInfoList[objectPrefabs[i].playerid.helixPlayerInfo.userID];
                }
                else
                {
                    SocketPlayerManager.Instance.helixPlayerInfoList.Add(objectPrefabs[i].playerid.helixPlayerInfo.userID, objectPrefabs[i].playerid.helixPlayerInfo);
                }

                SocketPlayerManager.Instance.helixPlayerInfoList[objectPrefabs[i].playerid.helixPlayerInfo.userID].teamA = objectPrefabs[i].playerid.helixPlayerInfo.teamA;
                SocketPlayerManager.Instance.helixPlayerInfoList[objectPrefabs[i].playerid.helixPlayerInfo.userID].teamB = objectPrefabs[i].playerid.helixPlayerInfo.teamB;


                //  if ((objectPrefabs[i].playerid.helixPlayerInfo.teamA && !SocketPlayerManager.Instance.helixPlayerInfo.teamA) || (!objectPrefabs[i].playerid.helixPlayerInfo.teamA && SocketPlayerManager.Instance.helixPlayerInfo.teamA))
                objectPrefabs[i].gameObject.SetActive(true);
            }

        }
        if (SocketPlayerManager.Instance.MyPlayer.helixPlayerInfo.isRoomHostUser)
        {
            SocketNetworkManager.Instance.EmitPlayerList();
        }
        else
        {
            yield return new WaitForSeconds(4f);


            for (int j = 0; j < objectPrefabs.Length; j++)
            {
                if (SocketPlayerManager.Instance.helixPlayerInfoList.ContainsKey(objectPrefabs[j].playerid.helixPlayerInfo.userID) && string.IsNullOrEmpty(SocketPlayerManager.Instance.helixPlayerInfoList[objectPrefabs[j].playerid.helixPlayerInfo.userID].userName))
                {
                    SocketPlayerManager.Instance.helixPlayerInfoList[objectPrefabs[j].playerid.helixPlayerInfo.userID].userName = objectPrefabs[j].playerid.helixPlayerInfo.userName;
                }
            }
        }
    }
    private void Update()
    {
        //Debug.Log("Timer Update");
        if (countdownTime > 0f)
        {
            TimerUpdate();
        }
    }
    #endregion

    #region RE-GANRETED-PLAYER
    public IEnumerator RespawnCountDown(string playerID)
    {
        if (SocketPlayerManager.Instance.player_Id == playerID)
        {
            float timer = respawnTime;
            gameCanvas.text_RespwanTime.gameObject.SetActive(true);
            while (timer > 0)
            {

                if (timer < 1)
                {
                    gameCanvas.text_RespwanTime.text = "Go!";
                }
                else
                {
                    gameCanvas.text_RespwanTime.text = Mathf.Ceil(timer - 1).ToString();
                }
                // Wait for the next frame
                yield return null;
                // Decrease the timer by the time passed since the last frame
                timer -= Time.deltaTime;

            }
            gameCanvas.text_RespwanTime.gameObject.SetActive(false);
            //ReGanretedPlayer(playerID);

        }
    }
    public void ReGanretedPlayer(string playerID)
    {
        try
        {
            StartCoroutine(InstiatePlayer(playerID));
        }
        catch (Exception e)
        {
            Debug.Log($"GameplayManager ---> ReGanretedPlayer ---> {e.ToString()}");
        }
    }
    public IEnumerator InstiatePlayer(string playerId)
    {

        #region CAR COLOR AND WEAPON DATA FETCHER

        yield return null;

        //using (UnityWebRequest request = UnityWebRequest.Get(playerVehicleURL))
        //{
        //    yield return request.SendWebRequest();
        //    if (request.result == UnityWebRequest.Result.ConnectionError)
        //    {
        //        Debug.Log(request.error);
        //    }
        //    else
        //    {
        //        Debug.Log(request.downloadHandler.text);

        //        JsonUtility.FromJsonOverwrite(request.downloadHandler.text, currentPlayerInformation);

        //        JsonUtility.FromJsonOverwrite(currentPlayerInformation.attributes, currentPlayerInformation.playerAttributes);

        //        #region ARRAY OF OBJECTS CONVERSION
        //        JSONObject newJsonObject = new JSONObject(currentPlayerInformation.weapons);

        //        currentPlayerInformation.playerWeapons.Clear();

        //        //if (newJsonObject.Count > 1)
        //        //{
        //        //    for (int j = 0; j < newJsonObject.Count; j++)
        //        //    {
        //        //        PlayerWeapons currentPlayerWeapons = new();

        //        //        JsonUtility.FromJsonOverwrite(newJsonObject[j].ToString(), currentPlayerWeapons);

        //        //        currentPlayerInformation.playerWeapons.Add(currentPlayerWeapons);
        //        //    }
        //        //}
        //        //else if (newJsonObject.Count == 1)
        //        //{
        //        //    PlayerWeapons currentPlayerWeapons = new();

        //        //    JsonUtility.FromJsonOverwrite(newJsonObject[0].ToString(), currentPlayerWeapons);

        //        //    currentPlayerInformation.playerWeapons.Add(currentPlayerWeapons);
        //        //}
        //        #endregion
        //    }
        //}

        #endregion
        yield return new WaitForSeconds(3f);
        if (!SocketPlayerManager.Instance.allPlayers.ContainsKey(playerId))
        {
            PlayerID g;

            if (playerId == SocketPlayerManager.Instance.player_Id)
            {
                switch (currentPlayerInformation.title)
                {
                    case "Hype":
                        g = Instantiate(playerHype, spawnPoints[UnityEngine.Random.Range(0, 10)].position, Quaternion.identity, playerParent);
                        break;

                    case "Hero":
                        g = Instantiate(playerHero, spawnPoints[UnityEngine.Random.Range(0, 10)].position, Quaternion.identity, playerParent);
                        break;

                    case "Hammer":
                        g = Instantiate(playerHammer, spawnPoints[UnityEngine.Random.Range(0, 10)].position, Quaternion.identity, playerParent);
                        break;

                    default:
                        g = Instantiate(playerHammer, spawnPoints[UnityEngine.Random.Range(0, 10)].position, Quaternion.identity, playerParent);
                        break;
                }

                //g = Instantiate(player, spawnPoints[UnityEngine.Random.Range(0, 10)].position, Quaternion.identity, playerParent);
                SocketPlayerManager.Instance.MyPlayer = g;
                g.name = g.helixPlayerInfo.userName;
                g.tag = "Player";
                
                g.helixPlayerInfo = SocketPlayerManager.Instance.helixPlayerInfo;
                SocketPlayerManager.Instance.allPlayers.Add(playerId, g);
                g.controller = g.GetComponent<CarController>();
                g.gunController = g.GetComponent<GunController>();

                g.controller = g.GetComponent<CarController>().setplayerID(g);
                g.gunController = g.GetComponent<GunController>().setplayerID(g);


             
                gameCanvas.helixHealthBar.controller = g.controller;
                gameCanvas.speedometerScript.controller = g.controller;
                gameCanvas.helixHealthBar.helixPlayerHealth = SocketPlayerManager.Instance.allPlayers[playerId].helixPlayerHealth;
            }
            else
            {

                switch (currentPlayerInformation.title)
                {
                    case "Hype":
                        g = Instantiate(otherPlayerHype, spawnPoints[UnityEngine.Random.Range(0, 10)].position, Quaternion.identity, playerParent);
                        break;

                    case "Hero":
                        g = Instantiate(otherPlayerHero, spawnPoints[UnityEngine.Random.Range(0, 10)].position, Quaternion.identity, playerParent);
                        break;

                    case "Hammer":
                        g = Instantiate(otherPlayerHammer, spawnPoints[UnityEngine.Random.Range(0, 10)].position, Quaternion.identity, playerParent);
                        break;

                    default:
                        g = Instantiate(otherPlayerHammer, spawnPoints[UnityEngine.Random.Range(0, 10)].position, Quaternion.identity, playerParent);
                        break;
                }
                //g = Instantiate(otherPlayer, spawnPoints[UnityEngine.Random.Range(0, 10)].position, Quaternion.identity, playerParent);
                g.isLocalPlayer = false;
                g.tag = "Enemy";
                g.GetComponent<CarController>().setplayerID(g);
                g.GetComponent<GunController>().setplayerID(g);
            }
            CarColorAssigner(g);
            g.playerID = playerId;
            if (SocketPlayerManager.Instance.allPlayers.ContainsKey(playerId))
            {
                SocketPlayerManager.Instance.allPlayers[playerId] = g;
            }
            else
            {
                SocketPlayerManager.Instance.allPlayers.Add(playerId, g);
            }
            playerList.Add(g);
        }
    }
    #endregion




    #region GAME-PLAY-TIMER
    public void TimerUpdate()
    {
        try
        {
            // Update the countdown time
            countdownTime -= Time.deltaTime;

            // Convert the remaining time to minutes and seconds
            int minutes = Mathf.FloorToInt(countdownTime / 60);
            int seconds = Mathf.FloorToInt(countdownTime % 60);

            // Display the time in a specific format (e.g., "00:00")
            string timeString = string.Format("{0:00}:{1:00}", minutes, seconds);
            if (countdownTime < 10)
                GameCanvas.Instance.text_timer.color = Color.red;
            else
                GameCanvas.Instance.text_timer.color = Color.white;

            GameCanvas.Instance.text_timer.text = timeString;
            // Output the time to the console (you can use this value in your game as needed)


            t += Time.deltaTime;
            if (t > 1)
            {
                t -= 1;
                if (SocketPlayerManager.Instance.helixPlayerInfo.isRoomHostUser)
                {
                    SocketNetworkManager.Instance.emitSetTimer((Int32)countdownTime);
                }
            }

            // Check if the countdown has reached zero
            if (countdownTime <= 0f)
            {
                // Do something when the timer reaches zero (e.g., end the game)
                //Debug.Log("Timer reached zero!");
                GameCanvas.Instance.leaderboardBattleRoyale.gameObject.SetActive(true);
                /*Time.timeScale = 0;*/
                Debug.Log(".......stop timer.......");
            }
        }
        catch (Exception e)
        {
            Debug.Log($"GameplayManager ---> TimerUpdate ---> {e.ToString()}");
        }

    }

    public void ReceiveTimerData(int count)
    {

        try
        {
            //Debug.Log(count);
            int minutes = Mathf.FloorToInt(count / 60);
            int seconds = Mathf.FloorToInt(count % 60);

            // Display the time in a specific format (e.g., "00:00")
            string timeString = string.Format("{0:00}:{1:00}", minutes, seconds);
            if (gameCanvas.text_timer)
                gameCanvas.text_timer.text = timeString;
            if (count <= 0f)
            {
                for (int i = 0; i < objectPrefabs.Length; i++)
                {
                    objectPrefabs[i]?.gameObject.SetActive(false);
                }

                // Do something when the timer reaches zero (e.g., end the game)

                //Debug.Log("|||||||||||||||||||||||||||||||||||||||");
                WinText();
                //gameCanvas.TimeOverPanel.SetActive(true);
                /*Time.timeScale = 0;*/
            }
        }
        catch (Exception e)
        {
            Debug.Log($"GameCanvas ---> Update_Text_Timer ---> {e.ToString()}");
        }
    }

    #endregion

    #region TEAM-SCORE-MANAGER
    public void IncrementTeamAScore(int points)
    {
        teamAScore += points;
        UpdateScoreUI();
    }

    public void IncrementTeamBScore(int points)
    {
        teamBScore += points;
        UpdateScoreUI();
    }
    void UpdateScoreUI()
    {
        gameCanvas.teamAScoreText.text = $"<size=10>Team A Score:</size><br>{teamAScore}";

        gameCanvas.teamBScoreText.text = $"<size=10>Team B Score:</size><br>{teamBScore}";
    }


    // You might want to reset scores at the start of a new round or match
    public void ResetScores()
    {
        teamAScore = 0;
        teamBScore = 0;
        UpdateScoreUI();
    }
    #endregion

    #region WINNER
    public void WinText()
    {
        try
        {
            //Debug.Log("In WinText");

            string winMessage = "";
            Debug.Log($"Team A: {teamAScore} || Team B: {teamBScore}");
            if (teamAScore > teamBScore)
            {
                //Debug.Log("TEAM-A WINS!");    
                winMessage = "TEAM-A WINS!";
                //SocketNetworkManager.Instance.UpdateLeaderboardUI();
                gameCanvas.leaderboardBattleRoyale.TeamAWin(new List<HelixPlayerInfo>(SocketPlayerManager.Instance.helixPlayerInfoList.Values));
                //gameCanvas.leaderboardBattleRoyale.TeamAWin(SocketPlayerManager.Instance.helixPlayerInfoList);
            }
            else if (teamAScore < teamBScore)
            {
                // Debug.Log("TEAM-B WINS!");    
                winMessage = "TEAM-B WINS!";
                //SocketNetworkManager.Instance.UpdateLeaderboardUI();
                gameCanvas.leaderboardBattleRoyale.TeamBWin(new List<HelixPlayerInfo>(SocketPlayerManager.Instance.helixPlayerInfoList.Values));
            }
            else
            {
                winMessage = "IT'S A TIE!";


                gameCanvas.leaderboardBattleRoyale.TeamAWin(new List<HelixPlayerInfo>(SocketPlayerManager.Instance.helixPlayerInfoList.Values));
                gameCanvas.leaderboardBattleRoyale.win.text = winMessage;
                gameCanvas.leaderboardBattleRoyale.lose.text = winMessage;
            }

            // Set the win text or display it on your canvas
            gameCanvas.leaderboardBattleRoyale.winText.text = winMessage;

            // If you have a leaderboard, you can display the win message there
            if (gameCanvas.leaderboardBattleRoyale != null)
            {
                //gameCanvas.leaderboardBattleRoyale.SetWinText(winMessage);
                gameCanvas.leaderboardBattleRoyale.gameObject.SetActive(true);
            }
        }
        catch (Exception e)
        {
            Debug.Log($"GameCanvas ---> WinText ---> {e.ToString()}");
        }
    }
    #endregion

    //[Obsolete]
    Vector3 missilePosition;
    Vector3 missileRotation;

    public void UpdateBulletHit(Data data)
    {
        try
        {

            missilePosition = new Vector3(data.dx, data.dy, data.dz);
            missileRotation = new Vector3(data.rx, data.ry, data.rz);

            //Debug.Log("Fire Get::::::::::::::>>>>" + data.fire + ":::::::::::" + data.player_name + "::::::::" + data.fire);
            if (allmissiles.ContainsKey(data.id))
            {

                if (data.deadPlayerId == true.ToString())
                {
                    if (allmissiles[data.id].gameObject)
                        allmissiles[data.id].ExplodeAndDestroy();
                }
                else
                    allmissiles[data.id].UpdatePosRot(missilePosition, missileRotation);
            }

            if (!allmissiles.ContainsKey(data.id) && SocketPlayerManager.Instance.allPlayers.ContainsKey(data.player_id) && data.deadPlayerId != true.ToString())
            {


                #region Old Gun
                //if (data.fire.Equals("bullet"))
                //{
                //    if (SocketPlayerManager.Instance.allPlayers[data.player_id].isAIPlayer)
                //    {
                //        if (!SocketPlayerManager.Instance.helixPlayerInfo.isRoomHostUser)
                //            SocketPlayerManager.Instance.allPlayers[data.player_id].gunControllerinAi.GenBullate(missilePosition, missileRotation);
                //    }
                //    else
                //        SocketPlayerManager.Instance.allPlayers[data.player_id].gunController.GenBullate(missilePosition, missileRotation);
                //}

                //else if (data.fire.Equals("missile"))
                //{
                //    if (SocketPlayerManager.Instance.allPlayers[data.player_id].isAIPlayer)
                //    {
                //        SocketPlayerManager.Instance.allPlayers[data.player_id].gunControllerinAi.MissailBullate(missilePosition, missileRotation, data.force, data.id);
                //    }
                //    else
                //        SocketPlayerManager.Instance.allPlayers[data.player_id].gunController.MissailBullate(missilePosition, missileRotation, data.force, data.id);
                //}
                #endregion

                #region New Gun
                if (data.fire.Equals("bullet"))
                {
                    //if (SocketPlayerManager.Instance.allPlayers[data.player_id].isAIPlayer)
                    //{
                    //    if (!SocketPlayerManager.Instance.helixPlayerInfo.isRoomHostUser && SocketPlayerManager.Instance.allPlayers[data.player_id].currentGunController)
                    //    {
                    //        SocketPlayerManager.Instance.allPlayers[data.player_id].currentGunController.Fire(false);
                    //    }
                    //        //SocketPlayerManager.Instance.allPlayers[data.player_id].gunControllerinAi.GenBullate(missilePosition, missileRotation);
                    //}
                    //else
                    //{
                    if (SocketPlayerManager.Instance.allPlayers[data.player_id].currentGunController)
                    {

                        if (data.force == -1)
                        {
                            Debug.Log(":::::::Get STOP:::>>>");
                            SocketPlayerManager.Instance.allPlayers[data.player_id].currentGunController.Stop();
                            SocketPlayerManager.Instance.allPlayers[data.player_id].currentGunController.gameObject.SetActive(false);
                            SocketPlayerManager.Instance.allPlayers[data.player_id].currentGunController.f3DPlayerTurretController.isFiring = false;
                            SocketPlayerManager.Instance.allPlayers[data.player_id].currentGunController.gameObject.SetActive(true);
                        }
                        else
                            SocketPlayerManager.Instance.allPlayers[data.player_id].currentGunController.Fire(false);
                    }

                    //}
                }

                else if (data.fire.Equals("missile"))
                {
                    if (SocketPlayerManager.Instance.allPlayers[data.player_id].isAIPlayer)
                    {
                        SocketPlayerManager.Instance.allPlayers[data.player_id].gunControllerinAi.MissailBullate(missilePosition, missileRotation, data.force, data.id);
                    }
                    else
                        SocketPlayerManager.Instance.allPlayers[data.player_id].gunController.MissailBullate(missilePosition, missileRotation, data.force, data.id);
                }

                else if (data.fire.Equals("NormalBullet") && SocketPlayerManager.Instance.allPlayers.ContainsKey(data.player_id))
                {
                    //Debug.Log(":::::::>>>>>>>>>>&&&&&&&&&&&&&&&&*************");
                    if (SocketPlayerManager.Instance.allPlayers[data.player_id].isAIPlayer)
                    {
                        if (!SocketPlayerManager.Instance.helixPlayerInfo.isRoomHostUser)
                            SocketPlayerManager.Instance.allPlayers[data.player_id].gunControllerinAi.GenBullate(missilePosition, missileRotation);
                    }
                    else
                        SocketPlayerManager.Instance.allPlayers[data.player_id].gunController.GenBullate(missilePosition, missileRotation);
                }

                else if (data.fire.Equals("NormalMissile") && SocketPlayerManager.Instance.allPlayers.ContainsKey(data.player_id))
                {

                    if ((SocketPlayerManager.Instance.allPlayers[data.player_id].isAIPlayer && !SocketPlayerManager.Instance.helixPlayerInfo.isRoomHostUser) || !SocketPlayerManager.Instance.allPlayers[data.player_id].isAIPlayer)
                        SocketPlayerManager.Instance.allPlayers[data.player_id].f3DMissileLauncher.FireMissile((int)data.force);
                }
                //Debug.Log(":::::::>>>>>>>>>>&&&&&&&&&&&&&&&&*************" + data.fire);
                #endregion
            }

        }
        catch (Exception e)
        {
            Debug.Log($"GameplayManager ---> UpdateBulletHit ---> {e.ToString()}");
        }
    }

    public void OnLoadeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);

    }

    public void OnOthorplayerLeft(Data data)
    {
        for (int i = 0; i < playerList.Count; i++)
        {
            if (playerList.Count > i && playerList[i].playerID.Equals(data.left_player_id))
            {
                Destroy(playerList[i].gameObject);
                playerList.RemoveAt(i);
            }
        }
    }

    public void MyPlayerLeft()
    {
        for (int i = 0; i < playerList.Count; i++)
        {
            Debug.Log("UserName: " + playerList[i].name);
            //Debug.Log("UserID: " + playerList[i].playerID);
            Destroy(playerList[i].gameObject);
        }
        //Debug.Log("Before Clear List Count: " + playerList.Count);
        playerList.Clear();
        //Debug.Log("After Clear List Count: " + playerList.Count);
        SceneManager.LoadScene("FirstScene");

    }




    public void CarColorAssigner(PlayerID currentPlayer)
    {
        #region COLORS

        Color bland = new Color32(255, 255, 255, 255);



        Color wheel_light = new Color32(255, 255, 255, 255);

        Color body = new Color32(255, 255, 255, 255);

        Color molding = new Color32(32, 32, 32, 255);

        Color frame = new Color32(255, 255, 255, 255);

        Color inner_frame = new Color32(255, 255, 255, 255);

        Color logo = new Color32(255, 255, 255, 255);

        Color bumpers = new Color32(255, 255, 255, 255);

        Color spoiler = new Color32(255, 255, 255, 255);

        Color front_rear_fascia = new Color32(255, 255, 255, 255);

        Color accent_lights = new Color32(79, 170, 244, 255);

        Color led_logo = new Color32(255, 255, 255, 255);

        Color cargo_cover = new Color32(32, 32, 32, 255);

        Color front_grille = new Color32(32, 32, 32, 255);

        Color warning_lights = new Color32(255, 135, 0, 255);

        Color molding_material = new Color32(255, 255, 255, 255);

        Color upper_body = new Color32(255, 255, 255, 255);

        Color lower_body = new Color32(255, 255, 255, 255);

        Color rear_frame = new Color32(255, 255, 255, 255);

        Color accent_piece_one = new Color32(255, 255, 255, 255);

        Color led_lights = new Color32(255, 255, 255, 255);


        #endregion

        string[] colorArray;

        byte subRed;

        byte subGreen;

        byte subBlue;

        byte subAlpha = 255;

        if (currentPlayerInformation.title.Equals("Hunter") || currentPlayerInformation.title.Equals("Hacker"))
        {
            currentPlayerInformation.title = "Hammer";
        }

        switch (currentPlayerInformation.title)
        {

            #region Hunter
            //case "Hunter":

            //    #region COLOR SETTINGS

            //    #region WHEEL LIGHT

            //    if (currentPlayerInformation.playerAttributes.wheel_light.color != "original")
            //    {
            //        colorArray = currentPlayerInformation.playerAttributes.wheel_light.color.Substring(4, currentPlayerInformation.playerAttributes.wheel_light.color.Length - 4 - 1).Split(char.Parse(","));

            //        subRed = (byte)int.Parse(colorArray[0]);

            //        subGreen = (byte)int.Parse(colorArray[1]);

            //        subBlue = (byte)int.Parse(colorArray[2]);

            //        wheel_light = new Color32(subRed, subGreen, subBlue, subAlpha);
            //    }
            //    #endregion

            //    #region BODY

            //    if (currentPlayerInformation.playerAttributes.body.color != "original")
            //    {
            //        colorArray = currentPlayerInformation.playerAttributes.body.color.Substring(4, currentPlayerInformation.playerAttributes.body.color.Length - 4 - 1).Split(char.Parse(","));

            //        subRed = (byte)int.Parse(colorArray[0]);

            //        subGreen = (byte)int.Parse(colorArray[1]);

            //        subBlue = (byte)int.Parse(colorArray[2]);

            //        body = new Color32(subRed, subGreen, subBlue, subAlpha);
            //    }
            //    #endregion

            //    #region FRAME

            //    if (currentPlayerInformation.playerAttributes.frame.color != "original")
            //    {
            //        colorArray = currentPlayerInformation.playerAttributes.frame.color.Substring(4, currentPlayerInformation.playerAttributes.frame.color.Length - 4 - 1).Split(char.Parse(","));

            //        subRed = (byte)int.Parse(colorArray[0]);

            //        subGreen = (byte)int.Parse(colorArray[1]);

            //        subBlue = (byte)int.Parse(colorArray[2]);

            //        frame = new Color32(subRed, subGreen, subBlue, subAlpha);
            //    }
            //    #endregion

            //    #region INNER FRAME

            //    if (currentPlayerInformation.playerAttributes.inner_frame.color != "original")
            //    {
            //        colorArray = currentPlayerInformation.playerAttributes.inner_frame.color.Substring(4, currentPlayerInformation.playerAttributes.inner_frame.color.Length - 4 - 1).Split(char.Parse(","));

            //        subRed = (byte)int.Parse(colorArray[0]);

            //        subGreen = (byte)int.Parse(colorArray[1]);

            //        subBlue = (byte)int.Parse(colorArray[2]);

            //        inner_frame = new Color32(subRed, subGreen, subBlue, subAlpha);
            //    }
            //    #endregion

            //    #region LOGO

            //    if (currentPlayerInformation.playerAttributes.logo.color != "original")
            //    {
            //        colorArray = currentPlayerInformation.playerAttributes.logo.color.Substring(4, currentPlayerInformation.playerAttributes.logo.color.Length - 4 - 1).Split(char.Parse(","));

            //        subRed = (byte)int.Parse(colorArray[0]);

            //        subGreen = (byte)int.Parse(colorArray[1]);

            //        subBlue = (byte)int.Parse(colorArray[2]);

            //        logo = new Color32(subRed, subGreen, subBlue, subAlpha);
            //    }
            //    #endregion

            //    #endregion

            //    break;

            #endregion
            case "Hammer":

                #region BODY

                if (currentPlayerInformation.playerAttributes.body.color != "original")
                {

                    currentPlayerInformation.playerAttributes.body.color = currentPlayerInformation.playerAttributes.body.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.body.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    body = new Color32(subRed, subGreen, subBlue, subAlpha);


                }
                else
                    body = new Color32(233, 233, 233, 255);

                #endregion

                #region Front_grille
                if (currentPlayerInformation.playerAttributes.front_grille.color != "original")
                {

                    Debug.Log(currentPlayerInformation.playerAttributes.front_grille.color.ToString());

                    currentPlayerInformation.playerAttributes.front_grille.color = currentPlayerInformation.playerAttributes.front_grille.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.front_grille.color.Split(char.Parse(","));

                    for (int i = 0; i < colorArray.Length; i++)
                    {
                        Debug.Log("colorArray:::>>>" + colorArray[i].ToString());

                    }
                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    front_grille = new Color32(subRed, subGreen, subBlue, subAlpha);

                    Debug.Log("Front_grille Color:::>>>>>>" + subRed + ":::::" + subGreen + ":::::" + subBlue + "colorArray::" + colorArray.ToString() + "::::::" + currentPlayerInformation.playerAttributes.front_grille.color.ToString());
                }
                #endregion

                #region MOLDING

                if (currentPlayerInformation.playerAttributes.molding.color != "original")
                {
                    currentPlayerInformation.playerAttributes.molding.color = currentPlayerInformation.playerAttributes.molding.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.molding.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    molding = new Color32(subRed, subGreen, subBlue, subAlpha);
                }

                #endregion

                #region Cargo Cover

                if (currentPlayerInformation.playerAttributes.cargo_cover.color != "original")
                {
                    currentPlayerInformation.playerAttributes.cargo_cover.color = currentPlayerInformation.playerAttributes.cargo_cover.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.cargo_cover.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    cargo_cover = new Color32(subRed, subGreen, subBlue, subAlpha);
                }

                #endregion

                #region LED_LOGO

                if (currentPlayerInformation.playerAttributes.led_logo.color != "original")
                {
                    currentPlayerInformation.playerAttributes.led_logo.color = currentPlayerInformation.playerAttributes.led_logo.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.led_logo.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    led_logo = new Color32(subRed, subGreen, subBlue, subAlpha);
                }

                #endregion

                #region Accent_lights

                if (currentPlayerInformation.playerAttributes.accent_lights.color != "original")
                {
                    currentPlayerInformation.playerAttributes.accent_lights.color = currentPlayerInformation.playerAttributes.accent_lights.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.accent_lights.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    accent_lights = new Color32(subRed, subGreen, subBlue, subAlpha);
                }

                #endregion

                #region Warning_lights

                if (currentPlayerInformation.playerAttributes.warning_lights.color != "original")
                {
                    currentPlayerInformation.playerAttributes.warning_lights.color = currentPlayerInformation.playerAttributes.warning_lights.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.warning_lights.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    warning_lights = new Color32(subRed, subGreen, subBlue, subAlpha);
                }

                #endregion

                switch (currentPlayerInformation.playerWeapons.Count)
                {
                    case 0:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColorAndAddWeapons_Hammer(body, molding, led_logo, cargo_cover, front_grille, accent_lights, warning_lights, molding_material, "", "", "");
                        break;
                    case 1:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColorAndAddWeapons_Hammer(body, molding, led_logo, cargo_cover, front_grille, accent_lights, warning_lights, molding_material, currentPlayerInformation.playerWeapons[0].weapon, "", "");
                        break;
                    case 2:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColorAndAddWeapons_Hammer(body, molding, led_logo, cargo_cover, front_grille, accent_lights, warning_lights, molding_material, currentPlayerInformation.playerWeapons[0].weapon, currentPlayerInformation.playerWeapons[1].weapon, "");
                        break;
                    case 3:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColorAndAddWeapons_Hammer(body, molding, led_logo, cargo_cover, front_grille, accent_lights, warning_lights, molding_material, currentPlayerInformation.playerWeapons[0].weapon, currentPlayerInformation.playerWeapons[1].weapon, currentPlayerInformation.playerWeapons[2].weapon);
                        break;
                    default:

                        break;
                }

                break;

            case "Hype":

                #region BODY

                if (currentPlayerInformation.playerAttributes.body.color != "original")
                {

                    currentPlayerInformation.playerAttributes.body.color = currentPlayerInformation.playerAttributes.body.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.body.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    body = new Color32(subRed, subGreen, subBlue, subAlpha);


                }
                else
                {
                    body = new Color32(183, 183, 183, 255);
                }

                #endregion

                #region logo

                if (currentPlayerInformation.playerAttributes.logo.color != "original")
                {
                    currentPlayerInformation.playerAttributes.logo.color = currentPlayerInformation.playerAttributes.logo.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.logo.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    logo = new Color32(subRed, subGreen, subBlue, subAlpha);
                }

                #endregion

                #region frame

                if (currentPlayerInformation.playerAttributes.frame.color != "original")
                {
                    currentPlayerInformation.playerAttributes.frame.color = currentPlayerInformation.playerAttributes.frame.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.frame.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    frame = new Color32(subRed, subGreen, subBlue, subAlpha);
                }
                else
                    frame = new Color32(43, 43, 43, 255);

                #endregion


                #region bumpers

                if (currentPlayerInformation.playerAttributes.bumpers.color != "original")
                {
                    currentPlayerInformation.playerAttributes.bumpers.color = currentPlayerInformation.playerAttributes.bumpers.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.bumpers.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    bumpers = new Color32(subRed, subGreen, subBlue, subAlpha);
                }
                else
                    bumpers = new Color32(42, 72, 204, 255);

                #endregion

                #region spoiler

                if (currentPlayerInformation.playerAttributes.spoiler.color != "original")
                {
                    currentPlayerInformation.playerAttributes.spoiler.color = currentPlayerInformation.playerAttributes.spoiler.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.spoiler.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    spoiler = new Color32(subRed, subGreen, subBlue, subAlpha);
                }
                else
                    spoiler = new Color32(42, 72, 204, 255);
                #endregion

                #region led_lights

                if (currentPlayerInformation.playerAttributes.led_lights.color != "original")
                {
                    currentPlayerInformation.playerAttributes.led_lights.color = currentPlayerInformation.playerAttributes.led_lights.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.led_lights.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    led_lights = new Color32(subRed, subGreen, subBlue, subAlpha);
                }

                #endregion

                #region wheel_light

                if (currentPlayerInformation.playerAttributes.wheel_light.color != "original")
                {
                    currentPlayerInformation.playerAttributes.wheel_light.color = currentPlayerInformation.playerAttributes.wheel_light.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.wheel_light.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    wheel_light = new Color32(subRed, subGreen, subBlue, subAlpha);
                }
                else
                {
                    wheel_light = new Color32(0, 0, 0, 0);
                }

                #endregion


                switch (currentPlayerInformation.playerWeapons.Count)
                {
                    case 0:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColor_Hype(body, logo, frame, bumpers, spoiler, led_lights, wheel_light);
                        break;
                    case 1:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColor_Hype(body, logo, frame, bumpers, spoiler, led_lights, wheel_light);
                        break;
                    case 2:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColor_Hype(body, logo, frame, bumpers, spoiler, led_lights, wheel_light);
                        break;
                    case 3:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColor_Hype(body, logo, frame, bumpers, spoiler, led_lights, wheel_light);
                        break;
                    default:

                        break;
                }

                break;


            case "Hero":

                #region BODY

                if (currentPlayerInformation.playerAttributes.body.color != "original")
                {

                    currentPlayerInformation.playerAttributes.body.color = currentPlayerInformation.playerAttributes.body.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.body.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    body = new Color32(subRed, subGreen, subBlue, subAlpha);


                }
                else
                    body = new Color32(160, 160, 160, 255);

                #endregion

                #region LED_LOGO

                if (currentPlayerInformation.playerAttributes.logo.color != "original")
                {
                    currentPlayerInformation.playerAttributes.logo.color = currentPlayerInformation.playerAttributes.logo.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.logo.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    logo = new Color32(subRed, subGreen, subBlue, subAlpha);
                }

                #endregion

                #region Accent_lights

                if (currentPlayerInformation.playerAttributes.accent_lights.color != "original")
                {
                    currentPlayerInformation.playerAttributes.accent_lights.color = currentPlayerInformation.playerAttributes.accent_lights.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.accent_lights.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    accent_lights = new Color32(subRed, subGreen, subBlue, subAlpha);
                }

                #endregion

                #region wheel_light

                if (currentPlayerInformation.playerAttributes.wheel_light.color != "original")
                {
                    currentPlayerInformation.playerAttributes.wheel_light.color = currentPlayerInformation.playerAttributes.wheel_light.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.wheel_light.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    wheel_light = new Color32(subRed, subGreen, subBlue, subAlpha);
                }

                #endregion

                #region front_rear_fascia

                if (currentPlayerInformation.playerAttributes.front_rear_fascia.color != "original")
                {
                    currentPlayerInformation.playerAttributes.front_rear_fascia.color = currentPlayerInformation.playerAttributes.front_rear_fascia.color.Replace("r", "").Replace("g", "").Replace("b", "").Replace("(", "").Replace(")", "");
                    colorArray = currentPlayerInformation.playerAttributes.front_rear_fascia.color.Split(char.Parse(","));

                    subRed = (byte)int.Parse(colorArray[0]);

                    subGreen = (byte)int.Parse(colorArray[1]);

                    subBlue = (byte)int.Parse(colorArray[2]);

                    front_rear_fascia = new Color32(subRed, subGreen, subBlue, subAlpha);
                }
                else
                    front_rear_fascia = new Color32(0, 0, 0, 255);


                #endregion


                switch (currentPlayerInformation.playerWeapons.Count)
                {
                    case 0:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColor_Hero(body, logo, front_rear_fascia, accent_lights);
                        break;
                    case 1:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColor_Hero(body, logo, front_rear_fascia, accent_lights);
                        break;
                    case 2:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColor_Hero(body, logo, front_rear_fascia, accent_lights);
                        break;
                    case 3:
                        currentPlayer.currentPlayerColorChanger.ChangePartsColor_Hero(body, logo, front_rear_fascia, accent_lights);
                        break;
                    default:

                        break;
                }

                break;
        }
    }


}
