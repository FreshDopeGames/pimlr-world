using UnityEngine;
using System;
using System.Collections; // For coroutines
using Cinemachine;

public class MissileControl : MonoBehaviour
{
    float speed = 20;
    public float rotationSpeed = 1.1f;
    private Rigidbody rb;

    // Static reference to the currently controlled missile

    // Event to notify missile destruction

    private bool controlEnabled = false; // Flag to enable control


    internal string playerID;

    internal bool isAI = false;

    void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
        _ = StartCoroutine(EnableControlAfterDelay(1f)); // 2 seconds delay before enabling control

    }

    IEnumerator EnableControlAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        controlEnabled = true;
    }

    float moveHorizontal = 0;
    float moveVertical = 0;




    public float RotationSmoothTime = 0.12f;



    void Update()
    {
        // Check if this missile is the currently controlled one
        if (playerID == SocketPlayerManager.Instance.player_Id && controlEnabled && !isAI)
        {
            //Debug.Log("::::::::::::::::::::>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
            moveHorizontal = Input.GetAxisRaw("Horizontal");
            moveVertical = Input.GetAxisRaw("Vertical");


            ///OLD
            //Debug.Log(moveHorizontal + ":::::::::" + moveVertical + "::::::" + transform.eulerAngles);
            transform.Rotate(moveVertical * rotationSpeed * Time.deltaTime, moveHorizontal * rotationSpeed * Time.deltaTime, 0, Space.Self);


            ///Try1
            //_targetRotation = Mathf.Atan2(moveHorizontal, moveVertical) * Mathf.Rad2Deg +Camera.main.transform.eulerAngles.y;
            //rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,RotationSmoothTime);

            //// rotate to face input direction relative to camera position
            //transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);

            // transform.Rotate(new Vector3 (moveHorizontal,moveVertical,0) * rotationSpeed * Time.deltaTime);




            ///Try 2
            //transform.localEulerAngles = transform.localEulerAngles+( new Vector3(moveVertical, moveHorizontal, 0)*2);



            ///Try 3
            //Get camera angle: input to radians - radians to  degrees - degrees plus the camera angle - limit angle to 45 degree increments
            //angle = Mathf.Atan2(moveHorizontal, moveVertical);
            //angle = Mathf.Rad2Deg * angle;
            ////angle += Camera.main.transform.eulerAngles.y;
            //angle = Mathf.Round(angle / 45) * 45;

            //Rotate Character
            //targetRotation = Quaternion.Euler(0, angle, 0);
            //transform.rotation = targetRotation;

            //Try4
            // transform.Rotate(new Vector3(moveVertical, moveHorizontal, 0) * Time.deltaTime*50);

            ////Try 5
            //float horizontalRotation = moveHorizontal * rotationSpeed * Time.deltaTime;
            //float verticalRotation = moveVertical * rotationSpeed * Time.deltaTime;
            //// Apply rotation to the player object
            //transform.Rotate(Vector3.up, horizontalRotation);
            //transform.Rotate(Vector3.right, verticalRotation);

            ////Try6
            //float horizontalInput = Input.GetAxis("Horizontal");
            //float verticalInput = Input.GetAxis("Vertical");

            //// Rotate the player object based on input
            //RotatePlayer(horizontalInput, verticalInput);




        }

        //else if (isAI && SocketPlayerManager.Instance.helixPlayerInfo.isRoomHostUser)
        //{
        //    //Vector3 force = playerID.gunController.FiringPoints_Missiles.transform.forward * 15;
        //    //rb.AddForce(force, ForceMode.Impulse);
        //}


    }






    //void RotatePlayer(float horizontalInput, float verticalInput)
    //{
    //    // Calculate the rotation amounts based on input and speed
    //    float horizontalRotation = horizontalInput * rotationSpeed * Time.deltaTime;
    //    float verticalRotation = verticalInput * rotationSpeed * Time.deltaTime;

    //    // Apply rotation to the player object in local space
    //    transform.Rotate(Vector3.up, horizontalRotation, Space.Self);
    //    transform.Rotate(Vector3.right, -verticalRotation, Space.Self); // Invert vertical rotation to match typical controls
    //}
    void RotatePlayer(float horizontalInput, float verticalInput)
    {
        // Calculate the rotation amounts based on input and speed
        float horizontalRotation = horizontalInput * rotationSpeed * Time.deltaTime;
        float verticalRotation = verticalInput * rotationSpeed * Time.deltaTime;

        // Combine rotations into a single Quaternion
        Quaternion rotation = Quaternion.Euler(-verticalRotation, horizontalRotation, 0f);

        // Apply the rotation to the player object
        transform.localRotation *= rotation;
    }










    void OnDestroy()
    {
        if (playerID == SocketPlayerManager.Instance.player_Id)
        {
            // Trigger the event when the controlled missile is destroyed

            if (SocketPlayerManager.Instance.MyPlayer)
                SocketPlayerManager.Instance.MyPlayer.controller.playeInputsActivate = true;
            GameplayManager.Instance.OnRaiseMissileDestroyHandler();

        }

        if (SocketPlayerManager.Instance.allPlayers.ContainsKey(playerID))
        {
            if ((isAI && SocketPlayerManager.Instance.helixPlayerInfo.isRoomHostUser) || playerID == SocketPlayerManager.Instance.player_Id)
            {
                if (SocketPlayerManager.Instance.allPlayers[playerID].controller.currentSecondaryPowerUp != null)
                    SocketPlayerManager.Instance.allPlayers[playerID].controller.currentSecondaryPowerUp.OnCompletedUsePowerUp(playerID, true);

            }
        }

    }

    void FixedUpdate()
    {
        if ((playerID == SocketPlayerManager.Instance.player_Id || isAI) && rb != null)
        {
            rb.velocity = transform.forward * speed;
            //Debug.Log("Speed:::::::::" + speed);
        }

    }

    public void SetAsCurrentControlledID(string currentPlayerID)
    {
        playerID = currentPlayerID;
        //CurrentControlledMissile = this;
    }
}
