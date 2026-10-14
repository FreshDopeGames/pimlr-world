using JUTPS;
using JUTPS.InventorySystem;
using JUTPS.ItemSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerHandler : MonoBehaviour
{

    public GameObject playerCar;
    public ItemSwitchManager itemSwitchManager;
    public JUCharacterController jUCharacterController;
    public Camera minimapCamera;

    private void OnEnable()
    {
        SceneManagerScript.Instance.minimapBlipController.playerBlip.gameObject.SetActive(true);
    }
    // Start is called before the first frame update
    void Start()
    {
        SceneManagerScript.Instance.minimapBlipController.player = this.transform;
        SceneManagerScript.Instance.minimapBlipController.minimapCamera = minimapCamera;
        SceneManagerScript.Instance.minimapBlipController.playercar = playerCar.transform;

        //itemSwitchManager.IsPlayer = false;
    }

    // Update is called once per frame
    void Update()
    {
        //if (GameExecutionManager.Instance.zone2Finish == true)
        //{
        //    playerCar.SetActive(true);
        //}
    }

    public void OnLoadScene(string sceneName)
    {
        // PIMLR: the player's JUHealth.OnDeath calls this to restart the level. Play the slow-motion
        // "WASHED" sequence first; it reloads the scene (or shows the Infinite run summary) when it's done.
        JUHealth health = jUCharacterController != null && jUCharacterController.CharacterHealth != null
            ? jUCharacterController.CharacterHealth : GetComponent<JUHealth>();
        if (health != null && health.IsDead)
        {
            PimlrDeathSequence.Play(sceneName);
            return;
        }
        SceneManagerScript.Instance.LoadScene(sceneName);
    }

    /*int count = 0;
    public void PlayerSpwanCar()
    {
        count++;
        Debug.Log("Count"+count);
        if (count > 4)
            playerCar.SetActive(true);
    }*/
}
