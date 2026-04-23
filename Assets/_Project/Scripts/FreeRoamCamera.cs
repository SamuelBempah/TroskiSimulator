using UnityEngine;

namespace CinematicTools
{
    [RequireComponent(typeof(Camera))]
    public class FreeRoamCamera : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Standard movement speed.")]
        public float baseSpeed = 10f;
        [Tooltip("Speed when holding Shift for fast panning.")]
        public float sprintSpeed = 25f;
        [Tooltip("Speed when holding Control for slow, cinematic creeping.")]
        public float crawlSpeed = 2f;
        [Tooltip("How smoothly the camera starts and stops moving.")]
        public float movementDamping = 5f;

        [Header("Look Settings")]
        [Tooltip("Mouse look sensitivity.")]
        public float lookSensitivity = 2f;
        [Tooltip("How smoothly the camera rotation comes to a stop.")]
        public float lookDamping = 10f;

        [Header("Cinematic Shake (Idle)")]
        [Tooltip("How fast the camera drifts/bobs when idle.")]
        public float shakeFrequency = 0.5f;
        [Tooltip("How far the camera drifts/bobs when idle.")]
        public float shakeAmplitude = 0.05f;

        [Header("Field of View (Zoom)")]
        [Tooltip("Minimum FOV (zoomed in).")]
        public float minFOV = 15f;
        [Tooltip("Maximum FOV (zoomed out).")]
        public float maxFOV = 90f;
        [Tooltip("How fast the camera zooms.")]
        public float zoomSpeed = 30f;
        [Tooltip("How smoothly the zoom stops.")]
        public float zoomDamping = 5f;
        [Tooltip("Keyboard key to zoom in.")]
        public KeyCode zoomInKey = KeyCode.Z;
        [Tooltip("Keyboard key to zoom out.")]
        public KeyCode zoomOutKey = KeyCode.X;

        [Header("Camera Lock")]
        [Tooltip("Key to lock/unlock the camera in place.")]
        public KeyCode lockKey = KeyCode.L;
        [Tooltip("If true, the camera is locked and ignores input.")]
        public bool isLocked = false;

        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private float targetFOV;

        private float pitch = 0f;
        private float yaw = 0f;
        private Camera cam;
        private float noiseOffset;

        private void Start()
        {
            cam = GetComponent<Camera>();

            // Initialize targets to current transform to prevent jumping on play
            targetPosition = transform.position;
            targetRotation = transform.rotation;
            
            Vector3 angles = transform.eulerAngles;
            pitch = angles.x;
            yaw = angles.y;

            if (cam != null)
            {
                targetFOV = cam.fieldOfView;
            }

            // Randomize the start of the Perlin noise so multiple cameras wouldn't bob in sync
            noiseOffset = Random.Range(0f, 1000f);
        }

        private void Update()
        {
            // Toggle the lock state when the lock key is pressed
            if (Input.GetKeyDown(lockKey))
            {
                isLocked = !isLocked;
                if (isLocked)
                {
                    Debug.Log("Cinematic Camera Locked. Press " + lockKey.ToString() + " to unlock.");
                }
                else
                {
                    Debug.Log("Cinematic Camera Unlocked.");
                }
            }

            // Always allow zooming, even when the camera is locked
            HandleZoom();

            // Only process movement and rotation input if the camera is not locked
            if (!isLocked)
            {
                HandleRotation();
                HandleMovement();
            }
        }

        private void LateUpdate()
        {
            // Apply smoothed movement
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * movementDamping);
            
            // Add the continuous, organic idle shake on top of the movement (continues even when locked)
            Vector3 shakeOffset = CalculateIdleShake();
            transform.position += shakeOffset;

            // Apply smoothed rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookDamping);

            // Apply smoothed FOV zoom
            if (cam != null)
            {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * zoomDamping);
            }
        }

        private void HandleRotation()
        {
            // Only look around if holding the Right Mouse Button
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
                
                // Clamp pitch so you can't break your neck looking too far up or down
                pitch = Mathf.Clamp(pitch, -89f, 89f);
            }

            targetRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void HandleMovement()
        {
            // Determine current speed based on modifier keys
            float currentSpeed = baseSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) currentSpeed = sprintSpeed;
            if (Input.GetKey(KeyCode.LeftControl)) currentSpeed = crawlSpeed;

            Vector3 movement = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            
            // Vertical movement controls
            if (Input.GetKey(KeyCode.E)) movement.y += 1f;
            if (Input.GetKey(KeyCode.Q)) movement.y -= 1f;

            // Normalize to prevent diagonal speeding, apply speed and delta time
            movement = movement.normalized * currentSpeed * Time.deltaTime;

            // Move relative to where the camera is currently looking
            targetPosition += targetRotation * movement;
        }

        private void HandleZoom()
        {
            if (cam == null) return;

            float zoomInput = 0f;

            // Keep scroll wheel support just in case you ever plug a mouse in
            zoomInput += Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;

            // Add keyboard support for trackpad users
            if (Input.GetKey(zoomInKey)) zoomInput += zoomSpeed * Time.deltaTime;
            if (Input.GetKey(zoomOutKey)) zoomInput -= zoomSpeed * Time.deltaTime;

            if (Mathf.Abs(zoomInput) > 0.001f)
            {
                targetFOV -= zoomInput;
                targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
            }
        }

        private Vector3 CalculateIdleShake()
        {
            // Use Perlin noise for a smooth, organic floaty feel rather than a rigid sine wave
            float x = Mathf.PerlinNoise(Time.time * shakeFrequency + noiseOffset, 0f) - 0.5f;
            float y = Mathf.PerlinNoise(0f, Time.time * shakeFrequency + noiseOffset) - 0.5f;
            float z = Mathf.PerlinNoise(Time.time * shakeFrequency + noiseOffset, Time.time * shakeFrequency + noiseOffset) - 0.5f;

            return new Vector3(x, y, z) * shakeAmplitude;
        }
    }
}