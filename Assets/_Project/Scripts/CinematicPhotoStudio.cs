using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

namespace CinematicTools
{
    [RequireComponent(typeof(Camera))]
    public class CinematicPhotoStudio : MonoBehaviour
    {
        [System.Serializable]
        public class PhotoPreset
        {
            public string presetName;
            [Tooltip("Position of the camera relative to the vehicle.")]
            public Vector3 cameraOffset;
            [Tooltip("Where the camera should look, relative to the vehicle center.")]
            public Vector3 lookAtOffset;
            [Tooltip("Camera Field of View. Low = Telephoto/Zoomed, High = Wide Angle.")]
            public float fieldOfView = 50f;
            [Tooltip("Z-axis rotation for Dutch Angles (tilted dramatic shots).")]
            public float rollAngle = 0f;
        }

        [Header("Controls")]
        [Tooltip("Key to freeze time and enter Photo Mode. (P)")]
        public KeyCode togglePhotoModeKey = KeyCode.P;
        [Tooltip("Key to cycle to the next cinematic camera angle. (C)")]
        public KeyCode cycleAngleKey = KeyCode.C;
        [Tooltip("Key to take the high-resolution screenshot. (Enter)")]
        public KeyCode takePhotoKey = KeyCode.Return;

        [Header("Targeting & Integration")]
        [Tooltip("The vehicle to focus on. If empty, it will try to find the VehicleCinematicRecorder.")]
        public Transform targetVehicle;
        [Tooltip("Optional: Link your VehicleCinematicRecorder so the Photo Studio can pause the Auto Director.")]
        public VehicleCinematicRecorder cinematicRecorder;
        
        [Header("Output Settings")]
        [Tooltip("Multiplier for resolution. 1 = Screen Res, 2 = 4K (if playing 1080p), 4 = 8K. Great for billboards/print.")]
        [Range(1, 5)]
        public int screenshotResolutionMultiplier = 3;
        [Tooltip("Folder name to save images inside your project directory.")]
        public string saveFolderName = "CinematicShots";

        [Header("Cinematic Angles")]
        public List<PhotoPreset> cameraPresets = new List<PhotoPreset>();

        [Header("UI Management")]
        [Tooltip("Assign your main Canvas here to automatically hide UI elements when taking a photo.")]
        public Canvas mainUICanvas;

        // Internal State
        private bool isPhotoModeActive = false;
        private int currentPresetIndex = 0;
        private Camera photoCamera;
        
        // State Restoration
        private float originalTimeScale;
        private Vector3 originalCamPosition;
        private Quaternion originalCamRotation;
        private float originalFOV;
        private bool wasAutoDirectorActive = false;

        private void Start()
        {
            photoCamera = GetComponent<Camera>();

            // Auto-assign target if not set
            if (targetVehicle == null && cinematicRecorder != null)
            {
                targetVehicle = cinematicRecorder.transform;
            }
            else if (targetVehicle == null)
            {
                VehicleCinematicRecorder rec = FindObjectOfType<VehicleCinematicRecorder>();
                if (rec != null)
                {
                    cinematicRecorder = rec;
                    targetVehicle = rec.transform;
                }
            }

            // Generate default high-quality presets if the list is empty
            if (cameraPresets.Count == 0)
            {
                GenerateDefaultPresets();
            }

            // Ensure the save directory exists
            string folderPath = Path.Combine(Application.dataPath, "../" + saveFolderName);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(togglePhotoModeKey))
            {
                TogglePhotoMode();
            }

            if (isPhotoModeActive)
            {
                if (Input.GetKeyDown(cycleAngleKey))
                {
                    CyclePreset();
                }

                if (Input.GetKeyDown(takePhotoKey))
                {
                    StartCoroutine(CaptureHighResPhoto());
                }

                // Smoothly update camera to stick to the car even if time is technically frozen
                // (Useful if rigidbodies are drifting slightly or for immediate application)
                ApplyCurrentPreset();
            }
        }

        private void TogglePhotoMode()
        {
            isPhotoModeActive = !isPhotoModeActive;

            if (isPhotoModeActive)
            {
                Debug.Log("[PhotoStudio] Photo Mode ENABLED. Time frozen.");
                
                // Save original states
                originalTimeScale = Time.timeScale;
                originalCamPosition = photoCamera.transform.position;
                originalCamRotation = photoCamera.transform.rotation;
                originalFOV = photoCamera.fieldOfView;

                // Freeze Time
                Time.timeScale = 0f;

                // Disable the cinematic recorder's auto-director so they don't fight for the camera
                if (cinematicRecorder != null)
                {
                    wasAutoDirectorActive = cinematicRecorder.useAutoDirector;
                    cinematicRecorder.useAutoDirector = false;
                }

                if (mainUICanvas != null) mainUICanvas.enabled = false;

                ApplyCurrentPreset();
            }
            else
            {
                Debug.Log("[PhotoStudio] Photo Mode DISABLED. Time restored.");
                
                // Restore original states
                Time.timeScale = originalTimeScale;
                photoCamera.transform.position = originalCamPosition;
                photoCamera.transform.rotation = originalCamRotation;
                photoCamera.fieldOfView = originalFOV;

                // Restore auto-director
                if (cinematicRecorder != null)
                {
                    cinematicRecorder.useAutoDirector = wasAutoDirectorActive;
                }

                if (mainUICanvas != null) mainUICanvas.enabled = true;
            }
        }

