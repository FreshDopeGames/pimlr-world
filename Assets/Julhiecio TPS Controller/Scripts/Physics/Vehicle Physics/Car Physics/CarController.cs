using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JUTPS.JUInputSystem;

namespace JUTPS.VehicleSystem
{
    [AddComponentMenu("JU TPS/Vehicle System/Car Controller")]
    public class CarController : Vehicle
    {
        [Header("Wheels")]
        public WheelCollider[] WheelColliders;
        public Transform[] WheelModels;

        [Header("Anti Overturn")]
        public VehicleOverturnCheck OverturnCheck;

        [Header("Settings")]
        public bool UseDefaultInputs = true;

        // PIMLR: weighty "truck" handling for the player-driven car (only when UseDefaultInputs, so AI is untouched).
        [Header("PIMLR Truck Handling")]
        public bool TruckHandling = true;
        [Tooltip("Max front-wheel angle at low speed. The scene's MaxSteerAngle (60) is used only if lower.")]
        public float TruckMaxSteerAngle = 34f;
        [Tooltip("Fraction of the steer angle left at FullSteerFalloffSpeed and above.")]
        [Range(0.1f, 1f)] public float HighSpeedSteerFactor = 0.35f;
        [Tooltip("Speed (m/s) at which steering reaches HighSpeedSteerFactor. 22 m/s ~ 80 km/h.")]
        public float FullSteerFalloffSpeed = 22f;
        [Tooltip("How fast the wheel turns in (input units/sec). Lower = heavier.")]
        public float SteerInRate = 1.6f;
        [Tooltip("How fast the wheel re-centres (input units/sec).")]
        public float SteerOutRate = 3.2f;
        [Tooltip("How fast throttle builds (input units/sec). Lower = heavier acceleration feel.")]
        public float ThrottleRiseRate = 1.2f;
        [Tooltip("Rolling resistance when off the throttle (replaces BrakeForce/15, which stopped the truck dead).")]
        public float CoastBrakeTorque = 250f;
        [Tooltip("Brake torque for the brake input (BrakeForce is 450000 in the scene, which locks the wheels).")]
        public float TruckBrakeTorque = 1500f; // ~1.5 g on a 900 kg car with 0.45 m wheels
        [Tooltip("Yaw damping to stop the rear stepping out.")]
        public float TruckAngularDrag = 0.6f;
        [Tooltip("Gamepad steering/throttle respond this much faster than keyboard (analog sticks are already gradual).")]
        public float GamepadRateMultiplier = 1.75f;

        float truckSteer, truckThrottle;
        bool PlayerTruck => TruckHandling && UseDefaultInputs;

