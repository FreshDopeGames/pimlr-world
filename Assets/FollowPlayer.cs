using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        //Debug.Log(GameObject.FindGameObjectWithTag("MainCamera").GetComponent<FollowMissileCamera>().name);
        //GameObject.FindGameObjectWithTag("MainCamera").GetComponent<FollowMissileCamera>().CarCameraTarget = this.gameObject.transform;
        GameplayManager.Instance.virtualCamera.m_LookAt = this.gameObject.transform;
        GameplayManager.Instance.virtualCamera.m_Follow = this.gameObject.transform;
    }
    private void Update()
    {
        //Debug.Log("FollowPlayer Update");
    }
}