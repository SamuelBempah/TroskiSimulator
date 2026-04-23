using UnityEngine;
using System.Collections.Generic;

namespace CinematicTools
{
    public class VehicleCinematicRecorder : MonoBehaviour
    {
        [System.Serializable]
        public class VehicleFrame
        {
            public float timestamp;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3[] wheelLocalPositions;
            public Quaternion[] wheelLocalRotations;
        }

        private enum CinematicShotType
        {
            AttachedActionCam,
            HelicopterCam,
            StaticDriveByCam,
            LowBumperCam,
            WheelCam,
            DroneTrackingCam,
            ActionPanCam
        }

        [Header("Controls")]
        [Tooltip("Key to start and stop recording. (J)")]
        public KeyCode recordKey = KeyCode.J;
        [Tooltip("Key to start and stop playback. (K)")]
        public KeyCode playKey = KeyCode.K;

        [Header("Auto Director (GTA Style)")]
        [Tooltip("If true, the script will auto-cut between cinematic camera angles during playback.")]
        public bool useAutoDirector = true;
        [Tooltip("The Main Camera to control during playback.")]
        public Camera playbackCamera;
        [Tooltip("The FreeRoamCamera script to disable while the Auto Director is running.")]
        public MonoBehaviour freeRoamCameraScript;

        [Header("Director Effects (Wobble & Zoom)")]
        [Tooltip("How fast the camera shakes/bobs to simulate a handheld or helicopter camera.")]
        public float shakeFrequency = 1.5f;
        [Tooltip("How intense the camera shake is.")]
        public float shakeAmplitude = 0.5f;
        [Tooltip("How fast the helicopter camera slowly zooms in or out.")]
        public float heliZoomSpeed = 2f;

        [Header("Collision & Physics")]
        [Tooltip("Layers the camera should collide with. (IMPORTANT: Uncheck your vehicle's layer so the camera doesn't collide with the car itself)")]
        public LayerMask cameraCollisionLayers = ~0;
        [Tooltip("The minimum distance the camera must maintain from the center of the car to prevent going inside the interior.")]
        public float minCarClearance = 3.0f;

        [Header("Target References")]
        [Tooltip("The Rigidbody of the vehicle to disable physics during playback.")]
        public Rigidbody vehicleRigidbody;
        [Tooltip("The MonoBehaviour driving script (e.g., PlayerCar) to disable during playback.")]
        public MonoBehaviour vehicleController;
        [Tooltip("Visual wheels automatically gathered from WheelColliders. Do not assign manually unless needed.")]
        public Transform[] visualWheels;

        [Header("AI Vehicle Management")]
        [Tooltip("Should other AI vehicles be hidden during playback?")]
        public bool hideAIVehiclesDuringPlayback = true;
        [Tooltip("The tag assigned to your AI vehicles so the recorder can find and hide them.")]
        public string aiVehicleTag = "AIVehicle";

        [Header("Status (Read Only)")]
        public bool isRecording = false;
        public bool isPlaying = false;
        public int totalFramesRecorded = 0;

        private List<VehicleFrame> recordedFrames = new List<VehicleFrame>();
        private float timer = 0f;
        private int currentPlaybackIndex = 0;

        private bool originalKinematicState;
        private bool originalControllerState;
        private bool originalFreeRoamState;

        // AI Vehicle Variables
        private List<GameObject> hiddenAIVehicles = new List<GameObject>();

        // Director Variables
        private CinematicShotType currentShot;
        private float nextCutTime = 0f;
        private Vector3 currentCameraOffset;
        private Vector3 staticCameraPosition;
        private float targetHeliFOV;
        private float noiseOffset;
        
        // Drone Variables
        private Vector3 droneVelocity = Vector3.zero;
        private float droneAngle = 0f;
        private float droneRadius = 10f;
        private float droneHeight = 5f;
        private float droneSpeed = 15f;
        
        // Helicopter Variables
        private bool isHeliHovering = false;
        private Vector3 heliHoverPosition;