        private void CyclePreset()
        {
            if (cameraPresets.Count == 0) return;

            currentPresetIndex++;
            if (currentPresetIndex >= cameraPresets.Count)
            {
                currentPresetIndex = 0;
            }

            Debug.Log($"[PhotoStudio] Angle Changed: {cameraPresets[currentPresetIndex].presetName}");
            ApplyCurrentPreset();
        }

        private void ApplyCurrentPreset()
        {
            if (targetVehicle == null || cameraPresets.Count == 0) return;

            PhotoPreset preset = cameraPresets[currentPresetIndex];

            // Calculate target position based on vehicle's rotation and position
            Vector3 worldPositionOffset = targetVehicle.TransformDirection(preset.cameraOffset);
            Vector3 targetPosition = targetVehicle.position + worldPositionOffset;

            // Calculate look at target
            Vector3 lookAtTarget = targetVehicle.position + targetVehicle.TransformDirection(preset.lookAtOffset);

            // Apply to Camera
            photoCamera.transform.position = targetPosition;
            
            // Calculate rotation looking at target, then apply the dutch angle (roll)
            Vector3 directionToTarget = (lookAtTarget - photoCamera.transform.position).normalized;
            if (directionToTarget != Vector3.zero)
            {
                Quaternion baseRotation = Quaternion.LookRotation(directionToTarget);
                // Apply roll on the Z axis
                photoCamera.transform.rotation = baseRotation * Quaternion.Euler(0, 0, preset.rollAngle);
            }

            photoCamera.fieldOfView = preset.fieldOfView;
        }

        private System.Collections.IEnumerator CaptureHighResPhoto()
        {
            // Optional: Hide any debug UI or Gizmos right before capturing
            yield return new WaitForEndOfFrame();

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"{cameraPresets[currentPresetIndex].presetName.Replace(" ", "")}_{timestamp}.png";
            string filePath = Path.Combine(Application.dataPath, "../" + saveFolderName, filename);

            ScreenCapture.CaptureScreenshot(filePath, screenshotResolutionMultiplier);
            
            Debug.Log($"[PhotoStudio] SNAP! Saved high-res photo to: {filePath}");
        }

        private void GenerateDefaultPresets()
        {
            cameraPresets.Add(new PhotoPreset { presetName = "Hero Wide Front", cameraOffset = new Vector3(3f, 0.5f, 5f), lookAtOffset = new Vector3(0f, 0.5f, 0f), fieldOfView = 45f, rollAngle = 0f });
            cameraPresets.Add(new PhotoPreset { presetName = "Aggressive Low Bumper", cameraOffset = new Vector3(0f, 0.2f, 4.5f), lookAtOffset = new Vector3(0f, 0.8f, 0f), fieldOfView = 65f, rollAngle = -5f });
            cameraPresets.Add(new PhotoPreset { presetName = "Dramatic Low Rear", cameraOffset = new Vector3(-2.5f, 0.3f, -5f), lookAtOffset = new Vector3(0f, 0.8f, 0f), fieldOfView = 50f, rollAngle = 8f });
            cameraPresets.Add(new PhotoPreset { presetName = "Telephoto Far Chase", cameraOffset = new Vector3(15f, 3f, 25f), lookAtOffset = new Vector3(0f, 1f, 0f), fieldOfView = 15f, rollAngle = 0f });
            cameraPresets.Add(new PhotoPreset { presetName = "Action Side Pan", cameraOffset = new Vector3(-6f, 1.2f, 0f), lookAtOffset = new Vector3(0f, 1f, 0f), fieldOfView = 55f, rollAngle = 0f });
            cameraPresets.Add(new PhotoPreset { presetName = "Macro Rim Detail", cameraOffset = new Vector3(2.5f, 0.4f, 1.5f), lookAtOffset = new Vector3(1f, 0.4f, 1.5f), fieldOfView = 25f, rollAngle = 0f });
            cameraPresets.Add(new PhotoPreset { presetName = "Drone Top Down", cameraOffset = new Vector3(0f, 10f, 0f), lookAtOffset = new Vector3(0f, 0f, 0f), fieldOfView = 40f, rollAngle = 0f });
            cameraPresets.Add(new PhotoPreset { presetName = "Trailer Teaser (Dutch)", cameraOffset = new Vector3(4f, 0.5f, -4f), lookAtOffset = new Vector3(0f, 0.5f, 0f), fieldOfView = 60f, rollAngle = 15f });
        }
    }
}