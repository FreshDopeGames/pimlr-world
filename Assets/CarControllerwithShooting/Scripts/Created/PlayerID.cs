using CarControllerwithShooting;
using UnityEngine;
//using System.Collections.Generic;
using System.Collections;
using FORGE3D;
using TMPro;

[System.Serializable]
public class PlayerID : MonoBehaviour
{
    public bool isLocalPlayer = false;
    public bool isAIPlayer = false;
    public string playerID;
    public GunController gunController;
    public GunControllerInAi gunControllerinAi;
    public Rigidbody rb;
    public CarController controller;
    public SpriteRenderer spriteRenderer;
    public HelixPlayerInfo helixPlayerInfo;
    public HelixPlayerHealth helixPlayerHealth;
    public F3DFXController f3DFXController1, f3DFXController2, f3DFXController3;
    public ColorChangeAndWeaponAssign currentPlayerColorChanger;
    public HelixCarFollower helixCarFollower;


    [SerializeField] internal F3DFXController currentGunController;
    public F3DMissileLauncher f3DMissileLauncher;
    public Transform shootingTransform;

    [SerializeField] internal TextMeshPro playerNameText;
    private IEnumerator Start()
    {
        yield return new WaitUntil(() => helixPlayerInfo.teamA || helixPlayerInfo.teamB);

        //Debug.Log("::::::::Assing Team"+ helixPlayerInfo.teamA+"::"+ helixPlayerInfo.teamB+"::>>"+helixPlayerInfo.userName);
        SetTeamColor();

		

		if (isLocalPlayer)
        {

            GameplayManager.Instance.virtualCamera.m_LookAt = controller.camTarget.transform;
            GameplayManager.Instance.virtualCamera.m_Follow = controller.camTarget.transform;

			if (SocketPlayerManager.Instance.helixPlayerInfo.teamA)
			{
				GameCanvas.Instance.scoreBgImageHolder.sprite = GameCanvas.Instance.scoreBgImage;
			}
		}
        if (!isAIPlayer)
        {
            gunController.Weapon_MissileLeft.SetActive(true);
            gunController.Weapon_MissileRight.SetActive(true);
        }
        else
        {
            gunControllerinAi.Weapon_MissileLeft.SetActive(true);
            gunControllerinAi.Weapon_MissileRight.SetActive(true);
        }
    }
    private void OnEnable()
    {
        if (isLocalPlayer && GameCanvas.Instance)
            GameCanvas.Instance.crossHair.SetActive(true);
    }


    public void UpdatePosRot(Data data)
    {


        helixPlayerInfo.userName = data.player_name;
        transform.position = new Vector3(data.dx, data.dy, data.dz);
        transform.eulerAngles = new Vector3(data.rx, data.ry, data.rz);
    }

    public void SetTeamColor()
    {
        //HelixPlayerInfo _player = SocketNetworkManager.GetPlayerData(player);
        //Debug.Log("SPrite color:::" + teamA+":::::1"+TeamB);


        playerNameText.text = helixPlayerInfo.userName;
        gameObject.name = helixPlayerInfo.userName;
        if (helixPlayerInfo.teamA)
        {
            

            //Debug.Log("TeamA::>>" + helixPlayerInfo.teamA + ":::MyPlayer::" + SocketPlayerManager.Instance.MyPlayer.helixPlayerInfo.teamA);
            //Debug.Log("PlayerName::>>" + helixPlayerInfo.userName);
            if (SocketPlayerManager.Instance.helixPlayerInfo.teamA)
            {
                playerNameText.color = Color.green;
				spriteRenderer.color = Color.blue;
			}
            else
            {
                playerNameText.color = Color.red;
				spriteRenderer.color = Color.red;
			}
        }
        else if (helixPlayerInfo.teamB)
        {
            //Debug.Log("TeamB::>>" + helixPlayerInfo.teamB + ":::MyPlayer::" + SocketPlayerManager.Instance.MyPlayer.helixPlayerInfo.teamB);
           
            if (SocketPlayerManager.Instance.helixPlayerInfo.teamB)
            {
                playerNameText.color = Color.green;
				spriteRenderer.color = Color.blue;
			}
            else
            {
                playerNameText.color = Color.red;
				spriteRenderer.color = Color.red;
			}
        }

    }

    public IEnumerator SetGun(bool active, F3DFXType collectedGun, float damagePower, float ammo)
    {

        //Debug.Log("SET GUN::::>>>>>>>>>???????>>>>>>>>>>????????>>>>>>"+this.gameObject.name+":::::"+collectedGun);
        if (f3DFXController1 != null)
        {

            if (!active)
            {
                yield return new WaitForSeconds(0.3f);
                f3DFXController1.gameObject.SetActive(false);
                f3DFXController2.gameObject.SetActive(false);
                f3DFXController3.gameObject.SetActive(false);
                currentGunController = null;
            }
            else
            {

                if (collectedGun == F3DFXType.Sniper || collectedGun == F3DFXType.RailGun || collectedGun == F3DFXType.PlasmaBeamHeavy || collectedGun == F3DFXType.FlameRed)
                {
                    //Debug.Log(":::::" + 1);
                    f3DFXController1.gameObject.SetActive(true);
                    f3DFXController2.gameObject.SetActive(false);
                    f3DFXController3.gameObject.SetActive(false);
                    currentGunController = f3DFXController1;


                }
                else if (collectedGun == F3DFXType.SoloGun || collectedGun == F3DFXType.Vulcan || collectedGun == F3DFXType.Seeker || collectedGun == F3DFXType.LightningGun)
                {
                    //Debug.Log(":::::" + 2);
                    f3DFXController1.gameObject.SetActive(false);
                    f3DFXController2.gameObject.SetActive(true);
                    f3DFXController3.gameObject.SetActive(false);
                    currentGunController = f3DFXController2;

                }
                else if (collectedGun == F3DFXType.ShotGun || collectedGun == F3DFXType.PlasmaBeam || collectedGun == F3DFXType.PlasmaGun || collectedGun == F3DFXType.LaserImpulse)
                {
                    //Debug.Log(":::::" + 3);
                    f3DFXController1.gameObject.SetActive(false);
                    f3DFXController2.gameObject.SetActive(false);
                    f3DFXController3.gameObject.SetActive(true);
                    currentGunController = f3DFXController3;

                }
                currentGunController.currentRound = ammo;
                currentGunController.DefaultFXType = collectedGun;
                currentGunController.DamagePower = damagePower;
                currentGunController.playerid = this;

                if (isLocalPlayer)
                {
                    // currentGunController.f3DPlayerTurretController.turret.DebugTarget = GameplayManager.Instance.gunTarget;
                }
            }
        }
    }



    private void OnDisable()
    {
        if (isLocalPlayer)
            GameCanvas.Instance.crossHair.SetActive(false);

        gunController.Weapon_MissileLeft.SetActive(false);
        gunController.Weapon_MissileRight.SetActive(false);
    }
}

