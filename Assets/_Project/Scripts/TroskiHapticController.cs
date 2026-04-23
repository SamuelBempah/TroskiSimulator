using UnityEngine;
using System.Collections;

public class TroskiHapticController : MonoBehaviour
{
    private float lastFrameSpeed = 0f;
    private bool gearButtonFound = false;
    private float timeSinceStart = 0f;

    void Start()
    {
        StartCoroutine(HookIntoUI());
    }

    void Update()
    {
        timeSinceStart += Time.deltaTime;

        if (TroskiGameManager.Instance == null) return;

        float currentSpeed = TroskiGameManager.Instance.CurrentSpeedKmH;

        // Detect Crash/Sudden Impact:
        // Added a 2-second grace period so spawning/dropping doesn't trigger a crash
        if (timeSinceStart > 2.0f && lastFrameSpeed - currentSpeed > 30f)
        {
            TriggerHeavyHaptic();
        }

        lastFrameSpeed = currentSpeed;
    }

    private IEnumerator HookIntoUI()
    {
        // Give the GameManager 1 second to spawn the canvas and buttons
        yield return new WaitForSeconds(1.0f);

        GameObject canvasObj = GameObject.Find("GameCanvas");
        
        if (canvasObj != null)
        {
            Transform mobileControls = canvasObj.transform.Find("MobileControls");
            if (mobileControls != null)
            {
                Transform gearBtnTrans = mobileControls.Find("GearBtn");
                if (gearBtnTrans != null)
                {
                    // FIX: Looking for your custom MobileButton component instead of standard Button
                    MobileButton gearUIBtn = gearBtnTrans.GetComponent<MobileButton>();
                    if (gearUIBtn != null)
                    {
                        // Attach our haptic trigger directly to your MobileButton's onDown action
                        gearUIBtn.onDown += TriggerLightHaptic;
                        gearButtonFound = true;
                        Debug.Log("HapticController: Successfully hooked into GearBtn (MobileButton).");
                    }
                }
            }
        }

        if (!gearButtonFound)
        {
            Debug.LogWarning("HapticController: Could not find MobileButton on GearBtn.");
        }
    }

    // --- HAPTIC METHODS ---

    public void TriggerLightHaptic()
    {
        Debug.Log("[HAPTIC TRIGGERED] Light Haptic (Gear Shift)!");

        #if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                if (vibrator.Call<bool>("hasVibrator"))
                {
                    // 30 milliseconds is a quick "tap" feeling
                    vibrator.Call("vibrate", 30L); 
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Haptics failed: " + e.Message);
        }
        #endif
    }

    public void TriggerHeavyHaptic()
    {
        Debug.Log("[HAPTIC TRIGGERED] Heavy Haptic (CRASH)!");

        #if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                if (vibrator.Call<bool>("hasVibrator"))
                {
                    // 150 milliseconds is a jarring buzz
                    vibrator.Call("vibrate", 150L); 
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Haptics failed: " + e.Message);
        }
        #endif
    }
}