        // Anti-bounce timer to prevent accidental double-clicks on laptops
        private float inputCooldown = 0f;

        // The Ultimate Ghost-Buster: A Global Singleton
        private static VehicleCinematicRecorder globalInstance;

        private void Awake()
        {
            if (globalInstance != null && globalInstance != this)
            {
                Debug.LogWarning($"[Global Auto-Cleaner] Destroyed an unauthorized clone of the CinematicRecorder on: {gameObject.name}");
                Destroy(this);
                return;
            }
            
            globalInstance = this;
        }

        private void Start()
        {
            if (vehicleRigidbody == null)
            {
                vehicleRigidbody = GetComponent<Rigidbody>();
            }
            if (playbackCamera == null)
            {
                playbackCamera = Camera.main;
            }

            if (visualWheels == null || visualWheels.Length == 0)
            {
                WheelCollider[] wheelColliders = GetComponentsInChildren<WheelCollider>();
                List<Transform> foundWheels = new List<Transform>();
                
                foreach (WheelCollider wc in wheelColliders)
                {
                    if (wc.transform.childCount > 0)
                    {
                        foundWheels.Add(wc.transform.GetChild(0));
                    }
                }

                if (foundWheels.Count > 0)
                {
                    visualWheels = foundWheels.ToArray();
                    Debug.Log($"[CinematicRecorder] Successfully linked {visualWheels.Length} wheels via WheelColliders.");
                }
                else
                {
                    Debug.LogWarning("[CinematicRecorder] Could not find any visual wheels attached to WheelColliders. Ensure visual wheels are children of WheelColliders.");
                }
            }

            noiseOffset = Random.Range(0f, 1000f);
        }

        private void Update()
        {
            if (inputCooldown > 0f)
            {
                inputCooldown -= Time.deltaTime;
            }

            HandleInputs();

            if (isRecording)
            {
                RecordFrame();
            }
        }

        private void LateUpdate()
        {
            if (isPlaying)
            {
                PlayFrame();
            }
        }

        private void HandleInputs()
        {
            if (inputCooldown <= 0f)
            {
                if (Input.GetKeyDown(recordKey))
                {
                    inputCooldown = 0.5f;

                    if (isPlaying) StopPlayback();

                    if (isRecording)
                        StopRecording();
                    else
                        StartRecording();
                }

                if (Input.GetKeyDown(playKey))
                {
                    inputCooldown = 0.5f;

                    if (isRecording) StopRecording();

                    if (isPlaying)
                    {
                        StopPlayback();
                    }
                    else
                    {
                        if (recordedFrames.Count > 0)
                            StartPlayback();
                        else
                            Debug.LogWarning($"[Attached To: {gameObject.name}] CinematicRecorder: No frames recorded to play back!");
                    }
                }
            }
        }

        private void StartRecording()
        {
            Debug.Log($"[Attached To: {gameObject.name}] CinematicRecorder: Recording Started...");
            isRecording = true;
            timer = 0f;
            recordedFrames.Clear();
            totalFramesRecorded = 0;
        }

        private void StopRecording()
        {
            isRecording = false;
            Debug.Log($"[Attached To: {gameObject.name}] CinematicRecorder: Recording Stopped. Total Frames: {recordedFrames.Count}");
        }

        private void StartPlayback()
        {
            Debug.Log($"[Attached To: {gameObject.name}] CinematicRecorder: Playback Started...");
            isPlaying = true;
            timer = 0f;
            currentPlaybackIndex = 0;

            if (vehicleRigidbody != null)
            {
                originalKinematicState = vehicleRigidbody.isKinematic;
                vehicleRigidbody.isKinematic = true;
            }

            if (vehicleController != null)
            {
                originalControllerState = vehicleController.enabled;
                vehicleController.enabled = false; 
            }

            if (useAutoDirector && freeRoamCameraScript != null)
            {
                originalFreeRoamState = freeRoamCameraScript.enabled;
                freeRoamCameraScript.enabled = false;
            }

            // Hide AI Vehicles
            if (hideAIVehiclesDuringPlayback)
            {
                hiddenAIVehicles.Clear();
                GameObject[] aiVehicles = GameObject.FindGameObjectsWithTag(aiVehicleTag);
                foreach (GameObject ai in aiVehicles)
                {
                    // Ensure we don't accidentally hide the playback vehicle itself
                    if (ai != this.gameObject)
                    {
                        ai.SetActive(false);
                        hiddenAIVehicles.Add(ai);
                    }
                }
            }

            if (useAutoDirector)
            {
                CutToNextShot();
            }
        }

