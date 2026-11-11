using AshVP;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackManager : MonoBehaviour
{
    public static TrackManager instance;

    public CinemachineBrain mainCamBrain;

    public WaypointCircuit circuit;

    public List<PlayerCarContrtoller> playerCarContrtollers;

    public List<GameObject> currentPlayerCamera;


    private void Awake ()
    {
        instance = this;
    }



    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
