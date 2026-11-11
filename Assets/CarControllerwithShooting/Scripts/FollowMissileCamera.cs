
using System.Collections;
using UnityEngine;
using Cinemachine;
using CarControllerwithShooting;


public class FollowMissileCamera : MonoBehaviour
{
    public Transform Target;

    public CinemachineVirtualCamera vc;
    //public Transform CarCameraTarget;
    public float PositionFolowForce = 5f;
    public float RotationFolowForce = 5f;

    void Start()
    {
        // Subscribe to the missile fired event (Dhruv)
        GameplayManager.OnMissileFired += HandleMissileFired;

    }


    

    void FixedUpdate()
    {
        if (Target != null)
        {
            var vector = Vector3.forward;
            var dir = Target.rotation * Vector3.forward;
            dir.y = 0f;
            if (dir.magnitude > 0f) vector = dir / dir.magnitude;

            transform.position = Vector3.Lerp(transform.position, Target.position, PositionFolowForce * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(vector), RotationFolowForce * Time.deltaTime);
        }
    }
    private void HandleMissileFired(Transform missileTransform, string playerid)
    {
        //Debug.Log("::::::::::::::::::::::::::>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>" + missileTransform != null);
        if (missileTransform != null)
        {
            // Change the target to the missile (Dhruv)
            Target = missileTransform;
            vc.m_LookAt = Target;
            vc.m_Follow = Target;
            GameCanvas.Instance.crossHairHolder.SetActive(false);
            vc.gameObject.SetActive(true);

        }
        else
        {
            // Reset the target back to the car (Dhruv)
            vc.m_LookAt = null;
            vc.m_Follow = null;
            GameCanvas.Instance.crossHairHolder.SetActive(true);
            StartCoroutine(AssigncardCamera());
        }
    }


    IEnumerator AssigncardCamera()
    {
        //Debug.Log("::::::::::>>>????");
        yield return new WaitForSeconds(2);
        if (vc.m_Follow ==null)
        {
            if (SocketPlayerManager.Instance.MyPlayer)
                Target = SocketPlayerManager.Instance.MyPlayer.controller.camTarget.transform;
            vc.gameObject.SetActive(false);
            //Debug.Log("UnSubscribe Event");
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from the event when the script is destroyed (Dhruv)
        GameplayManager.OnMissileFired -= HandleMissileFired;
    }
}
