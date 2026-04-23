using UnityEngine;

/// <summary>
/// A high-performance Day/Night cycle for the Built-in Render Pipeline.
/// Optimized for mobile devices (Intel HD 520 / Samsung S21).
/// Provides realistic color transitions using gradients.
/// </summary>
[ExecuteAlways]
public class MobileDayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    [Tooltip("How many real-time seconds one full game day lasts.")]
    public float dayDurationInSeconds = 120f;
    [Range(0, 1)]
    public float timeOfDay = 0.25f; // 0.25 is morning, 0.5 is noon, 0.75 is sunset, 0 is midnight
    public float timeScale = 1f;

    [Header("Sun & Moon")]
    public Light sunLight;
    public Gradient sunColor;
    public AnimationCurve sunIntensity;

    [Header("Environment Realism (Trilight)")]
    public Gradient skyColor;     // Top of the sky
    public Gradient horizonColor; // The hazy "dusty" part of the horizon
    public Gradient groundColor;  // Light bouncing off the road/dirt
    
    [Header("Fog (Atmospheric Depth)")]
    public bool enableFog = true;
    public Gradient fogColor;
    public AnimationCurve fogDensity;

    private void Update()
    {
        // Only progress time in Play Mode
        if (Application.isPlaying)
        {
            UpdateTimer();
        }

        ApplyLighting();
    }

    private void UpdateTimer()
    {
        // Calculate the passage of time
        float timeStep = Time.deltaTime / dayDurationInSeconds;
        timeOfDay += timeStep * timeScale;

        // Reset day cycle
        if (timeOfDay >= 1f) timeOfDay = 0f;
    }

    public void ApplyLighting()
    {
        if (sunLight == null) return;

        // 1. Rotate the Sun
        // -90 is sunrise, 90 is sunset, 270 is midnight
        float sunAngle = timeOfDay * 360f - 90f;
        sunLight.transform.localRotation = Quaternion.Euler(sunAngle, 170f, 0f);

        // 2. Apply Gradients based on current time
        float t = timeOfDay;
        sunLight.color = sunColor.Evaluate(t);
        sunLight.intensity = sunIntensity.Evaluate(t);

        // 3. Update Ambient Trilight (Crucial for Mobile Realism)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyColor.Evaluate(t);
        RenderSettings.ambientEquatorColor = horizonColor.Evaluate(t);
        RenderSettings.ambientGroundColor = groundColor.Evaluate(t);

        // 4. Update Fog
        RenderSettings.fog = enableFog;
        RenderSettings.fogColor = fogColor.Evaluate(t);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fogDensity.Evaluate(t);

        // 5. Toggle Light based on Intensity (Save performance at night)
        sunLight.enabled = (sunLight.intensity > 0.01f);
    }

    // Context menu to quickly set a "Golden Hour" look for testing
    [ContextMenu("Set To Sunset")]
    private void SetSunset()
    {
        timeOfDay = 0.73f;
        ApplyLighting();
    }
}