using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TroskiAdrenalineCamera : MonoBehaviour
{
    [Header("Dependencies")]
    private Camera cam;
    
    [Tooltip("Drag your GlobalPostProcessing GameObject here")]
    public Volume postProcessVolume; 
    private MotionBlur motionBlur;

    [Header("FOV Settings (Speed Feeling)")]
    public float baseFOV = 50f;
    public float maxSpeedFOV = 85f; 
    [Tooltip("Speed where the camera STARTS panning out")]
    public float fovStartSpeed = 95f; 
    [Tooltip("Speed where maximum effects (FOV, Shake) are reached")]
    public float maxEffectSpeed = 140f; 
    [Tooltip("Lowered to make the panning slower and smoother")]
    public float fovTransitionSpeed = 3f;

    [Header("Camera Shake & Tilt (Subtle)")]
    public float steerTiltIntensity = 3.0f;
    public float maxRollShake = 0.3f; 
    public float maxFovShake = 0.2f;  
    public float shakeStartThreshold = 70f;

    [Header("Motion Blur Controls")]
    public float blurStartSpeed = 100f; 
    public float maxBlurIntensity = 0.8f;

    private float smoothedTargetFOV;
    private float currentCameraRoll = 0f;

    void Start()
    {
        cam = GetComponent<Camera>();
        smoothedTargetFOV = baseFOV;

        if (postProcessVolume != null)
        {
            if (postProcessVolume.profile.TryGet(out motionBlur))
            {
                motionBlur.intensity.value = 0f; 
            }
        }
    }

    void LateUpdate()
    {
        if (TroskiGameManager.Instance == null || TroskiGameManager.Instance.PlayerTroski == null)
            return;

        float currentSpeed = TroskiGameManager.Instance.CurrentSpeedKmH;
        float steerInput = TroskiGameManager.SteerInput;

        ApplyDynamicFOV(currentSpeed);
        ApplyDynamicMotionBlur(currentSpeed);
        ApplySteeringTiltAndShake(currentSpeed, steerInput);
    }

    private void ApplyDynamicFOV(float speed)
    {
        float targetFOV = baseFOV;

        // Only start zooming out if we pass the fovStartSpeed (e.g., 95 km/h)
        if (speed > fovStartSpeed)
        {
            float overSpeed = speed - fovStartSpeed;
            float speedRange = maxEffectSpeed - fovStartSpeed;
            float speedFactor = Mathf.Clamp01(overSpeed / speedRange);
            
            // Exponential curve for a dramatic but smooth push back at high speeds
            targetFOV = Mathf.Lerp(baseFOV, maxSpeedFOV, speedFactor * speedFactor);
        }
        
        // Smooth transition applied here. Lower fovTransitionSpeed makes it pan slower
        smoothedTargetFOV = Mathf.Lerp(smoothedTargetFOV, targetFOV, Time.deltaTime * fovTransitionSpeed);
        
        float finalFOV = smoothedTargetFOV;

        // Subtle FOV wind buffeting
        if (speed > shakeStartThreshold)
        {
            float overSpeed = speed - shakeStartThreshold;
            float shakeFactor = Mathf.Clamp01(overSpeed / (maxEffectSpeed - shakeStartThreshold));
            float fovNoise = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 2f;
            finalFOV += (fovNoise * maxFovShake * shakeFactor);
        }

        cam.fieldOfView = finalFOV;
    }

    private void ApplySteeringTiltAndShake(float speed, float steerInput)
    {
        float targetRoll = -steerInput * steerTiltIntensity;

        if (speed > shakeStartThreshold)
        {
            float overSpeed = speed - shakeStartThreshold;
            float shakeFactor = Mathf.Clamp01(overSpeed / (maxEffectSpeed - shakeStartThreshold));
            float rollNoise = (Mathf.PerlinNoise(0f, Time.time * 20f) - 0.5f) * 2f;
            targetRoll += rollNoise * maxRollShake * shakeFactor;
        }

        currentCameraRoll = Mathf.Lerp(currentCameraRoll, targetRoll, Time.deltaTime * 5f);

        Vector3 currentEuler = transform.localEulerAngles;
        transform.localEulerAngles = new Vector3(currentEuler.x, currentEuler.y, currentCameraRoll);
    }

    private void ApplyDynamicMotionBlur(float speed)
    {
        if (motionBlur != null)
        {
            if (speed >= blurStartSpeed)
            {
                float overSpeed = speed - blurStartSpeed;
                float blurFactor = Mathf.Clamp01(overSpeed / (maxEffectSpeed - blurStartSpeed)); 
                motionBlur.intensity.value = Mathf.Lerp(0f, maxBlurIntensity, blurFactor);
            }
            else
            {
                motionBlur.intensity.value = Mathf.Lerp(motionBlur.intensity.value, 0f, Time.deltaTime * 5f);
            }
        }
    }
}