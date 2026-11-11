using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class UserCar : MonoBehaviour
{
    public CinemachineVirtualCamera[] carCameras; // Assign your car cameras in the order: Helix, Hero, Hype

    private void Start()
    {
        SetCameraPriority();
    }

    private void SetCameraPriority()
    {
        if (PlayerPrefs.HasKey("SelectedCar"))
        {
            string selectedCar = PlayerPrefs.GetString("SelectedCar");
            Debug.Log(selectedCar);

            int highestPriority = 10; // Setting a higher initial value for the selected car's camera

            foreach (var camera in carCameras)
            {
                Debug.Log(camera.gameObject.name);

                if (camera.transform.parent.gameObject.name == selectedCar)
                {
                    camera.Priority = highestPriority;
                }
                else
                {
                    camera.Priority = 1; // Setting other cameras to the default priority
                }
            }
        }
    }

}