        private void Start()
        {
            CreateSteeringWheelRotationPivot(SteeringWheel);
            SetVehicleCenterOfMass(VehicleEngine.CenterOfMass);
        }
        protected override void VehicleUpdate()
        {
            //Ground Check
            GroundCheck.GroundCheck(transform);

            //Set default inputs
            if (UseDefaultInputs)
            {
                // PIMLR: on gamepad, RT accelerates and LT brakes / reverses (left stick only steers; B exits).
                float verticalInput = JUInput.GetAxis(JUInput.Axis.MoveVertical);
                bool brakeInput = JUInput.GetButton(JUInput.Buttons.JumpButton);
                if (PimlrInput.UsingGamepad) PimlrInput.GamepadVehicleInputs(rb, transform, out verticalInput, out brakeInput, pedalVehicle: false); // PIMLR: car = RT/LT

                SetEngineInputs(
                JUInput.GetAxis(JUInput.Axis.MoveHorizontal),   //Left/Right Input Value
                verticalInput,                                  //Forward/Backward Input Value
                brakeInput);                                    //Brake Vehicle Input Value

                if (PlayerTruck) SmoothTruckInputs();
            }

            //Anti Overturn
            OverturnCheck.OverturnCheck(transform);
            OverturnCheck.AntiOverturn(transform);

            //Update Wheel Models
            for (int i = 0; i < WheelColliders.Length; i++)
            {
                UpdateWheelModelTransformation(WheelColliders[i], WheelModels[i]);
            }

            //Steering Wheel Rotation
            SteeringWheel.transform.localEulerAngles = SteeringWheelRotation(SteeringWheel, WheelColliders[0], 2).eulerAngles;
        }
        protected override void VehiclePhysicsUpdate()
        {
            //Turn Off Vehicle
            if (!IsOn)
            {
                //Set Wheels torque and brake
                for (int i = 0; i < WheelColliders.Length; i++)
                {
                    WheelBrake(WheelColliders[i]);
                }
                return;
            }
            //Set Wheels torque and brake
            for (int i = 0; i < WheelColliders.Length; i++)
            {
                WheelTorque(WheelColliders[i]);
                if (PlayerTruck) TruckWheelBrake(WheelColliders[i]);
                else WheelBrake(WheelColliders[i]);
            }

            if (PlayerTruck)
            {
                if (rb) rb.angularDamping = TruckAngularDrag;
                float speed = rb ? rb.linearVelocity.magnitude : GetVehicleCurrentSpeed();
                float steerFactor = Mathf.Lerp(1f, HighSpeedSteerFactor, Mathf.Clamp01(speed / Mathf.Max(1f, FullSteerFalloffSpeed)));
                float maxAngle = Mathf.Min(MaxSteerAngle, TruckMaxSteerAngle);
                WheelSteerAngle(WheelColliders[0], truckSteer * steerFactor * maxAngle, maxAngle);
                WheelSteerAngle(WheelColliders[1], truckSteer * steerFactor * maxAngle, maxAngle);

                if (GroundCheck.IsGrounded == false) Align(Vector3.up, 0.5f);
                LimitVehicleSpeed(GroundCheck.IsGrounded, false);
                return;
            }

            //Get Steer Angle direction
            //float SteerAngleDirection = Mathf.Lerp(GetSmoothedHorizontalMovement(), GetSmoothedHorizontalMovement() / 4, GetSmoothedForwardMovement() * GetVehicleCurrentSpeed(0.1f));


            float speedFactor = Mathf.Clamp01(GetVehicleCurrentSpeed(0.05f)); // less aggressive
            float steerReduction = Mathf.Lerp(1f, 0.6f, speedFactor); // previously 0.25 (too small)
            float SteerAngleDirection = GetSmoothedHorizontalMovement() * steerReduction;
            //Set Front Wheels Steer Angle
            WheelSteerAngle(WheelColliders[0], SteerAngleDirection * MaxSteerAngle, MaxSteerAngle);
            WheelSteerAngle(WheelColliders[1], SteerAngleDirection * MaxSteerAngle, MaxSteerAngle);

            //On Air Rotation
            if (GroundCheck.IsGrounded == false) Align(Vector3.up, 0.5f);

            //Limit Speed
            LimitVehicleSpeed(GroundCheck.IsGrounded, false);
        }




        // PIMLR: ramp steering and throttle toward the raw input so a keyboard press doesn't snap to full lock.
        void SmoothTruckInputs()
        {
            float k = PimlrInput.UsingGamepad ? GamepadRateMultiplier : 1f;

            bool sameSide = truckSteer == 0f || Mathf.Sign(_horizontal) == Mathf.Sign(truckSteer);
            bool turningIn = sameSide && Mathf.Abs(_horizontal) > Mathf.Abs(truckSteer);
            float steerRate = (turningIn ? SteerInRate : SteerOutRate) * k;
            truckSteer = Mathf.MoveTowards(truckSteer, _horizontal, steerRate * Time.deltaTime);

            bool throttleUp = Mathf.Abs(_vertical) > Mathf.Abs(truckThrottle);
            float throttleRate = (throttleUp ? ThrottleRiseRate : ThrottleRiseRate * 3f) * k;
            truckThrottle = Mathf.MoveTowards(truckThrottle, _vertical, throttleRate * Time.deltaTime);

            _vertical = truckThrottle; // WheelTorque reads _vertical
        }

        void TruckWheelBrake(WheelCollider wheel)
        {
            if (_brake || !IsOn) wheel.brakeTorque = TruckBrakeTorque;
            else wheel.brakeTorque = Mathf.Abs(_vertical) < 0.05f ? CoastBrakeTorque : 0f;
        }

        private void OnDrawGizmos()
        {
            VehicleGizmo.DrawVector3Position(CharacterExitingPosition, transform, "Exit Position", Color.green);
            VehicleGizmo.DrawOverturnCheck(OverturnCheck, transform);
            VehicleGizmo.DrawVehicleGroundCheck(GroundCheck, transform);
        }
    }

}