        private void StopPlayback()
        {
            isPlaying = false;
            Debug.Log($"[Attached To: {gameObject.name}] CinematicRecorder: Playback Stopped.");

            if (vehicleRigidbody != null) vehicleRigidbody.isKinematic = originalKinematicState;
            if (vehicleController != null) vehicleController.enabled = originalControllerState;
            
            if (useAutoDirector && freeRoamCameraScript != null)
            {
                freeRoamCameraScript.enabled = originalFreeRoamState;
            }

            // Restore AI Vehicles
            if (hideAIVehiclesDuringPlayback)
            {
                foreach (GameObject ai in hiddenAIVehicles)
                {
                    if (ai != null)
                    {
                        ai.SetActive(true);
                    }
                }
                hiddenAIVehicles.Clear();
            }
        }

        private void RecordFrame()
        {
            timer += Time.deltaTime;

            VehicleFrame frame = new VehicleFrame();
            frame.timestamp = timer;
            frame.position = transform.position;
            frame.rotation = transform.rotation;

            if (visualWheels != null && visualWheels.Length > 0)
            {
                frame.wheelLocalPositions = new Vector3[visualWheels.Length];
                frame.wheelLocalRotations = new Quaternion[visualWheels.Length];
                for (int i = 0; i < visualWheels.Length; i++)
                {
                    if (visualWheels[i] != null)
                    {
                        frame.wheelLocalPositions[i] = visualWheels[i].localPosition;
                        frame.wheelLocalRotations[i] = visualWheels[i].localRotation;
                    }
                }
            }

            recordedFrames.Add(frame);
            totalFramesRecorded = recordedFrames.Count;
        }

        private void PlayFrame()
        {
            timer += Time.deltaTime;

            if (timer > recordedFrames[recordedFrames.Count - 1].timestamp)
            {
                StopPlayback();
                return;
            }

            while (currentPlaybackIndex < recordedFrames.Count - 2 && 
                   recordedFrames[currentPlaybackIndex + 1].timestamp < timer)
            {
                currentPlaybackIndex++;
            }

            VehicleFrame frameA = recordedFrames[currentPlaybackIndex];
            VehicleFrame frameB = recordedFrames[currentPlaybackIndex + 1];

            float t = (timer - frameA.timestamp) / (frameB.timestamp - frameA.timestamp);

            transform.position = Vector3.Lerp(frameA.position, frameB.position, t);
            transform.rotation = Quaternion.Slerp(frameA.rotation, frameB.rotation, t);

            if (visualWheels != null && visualWheels.Length > 0 && frameA.wheelLocalRotations != null && frameB.wheelLocalRotations != null)
            {
                for (int i = 0; i < visualWheels.Length; i++)
                {
                    if (visualWheels[i] != null && i < frameA.wheelLocalRotations.Length && i < frameB.wheelLocalRotations.Length)
                    {
                        visualWheels[i].localPosition = Vector3.Lerp(frameA.wheelLocalPositions[i], frameB.wheelLocalPositions[i], t);
                        visualWheels[i].localRotation = Quaternion.Slerp(frameA.wheelLocalRotations[i], frameB.wheelLocalRotations[i], t);
                    }
                }
            }

            if (useAutoDirector && playbackCamera != null)
            {
                UpdateAutoDirectorCamera();
            }
        }

