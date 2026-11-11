using Cinemachine;
using System.Collections;
using UnityEngine;

namespace CarControllerwithShooting
{
    public class FollowTargetCamera : MonoBehaviour
    {
        public Transform Target;
        public Transform CarCameraTarget;
        public float DistanceBehind = 10f; // Static distance behind the car
        public float BaseDistanceAbove = 5f; // Base distance above the car
        public float PositionFollowForce = 5f;
        public float RotationFollowForce = 5f;
        public float ZoomSensitivity = 10f;
        public float MinFOV = 15f;
        public float MaxFOV = 90f;
        public float MinDistanceAbove = 2f; // Minimum distance above the car
        public float MaxDistanceAbove = 15f; // Maximum distance above the car

        //private CinemachineVirtualCamera childCamera;

        void Start()
        {
            //childCamera = GetComponentInChildren<CinemachineVirtualCamera>();
            GameplayManager.OnMissileFired += HandleMissileFired;
        }

        bool isFinding;
        void FixedUpdate()
        {
            if (Target != null)
            {
                // Handle mouse scroll input for zooming
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (GameplayManager.Instance.virtualCamera != null)
                {
                    GameplayManager.Instance.virtualCamera.m_Lens.FieldOfView -= scroll * ZoomSensitivity;
                    GameplayManager.Instance.virtualCamera.m_Lens.FieldOfView = Mathf.Clamp(GameplayManager.Instance.virtualCamera.m_Lens.FieldOfView, MinFOV, MaxFOV);
                    //childCamera.LookAt = SocketPlayerManager.Instance.MyPlayer.transform;


                }
            }
            else if(!isFinding && Target==null)
            {
                isFinding = true;
                StartCoroutine(FindCameraReffernce());
            }
        }

        private void OnDisable()
        {
            StopCoroutine(FindCameraReffernce());
        }

        IEnumerator FindCameraReffernce()
        {
            yield return new WaitUntil(() => SocketPlayerManager.Instance && SocketPlayerManager.Instance.MyPlayer);

            Target = SocketPlayerManager.Instance.MyPlayer.controller.camTarget.transform;
            CarCameraTarget = SocketPlayerManager.Instance.MyPlayer.controller.camTarget.transform;

            isFinding = false;
        }

        private void HandleMissileFired(Transform missileTransform,string playerId)
        {

            if (playerId == SocketNetworkManager.Instance.playerID)
            {
                if (missileTransform != null)
                {
                    Target = missileTransform;
                    //GameplayManager.Instance.virtualCamera.LookAt = missileTransform;
                    //GameplayManager.Instance.virtualCamera.LookAt = null;
                }
                else
                {
                    //Target = SocketPlayerManager.Instance.MyPlayer.transform;
                    //childCamera.LookAt = SocketPlayerManager.Instance.MyPlayer.transform;
                }
            }
        }

        void OnDestroy()
        {
            GameplayManager.OnMissileFired -= HandleMissileFired;
        }
    }
}
