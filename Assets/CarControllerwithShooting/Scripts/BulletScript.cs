using System.Collections;
using UnityEngine;

namespace CarControllerwithShooting
{
    public class BulletScript : MonoBehaviour
    {
        public GameObject explosionPrefab;
        int DamagePower = 5;
        public Rigidbody rb;

        internal string shooterID;
        internal bool teamA, teamB;

        float speed = 60;

        float activeTime = 0;
        private void OnEnable()
        {
            activeTime = 0;
        }


        /* IEnumerator Start()
         {
             yield return new WaitForSeconds(3);
             Destroy(gameObject);
         }*/



        private void Update()
        {
            //if ((playerID == SocketPlayerManager.Instance.player_Id || isAI) && rb != null)
            //{

            rb.velocity = this.transform.forward * speed;

            activeTime += Time.deltaTime;

            if (activeTime > 20)
            {
                ExplodeAndDestroy();
            }

            //}
        }

        private void OnTriggerEnter(Collider other)
        {

            if (other.CompareTag("Enemy"))
            {
                PlayerID playerIDComponent = other.GetComponent<PlayerID>();

                //   Debug.Log(" ENEMY::  Player ID: " + playerIDComponent.helixPlayerInfo.userName + "::::" + SocketPlayerManager.Instance.allPlayers[shooterID].helixPlayerInfo.userName);
                if (playerIDComponent != null && playerIDComponent.playerID != SocketNetworkManager.Instance.playerID)
                {

                    if ((playerIDComponent.helixPlayerInfo.teamA && !teamA) || (!playerIDComponent.helixPlayerInfo.teamA && teamA) || (playerIDComponent.helixPlayerInfo.teamB && !teamB) || (!playerIDComponent.helixPlayerInfo.teamB && teamB))
                        SocketNetworkManager.Instance.EmitHealth(shooterID, playerIDComponent.playerID, DamagePower.ToString());
                    else
                    {
                        //Debug.Log(playerIDComponent.helixPlayerInfo.userName +"::Get damage:::::>>>" + playerIDComponent.helixPlayerInfo.teamA + "::::::" + teamA + "BBBBB" + playerIDComponent.helixPlayerInfo.teamB + "::::::" + teamB);
                    }
                }
            }

            else if (other.CompareTag("Player"))
            {
                PlayerID playerIDComponent = other.GetComponent<PlayerID>();
                //  Debug.Log("PLAYER::  Player ID: " + playerIDComponent.helixPlayerInfo.userName + "::::" + SocketPlayerManager.Instance.allPlayers[shooterID].helixPlayerInfo.userName);
                if (playerIDComponent != null /*&& playerIDComponent.playerID != SocketNetworkManager.Instance.playerID*/)
                {

                    if ((playerIDComponent.helixPlayerInfo.teamA && !teamA) || (!playerIDComponent.helixPlayerInfo.teamA && teamA) || (playerIDComponent.helixPlayerInfo.teamB && !teamB) || (!playerIDComponent.helixPlayerInfo.teamB && teamB))
                        SocketNetworkManager.Instance.EmitHealth(shooterID, playerIDComponent.playerID, DamagePower.ToString());
                    else
                    {
                        //Debug.Log(  playerIDComponent.helixPlayerInfo.userName +":::Get damage:::::>>>" + playerIDComponent.helixPlayerInfo.teamA + "::::::" + teamA + "BBBBB" + playerIDComponent.helixPlayerInfo.teamB + "::::::" + teamB);
                    }
                }
            }

            if (other.gameObject.CompareTag("OilBarrel")/*&&f3DFXController && f3DFXController.playerid*/)
            {
                //Debug.Log("Other Gameobject Name::" + other.gameObject.name);
                other.gameObject.GetComponent<OilBarrel>().Blast(shooterID);
            }
            //Debug.Log(other.gameObject.name);
            ExplodeAndDestroy();
        }

        private void ExplodeAndDestroy()
        {
            //alive = false;
            GameObject muzzle = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            muzzle.transform.eulerAngles = new Vector3(Random.Range(0, -180), 0, 0);
            Destroy(gameObject, 0);

        }
    }


}