        private void CutToNextShot()
        {
            currentShot = (CinematicShotType)Random.Range(0, 7);
            
            if (currentShot == CinematicShotType.AttachedActionCam || currentShot == CinematicShotType.LowBumperCam || currentShot == CinematicShotType.ActionPanCam)
            {
                nextCutTime = timer + Random.Range(1.5f, 3.5f); 
            }
            else
            {
                nextCutTime = timer + Random.Range(3.5f, 7.0f); 
            }

            switch (currentShot)
            {
                case CinematicShotType.AttachedActionCam:
                    // Pushed all these offsets significantly outward to avoid the car's interior mesh
                    Vector3[] actionOffsets = new Vector3[] {
                        new Vector3(3.5f, 1.5f, 3.0f),   // Distant Front Right
                        new Vector3(-3.5f, 1.5f, -3.0f), // Distant Rear Left
                        new Vector3(0f, 1.8f, 5.5f),     // High Front Bumper
                        new Vector3(0f, 2.0f, -6.5f),    // High Rear Tail
                        new Vector3(-4.0f, 1.5f, 0f)     // Side profile
                    };
                    currentCameraOffset = actionOffsets[Random.Range(0, actionOffsets.Length)];
                    playbackCamera.fieldOfView = Random.Range(55f, 75f);
                    break;

                case CinematicShotType.HelicopterCam:
                    isHeliHovering = Random.value > 0.4f; 
                    
                    if (isHeliHovering)
                    {
                        heliHoverPosition = transform.position + new Vector3(Random.Range(-30f, 30f), Random.Range(30f, 50f), Random.Range(-40f, 40f));
                        playbackCamera.fieldOfView = Random.Range(30f, 45f);
                        targetHeliFOV = playbackCamera.fieldOfView - Random.Range(15f, 25f); 
                    }
                    else
                    {
                        currentCameraOffset = new Vector3(Random.Range(-20f, 20f), Random.Range(20f, 40f), Random.Range(-30f, -10f));
                        playbackCamera.fieldOfView = Random.Range(40f, 65f);
                        targetHeliFOV = playbackCamera.fieldOfView + Random.Range(-10f, 10f); 
                    }
                    break;

                case CinematicShotType.StaticDriveByCam:
                    SetupStaticDriveBy(2.5f, Random.Range(8f, 15f), Random.Range(0.5f, 3f));
                    break;

                case CinematicShotType.ActionPanCam:
                    SetupStaticDriveBy(1.5f, Random.Range(5f, 8f), Random.Range(0.2f, 1.0f));
                    playbackCamera.fieldOfView = Random.Range(70f, 95f);
                    break;

                case CinematicShotType.LowBumperCam:
                    // Pushed outward to prevent clipping inside engine bays or exhaust pipes
                    Vector3[] bumperOffsets = new Vector3[] {
                        new Vector3(0f, 0.5f, 4.5f),
                        new Vector3(0f, 0.5f, -4.5f),
                        new Vector3(2.0f, 0.4f, 0f)
                    };
                    currentCameraOffset = bumperOffsets[Random.Range(0, bumperOffsets.Length)];
                    playbackCamera.fieldOfView = Random.Range(85f, 105f); 
                    break;

                case CinematicShotType.WheelCam:
                    // Pushed outward horizontally and vertically
                    float side = Random.value > 0.5f ? 2.0f : -2.0f;
                    float frontRear = Random.value > 0.5f ? 2.0f : -2.0f;
                    currentCameraOffset = new Vector3(side, 0.6f, frontRear);
                    playbackCamera.fieldOfView = Random.Range(65f, 80f);
                    break;

                case CinematicShotType.DroneTrackingCam:
                    droneAngle = Random.Range(0f, 360f);
                    droneRadius = Random.Range(7f, 15f); // Increased minimum drone radius
                    droneHeight = Random.Range(4f, 9f);
                    droneSpeed = (Random.value > 0.5f ? 1f : -1f) * Random.Range(15f, 45f); 
                    playbackCamera.fieldOfView = Random.Range(55f, 75f);
                    break;
            }
        }

