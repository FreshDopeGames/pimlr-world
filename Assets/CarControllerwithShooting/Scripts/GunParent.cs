using DG.Tweening;
using GLTFast.Schema;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunParent : MonoBehaviour
{
    public Transform directionArrow;
    //float rotationSpeed = 10;

    private void Update()
    {
        //FindDirection();
    }

    public void FindDirection()
    {
        GameObject nearestObject = FindNearestObject();
        Debug.Log("nearestObject: " + nearestObject.name);
        if (nearestObject != null)
        {
            // Calculate the direction to the nearest object
            Vector3 direction = nearestObject.transform.position - directionArrow.position;
            transform.Rotate(0,0, direction.z);
        }
    }
    public GameObject FindNearestObject()
    {
        GameObject nearestObject = null;
        float minDistance = float.MaxValue;
        foreach (PlayerID obj in SocketPlayerManager.Instance.allPlayers.Values)
        {

            if (obj != null && obj.gameObject.activeSelf)
            {
                float distance = Vector3.Distance(directionArrow.position, obj.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestObject = obj.gameObject;
                }
            }
        }
        return nearestObject;
    }
}
