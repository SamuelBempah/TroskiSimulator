using UnityEngine;

/// <summary>
/// A realism controller for the Built-in Render Pipeline (Standard/Mobile).
/// Focuses on Ambient light, Fog, and Skybox adjustments.
/// </summary>
[ExecuteAlways]
public class MobileBuiltInRealism : MonoBehaviour
{
    [Header("Sun & Shadows")]
    public Light sunLight;
    [Range(0, 2)] public float sunIntensity = 1.1f;
    public Color sunColor = new Color(1f, 0.95f, 0.85f);

    [Header("Ambient Atmosphere")]
    [Tooltip("Gradient is the best balance for mobile realism.")]
    public Color skyColor = new Color(0.3f, 0.4f, 0.6f);
    public Color horizonColor = new Color(0.7f, 0.75f, 0.8f);
    public Color groundColor = new Color(0.2f, 0.15f, 0.1f);

    [Header("Depth (Fog)")]
    public bool useFog = true;
    public Color fogColor = new Color(0.7f, 0.75f, 0.8f);
    [Range(0.001f, 0.05f)] public float fogDensity = 0.015f;

    void Update()
    {
        ApplySettings();
    }

    public void ApplySettings()
    {
        // 1. Update the Sun
        if (sunLight != null)
        {
            sunLight.intensity = sunIntensity;
            sunLight.color = sunColor;
            // For mobile, 'Hard Shadows' are faster, 'Soft' are realistic.
            // Use 'Soft' if your device can handle it.
            sunLight.shadows = LightShadows.Soft; 
        }

        // 2. Setup Ambient Lighting (The 'Theme' of the scene)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyColor;
        RenderSettings.ambientEquatorColor = horizonColor;
        RenderSettings.ambientGroundColor = groundColor;

        // 3. Setup Fog (Hides the edge of the world and adds realism)
        RenderSettings.fog = useFog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fogDensity;
    }

    [ContextMenu("Apply Realism Now")]
    private void ManualApply()
    {
        ApplySettings();
        Debug.Log("Mobile Lighting Applied!");
    }
}