        private void SetupStaticDriveBy(float timeAhead, float distanceSide, float height)
        {
            float futureTime = timer + timeAhead;
            VehicleFrame futureFrame = null;
            
            foreach (var f in recordedFrames)
            {
                if (f.timestamp >= futureTime)
                {
                    futureFrame = f;
                    break;
                }
            }

            if (futureFrame != null)
            {
                Vector3 rightVector = futureFrame.rotation * Vector3.right;
                float sideDirection = Random.value > 0.5f ? 1f : -1f; 
                staticCameraPosition = futureFrame.position + (rightVector * distanceSide * sideDirection) + new Vector3(0, height, 0);
                playbackCamera.fieldOfView = Random.Range(35f, 60f); 
            }
            else
            {
                currentShot = CinematicShotType.HelicopterCam;
                isHeliHovering = false;
                currentCameraOffset = new Vector3(0, 20, -10);
                playbackCamera.fieldOfView = 60f;
                targetHeliFOV = 40f;
            }
        }

        private void UpdateAutoDirectorCamera()
        {
            if (timer >= nextCutTime)
            {
                CutToNextShot();
            }

            Vector3 positionWobble = CalculateWobbleOffset() * shakeAmplitude;
            Quaternion rotationWobble = Quaternion.Euler(CalculateWobbleRotationEuler() * shakeAmplitude);

            Vector3 targetPosition = playbackCamera.transform.position;
            Quaternion targetRotation = playbackCamera.transform.rotation;

            switch (currentShot)
            {
                case CinematicShotType.AttachedActionCam:
                    targetPosition = transform.TransformPoint(currentCameraOffset) + positionWobble;
                    targetRotation = Quaternion.LookRotation((transform.position + transform.forward * 3f) - targetPosition); 
                    break;

                case CinematicShotType.HelicopterCam:
                    if (isHeliHovering)
                    {
                        targetPosition = heliHoverPosition + (positionWobble * 3f);
                        targetRotation = Quaternion.LookRotation(transform.position - targetPosition);
                        playbackCamera.fieldOfView = Mathf.Lerp(playbackCamera.fieldOfView, targetHeliFOV, Time.deltaTime * heliZoomSpeed * 0.05f);
                    }
                    else
                    {
                        Vector3 desiredHeliPos = transform.position + currentCameraOffset;
                        targetPosition = Vector3.Lerp(playbackCamera.transform.position, desiredHeliPos, Time.deltaTime * 3f) + (positionWobble * 2f);
                        targetRotation = Quaternion.LookRotation(transform.position - targetPosition);
                        playbackCamera.fieldOfView = Mathf.Lerp(playbackCamera.fieldOfView, targetHeliFOV, Time.deltaTime * heliZoomSpeed * 0.1f);
                    }
                    break;

                case CinematicShotType.StaticDriveByCam:
                case CinematicShotType.ActionPanCam:
                    targetPosition = staticCameraPosition + positionWobble;
                    targetRotation = Quaternion.LookRotation(transform.position - targetPosition);
                    
                    float distanceToCamera = Vector3.Distance(transform.position, staticCameraPosition);
                    float cutoffDistance = currentShot == CinematicShotType.ActionPanCam ? 2f : 4f;
                    
                    if (distanceToCamera < cutoffDistance || Vector3.Dot(targetRotation * Vector3.forward, transform.forward) > 0.5f)
                    {
                        CutToNextShot();
                    }
                    break;

                case CinematicShotType.LowBumperCam:
                    targetPosition = transform.TransformPoint(currentCameraOffset) + (positionWobble * 0.5f);
                    targetRotation = transform.rotation;
                    break;

                case CinematicShotType.WheelCam:
                    targetPosition = transform.TransformPoint(currentCameraOffset) + (positionWobble * 0.3f);
                    targetRotation = Quaternion.LookRotation((transform.position + new Vector3(0, 0.2f, 0)) - targetPosition);
                    break;

                case CinematicShotType.DroneTrackingCam:
                    droneAngle += droneSpeed * Time.deltaTime;
                    float rad = droneAngle * Mathf.Deg2Rad;
                    Vector3 orbitOffset = new Vector3(Mathf.Cos(rad) * droneRadius, droneHeight, Mathf.Sin(rad) * droneRadius);
                    
                    Vector3 desiredDronePos = transform.position + orbitOffset;
                    
                    targetPosition = Vector3.SmoothDamp(playbackCamera.transform.position, desiredDronePos, ref droneVelocity, 0.4f);
                    targetRotation = Quaternion.LookRotation(transform.position - targetPosition);
                    break;
            }

            targetRotation *= rotationWobble;

            // --- ANTI-CLIPPING SYSTEM (UPGRADED) ---
            Vector3 carCenterPos = transform.position + Vector3.up * 1.0f; // Approximate center mass of the car
            Vector3 directionToCamera = targetPosition - carCenterPos;
            float distanceToTarget = directionToCamera.magnitude;

            // 1. Exterior Check Failsafe: Absolutely forces the camera out of the car's interior bounding sphere
            if (distanceToTarget < minCarClearance)
            {
                Vector3 pushOutDir = directionToCamera.normalized;
                if (pushOutDir == Vector3.zero) pushOutDir = Vector3.up; 
                targetPosition = carCenterPos + (pushOutDir * minCarClearance);
                
                // Recalculate direction/distance for the environmental cast below
                directionToCamera = targetPosition - carCenterPos;
                distanceToTarget = directionToCamera.magnitude;
            }

            // 2. Environmental Clipping: Uses SphereCast to give the camera physical size/thickness against walls
            if (Physics.SphereCast(carCenterPos, 0.4f, directionToCamera.normalized, out RaycastHit hit, distanceToTarget, cameraCollisionLayers))
            {
                // Push the camera outward from the wall along the wall's normal
                targetPosition = hit.point + (hit.normal * 0.4f); 
            }

            // 3. Ground Clipping: Final failsafe to stop subterranean diving beneath the terrain
            if (Physics.Raycast(targetPosition + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 10f, cameraCollisionLayers))
            {
                float minHeight = groundHit.point.y + 0.4f;
                if (targetPosition.y < minHeight)
                {
                    targetPosition.y = minHeight;
                }
            }

            // Apply final computed transforms
            playbackCamera.transform.position = targetPosition;
            playbackCamera.transform.rotation = targetRotation;
        }

