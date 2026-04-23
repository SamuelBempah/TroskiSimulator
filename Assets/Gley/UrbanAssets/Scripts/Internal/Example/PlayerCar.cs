using UnityEngine;
using System.Collections.Generic;

namespace Gley.UrbanSystem.Internal
{
    /// <summary>
    /// This class is for testing purpose only
    /// It is the car controller provided by Unity:
    /// https://docs.unity3d.com/Manual/WheelColliderTutorial.html
    /// </summary>
    [System.Serializable]
    public class AxleInfo
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public bool motor;
        public bool steering;
    }


    public class PlayerCar : MonoBehaviour
    {
        [Header("Axles and Physics")]
        public List<AxleInfo> axleInfos;
        public Transform centerOfMass;
        
        [Header("Engine and Power")]
        public float maxMotorTorque = 1500f;
        public float maxBrakeTorque = 3000f;
        
        [Header("Steering and Handling")]
        public float maxSteeringAngle = 35f;
        [Tooltip("Smooths out the steering input to feel heavier and more realistic.")]
        public float smoothSteerTime = 0.15f;
        [Tooltip("Applies downward force based on speed to keep the vehicle planted.")]
        public float downForce = 50f;
        [Tooltip("Prevents the tall vehicle from flipping easily on sharp turns.")]
        public float antiRollForce = 5000f;
        [Tooltip("Helps the vehicle drive perfectly straight when no steering input is applied.")]
        [Range(0, 1)] public float steerHelper = 0.9f;

        IVehicleLightsComponent lightsComponent;
        bool mainLights;
        bool brake;
        bool reverse;
        bool blinkLeft;
        bool blinkRifgt;
        float realtimeSinceStartup;
        Rigidbody rb;

        UIInput inputScript;

        // Internal variables for smoothing and physics
        private float currentSteerVelocity;
        private float currentSteerAngle;
        private float oldRotation;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = centerOfMass.localPosition;
            
            inputScript = gameObject.AddComponent<UIInput>().Initialize();
            lightsComponent = gameObject.GetComponent<VehicleLightsComponent>();
            lightsComponent.Initialize();
        }

        // finds the corresponding visual wheel
        // correctly applies the transform
        public void ApplyLocalPositionToVisuals(WheelCollider collider)
        {
            if (collider.transform.childCount == 0)
            {
                return;
            }

            Transform visualWheel = collider.transform.GetChild(0);

            Vector3 position;
            Quaternion rotation;
            collider.GetWorldPose(out position, out rotation);

            visualWheel.transform.position = position;
            visualWheel.transform.rotation = rotation;
        }

        public void FixedUpdate()
        {
            float horizontalInput = 0f;
            float verticalInput = 0f;
            bool isExplicitBraking = false;

            // Link inputs directly to TroskiGameManager if it exists
            if (global::TroskiGameManager.Instance != null)
            {
                horizontalInput = global::TroskiGameManager.SteerInput;
                verticalInput = global::TroskiGameManager.AccelInput;
                isExplicitBraking = global::TroskiGameManager.BrakeInput;
            }
            else
            {
                // Fallback to default Gley inputs if testing without GameManager
                horizontalInput = inputScript.GetHorizontalInput();
                verticalInput = inputScript.GetVerticalInput();
                isExplicitBraking = Input.GetKey(KeyCode.Space);
            }

            // Smooth out the steering to simulate the heavy wheel of a minibus
            float targetSteering = maxSteeringAngle * horizontalInput;
            currentSteerAngle = Mathf.SmoothDamp(currentSteerAngle, targetSteering, ref currentSteerVelocity, smoothSteerTime);

            float motor = maxMotorTorque * verticalInput;

#if UNITY_6000_0_OR_NEWER
            var velocity = rb.linearVelocity;
#else
            var velocity = rb.velocity;
#endif
            float localVelocity = transform.InverseTransformDirection(velocity).z + 0.1f;
            float forwardSpeed = transform.InverseTransformDirection(velocity).z;
            
            reverse = false;
            brake = false;

            // Proper Braking and Reversing Logic
            float currentBrakeTorque = 0f;
            float currentMotorTorque = 0f;

            if (isExplicitBraking)
            {
                // Explicit brake command from UI or Spacebar
                brake = true;
                currentBrakeTorque = maxBrakeTorque;
                currentMotorTorque = 0f;
            }
            else if (verticalInput < 0)
            {
                if (localVelocity > 1f)
                {
                    // Moving forward, pressing backward -> Brake
                    brake = true;
                    currentBrakeTorque = Mathf.Abs(verticalInput) * maxBrakeTorque;
                }
                else
                {
                    // Stopped or moving backward, pressing backward -> Reverse
                    reverse = true;
                    currentMotorTorque = motor;
                }
            }
            else if (verticalInput > 0)
            {
                if (localVelocity < -1f)
                {
                    // Moving backward, pressing forward -> Brake
                    brake = true;
                    currentBrakeTorque = Mathf.Abs(verticalInput) * maxBrakeTorque;
                }
                else
                {
                    // Moving forward -> Accelerate
                    currentMotorTorque = motor;
                }
            }
            else
            {
                // No input -> slight engine braking
                currentBrakeTorque = 10f; 
            }

            // Steer Helper (Anti-Drift to force straight driving)
            ApplySteerHelper(horizontalInput);

            // Apply Downforce
            rb.AddForce(-transform.up * Mathf.Abs(forwardSpeed) * downForce);

            int groundedWheels = 0;

            foreach (AxleInfo axleInfo in axleInfos)
            {
                // Apply Anti-Roll Bar force to keep the heavy vehicle stable
                ApplyAntiRollBar(axleInfo.leftWheel, axleInfo.rightWheel);

                if (axleInfo.steering)
                {
                    axleInfo.leftWheel.steerAngle = currentSteerAngle;
                    axleInfo.rightWheel.steerAngle = currentSteerAngle;
                }

                if (axleInfo.motor)
                {
                    axleInfo.leftWheel.motorTorque = currentMotorTorque;
                    axleInfo.rightWheel.motorTorque = currentMotorTorque;
                }

                // Apply proper brake torque
                axleInfo.leftWheel.brakeTorque = currentBrakeTorque;
                axleInfo.rightWheel.brakeTorque = currentBrakeTorque;

                ApplyLocalPositionToVisuals(axleInfo.leftWheel);
                ApplyLocalPositionToVisuals(axleInfo.rightWheel);

                if (axleInfo.leftWheel.isGrounded) groundedWheels++;
                if (axleInfo.rightWheel.isGrounded) groundedWheels++;
            }
        }

        private void ApplyAntiRollBar(WheelCollider leftWheel, WheelCollider rightWheel)
        {
            WheelHit hit;
            float travelL = 1.0f;
            float travelR = 1.0f;

            bool groundedL = leftWheel.GetGroundHit(out hit);
            if (groundedL)
                travelL = (-leftWheel.transform.InverseTransformPoint(hit.point).y - leftWheel.radius) / leftWheel.suspensionDistance;

            bool groundedR = rightWheel.GetGroundHit(out hit);
            if (groundedR)
                travelR = (-rightWheel.transform.InverseTransformPoint(hit.point).y - rightWheel.radius) / rightWheel.suspensionDistance;

            float antiRollVal = (travelL - travelR) * antiRollForce;

            if (groundedL)
                rb.AddForceAtPosition(leftWheel.transform.up * -antiRollVal, leftWheel.transform.position);
            if (groundedR)
                rb.AddForceAtPosition(rightWheel.transform.up * antiRollVal, rightWheel.transform.position);
        }

        private void ApplySteerHelper(float horizontalInput)
        {
#if UNITY_6000_0_OR_NEWER
            Vector3 currentVelocity = rb.linearVelocity;
#else
            Vector3 currentVelocity = rb.velocity;
#endif
            float speed = currentVelocity.magnitude;

            if (speed > 1f)
            {
                // If there is no steering input, strictly force the vehicle to go straight
                if (Mathf.Abs(horizontalInput) < 0.05f)
                {
                    // 1. Kill Twisting Rotation: Force the Y-axis angular velocity to 0 so the body stops trying to spin.
                    Vector3 angularVel = rb.angularVelocity;
                    angularVel.y = Mathf.Lerp(angularVel.y, 0f, Time.fixedDeltaTime * 20f); 
                    rb.angularVelocity = angularVel;

                    // 2. Lock Velocity Vector: Project the current speed purely onto the forward direction of the car.
                    float forwardSpeed = Vector3.Dot(currentVelocity, transform.forward);
                    Vector3 straightVelocity = transform.forward * forwardSpeed;
                    
                    // Keep the vertical velocity (gravity/bouncing) untouched
                    straightVelocity.y = currentVelocity.y;

#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = Vector3.Lerp(currentVelocity, straightVelocity, Time.fixedDeltaTime * 15f);
#else
                    rb.velocity = Vector3.Lerp(currentVelocity, straightVelocity, Time.fixedDeltaTime * 15f);
#endif
                }
                else
                {
                    // Assist in aligning velocity with vehicle forward direction to reduce unwanted drifting during turns
                    if (Mathf.Abs(oldRotation - transform.eulerAngles.y) < 10f)
                    {
                        var turnAdjust = (transform.eulerAngles.y - oldRotation) * steerHelper;
                        Quaternion velRotation = Quaternion.AngleAxis(turnAdjust, Vector3.up);
#if UNITY_6000_0_OR_NEWER
                        rb.linearVelocity = velRotation * currentVelocity;
#else
                        rb.velocity = velRotation * currentVelocity;
#endif
                    }
                }
            }
            oldRotation = transform.eulerAngles.y;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Ignore the collision if it's explicitly tagged as the Road
            if (collision.gameObject.CompareTag("Road")) return;

            // Calculate how hard the vehicle hit the object based on relative velocity
            float impactForce = collision.relativeVelocity.magnitude;

            // Only register a crash if the impact is hard enough (ignores tiny scrapes and resting against objects)
            if (impactForce > 4f)
            {
                // Scale the impact force into a damage integer 
                int damage = Mathf.RoundToInt(impactForce * 0.8f);
                if (damage < 1) damage = 1;

                // Send the damage to the Game Manager
                if (global::TroskiGameManager.Instance != null)
                {
                    global::TroskiGameManager.Instance.ApplyDamage(damage);
                }
            }
        }

        private void Update()
        {
            realtimeSinceStartup += Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                mainLights = !mainLights;
                lightsComponent.SetMainLights(mainLights);
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                blinkLeft = !blinkLeft;
                if (blinkLeft == true)
                {
                    blinkRifgt = false;
                    lightsComponent.SetBlinker(BlinkType.Left);
                }
                else
                {
                    lightsComponent.SetBlinker(BlinkType.Stop);
                }
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                blinkRifgt = !blinkRifgt;
                if (blinkRifgt == true)
                {
                    blinkLeft = false;
                    lightsComponent.SetBlinker(BlinkType.Right);
                }
                else
                {
                    lightsComponent.SetBlinker(BlinkType.Stop);
                }
            }

            lightsComponent.SetBrakeLights(brake);
            lightsComponent.SetReverseLights(reverse);
            lightsComponent.UpdateLights(realtimeSinceStartup);
        }
    }
}