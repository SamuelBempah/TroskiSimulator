using UnityEngine;

namespace Gley.UrbanSystem.Internal
{
    /// <summary>
    /// Script for presentation purpose only
    /// Follows the player car. 
    /// Credits goes to the following github user:
    /// https://gist.github.com/Hamcha/6096905
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public bool shouldRotate = true;
        // The target we are following
        public Transform target;
        
        [Header("Positioning")]
        // The distance in the x-z plane to the target
        public float distance = 8.0f;
        // the height we want the camera to be above the target
        public float height = 2.2f;
        
        [Header("Smoothing")]
        // How much we damp the vertical movement
        public float heightDamping = 2.0f;
        
        [Tooltip("How smoothly the camera follows the car's rotation. Higher is smoother/slower. Lower is faster/tighter.")]
        public float rotationSmoothTime = 0.35f; 
        
        float wantedRotationAngle;
        float wantedHeight;
        float currentRotationAngle;
        float currentHeight;
        Quaternion currentRotation;
        
        // Velocity reference required for SmoothDamp
        private float rotationVelocity;

        void FixedUpdate()
        {
            if (target)
            {
                // Calculate the current rotation angles
                wantedRotationAngle = target.eulerAngles.y;
                wantedHeight = target.position.y + height;
                currentRotationAngle = transform.eulerAngles.y;
                currentHeight = transform.position.y;
                
                // Damp the rotation around the y-axis using SmoothDamp for a realistic, spring-like follow 
                // that prevents the sharp, disorienting "snap" when turning heavily.
                currentRotationAngle = Mathf.SmoothDampAngle(currentRotationAngle, wantedRotationAngle, ref rotationVelocity, rotationSmoothTime);
                
                // Damp the height
                currentHeight = Mathf.Lerp(currentHeight, wantedHeight, heightDamping * Time.deltaTime);
                
                // Convert the angle into a rotation
                currentRotation = Quaternion.Euler(0, currentRotationAngle, 0);
                
                // Set the position of the camera on the x-z plane to:
                // distance meters behind the target
                transform.position = target.position;
                transform.position -= currentRotation * Vector3.forward * distance;
                
                // Set the height of the camera
                transform.position = new Vector3(transform.position.x, currentHeight, transform.position.z);
                
                // Always look at the target (lifted slightly so the car body doesn't block the road ahead)
                if (shouldRotate)
                    transform.LookAt(target.position + (Vector3.up * 1.2f));
            }
        }
    }
}