        private Vector3 CalculateWobbleOffset()
        {
            float x = Mathf.PerlinNoise(Time.time * shakeFrequency + noiseOffset, 0f) - 0.5f;
            float y = Mathf.PerlinNoise(0f, Time.time * shakeFrequency + noiseOffset) - 0.5f;
            float z = Mathf.PerlinNoise(Time.time * shakeFrequency + noiseOffset, Time.time * shakeFrequency + noiseOffset) - 0.5f;
            return new Vector3(x, y, z);
        }

        private Vector3 CalculateWobbleRotationEuler()
        {
            float x = (Mathf.PerlinNoise(Time.time * shakeFrequency + noiseOffset + 10f, 0f) - 0.5f) * 5f;
            float y = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency + noiseOffset + 10f) - 0.5f) * 5f;
            float z = (Mathf.PerlinNoise(Time.time * shakeFrequency + noiseOffset + 10f, Time.time * shakeFrequency + noiseOffset + 10f) - 0.5f) * 5f;
            return new Vector3(x, y, z);
        }

        private void OnGUI()
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 24;
            style.fontStyle = FontStyle.Bold;

            if (isRecording)
            {
                style.normal.textColor = Color.red;
                GUI.Label(new Rect(20, 20, 500, 40), $"● RECORDING ({recordedFrames.Count} frames)", style);
            }
            else if (isPlaying)
            {
                style.normal.textColor = Color.green;
                GUI.Label(new Rect(20, 20, 500, 40), $"▶ PLAYING ({currentPlaybackIndex} / {recordedFrames.Count})", style);
            }
            else if (recordedFrames.Count > 0)
            {
                style.normal.textColor = Color.white;
                GUI.Label(new Rect(20, 20, 500, 40), $"READY TO PLAY ({recordedFrames.Count} frames saved)", style);
            }
        }
    }
}