using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;

// --- TROSKI SIMULATOR: PACKED & UNIQUE UI MENU MANAGER ---
// Fully Functional Version: 
// - Dynamic Leveling (Slower progression at higher levels) & Rank System.
// - First-time driver Company Name & Logo setup.
// - Clickable Top Bar to Edit Profile at any time.
// - Functional Music Player seamlessly integrated under Game Settings.
// - Real Daily Sales Targets & regenerating Union Tasks.
// - Location Modal with Level-locked routes & calculated earnings.
// - Dynamic Color Theme Customization using real color preview buttons.
// - Expanded Graphics & Framerate Selection Options via Tap Grid.
// - Real Money Shop Simulation (Apex Legends Style Store Revamp) with GHS currency.
// - Custom Shop Thumbnails support.
// - Audio Fade out on Shift Start.
// - Procedurally generated smooth rounded corners applied to all UI blocks and buttons.
// - Dynamic Stats & Hiring System based on vehicle/mate queue.
// - In-game Time System (1 Real Hour = 1 In-Game Day).
// - Mate Cut System changed to "Per Shift" logic.
// - Intro Sequence Bypass (Skips Video & Notice when returning from another scene).
// - Fixed ambient graphics/lighting darkening bug on scene return.
// - Animated Currency & Daily Goals (Lerps from previous value to new value seamlessly).
// - Integrated SFX System matching TroskiGameManager.
// - FIXED: Screen Orientation forced firmly to Landscape.
// - FIXED: Full screen panels stretch dynamically and lock to edges to avoid clipping on varying aspect ratios.
// - FIXED: Loading bar clipping issues resolved.
// - ADDED: Native Gallery support for mobile profile picture selection.
// - ADDED: Live Notification & Promo System (Real-time JSON fetching, Video/Carousel Banners).
// - FIXED: Image Carousel now slides smoothly and links to unique text per image.
// - FIXED: Promo banners now utilize procedural rounded corners via Masks.
// - FIXED: GPRTU Union Tasks "Claim Rewards" button clipping resolved.
// - ADDED: Smart Local Caching & Cleanup for Promo Images to eliminate load times, save mobile data, and optimize storage.
// - ADDED: Auto-Detect Hardware feature to automatically set Graphics for Low-end Devices.
// - ADDED: Resolution Downscaling for significant performance boosts on Low-end Devices.
// - UPDATED: Daily Goal System now tracks Real-World Time (Resets at Midnight).
// - UPDATED: Daily Goal dynamically randomized every new day. Reaching the goal shows "COME BACK TOMORROW".

[System.Serializable]
public class TroskiMission
{
    public string title;
    public int currentProgress;
    public int targetProgress;
    public int rewardGHS;
    public int rewardXP;

    public bool IsComplete => currentProgress >= targetProgress;
}

[System.Serializable]
public class MissionWrapper
{
    public List<TroskiMission> missions = new List<TroskiMission>();
}

[System.Serializable]
public class RouteData
{
    public string routeName;
    public int requiredLevel;
    public int baseEarnings;

    public RouteData(string name, int reqLvl, int baseEarn)
    {
        routeName = name;
        requiredLevel = reqLvl;
        baseEarnings = baseEarn;
    }
}

// Data structure for the remote notifications
[System.Serializable]
public class PromoSlide
{
    public string title;
    public string description;
    public string mediaUrl; // Can be .jpg, .png, or .mp4
    public string actionBtnText;
    public string actionUrl;
}

[System.Serializable]
public class NotificationData
{
    public bool isActive;
    public bool popOnStartup;
    public PromoSlide[] slides; 
}

public class TroskiMenuManager : MonoBehaviour
{
    public enum FocusTarget { None, Troski, Mate }

    // Static flag to track if the intro has played during this app session
    private static bool hasPlayedIntro = false;

    [Header("Intro & Notice Sequence")]
    public VideoClip introVideoClip;
    private bool isStartupSequenceRunning = false;

    [Header("Loading Screen Settings")]
    public Texture2D loadingScreenImage; // Drop your crisp loading JPG here

    [Header("Notification Settings")]
    [Tooltip("URL to a raw JSON file to remotely update the banners/notices.")]
    public string notificationDataUrl = "https://raw.githubusercontent.com/SamuelBempah/TroskiData/main/notice.json";
    
    [Header("Main References")]
    public GameObject currentTroski;
    public GameObject currentMate;

    [Header("Selection Lists")]
    public GameObject[] troskiModels;
    public GameObject[] mateModels;

    [Header("Camera Settings")]
    public Transform cameraTransform;
    public Vector3 cameraTroskiViewOffset = new Vector3(-4.5f, 2.5f, -6.5f);
    public Vector3 cameraMateViewOffset = new Vector3(-2f, 1.5f, -3f);
    public float cameraMoveSpeed = 5f;
    public float mateSpinSpeed = 15f; 

    [Header("Player Stats")]
    public string transportCompany = "";
    public Texture2D playerCustomLogo; 
    public int playerCashGHS = 1000;
    public int playerPremium = 0;
    public int playerLevel = 1;
    public int currentXP = 0;
    public int xpToNextLevel = 500;
    public int dailyTargetEarnings = 2000;
    public int currentDailyEarnings = 0;
    public int vehicleCondition = 100;

    [Header("Audio Settings")]
    public AudioClip[] musicTracks;
    private AudioSource bgmSource;
    private int currentTrackIndex = 0;

    [Header("SFX Settings")]
    public AudioClip btnClickSFX;
    public AudioClip modalOpenSFX;
    public AudioClip buySFX;
    public AudioClip claimRewardSFX;
    private AudioSource sfxSource;

    [Header("UI Assets (Textures/Images)")]
    public Texture2D gameLogo; 
    public Texture2D settingsIcon;
    public Texture2D cashIcon;
    public Texture2D premiumIcon;
    public Texture2D defaultProfileIcon;

    [Header("Shop Thumbnails (Recommended Size: 580x440 pixels)")]
    public Texture2D shopItem1_Img;
    public Texture2D shopItem2_Img;
    public Texture2D shopItem3_Img;
    public Texture2D shopItem4_Img;

    [Header("Troski Brand Palette")]
    private Color themeColor = new Color(0.98f, 0.75f, 0.05f, 1f); 
    private Color troskiDarkAsphalt = new Color(0.1f, 0.1f, 0.12f, 0.98f);
    private Color troskiLightAsphalt = new Color(0.18f, 0.18f, 0.2f, 0.95f);
    private Color troskiGreen = new Color(0.15f, 0.7f, 0.25f, 1f); 
    private Color troskiRed = new Color(0.85f, 0.15f, 0.15f, 1f); 
    private Color textWhite = new Color(0.95f, 0.95f, 0.95f, 1f);
    private Color textMuted = new Color(0.7f, 0.7f, 0.7f, 1f);

    // Internal State
    private int currentTroskiIndex = 0;
    private int currentMateIndex = 0;
    private int selectedRouteIndex = -1;
    private FocusTarget currentFocus = FocusTarget.None;

    private Vector3 defaultCameraPosition;
    private Quaternion defaultCameraRotation;
    private Quaternion[] mateDefaultRotations; 

    private Font defaultFont;
    private Sprite roundedSprite; 
    private GameObject canvasObj;
    private GameObject topBarContainer;
    private GameObject packedHomeContainer;
    private GameObject troskiCustomizationContainer;
    private GameObject mateCustomizationContainer;
    
    // Dynamic Stats Containers
    private GameObject tStatsContainer;
    private GameObject mStatsContainer;
    private Text mateHireBtnText;

    // Time System
    private float dayTimer = 0f;
    private int currentDay = 1;
    private Text timeTextComp;
    private const float REAL_SECONDS_PER_DAY = 3600f; 
    private string lastSavedDate = "";
    
    // New UI Elements
    private GameObject settingsPanel;
    private GameObject shopPanel;
    private GameObject loadingPanel;
    private GameObject profileEditPanel;
    private GameObject stationsModal;
    private GameObject shiftModeModal;
    
    // Notification & Promo UI Elements
    private GameObject noticeModal;
    private RectTransform miniPromoSlider;
    private RectTransform modalPromoSlider;
    private Text promoFallbackText;
    private Text modalTitleText;
    private Text modalDescText;
    private Text modalActionText;
    private Button modalActionBtn;
    private TroskiNotificationManager notificationManager;
    
    // Loading Screen Elements
    private Text loadingText;
    private Text loadingPercentText;
    private RectTransform loadingFillRect;

    // Dynamic Text Components
    private Text cashTextComp;
    private Text premTextComp;
    private Text routeTextComp;
    private Text serviceBtnTextComp;
    private Text musicTitleComp;
    private Text dailyEarningsText;
    private RectTransform dailyProgressFill;
    
    // Animation State
    private int currentDisplayedCash = 0;
    private int currentDisplayedPrem = 0;
    private int currentDisplayedDaily = 0;
    private Coroutine statAnimCoroutine;

    // Profile Edit Components
    private InputField companyNameInput;
    private Text profileEditTitle;
    private Image profilePreviewImg;
    private Texture2D tempProfileTexture;
    
    private GameObject taskListContainer;
    private MissionWrapper missionData = new MissionWrapper();

    // Settings State
    private bool isAudioMuted = false;
    private int currentGraphicsLevel = 2; 
    private int currentFPS = 60;
    private int controlScheme = 0; 
    private int selectedThemeIndex = 0;

    private int[] availableFPS = new int[] { 30, 60, -1 };
    
    // Route Data
    private List<RouteData> availableRoutes = new List<RouteData>() {
        new RouteData("MADINA ⇌ CIRCLE", 1, 800),
        new RouteData("LAPAZ ⇌ ACCRA", 3, 1200),
        new RouteData("DANSOMAN ⇌ CIRCLE", 5, 1500),
        new RouteData("KASOA ⇌ CIRCLE", 10, 2500),
        new RouteData("TEMA ⇌ ACCRA", 15, 3500),
        new RouteData("KUMASI ⇌ ACCRA", 25, 8000)
    };

    private Color[] availableThemes = new Color[] {
        new Color(0.98f, 0.75f, 0.05f, 1f), // Yellow
        new Color(0.15f, 0.6f, 0.95f, 1f),  // Blue
        new Color(0.9f, 0.2f, 0.2f, 1f),    // Red
        new Color(0.6f, 0.2f, 0.8f, 1f),    // Purple
        new Color(0.15f, 0.85f, 0.4f, 1f)   // Green
    };

    void Start()
    {
        // Force Landscape Orientation firmly
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        // NEW: Auto-detect graphics for first-time players to ensure low-end devices don't crash
        if (PlayerPrefs.GetInt("HasAutoDetectedGraphics", 0) == 0)
        {
            AutoDetectAndApplyGraphics();
        }

        FixLightingBug(); // Ensure ambient light is correct when returning from async loads

        if (PlayerPrefs.GetInt("MissionProgressPatch", 0) == 0)
        {
            PlayerPrefs.DeleteKey("Missions");
            PlayerPrefs.SetInt("MissionProgressPatch", 1);
            PlayerPrefs.Save();
        }

        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (cameraTransform != null)
        {
            defaultCameraPosition = cameraTransform.position;
            defaultCameraRotation = cameraTransform.rotation;
        }

        if (mateModels != null)
        {
            mateDefaultRotations = new Quaternion[mateModels.Length];
            for (int i = 0; i < mateModels.Length; i++)
                if (mateModels[i] != null) mateDefaultRotations[i] = mateModels[i].transform.rotation;
        }

        GenerateRoundedSprite();
        LoadPlayerData();
        SetupAudio();
        LoadMissions();

        UpdateTroskiVisibility();
        UpdateMateVisibility();
        
        BuildUI();

        // Initialize Notification System
        notificationManager = gameObject.GetComponent<TroskiNotificationManager>();
        if (notificationManager == null) notificationManager = gameObject.AddComponent<TroskiNotificationManager>();
        notificationManager.Initialize(this, notificationDataUrl, miniPromoSlider, modalPromoSlider, noticeModal, promoFallbackText);

        // Intro Sequence Logic Check
        if (!hasPlayedIntro)
        {
            isStartupSequenceRunning = true;
            hasPlayedIntro = true;
            StartCoroutine(StartupSequence());
        }
        else
        {
            isStartupSequenceRunning = false;
            StartCoroutine(QuickFadeIn()); // Masks any initial rendering pop
            if (bgmSource != null && !isAudioMuted && musicTracks != null && musicTracks.Length > 0)
            {
                bgmSource.Play();
            }
            
            // Check for startup notice if intro was already played
            if (notificationManager != null) notificationManager.CheckStartupPop();
        }
    }

    private void AutoDetectAndApplyGraphics()
    {
        int ram = SystemInfo.systemMemorySize; // MB
        int cores = SystemInfo.processorCount;
        
        int totalLevels = QualitySettings.names.Length;
        int recommendedLevel = totalLevels - 1; // Default High
        int recommendedFPS = 60;

        // Low end (e.g. Samsung A20 has ~3GB RAM or less, weaker cores)
        if (ram <= 3500 || cores <= 4)
        {
            recommendedLevel = 0; // Lowest Graphics Quality
            recommendedFPS = 30;  // Cap FPS to save battery and heat
        }
        // Mid end (e.g. 4GB - 6GB RAM)
        else if (ram <= 6500)
        {
            recommendedLevel = Mathf.Clamp(totalLevels / 2, 0, totalLevels - 1); // Medium Quality
            recommendedFPS = 30; 
        }
        
        PlayerPrefs.SetInt("GraphicsLevel", recommendedLevel);
        PlayerPrefs.SetInt("TargetFPS", recommendedFPS);
        PlayerPrefs.SetInt("HasAutoDetectedGraphics", 1);
        PlayerPrefs.Save();
        
        Debug.Log($"Auto-Detected Hardware. RAM: {ram}MB, Cores: {cores}. Set Quality Level: {recommendedLevel}, FPS: {recommendedFPS}");
    }

    private void FixLightingBug()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.8f, 1f);
        DynamicGI.UpdateEnvironment();
    }

    private IEnumerator QuickFadeIn()
    {
        GameObject fadeCanvasObj = new GameObject("FadeCanvas");
        Canvas canvas = fadeCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        
        CreateFullScreenPanel(fadeCanvasObj.transform, "FadeBG", Color.black, false);
        CanvasGroup cg = fadeCanvasObj.AddComponent<CanvasGroup>();
        
        float timer = 0f;
        float duration = 0.5f;
        while(timer < duration)
        {
            timer += Time.deltaTime;
            cg.alpha = 1f - (timer / duration);
            yield return null;
        }
        Destroy(fadeCanvasObj);
    }

    private IEnumerator StartupSequence()
    {
        GameObject introCanvasObj = new GameObject("IntroCanvas");
        Canvas canvas = introCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; 
        CanvasScaler scaler = introCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f; 
        introCanvasObj.AddComponent<GraphicRaycaster>();
        CanvasGroup introCanvasGroup = introCanvasObj.AddComponent<CanvasGroup>();

        GameObject bg = CreateFullScreenPanel(introCanvasObj.transform, "IntroBG", Color.black, false);

        if (introVideoClip != null)
        {
            VideoPlayer vp = introCanvasObj.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.clip = introVideoClip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            
            RenderTexture rt = new RenderTexture(1920, 1080, 16, RenderTextureFormat.ARGB32);
            vp.targetTexture = rt;

            GameObject videoScreen = new GameObject("VideoScreen");
            videoScreen.transform.SetParent(bg.transform, false);
            RectTransform vRect = videoScreen.AddComponent<RectTransform>();
            vRect.anchorMin = Vector2.zero;
            vRect.anchorMax = Vector2.one;
            vRect.sizeDelta = Vector2.zero;
            RawImage rawImage = videoScreen.AddComponent<RawImage>();
            rawImage.texture = rt;
            
            AudioSource vpAudio = introCanvasObj.AddComponent<AudioSource>();
            vp.SetTargetAudioSource(0, vpAudio);

            vp.Prepare();
            while (!vp.isPrepared) yield return null;

            vp.Play();
            yield return new WaitForSeconds(0.1f);
            while (vp.isPlaying) yield return null;

            vp.Stop();
            Destroy(vp); 
            Destroy(vpAudio);
            rt.Release();
            Destroy(videoScreen);
        }

        GameObject textContainer = CreateFullScreenPanel(bg.transform, "TextContainer", Color.clear, false);
        CanvasGroup textCanvasGroup = textContainer.AddComponent<CanvasGroup>();
        textCanvasGroup.alpha = 0f; 

        string noticeText = "TROSKI SIMULATOR - IMPORTANT NOTICE\n\n" +
                            "Created by SBMind Interactive and published by SBMind Technologies.\n\n" +
                            "This game is a work of fiction. It does not bear any resemblance to the actual country; " +
                            "only the mechanics resemble actual troskis. By playing, you agree to experience the virtual simulation responsibly.\n\n" +
                            "WARNING: This game may contain flashing lights or visual patterns that could trigger seizures for people with photosensitive epilepsy. " +
                            "Player discretion is strongly advised. Furthermore, extended gameplay can be highly addictive. Please remember to take regular screen breaks. " +
                            "The developers are not liable for any real-world traffic violations inspired by gameplay. Always wear your seatbelt, respect local driving laws, and ensure your 'mate' collects the exact fare.\n\n" +
                            "Enjoy the ride!";
        
        Text pText = CreateTextElement(textContainer.transform, "NoticeText", noticeText, Vector2.zero, new Vector2(1500, 800), 28, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white, false);
        pText.lineSpacing = 1.3f;

        float fadeTextInDuration = 1.5f;
        float fadeTimer = 0f;
        while (fadeTimer < fadeTextInDuration)
        {
            fadeTimer += Time.deltaTime;
            textCanvasGroup.alpha = Mathf.Lerp(0f, 1f, fadeTimer / fadeTextInDuration);
            yield return null;
        }
        textCanvasGroup.alpha = 1f;

        yield return new WaitForSeconds(3f);

        Text tapTextComp = CreateTextElement(textContainer.transform, "TapToCont", "TAP ANYWHERE TO CONTINUE", new Vector2(0, -450), new Vector2(800, 50), 24, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, false);
        CanvasGroup tapCg = tapTextComp.gameObject.AddComponent<CanvasGroup>();
        tapCg.alpha = 0f;

        float tapFadeInDuration = 1.0f;
        fadeTimer = 0f;
        while (fadeTimer < tapFadeInDuration)
        {
            fadeTimer += Time.deltaTime;
            tapCg.alpha = Mathf.Lerp(0f, 1f, fadeTimer / tapFadeInDuration);
            yield return null;
        }
        tapCg.alpha = 1f;

        bool tapped = false;
        while (!tapped)
        {
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)) 
            {
                tapped = true;
            }
            yield return null;
        }

        float textFadeTime = 0.6f;
        float timer = 0f;
        while (timer < textFadeTime)
        {
            timer += Time.deltaTime;
            textCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / textFadeTime);
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);

        float bgFadeTime = 1.2f;
        timer = 0f;
        while (timer < bgFadeTime)
        {
            timer += Time.deltaTime;
            introCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / bgFadeTime);
            
            if (bgmSource != null && !isAudioMuted && timer > (bgFadeTime * 0.5f)) 
            {
                if (!bgmSource.isPlaying) bgmSource.Play();
                bgmSource.volume = Mathf.Lerp(0f, 1f, (timer - (bgFadeTime * 0.5f)) / (bgFadeTime * 0.5f));
            }
            yield return null;
        }

        isStartupSequenceRunning = false;

        if (bgmSource != null && !isAudioMuted) 
        {
            if (!bgmSource.isPlaying) bgmSource.Play();
            bgmSource.volume = 1f;
        }

        Destroy(introCanvasObj);
        
        if (notificationManager != null) notificationManager.CheckStartupPop();
    }

    private void GenerateRoundedSprite()
    {
        if (roundedSprite != null) return;
        int size = 64;
        int radius = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 1f;
                float cx = -1, cy = -1;
                
                if (x < radius && y < radius) { cx = radius; cy = radius; }
                else if (x >= size - radius && y < radius) { cx = size - radius - 1; cy = radius; }
                else if (x < radius && y >= size - radius) { cx = radius; cy = size - radius - 1; }
                else if (x >= size - radius && y >= size - radius) { cx = size - radius - 1; cy = size - radius - 1; }
                
                if (cx >= 0 && cy >= 0)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (dist > radius) alpha = 0f;
                    else if (dist > radius - 1f) alpha = radius - dist; 
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private void BuildUI()
    {
        bool settingsWasOpen = settingsPanel != null && settingsPanel.activeSelf;
        bool shopWasOpen = shopPanel != null && shopPanel.activeSelf;
        bool stationsWasOpen = stationsModal != null && stationsModal.activeSelf;
        bool noticeWasOpen = noticeModal != null && noticeModal.activeSelf;

        if (canvasObj != null) Destroy(canvasObj);
        
        CreatePackedTroskiUI();
        SetFocus(FocusTarget.None);
        
        UpdateDisplayStats(currentDisplayedCash, currentDisplayedPrem, currentDisplayedDaily);
        
        RefreshMissionUI();
        UpdateMusicUI();

        if (string.IsNullOrEmpty(transportCompany)) OpenProfileEditor();

        if (settingsWasOpen && settingsPanel != null) settingsPanel.SetActive(true);
        if (shopWasOpen && shopPanel != null) shopPanel.SetActive(true);
        if (stationsWasOpen && stationsModal != null) stationsModal.SetActive(true);
        if (noticeWasOpen && noticeModal != null) noticeModal.SetActive(true);
        
        if (notificationManager != null)
        {
            notificationManager.UpdateUIRefs(miniPromoSlider, modalPromoSlider, noticeModal, promoFallbackText);
        }
    }

    void Update()
    {
        if (isStartupSequenceRunning) return;

        // Daily Check - Midnight Real Time Local
        string todayStr = System.DateTime.Now.ToString("yyyyMMdd");
        if (lastSavedDate != todayStr)
        {
            ResetDailyGoal();
        }

        dayTimer += Time.deltaTime;
        if (dayTimer >= REAL_SECONDS_PER_DAY)
        {
            dayTimer -= REAL_SECONDS_PER_DAY;
            currentDay++;
            SavePlayerData();
        }
        UpdateTimeUI();

        if (bgmSource != null && !bgmSource.isPlaying && !isAudioMuted && musicTracks != null && musicTracks.Length > 0)
        {
            if (loadingPanel == null || !loadingPanel.activeSelf)
            {
                NextTrack();
            }
        }

        if (IsModalActive()) return;

        bool inputDetected = false;
        Vector3 inputPosition = Vector3.zero;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began && !IsPointerOverUI(touch.fingerId))
            {
                inputDetected = true;
                inputPosition = touch.position;
            }
        }
        else if (Input.GetMouseButtonDown(0) && !IsPointerOverUI(-1))
        {
            inputDetected = true;
            inputPosition = Input.mousePosition;
        }

        if (inputDetected) HandleSelection(inputPosition);
        if (cameraTransform != null) UpdateCameraPosition();
        UpdateMateRotation();
    }

    private void ResetDailyGoal()
    {
        currentDailyEarnings = 0;
        currentDisplayedDaily = 0;
        dailyTargetEarnings = UnityEngine.Random.Range(1000, 2500) + (playerLevel * UnityEngine.Random.Range(300, 600));
        lastSavedDate = System.DateTime.Now.ToString("yyyyMMdd");
        
        PlayerPrefs.SetInt("DailyEarnings", currentDailyEarnings);
        PlayerPrefs.SetInt("DailyTargetEarnings", dailyTargetEarnings);
        PlayerPrefs.SetString("LastPlayedDate", lastSavedDate);
        PlayerPrefs.SetInt("LastSeenDaily", currentDisplayedDaily);
        PlayerPrefs.Save();
        
        UpdateDisplayStats(currentDisplayedCash, currentDisplayedPrem, currentDailyEarnings);
    }

    private bool IsModalActive()
    {
        return (settingsPanel != null && settingsPanel.activeSelf) || 
               (shopPanel != null && shopPanel.activeSelf) ||
               (loadingPanel != null && loadingPanel.activeSelf) ||
               (profileEditPanel != null && profileEditPanel.activeSelf) ||
               (stationsModal != null && stationsModal.activeSelf) ||
               (shiftModeModal != null && shiftModeModal.activeSelf) ||
               (noticeModal != null && noticeModal.activeSelf);
    }

    // --- DATA MANAGEMENT ---

    private void LoadPlayerData()
    {
        transportCompany = PlayerPrefs.GetString("CompanyName", "");
        playerCashGHS = PlayerPrefs.GetInt("PlayerCash", 1000);
        playerPremium = PlayerPrefs.GetInt("PlayerPremium", 0);
        playerLevel = PlayerPrefs.GetInt("PlayerLevel", 1);
        currentXP = PlayerPrefs.GetInt("CurrentXP", 0);
        currentDailyEarnings = PlayerPrefs.GetInt("DailyEarnings", 0);
        vehicleCondition = PlayerPrefs.GetInt("VehicleCondition", 100); 
        currentTroskiIndex = PlayerPrefs.GetInt("SelectedTroski", 0);
        currentMateIndex = PlayerPrefs.GetInt("SelectedMate", 0);
        selectedRouteIndex = PlayerPrefs.GetInt("SelectedRoute", -1);
        selectedThemeIndex = PlayerPrefs.GetInt("ThemeIndex", 0);
        
        dailyTargetEarnings = PlayerPrefs.GetInt("DailyTargetEarnings", -1);
        lastSavedDate = PlayerPrefs.GetString("LastPlayedDate", "");
        
        currentDay = PlayerPrefs.GetInt("CurrentDay", 1);
        dayTimer = PlayerPrefs.GetFloat("DayTimer", 0f);

        int lastCash = PlayerPrefs.GetInt("LastSeenCash", playerCashGHS);
        int lastPrem = PlayerPrefs.GetInt("LastSeenPrem", playerPremium);
        int lastDaily = PlayerPrefs.GetInt("LastSeenDaily", currentDailyEarnings);

        currentDisplayedCash = lastCash;
        currentDisplayedPrem = lastPrem;
        currentDisplayedDaily = lastDaily;

        string b64Img = PlayerPrefs.GetString("ProfileImageBase64", "");
        if (!string.IsNullOrEmpty(b64Img))
        {
            try
            {
                byte[] imgBytes = Convert.FromBase64String(b64Img);
                playerCustomLogo = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                playerCustomLogo.LoadImage(imgBytes);
            }
            catch (Exception e) { Debug.LogError("Failed to load profile image: " + e.Message); }
        }
        
        themeColor = availableThemes[Mathf.Clamp(selectedThemeIndex, 0, availableThemes.Length - 1)];
        CalculateLevelThreshold();

        // Force reset if no daily target set or if date changed
        string todayStr = System.DateTime.Now.ToString("yyyyMMdd");
        if (dailyTargetEarnings == -1 || string.IsNullOrEmpty(lastSavedDate) || lastSavedDate != todayStr)
        {
            ResetDailyGoal();
        }

        isAudioMuted = PlayerPrefs.GetInt("AudioMuted", 0) == 1;
        controlScheme = PlayerPrefs.GetInt("ControlScheme", 0);
        
        currentGraphicsLevel = PlayerPrefs.HasKey("GraphicsLevel") ? PlayerPrefs.GetInt("GraphicsLevel") : QualitySettings.GetQualityLevel();
        
        // This will apply the graphics level AND the resolution downscaling correctly
        SetGraphicsQuality(currentGraphicsLevel, false);

        currentFPS = PlayerPrefs.GetInt("TargetFPS", 60);
        Application.targetFrameRate = currentFPS;

        if (bgmSource != null && !isAudioMuted) bgmSource.volume = 1f; 

        AnimateStats(lastCash, playerCashGHS, lastPrem, playerPremium, lastDaily, currentDailyEarnings);
    }

    public void SavePlayerData()
    {
        PlayerPrefs.SetString("CompanyName", transportCompany);
        PlayerPrefs.SetInt("PlayerCash", playerCashGHS);
        PlayerPrefs.SetInt("PlayerPremium", playerPremium);
        PlayerPrefs.SetInt("PlayerLevel", playerLevel);
        PlayerPrefs.SetInt("CurrentXP", currentXP);
        PlayerPrefs.SetInt("DailyEarnings", currentDailyEarnings);
        PlayerPrefs.SetInt("DailyTargetEarnings", dailyTargetEarnings);
        PlayerPrefs.SetString("LastPlayedDate", lastSavedDate);
        PlayerPrefs.SetInt("VehicleCondition", vehicleCondition);
        PlayerPrefs.SetInt("SelectedTroski", currentTroskiIndex);
        PlayerPrefs.SetInt("SelectedMate", currentMateIndex);
        PlayerPrefs.SetInt("SelectedRoute", selectedRouteIndex);
        PlayerPrefs.SetInt("ThemeIndex", selectedThemeIndex);
        
        PlayerPrefs.SetInt("CurrentDay", currentDay);
        PlayerPrefs.SetFloat("DayTimer", dayTimer);

        PlayerPrefs.SetInt("LastSeenCash", playerCashGHS);
        PlayerPrefs.SetInt("LastSeenPrem", playerPremium);
        PlayerPrefs.SetInt("LastSeenDaily", currentDailyEarnings);

        if (playerCustomLogo != null)
        {
            byte[] bytes = playerCustomLogo.EncodeToPNG();
            string b64 = Convert.ToBase64String(bytes);
            PlayerPrefs.SetString("ProfileImageBase64", b64);
        }
        
        PlayerPrefs.Save();
        
        AnimateStats(currentDisplayedCash, playerCashGHS, currentDisplayedPrem, playerPremium, currentDisplayedDaily, currentDailyEarnings);
        UpdateServiceButtonUI();
    }

    // --- CURRENCY ANIMATION SYSTEM ---

    private void AnimateStats(int startCash, int endCash, int startPrem, int endPrem, int startDaily, int endDaily)
    {
        if (startCash == endCash && startPrem == endPrem && startDaily == endDaily)
        {
            UpdateDisplayStats(endCash, endPrem, endDaily);
            return;
        }

        if (statAnimCoroutine != null) StopCoroutine(statAnimCoroutine);
        statAnimCoroutine = StartCoroutine(DoAnimateStats(startCash, endCash, startPrem, endPrem, startDaily, endDaily));
    }

    private IEnumerator DoAnimateStats(int startCash, int endCash, int startPrem, int endPrem, int startDaily, int endDaily)
    {
        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easeT = t * t * (3f - 2f * t); 

            int curCash = Mathf.RoundToInt(Mathf.Lerp(startCash, endCash, easeT));
            int curPrem = Mathf.RoundToInt(Mathf.Lerp(startPrem, endPrem, easeT));
            int curDaily = Mathf.RoundToInt(Mathf.Lerp(startDaily, endDaily, easeT));

            UpdateDisplayStats(curCash, curPrem, curDaily);
            yield return null;
        }

        UpdateDisplayStats(endCash, endPrem, endDaily);
    }

    private void UpdateDisplayStats(int c, int p, int d)
    {
        currentDisplayedCash = c;
        currentDisplayedPrem = p;
        currentDisplayedDaily = d;

        if (cashTextComp != null) cashTextComp.text = c.ToString("N0");
        if (premTextComp != null) premTextComp.text = p.ToString("N0");

        if (dailyEarningsText != null)
        {
            if (d >= dailyTargetEarnings)
            {
                dailyEarningsText.text = "<size=24>✓ COME BACK TOMORROW</size>";
            }
            else
            {
                dailyEarningsText.text = $"GHS {d:N0} / {dailyTargetEarnings:N0}";
            }
        }
        
        if (dailyProgressFill != null)
        {
            if (d >= dailyTargetEarnings)
            {
                dailyProgressFill.anchoredPosition = new Vector2(0, 0);
                dailyProgressFill.sizeDelta = new Vector2(380f, 20);
            }
            else
            {
                float dailyProgress = Mathf.Clamp01((float)d / dailyTargetEarnings);
                dailyProgressFill.anchoredPosition = new Vector2((dailyProgress * 380f - 380f) / 2f, 0);
                dailyProgressFill.sizeDelta = new Vector2(380f * dailyProgress, 20);
            }
        }
    }

    private void CalculateLevelThreshold()
    {
        xpToNextLevel = Mathf.RoundToInt(500 * Mathf.Pow(1.5f, playerLevel - 1));
    }

    public void AddXP(int amount)
    {
        currentXP += amount;
        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            playerLevel++;
            CalculateLevelThreshold();
        }
        SavePlayerData();
    }

    private string GetDriverRank(int level)
    {
        if (level < 5) return "Novice Driver";
        if (level < 10) return "Rookie Driver";
        if (level < 20) return "Journeyman";
        if (level < 30) return "Senior Driver";
        if (level < 40) return "Master Driver";
        return "Legendary Boss";
    }

    // --- MISSION SYSTEM ---

    private void LoadMissions()
    {
        string missionJson = PlayerPrefs.GetString("Missions", "");
        if (!string.IsNullOrEmpty(missionJson)) missionData = JsonUtility.FromJson<MissionWrapper>(missionJson);
        if (missionData.missions == null) missionData.missions = new List<TroskiMission>();

        if (missionData.missions.Count == 0) GenerateMissionBatch();
    }

    private void GenerateMissionBatch()
    {
        missionData.missions.Clear();
        for(int i = 0; i < 3; i++)
        {
            TroskiMission newMission = new TroskiMission();
            int type = UnityEngine.Random.Range(0, 3);
            if (type == 0)
            {
                int passengers = UnityEngine.Random.Range(2, 6) * 10 * playerLevel; 
                newMission.title = $"Load {passengers} Passengers";
                newMission.targetProgress = passengers;
                newMission.currentProgress = 0; 
                newMission.rewardGHS = passengers * 2;
                newMission.rewardXP = 100 * playerLevel;
            }
            else if (type == 1)
            {
                int routes = UnityEngine.Random.Range(2, 5);
                newMission.title = $"Complete {routes} Routes";
                newMission.targetProgress = routes;
                newMission.currentProgress = 0; 
                newMission.rewardGHS = routes * 200 * playerLevel;
                newMission.rewardXP = 150 * playerLevel;
            }
            else
            {
                newMission.title = "Service Troski or Buy Parts";
                newMission.targetProgress = 1;
                newMission.currentProgress = 0; 
                newMission.rewardGHS = 300 * playerLevel;
                newMission.rewardXP = 200 * playerLevel;
            }
            missionData.missions.Add(newMission);
        }
        SaveMissions();
    }

    private void SaveMissions()
    {
        PlayerPrefs.SetString("Missions", JsonUtility.ToJson(missionData));
        PlayerPrefs.Save();
    }

    public void ClaimRewards()
    {
        bool rewardsClaimed = false;
        bool allCompleted = true;

        foreach (var mission in missionData.missions)
        {
            if (mission.IsComplete && mission.rewardGHS > 0)
            {
                playerCashGHS += mission.rewardGHS;
                AddXP(mission.rewardXP);
                mission.rewardGHS = 0; 
                rewardsClaimed = true;
            }
            if (!mission.IsComplete) allCompleted = false;
        }

        if (rewardsClaimed) PlaySFX(claimRewardSFX);

        if (allCompleted) GenerateMissionBatch();
        else if (rewardsClaimed) SaveMissions();

        if (rewardsClaimed || allCompleted)
        {
            SavePlayerData();
            RefreshMissionUI();
        }
    }

    private void RefreshMissionUI()
    {
        if (taskListContainer == null) return;
        foreach (Transform child in taskListContainer.transform) Destroy(child.gameObject);

        float yPos = 45; 
        foreach (var mission in missionData.missions)
        {
            string status = mission.rewardGHS == 0 && mission.IsComplete ? "CLAIMED" : $"{mission.currentProgress}/{mission.targetProgress}";
            CreateTaskRow(taskListContainer.transform, mission.title, status, mission.IsComplete, new Vector2(0, yPos));
            yPos -= 50; 
        }
    }

    // --- AUDIO / BGM / SFX SYSTEM ---
    private void SetupAudio()
    {
        bgmSource = gameObject.GetComponent<AudioSource>();
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        
        bgmSource.loop = false; 
        bgmSource.playOnAwake = false;
        bgmSource.volume = 1f; 

        if (musicTracks != null && musicTracks.Length > 0)
        {
            currentTrackIndex = UnityEngine.Random.Range(0, musicTracks.Length); 
            bgmSource.clip = musicTracks[currentTrackIndex];
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null && !isAudioMuted) sfxSource.PlayOneShot(clip);
    }

    public void NextTrack()
    {
        if (musicTracks == null || musicTracks.Length == 0) return;
        currentTrackIndex = (currentTrackIndex + 1) % musicTracks.Length;
        PlayTrack();
    }

    public void PrevTrack()
    {
        if (musicTracks == null || musicTracks.Length == 0) return;
        currentTrackIndex--;
        if (currentTrackIndex < 0) currentTrackIndex = musicTracks.Length - 1;
        PlayTrack();
    }

    private void PlayTrack()
    {
        bgmSource.clip = musicTracks[currentTrackIndex];
        bgmSource.volume = 1f; 
        if (!isAudioMuted) bgmSource.Play();
        UpdateMusicUI();
    }

    private void UpdateMusicUI()
    {
        if (musicTitleComp != null)
        {
            if (musicTracks == null || musicTracks.Length == 0) musicTitleComp.text = "♫ NO DISK";
            else musicTitleComp.text = "♫ " + musicTracks[currentTrackIndex].name;
        }
    }

    // --- TIME & SALARY SYSTEM ---

    private void UpdateTimeUI()
    {
        if (timeTextComp != null)
        {
            float inGameHours = (dayTimer / REAL_SECONDS_PER_DAY) * 24f;
            int hours = Mathf.FloorToInt(inGameHours);
            int minutes = Mathf.FloorToInt((inGameHours - hours) * 60f);
            
            timeTextComp.text = $"<color=white>DAY {currentDay}</color>   {hours:00}:{minutes:00}";
        }
    }

    private int GetMateWage(int index)
    {
        return 50 + (index * 25);
    }

    // --- INTERACTION & LOGIC ---

    private void HandleSelection(Vector3 screenPosition)
    {
        if (currentFocus != FocusTarget.None) return;
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 2000f)) if (ProcessHit(hit.collider.transform)) return;
        
        RaycastHit[] hits = Physics.SphereCastAll(ray, 0.3f, 2000f); 
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit sphereHit in hits) if (ProcessHit(sphereHit.collider.transform)) return;
    }

    private bool ProcessHit(Transform hitTransform)
    {
        if (CheckTagUpwards(hitTransform, "Mate")) { FocusOnMate(); return true; }
        else if (CheckTagUpwards(hitTransform, "Troski")) { FocusOnTroski(); return true; }
        return false;
    }

    private bool CheckTagUpwards(Transform currentTransform, string tagToFind)
    {
        Transform current = currentTransform;
        while (current != null)
        {
            if (current.CompareTag(tagToFind)) return true;
            current = current.parent;
        }
        return false;
    }

    public void FocusOnMate() { SetFocus(FocusTarget.Mate); }
    public void FocusOnTroski() { SetFocus(FocusTarget.Troski); }
    public void SetFocusNone() { SetFocus(FocusTarget.None); }

    private void SetFocus(FocusTarget target)
    {
        currentFocus = target;
        if (packedHomeContainer != null) packedHomeContainer.SetActive(currentFocus == FocusTarget.None);
        if (troskiCustomizationContainer != null) troskiCustomizationContainer.SetActive(currentFocus == FocusTarget.Troski);
        if (mateCustomizationContainer != null) mateCustomizationContainer.SetActive(currentFocus == FocusTarget.Mate);
        HandleMateAnimation(currentFocus == FocusTarget.Mate);
    }

    private void HandleMateAnimation(bool isFocused)
    {
        if (currentMate == null) return;
        Animator mateAnimator = currentMate.GetComponent<Animator>();
        if (mateAnimator == null) mateAnimator = currentMate.GetComponentInChildren<Animator>();

        if (mateAnimator != null)
        {
            mateAnimator.ResetTrigger("TurnToCamera");
            mateAnimator.ResetTrigger("TurnBack");
            mateAnimator.SetTrigger(isFocused ? "TurnToCamera" : "TurnBack");
        }
    }

    private void UpdateCameraPosition()
    {
        Vector3 targetPosition = defaultCameraPosition;
        Quaternion targetRotation = defaultCameraRotation;

        if (currentFocus == FocusTarget.Mate && currentMate != null)
        {
            targetPosition = currentMate.transform.position + cameraMateViewOffset;
            Vector3 lookTarget = currentMate.transform.position + (Vector3.up * 1.2f);
            targetRotation = Quaternion.LookRotation(lookTarget - targetPosition);
        }
        else if (currentFocus == FocusTarget.Troski && currentTroski != null)
        {
            targetPosition = currentTroski.transform.position + cameraTroskiViewOffset;
            Vector3 lookTarget = currentTroski.transform.position + (Vector3.up * 1.6f); 
            targetRotation = Quaternion.LookRotation(lookTarget - targetPosition);
        }

        cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, Time.deltaTime * cameraMoveSpeed);
        cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, Time.deltaTime * cameraMoveSpeed);
    }

    private void UpdateMateRotation()
    {
        if (currentMate == null || mateDefaultRotations == null || mateDefaultRotations.Length <= currentMateIndex) return;
        Quaternion targetRotation = mateDefaultRotations[currentMateIndex];

        if (currentFocus == FocusTarget.Mate)
        {
            Vector3 directionToCamera = cameraTransform.position - currentMate.transform.position;
            directionToCamera.y = 0f;
            if (directionToCamera.sqrMagnitude > 0.001f) targetRotation = Quaternion.LookRotation(directionToCamera);
        }
        currentMate.transform.rotation = Quaternion.Slerp(currentMate.transform.rotation, targetRotation, Time.deltaTime * mateSpinSpeed);
    }

    public void SwitchModelNext()
    {
        if (currentFocus == FocusTarget.Mate && mateModels != null && mateModels.Length > 0)
        {
            currentMateIndex = (currentMateIndex + 1) % mateModels.Length;
            UpdateMateVisibility();
            RefreshMateStatsUI();
        }
        else if (currentFocus == FocusTarget.Troski && troskiModels != null && troskiModels.Length > 0)
        {
            currentTroskiIndex = (currentTroskiIndex + 1) % troskiModels.Length;
            UpdateTroskiVisibility();
            RefreshTroskiStatsUI();
        }
    }

    public void SwitchModelPrevious()
    {
        if (currentFocus == FocusTarget.Mate && mateModels != null && mateModels.Length > 0)
        {
            currentMateIndex--;
            if (currentMateIndex < 0) currentMateIndex = mateModels.Length - 1;
            UpdateMateVisibility();
            RefreshMateStatsUI();
        }
        else if (currentFocus == FocusTarget.Troski && troskiModels != null && troskiModels.Length > 0)
        {
            currentTroskiIndex--;
            if (currentTroskiIndex < 0) currentTroskiIndex = troskiModels.Length - 1;
            UpdateTroskiVisibility();
            RefreshTroskiStatsUI();
        }
    }

    public void SaveSelectedModels()
    {
        SavePlayerData();
        SetFocusNone();
    }

    public void ServiceTroski()
    {
        int cost = 200;
        if (vehicleCondition < 100 && playerCashGHS >= cost)
        {
            playerCashGHS -= cost;
            vehicleCondition = 100;
            PlaySFX(buySFX);
            SavePlayerData();
            
            foreach(var m in missionData.missions) {
                if(m.title.Contains("Service") && !m.IsComplete) { m.currentProgress = 1; SaveMissions(); RefreshMissionUI(); }
            }
        }
    }

    private void UpdateServiceButtonUI()
    {
        if (serviceBtnTextComp != null)
        {
            if (vehicleCondition >= 100) serviceBtnTextComp.text = "TROSKI IN PERFECT\n<color=#15B340><size=16>CONDITION</size></color>";
            else serviceBtnTextComp.text = "SERVICE TROSKI\n<color=#FACC15><size=16>GHS 200</size></color>";
        }
    }

    // --- PROFILE & MODAL LOGIC ---

    public void OpenProfileEditor()
    {
        if (profileEditPanel != null)
        {
            profileEditPanel.SetActive(true);
            PlaySFX(modalOpenSFX);
            tempProfileTexture = playerCustomLogo;
            
            if (profileEditTitle != null)
                profileEditTitle.text = string.IsNullOrEmpty(transportCompany) ? "WELCOME TO TROSKI SIMULATOR" : "EDIT COMPANY PROFILE";
                
            if (companyNameInput != null)
                companyNameInput.text = transportCompany;

            UpdateProfilePreviewImage();
        }
    }

    public void CloseProfileEditor()
    {
        if (profileEditPanel != null)
        {
            if (string.IsNullOrEmpty(transportCompany) && (companyNameInput == null || string.IsNullOrEmpty(companyNameInput.text)))
                return;

            profileEditPanel.SetActive(false);
        }
    }

    public void SaveCompanyProfile()
    {
        if (companyNameInput != null && !string.IsNullOrEmpty(companyNameInput.text))
        {
            transportCompany = companyNameInput.text.ToUpper();
            playerCustomLogo = tempProfileTexture;
            
            SavePlayerData();
            BuildUI(); 
            CloseProfileEditor();
        }
    }

    public void SelectProfilePicture()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Select Profile Picture", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(fileData); 
            tempProfileTexture = tex;
            UpdateProfilePreviewImage();
        }
#elif UNITY_ANDROID || UNITY_IOS
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (path != null)
            {
                Texture2D tex = NativeGallery.LoadImageAtPath(path, 512, false);
                if (tex != null)
                {
                    tempProfileTexture = tex;
                    UpdateProfilePreviewImage();
                }
            }
        }, "Select a custom logo", "image/*");
#endif
    }

    private void UpdateProfilePreviewImage()
    {
        if (profilePreviewImg != null)
        {
            Texture2D texToUse = tempProfileTexture != null ? tempProfileTexture : defaultProfileIcon;
            if (texToUse != null)
            {
                profilePreviewImg.sprite = Sprite.Create(texToUse, new Rect(0, 0, texToUse.width, texToUse.height), new Vector2(0.5f, 0.5f));
                profilePreviewImg.color = Color.white;
            }
            else
            {
                profilePreviewImg.sprite = null;
                profilePreviewImg.color = themeColor;
            }
        }
    }

    public void ToggleSettings() 
    { 
        if (settingsPanel != null) 
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);
            if (settingsPanel.activeSelf) PlaySFX(modalOpenSFX);
        }
    }
    
    public void ToggleShop() 
    { 
        if (shopPanel != null) 
        {
            shopPanel.SetActive(!shopPanel.activeSelf); 
            if (shopPanel.activeSelf) PlaySFX(modalOpenSFX);
        }
    }
    
    public void ToggleStationsModal() 
    { 
        if (stationsModal != null) 
        {
            stationsModal.SetActive(!stationsModal.activeSelf); 
            if (stationsModal.activeSelf) PlaySFX(modalOpenSFX);
        }
    }
    
    public void ToggleNoticeModal()
    {
        if (noticeModal != null && notificationManager != null && notificationManager.hasActiveNotice)
        {
            noticeModal.SetActive(!noticeModal.activeSelf);
            if (noticeModal.activeSelf) PlaySFX(modalOpenSFX);
        }
    }
    
    public void OpenShiftChoiceModal()
    {
        if (selectedRouteIndex < 0 || selectedRouteIndex >= availableRoutes.Count)
        {
            ToggleStationsModal(); 
            return;
        }
        if (shiftModeModal != null) 
        {
            shiftModeModal.SetActive(true);
            PlaySFX(modalOpenSFX);
        }
    }
    public void CloseShiftChoiceModal() { if (shiftModeModal != null) shiftModeModal.SetActive(false); }

    public void SelectRoute(int index)
    {
        if (index >= 0 && index < availableRoutes.Count)
        {
            RouteData r = availableRoutes[index];
            if (playerLevel >= r.requiredLevel)
            {
                selectedRouteIndex = index;
                SavePlayerData();
                if (routeTextComp != null) routeTextComp.text = r.routeName;
                ToggleStationsModal();
            }
        }
    }

    public void SetTheme(int index)
    {
        if(index >= 0 && index < availableThemes.Length)
        {
            selectedThemeIndex = index;
            themeColor = availableThemes[selectedThemeIndex];
            SavePlayerData();
            BuildUI(); 
        }
    }

    public void SetGraphicsQuality(int level, bool rebuildUI = true)
    {
        if (level >= 0 && level < QualitySettings.names.Length)
        {
            currentGraphicsLevel = level;
            QualitySettings.SetQualityLevel(currentGraphicsLevel, true);
            PlayerPrefs.SetInt("GraphicsLevel", currentGraphicsLevel);
            PlayerPrefs.Save();
            
            // Dynamic Resolution Scaling based on Quality Level to drastically boost performance on low-end
            int totalLevels = QualitySettings.names.Length;
            float scale = 1.0f;
            
            if (totalLevels > 1)
            {
                // Map the level to a scale multiplier (e.g., Lowest = 40% resolution, Highest = 100%)
                float t = (float)currentGraphicsLevel / (totalLevels - 1);
                scale = Mathf.Lerp(0.4f, 1.0f, t);
            }
            
            int baseWidth = Display.main.systemWidth;
            int baseHeight = Display.main.systemHeight;
            
            // Force landscape logic in case system returns portrait dimensions initially
            if (baseHeight > baseWidth) 
            {
                int temp = baseWidth;
                baseWidth = baseHeight;
                baseHeight = temp;
            }

            // Fallback safety if the display metrics return weirdly low
            if (baseWidth < 800) baseWidth = Screen.currentResolution.width;
            if (baseHeight < 480) baseHeight = Screen.currentResolution.height;

            int targetWidth = Mathf.RoundToInt(baseWidth * scale);
            int targetHeight = Mathf.RoundToInt(baseHeight * scale);

            Screen.SetResolution(targetWidth, targetHeight, true);

            if (rebuildUI) BuildSettingsPanel(); 
        }
    }

    public void SetFPSTarget(int fps)
    {
        currentFPS = fps;
        Application.targetFrameRate = currentFPS;
        PlayerPrefs.SetInt("TargetFPS", currentFPS);
        PlayerPrefs.Save();
        BuildSettingsPanel();
    }

    public void ToggleAudio()
    {
        isAudioMuted = !isAudioMuted;
        PlayerPrefs.SetInt("AudioMuted", isAudioMuted ? 1 : 0);
        PlayerPrefs.Save();
        if(bgmSource != null) {
            if(isAudioMuted) bgmSource.Pause();
            else if(musicTracks.Length > 0 && !isStartupSequenceRunning) bgmSource.Play();
        }
        BuildSettingsPanel();
    }

    public void ToggleControls()
    {
        controlScheme = (controlScheme + 1) % 2; 
        PlayerPrefs.SetInt("ControlScheme", controlScheme);
        PlayerPrefs.Save();
        BuildSettingsPanel();
    }

    public void SimulateIAP(int premiumAmount)
    {
        Debug.Log("Simulating IAP purchase for " + premiumAmount + " Premium Coins...");
        playerPremium += premiumAmount;
        PlaySFX(buySFX);
        SavePlayerData();
    }

    public void BuyCashWithPremium(int cashAmount, int premCost)
    {
        if (playerPremium >= premCost) { 
            playerPremium -= premCost; 
            playerCashGHS += cashAmount; 
            PlaySFX(buySFX);
            SavePlayerData(); 
        }
    }

    public void WatchAdForPremium()
    {
        Debug.Log("Simulating Rewarded Ad...");
        playerPremium += 10; 
        SavePlayerData();
    }

    public void StartShiftLoadMode() { StartCoroutine(TransitionToGame("Load at Station")); }
    public void StartShiftRoamMode() { StartCoroutine(TransitionToGame("Move & Pick")); }

    private IEnumerator TransitionToGame(string mode)
    {
        CloseShiftChoiceModal();
        PlayerPrefs.SetString("SelectedGameMode", mode); 
        PlayerPrefs.Save();

        CanvasGroup loadCg = null;
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            loadCg = loadingPanel.GetComponent<CanvasGroup>();
            if (loadCg != null) loadCg.alpha = 0f;

            if (topBarContainer != null) topBarContainer.SetActive(false);
            if (packedHomeContainer != null) packedHomeContainer.SetActive(false);
        }

        float fadeTimer = 0f;
        float fadeDuration = 0.5f;
        while (fadeTimer < fadeDuration)
        {
            fadeTimer += Time.deltaTime;
            if (loadCg != null) loadCg.alpha = Mathf.Lerp(0f, 1f, fadeTimer / fadeDuration);
            yield return null;
        }
        if (loadCg != null) loadCg.alpha = 1f;

        if (loadingFillRect != null) loadingFillRect.sizeDelta = new Vector2(0, loadingFillRect.sizeDelta.y);

        float timer = 0f;
        float startVolume = bgmSource != null ? bgmSource.volume : 0f;
        float minLoadingTime = 3f;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Main");
        if (asyncLoad != null) asyncLoad.allowSceneActivation = false;

        while (timer < minLoadingTime || (asyncLoad != null && asyncLoad.progress < 0.9f))
        {
            timer += Time.deltaTime;

            float realProgress = asyncLoad != null ? asyncLoad.progress / 0.9f : 1f;
            float timeProgress = Mathf.Clamp01(timer / minLoadingTime);
            float displayProgress = Mathf.Min(realProgress, timeProgress);

            if (loadingFillRect != null)
            {
                loadingFillRect.sizeDelta = new Vector2(800 * displayProgress, 30);
            }
            if (loadingPercentText != null)
            {
                loadingPercentText.text = Mathf.RoundToInt(displayProgress * 100f) + "%";
            }
            
            if (loadingText != null) loadingText.text = "DISPATCHING TROSKI" + new string('.', (int)(timer * 3) % 4);
            
            if (bgmSource != null && startVolume > 0 && !isAudioMuted)
            {
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / minLoadingTime);
            }

            yield return null; 
        }

        if (loadingFillRect != null) loadingFillRect.sizeDelta = new Vector2(800, 30);
        if (loadingPercentText != null) loadingPercentText.text = "100%";
        yield return new WaitForSeconds(0.2f); 

        if (asyncLoad != null) asyncLoad.allowSceneActivation = true;
    }

    private void UpdateTroskiVisibility()
    {
        if (troskiModels == null || troskiModels.Length == 0) return;
        for (int i = 0; i < troskiModels.Length; i++)
        {
            if (troskiModels[i] != null)
            {
                bool isSelected = (i == currentTroskiIndex);
                troskiModels[i].SetActive(isSelected);
                if (isSelected) currentTroski = troskiModels[i];
            }
        }
    }

    private void UpdateMateVisibility()
    {
        if (mateModels == null || mateModels.Length == 0) return;
        for (int i = 0; i < mateModels.Length; i++)
        {
            if (mateModels[i] != null)
            {
                bool isSelected = (i == currentMateIndex);
                mateModels[i].SetActive(isSelected);
                if (isSelected)
                {
                    currentMate = mateModels[i];
                    if (currentFocus == FocusTarget.Mate) HandleMateAnimation(true);
                }
            }
        }
    }

    private bool IsPointerOverUI(int pointerID)
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject(pointerID);
    }

    // --- PROCEDURAL UI GENERATION ---

    private void CreatePackedTroskiUI()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }

        canvasObj = new GameObject("MainMenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f; 
        
        canvasObj.AddComponent<GraphicRaycaster>();

        topBarContainer = CreatePanel(canvasObj.transform, "TopBar", Vector2.zero, new Vector2(1920, 100), troskiDarkAsphalt, false);
        RectTransform topRect = topBarContainer.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0, 1);
        topRect.anchorMax = new Vector2(1, 1);
        topRect.pivot = new Vector2(0.5f, 1);
        topRect.anchoredPosition = Vector2.zero;
        topRect.sizeDelta = new Vector2(0, 100);
        
        GameObject profileAreaBtn = CreateButtonElement(topBarContainer.transform, "ProfileAreaBtn", "", Vector2.zero, new Vector2(400, 80), OpenProfileEditor, Color.clear, Color.clear, 1, false, false);
        RectTransform profRect = profileAreaBtn.GetComponent<RectTransform>();
        profRect.anchorMin = new Vector2(0, 0.5f);
        profRect.anchorMax = new Vector2(0, 0.5f);
        profRect.pivot = new Vector2(0, 0.5f);
        profRect.anchoredPosition = new Vector2(40, 0); 
        
        Texture2D currentProfIcon = playerCustomLogo != null ? playerCustomLogo : defaultProfileIcon;
        
        GameObject profMask = CreatePanel(profileAreaBtn.transform, "ProfMask", new Vector2(-150, 15), new Vector2(45, 45), Color.white, true);
        Mask profMaskComp = profMask.AddComponent<Mask>();
        profMaskComp.showMaskGraphic = false; 
        CreateImageElement(profMask.transform, "ProfIcon", currentProfIcon, Vector2.zero, new Vector2(45, 45), Color.white);

        CreateTextElement(profileAreaBtn.transform, "LvlText", "LVL " + playerLevel, new Vector2(-150, -15), new Vector2(70, 20), 14, FontStyle.Bold, TextAnchor.MiddleCenter, textWhite, false);
        
        CreateTextElement(profileAreaBtn.transform, "CompName", string.IsNullOrEmpty(transportCompany) ? "NEW COMPANY" : transportCompany, new Vector2(50, 15), new Vector2(300, 40), 28, FontStyle.Bold, TextAnchor.MiddleLeft, textWhite, false);
        CreateTextElement(profileAreaBtn.transform, "Rank", GetDriverRank(playerLevel), new Vector2(50, -15), new Vector2(300, 30), 20, FontStyle.Italic, TextAnchor.MiddleLeft, themeColor, false);

        GameObject ecoArea = CreatePanel(topBarContainer.transform, "EcoArea", Vector2.zero, new Vector2(500, 80), Color.clear, false);
        RectTransform ecoRect = ecoArea.GetComponent<RectTransform>();
        ecoRect.anchorMin = new Vector2(1, 0.5f);
        ecoRect.anchorMax = new Vector2(1, 0.5f);
        ecoRect.pivot = new Vector2(1, 0.5f);
        ecoRect.anchoredPosition = new Vector2(-40, 0); 
        
        GameObject cashBlock = CreatePanel(ecoArea.transform, "CashBlock", new Vector2(-120, 0), new Vector2(220, 60), troskiLightAsphalt, true);
        CreateImageElement(cashBlock.transform, "CashIcon", cashIcon, new Vector2(-80, 0), new Vector2(40, 40), troskiGreen);
        cashTextComp = CreateTextElement(cashBlock.transform, "CashTxt", currentDisplayedCash.ToString("N0"), new Vector2(20, 0), new Vector2(150, 60), 26, FontStyle.Bold, TextAnchor.MiddleCenter, textWhite, false);
        CreateButtonElement(cashBlock.transform, "AddCash", "+", new Vector2(110, 0), new Vector2(40, 60), ToggleShop, troskiGreen, textWhite, 28, false, true);

        GameObject premBlock = CreatePanel(ecoArea.transform, "PremBlock", new Vector2(150, 0), new Vector2(180, 60), troskiLightAsphalt, true);
        CreateImageElement(premBlock.transform, "PremIcon", premiumIcon, new Vector2(-60, 0), new Vector2(35, 35), themeColor);
        premTextComp = CreateTextElement(premBlock.transform, "PremTxt", currentDisplayedPrem.ToString("N0"), new Vector2(15, 0), new Vector2(100, 60), 26, FontStyle.Bold, TextAnchor.MiddleCenter, textWhite, false);
        CreateButtonElement(premBlock.transform, "AddPrem", "+", new Vector2(90, 0), new Vector2(40, 60), ToggleShop, themeColor, troskiDarkAsphalt, 28, false, true);

        // PACKED HOME CONTAINER
        packedHomeContainer = new GameObject("PackedHomeContainer");
        packedHomeContainer.transform.SetParent(canvasObj.transform, false);
        RectTransform phRect = packedHomeContainer.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one; phRect.sizeDelta = Vector2.zero;

        timeTextComp = CreateTextElement(packedHomeContainer.transform, "TimeTxt", "DAY 1   00:00", Vector2.zero, new Vector2(500, 60), 32, FontStyle.Bold, TextAnchor.MiddleLeft, themeColor, true);
        RectTransform timeRect = timeTextComp.GetComponent<RectTransform>();
        timeRect.anchorMin = new Vector2(0, 1);
        timeRect.anchorMax = new Vector2(0, 1);
        timeRect.pivot = new Vector2(0, 1);
        timeRect.anchoredPosition = new Vector2(40, -120); 
        Outline timeGlow = timeTextComp.gameObject.AddComponent<Outline>();
        timeGlow.effectColor = new Color(0, 0, 0, 0.9f);
        timeGlow.effectDistance = new Vector2(2, -2);
        UpdateTimeUI();

        GameObject logoArea = CreatePanel(packedHomeContainer.transform, "LogoPlaceholder", Vector2.zero, new Vector2(600, 160), Color.clear, false);
        RectTransform logoRect = logoArea.GetComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0.5f, 1);
        logoRect.anchorMax = new Vector2(0.5f, 1);
        logoRect.pivot = new Vector2(0.5f, 1);
        logoRect.anchoredPosition = new Vector2(0, -120); 
        CreateImageElement(logoArea.transform, "GameLogoImg", gameLogo, Vector2.zero, new Vector2(580, 140), Color.clear);
        if (gameLogo == null) CreateTextElement(logoArea.transform, "LogoFallbackText", "TROSKI SIMULATOR\n<size=24>LOGO PLACEHOLDER</size>", Vector2.zero, new Vector2(600, 160), 42, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, true);

        // LEFT PANEL 
        GameObject leftPanel = CreatePanel(packedHomeContainer.transform, "LeftPanel", Vector2.zero, new Vector2(450, 700), Color.clear, false);
        RectTransform leftRect = leftPanel.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0, 0.5f);
        leftRect.anchorMax = new Vector2(0, 0.5f);
        leftRect.pivot = new Vector2(0, 0.5f);
        leftRect.anchoredPosition = new Vector2(40, -50); 
        
        // Progress Block
        GameObject progBlock = CreatePanel(leftPanel.transform, "ProgBlock", new Vector2(0, 250), new Vector2(450, 140), troskiLightAsphalt, true);
        CreateTextElement(progBlock.transform, "ProgTitle", "DAILY SALES TARGET", new Vector2(0, 40), new Vector2(400, 40), 22, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, false);
        dailyEarningsText = CreateTextElement(progBlock.transform, "ProgVal", $"GHS {currentDisplayedDaily:N0} / {dailyTargetEarnings:N0}", new Vector2(0, 0), new Vector2(400, 40), 32, FontStyle.Bold, TextAnchor.MiddleCenter, textWhite, false);
        GameObject pbBg = CreatePanel(progBlock.transform, "PBBg", new Vector2(0, -40), new Vector2(380, 20), troskiDarkAsphalt, true);
        float dailyProgress = Mathf.Clamp01((float)currentDisplayedDaily / dailyTargetEarnings);
        GameObject pbFillObj = CreatePanel(pbBg.transform, "PBFill", new Vector2((dailyProgress * 380 - 380)/2, 0), new Vector2(380 * dailyProgress, 20), themeColor, true);
        dailyProgressFill = pbFillObj.GetComponent<RectTransform>();

        // Promo / Notification Banner Block with rounded Mask
        GameObject promoBlock = CreatePanel(leftPanel.transform, "PromoBlock", new Vector2(0, 105), new Vector2(450, 130), troskiDarkAsphalt, true);
        
        GameObject miniMaskObj = CreatePanel(promoBlock.transform, "MiniMask", Vector2.zero, new Vector2(450, 130), Color.white, true);
        Mask miniMask = miniMaskObj.AddComponent<Mask>();
        miniMask.showMaskGraphic = false;

        GameObject miniSlideContainer = new GameObject("MiniSlideContainer");
        miniSlideContainer.transform.SetParent(miniMaskObj.transform, false);
        miniPromoSlider = miniSlideContainer.AddComponent<RectTransform>();
        miniPromoSlider.anchorMin = new Vector2(0, 0.5f);
        miniPromoSlider.anchorMax = new Vector2(0, 0.5f);
        miniPromoSlider.pivot = new Vector2(0, 0.5f);
        miniPromoSlider.anchoredPosition = new Vector2(0, 0);
        miniPromoSlider.sizeDelta = new Vector2(450, 130);

        promoFallbackText = CreateTextElement(promoBlock.transform, "PromoFallback", "LOADING NOTICES...", Vector2.zero, new Vector2(400, 30), 16, FontStyle.Bold, TextAnchor.MiddleCenter, textMuted, false);
        CreateButtonElement(promoBlock.transform, "PromoBtnOverlay", "", Vector2.zero, new Vector2(450, 130), ToggleNoticeModal, Color.clear, Color.clear, 1, false, true);

        // Task Block - Fixed height and positions to avoid clipping
        GameObject taskBlock = CreatePanel(leftPanel.transform, "TaskBlock", new Vector2(0, -95), new Vector2(450, 250), troskiLightAsphalt, true);
        CreateTextElement(taskBlock.transform, "TaskHeader", "GPRTU UNION TASKS", new Vector2(0, 95), new Vector2(400, 30), 22, FontStyle.Bold, TextAnchor.MiddleLeft, textWhite, false);
        CreatePanel(taskBlock.transform, "Div", new Vector2(0, 75), new Vector2(400, 2), textMuted, false);
        taskListContainer = CreatePanel(taskBlock.transform, "TaskList", new Vector2(0, 15), new Vector2(450, 140), Color.clear, false);
        CreateButtonElement(taskBlock.transform, "ClaimBtn", "CLAIM REWARDS", new Vector2(0, -95), new Vector2(400, 45), ClaimRewards, troskiGreen, textWhite, 20, true, true);

        CreateButtonElement(leftPanel.transform, "SettingsBtn", "⚙ GAME SETTINGS", new Vector2(0, -250), new Vector2(450, 55), ToggleSettings, troskiDarkAsphalt, textWhite, 22, true, true);

        GameObject musicBlock = CreatePanel(leftPanel.transform, "MusicBlock", new Vector2(0, -315), new Vector2(450, 55), troskiDarkAsphalt, true);
        musicTitleComp = CreateTextElement(musicBlock.transform, "SongTitle", "♫ NO DISK", new Vector2(-70, 0), new Vector2(280, 55), 16, FontStyle.Italic, TextAnchor.MiddleLeft, themeColor, false);
        CreateButtonElement(musicBlock.transform, "Btn_PrevTrack", "◀", new Vector2(120, 0), new Vector2(60, 40), PrevTrack, troskiLightAsphalt, textWhite, 20, true, true);
        CreateButtonElement(musicBlock.transform, "Btn_NextTrack", "▶", new Vector2(190, 0), new Vector2(60, 40), NextTrack, troskiLightAsphalt, textWhite, 20, true, true);

        GameObject rightPanel = CreatePanel(packedHomeContainer.transform, "RightPanel", Vector2.zero, new Vector2(450, 700), Color.clear, false);
        RectTransform rightRect = rightPanel.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(1, 0.5f);
        rightRect.anchorMax = new Vector2(1, 0.5f);
        rightRect.pivot = new Vector2(1, 0.5f);
        rightRect.anchoredPosition = new Vector2(-40, -50); 

        GameObject startBlock = CreatePanel(rightPanel.transform, "StartBlock", new Vector2(0, 150), new Vector2(450, 350), troskiLightAsphalt, true);
        CreateTextElement(startBlock.transform, "RouteTitle", "CURRENT ROUTE", new Vector2(0, 120), new Vector2(400, 40), 20, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, false);
        
        string routeNameStr = (selectedRouteIndex >= 0 && selectedRouteIndex < availableRoutes.Count) ? availableRoutes[selectedRouteIndex].routeName : "SELECT A STATION";
        routeTextComp = CreateTextElement(startBlock.transform, "RouteName", routeNameStr, new Vector2(0, 45), new Vector2(400, 50), 36, FontStyle.Bold, TextAnchor.MiddleCenter, textWhite, true);
        
        CreateButtonElement(startBlock.transform, "StartBtn", "START SHIFT", new Vector2(0, -80), new Vector2(400, 100), OpenShiftChoiceModal, themeColor, troskiDarkAsphalt, 42, true, true);

        GameObject actionsBlock = CreatePanel(rightPanel.transform, "ActionsBlock", new Vector2(0, -140), new Vector2(450, 200), Color.clear, false);
        CreateButtonElement(actionsBlock.transform, "HireMateBtn", "HIRE / SWAP MATE", new Vector2(0, 60), new Vector2(450, 60), FocusOnMate, troskiDarkAsphalt, textWhite, 20, true, true);
        CreateButtonElement(actionsBlock.transform, "CustomizeBtn", "CUSTOMIZE TROSKI", new Vector2(0, -10), new Vector2(450, 60), FocusOnTroski, troskiDarkAsphalt, textWhite, 20, true, true);
        
        GameObject serviceBtnObj = CreateButtonElement(actionsBlock.transform, "ServiceVanBtn", "", new Vector2(0, -80), new Vector2(450, 60), ServiceTroski, troskiDarkAsphalt, textWhite, 20, true, true);
        serviceBtnTextComp = serviceBtnObj.transform.Find("Text").GetComponent<Text>();
        UpdateServiceButtonUI();

        Text centerHintText = CreateTextElement(packedHomeContainer.transform, "CenterHint", "TAP YOUR TROSKI OR MATE TO UPGRADE", Vector2.zero, new Vector2(800, 50), 24, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1,1,1,0.5f), false);
        RectTransform chRect = centerHintText.GetComponent<RectTransform>();
        chRect.anchorMin = new Vector2(0.5f, 0);
        chRect.anchorMax = new Vector2(0.5f, 0);
        chRect.pivot = new Vector2(0.5f, 0);
        chRect.anchoredPosition = new Vector2(0, 120); 

        GameObject bottomNav = CreatePanel(packedHomeContainer.transform, "BottomNav", Vector2.zero, new Vector2(1200, 100), troskiDarkAsphalt, false);
        RectTransform botRect = bottomNav.GetComponent<RectTransform>();
        botRect.anchorMin = new Vector2(0, 0);
        botRect.anchorMax = new Vector2(1, 0);
        botRect.pivot = new Vector2(0.5f, 0);
        botRect.anchoredPosition = Vector2.zero;
        botRect.sizeDelta = new Vector2(0, 100); 
        
        CreateNavBtn(bottomNav.transform, "FLEET", new Vector2(-400, 0), FocusOnTroski);
        CreateNavBtn(bottomNav.transform, "STATIONS", new Vector2(-150, 0), ToggleStationsModal);
        CreateNavBtn(bottomNav.transform, "STAFF", new Vector2(100, 0), FocusOnMate);
        CreateNavBtn(bottomNav.transform, "SHOP", new Vector2(350, 0), ToggleShop);

        troskiCustomizationContainer = new GameObject("TroskiCustomizationContainer");
        troskiCustomizationContainer.transform.SetParent(canvasObj.transform, false);
        RectTransform tcRect = troskiCustomizationContainer.AddComponent<RectTransform>();
        tcRect.anchorMin = Vector2.zero; tcRect.anchorMax = Vector2.one; tcRect.sizeDelta = Vector2.zero;

        GameObject tBackBtn = CreateButtonElement(troskiCustomizationContainer.transform, "Btn_Back_Troski", "◀ BACK TO TERMINAL", Vector2.zero, new Vector2(300, 60), SetFocusNone, troskiRed, textWhite, 24, true, true);
        RectTransform tBackRect = tBackBtn.GetComponent<RectTransform>();
        tBackRect.anchorMin = new Vector2(0, 1); tBackRect.anchorMax = new Vector2(0, 1);
        tBackRect.pivot = new Vector2(0, 1); tBackRect.anchoredPosition = new Vector2(40, -120);

        GameObject tStatsPanel = CreatePanel(troskiCustomizationContainer.transform, "TroskiStatsPanel", Vector2.zero, new Vector2(400, 550), troskiDarkAsphalt, true);
        RectTransform tStatsRect = tStatsPanel.GetComponent<RectTransform>();
        tStatsRect.anchorMin = new Vector2(0, 0.5f); tStatsRect.anchorMax = new Vector2(0, 0.5f);
        tStatsRect.pivot = new Vector2(0, 0.5f); tStatsRect.anchoredPosition = new Vector2(40, -50);
        
        CreateTextElement(tStatsPanel.transform, "StatsHeader", "TROSKI PERFORMANCE", new Vector2(0, 220), new Vector2(360, 40), 28, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, false);
        CreatePanel(tStatsPanel.transform, "Div1", new Vector2(0, 180), new Vector2(360, 2), textMuted, false);
        
        tStatsContainer = CreatePanel(tStatsPanel.transform, "StatsContainer", Vector2.zero, new Vector2(360, 360), Color.clear, false);
        RefreshTroskiStatsUI();

        GameObject tCenterControls = CreatePanel(troskiCustomizationContainer.transform, "CenterControls", Vector2.zero, new Vector2(600, 150), Color.clear, false);
        RectTransform tCCRect = tCenterControls.GetComponent<RectTransform>();
        tCCRect.anchorMin = new Vector2(0.5f, 0); tCCRect.anchorMax = new Vector2(0.5f, 0);
        tCCRect.pivot = new Vector2(0.5f, 0); tCCRect.anchoredPosition = new Vector2(0, 120);
        
        CreateButtonElement(tCenterControls.transform, "Btn_Prev", "◀", new Vector2(-200, 0), new Vector2(80, 80), SwitchModelPrevious, troskiDarkAsphalt, textWhite, 36, true, true);
        CreateButtonElement(tCenterControls.transform, "Btn_Next", "▶", new Vector2(200, 0), new Vector2(80, 80), SwitchModelNext, troskiDarkAsphalt, textWhite, 36, true, true);
        CreateButtonElement(tCenterControls.transform, "Btn_Apply", "APPLY TROSKI SELECTION", new Vector2(0, 0), new Vector2(340, 80), SaveSelectedModels, troskiGreen, textWhite, 28, true, true);

        mateCustomizationContainer = new GameObject("MateCustomizationContainer");
        mateCustomizationContainer.transform.SetParent(canvasObj.transform, false);
        RectTransform mcRect = mateCustomizationContainer.AddComponent<RectTransform>();
        mcRect.anchorMin = Vector2.zero; mcRect.anchorMax = Vector2.one; mcRect.sizeDelta = Vector2.zero;

        GameObject mBackBtn = CreateButtonElement(mateCustomizationContainer.transform, "Btn_Back_Mate", "◀ BACK TO TERMINAL", Vector2.zero, new Vector2(300, 60), SetFocusNone, troskiRed, textWhite, 24, true, true);
        RectTransform mBackRect = mBackBtn.GetComponent<RectTransform>();
        mBackRect.anchorMin = new Vector2(0, 1); mBackRect.anchorMax = new Vector2(0, 1);
        mBackRect.pivot = new Vector2(0, 1); mBackRect.anchoredPosition = new Vector2(40, -120);

        GameObject mStatsPanel = CreatePanel(mateCustomizationContainer.transform, "MateStatsPanel", Vector2.zero, new Vector2(400, 450), troskiDarkAsphalt, true);
        RectTransform mStatsRect = mStatsPanel.GetComponent<RectTransform>();
        mStatsRect.anchorMin = new Vector2(0, 0.5f); mStatsRect.anchorMax = new Vector2(0, 0.5f);
        mStatsRect.pivot = new Vector2(0, 0.5f); mStatsRect.anchoredPosition = new Vector2(40, -50);
        
        CreateTextElement(mStatsPanel.transform, "StatsHeader", "MATE SKILLS", new Vector2(0, 170), new Vector2(360, 40), 28, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, false);
        CreatePanel(mStatsPanel.transform, "Div1", new Vector2(0, 130), new Vector2(360, 2), textMuted, false);
        
        mStatsContainer = CreatePanel(mStatsPanel.transform, "StatsContainer", Vector2.zero, new Vector2(360, 300), Color.clear, false);

        GameObject mCenterControls = CreatePanel(mateCustomizationContainer.transform, "CenterControls", Vector2.zero, new Vector2(600, 150), Color.clear, false);
        RectTransform mCCRect = mCenterControls.GetComponent<RectTransform>();
        mCCRect.anchorMin = new Vector2(0.5f, 0); mCCRect.anchorMax = new Vector2(0.5f, 0);
        mCCRect.pivot = new Vector2(0.5f, 0); mCCRect.anchoredPosition = new Vector2(0, 120);
        
        CreateButtonElement(mCenterControls.transform, "Btn_Prev", "◀", new Vector2(-200, 0), new Vector2(80, 80), SwitchModelPrevious, troskiDarkAsphalt, textWhite, 36, true, true);
        CreateButtonElement(mCenterControls.transform, "Btn_Next", "▶", new Vector2(200, 0), new Vector2(80, 80), SwitchModelNext, troskiDarkAsphalt, textWhite, 36, true, true);
        
        GameObject hireBtnObj = CreateButtonElement(mCenterControls.transform, "Btn_Apply", "HIRE SELECTED MATE", new Vector2(0, 0), new Vector2(340, 80), SaveSelectedModels, troskiGreen, textWhite, 22, true, true);
        mateHireBtnText = hireBtnObj.transform.Find("Text").GetComponent<Text>();
        
        RefreshMateStatsUI();

        BuildSettingsPanel();

        shopPanel = CreateFullScreenPanel(canvasObj.transform, "ShopPanel", new Color(0.05f, 0.05f, 0.05f, 0.98f), false);
        
        Text shopTitle = CreateTextElement(shopPanel.transform, "ShopTitle", "STORE", Vector2.zero, new Vector2(400, 60), 48, FontStyle.Bold, TextAnchor.MiddleLeft, textWhite, false);
        RectTransform stRect = shopTitle.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0, 1); stRect.anchorMax = new Vector2(0, 1);
        stRect.pivot = new Vector2(0, 1); stRect.anchoredPosition = new Vector2(60, -60);
        
        GameObject hLine = CreatePanel(shopPanel.transform, "HeaderLine", Vector2.zero, new Vector2(0, 4), troskiRed, false);
        RectTransform hlRect = hLine.GetComponent<RectTransform>();
        hlRect.anchorMin = new Vector2(0, 1); hlRect.anchorMax = new Vector2(1, 1);
        hlRect.pivot = new Vector2(0.5f, 1); hlRect.anchoredPosition = new Vector2(0, -130);
        hlRect.sizeDelta = new Vector2(-120, 4); 
        
        float cardWidth = 320;
        float spacing = 40;
        float totalWidth = (4 * cardWidth) + (3 * spacing);
        float startX = -(totalWidth / 2f) + (cardWidth / 2f);

        CreateShopCard(shopPanel.transform, "Card1", new Vector2(startX, 0), "100 COINS", shopItem1_Img != null ? shopItem1_Img : premiumIcon, "100 Coins", "", "GHS 15.00", () => SimulateIAP(100), troskiRed);
        CreateShopCard(shopPanel.transform, "Card2", new Vector2(startX + 1 * (cardWidth + spacing), 0), "500 COINS", shopItem2_Img != null ? shopItem2_Img : premiumIcon, "500 Coins", "+50 Bonus", "GHS 50.00", () => SimulateIAP(500), troskiRed);
        CreateShopCard(shopPanel.transform, "Card3", new Vector2(startX + 2 * (cardWidth + spacing), 0), "10,000 GHS", shopItem3_Img != null ? shopItem3_Img : cashIcon, "10,000 Cash", "Instant Transfer", "100 PREM", () => BuyCashWithPremium(10000, 100), troskiGreen);
        CreateShopCard(shopPanel.transform, "Card4", new Vector2(startX + 3 * (cardWidth + spacing), 0), "FREE REWARD", shopItem4_Img != null ? shopItem4_Img : premiumIcon, "10 Coins", "Watch Ad", "FREE", WatchAdForPremium, themeColor);

        GameObject closeShopBtn = CreateButtonElement(shopPanel.transform, "Btn_CloseShop", "BACK TO TERMINAL", Vector2.zero, new Vector2(250, 50), ToggleShop, Color.clear, textWhite, 24, true, false);
        RectTransform scbRect = closeShopBtn.GetComponent<RectTransform>();
        scbRect.anchorMin = new Vector2(0, 0); scbRect.anchorMax = new Vector2(0, 0);
        scbRect.pivot = new Vector2(0, 0); scbRect.anchoredPosition = new Vector2(150, 60);
        
        shopPanel.SetActive(false);

        stationsModal = CreateFullScreenPanel(canvasObj.transform, "StationsModal", new Color(0, 0, 0, 0.85f), false);
        GameObject statBox = CreatePanel(stationsModal.transform, "StatBox", Vector2.zero, new Vector2(700, 800), troskiDarkAsphalt, true);
        CreateTextElement(statBox.transform, "STitle", "SELECT DESTINATION", new Vector2(0, 340), new Vector2(400, 50), 36, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, false);
        
        float rY = 250;
        for(int i = 0; i < availableRoutes.Count; i++)
        {
            int idx = i; 
            RouteData r = availableRoutes[i];
            bool unlocked = playerLevel >= r.requiredLevel;
            
            float mateMultiplier = 1.0f + (currentMateIndex * 0.05f); 
            int estEarnings = Mathf.RoundToInt(r.baseEarnings * mateMultiplier);

            Color btnCol = unlocked ? troskiLightAsphalt : new Color(0.1f, 0.1f, 0.1f, 0.9f);
            Color txtCol = unlocked ? textWhite : textMuted;
            string prefix = unlocked ? "" : $"<color=red>[LVL {r.requiredLevel}]</color> ";
            
            CreateButtonElement(statBox.transform, "RBtn_"+i, prefix + r.routeName + $"\n<size=16>Est: {estEarnings:N0} GHS</size>", new Vector2(0, rY), new Vector2(600, 80), () => SelectRoute(idx), btnCol, txtCol, 24, unlocked, true);
            rY -= 90;
        }

        CreateButtonElement(statBox.transform, "Btn_CloseStat", "CANCEL", new Vector2(0, -340), new Vector2(300, 60), ToggleStationsModal, troskiRed, textWhite, 24, true, true);
        stationsModal.SetActive(false);

        shiftModeModal = CreateFullScreenPanel(canvasObj.transform, "ShiftModeModal", new Color(0, 0, 0, 0.85f), false);
        GameObject smBox = CreatePanel(shiftModeModal.transform, "SMBox", Vector2.zero, new Vector2(600, 400), troskiDarkAsphalt, true);
        CreateTextElement(smBox.transform, "SMTitle", "SELECT SHIFT TYPE", new Vector2(0, 140), new Vector2(400, 50), 36, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, false);
        
        CreateButtonElement(smBox.transform, "Btn_LoadStat", "LOAD AT STATION\n<size=16>Guaranteed Full Seats, Longer Wait</size>", new Vector2(0, 40), new Vector2(450, 80), StartShiftLoadMode, troskiLightAsphalt, textWhite, 20, true, true);
        CreateButtonElement(smBox.transform, "Btn_Roam", "MOVE & PICK\n<size=16>Instant Start, Find Passengers on Road</size>", new Vector2(0, -60), new Vector2(450, 80), StartShiftRoamMode, troskiLightAsphalt, textWhite, 20, true, true);
        
        CreateButtonElement(smBox.transform, "Btn_CloseSM", "CANCEL", new Vector2(0, -160), new Vector2(300, 60), CloseShiftChoiceModal, troskiRed, textWhite, 24, true, true);
        shiftModeModal.SetActive(false);

        profileEditPanel = CreateFullScreenPanel(canvasObj.transform, "ProfileEditPanel", new Color(0, 0, 0, 0.95f), false);
        GameObject ftBox = CreatePanel(profileEditPanel.transform, "FTBox", Vector2.zero, new Vector2(650, 500), troskiDarkAsphalt, true);
        
        profileEditTitle = CreateTextElement(ftBox.transform, "FTTitle", "WELCOME TO TROSKI SIMULATOR", new Vector2(0, 200), new Vector2(550, 50), 32, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, false);
        CreateTextElement(ftBox.transform, "FTDesc", "Enter your Transport Company Name:", new Vector2(0, 140), new Vector2(500, 30), 20, FontStyle.Normal, TextAnchor.MiddleCenter, textWhite, false);
        
        GameObject inputObj = CreatePanel(ftBox.transform, "InputBg", new Vector2(0, 70), new Vector2(450, 60), troskiLightAsphalt, true);
        companyNameInput = inputObj.AddComponent<InputField>();
        Text inputText = CreateTextElement(inputObj.transform, "Text", "", Vector2.zero, new Vector2(430, 60), 24, FontStyle.Bold, TextAnchor.MiddleCenter, textWhite, false);
        companyNameInput.textComponent = inputText;

        CreateTextElement(ftBox.transform, "PicDesc", "Select a Custom Logo / Profile Picture:", new Vector2(0, -10), new Vector2(500, 30), 20, FontStyle.Normal, TextAnchor.MiddleCenter, textWhite, false);
        
        GameObject picPreviewObj = CreatePanel(ftBox.transform, "PicPreviewBg", new Vector2(-150, -80), new Vector2(80, 80), Color.white, true);
        Mask picMaskComp = picPreviewObj.AddComponent<Mask>();
        picMaskComp.showMaskGraphic = false;

        GameObject imgObj = new GameObject("PicImg");
        imgObj.transform.SetParent(picPreviewObj.transform, false);
        RectTransform imgRect = imgObj.AddComponent<RectTransform>();
        imgRect.anchoredPosition = Vector2.zero; imgRect.sizeDelta = new Vector2(80, 80);
        profilePreviewImg = imgObj.AddComponent<Image>();
        UpdateProfilePreviewImage(); 

        CreateButtonElement(ftBox.transform, "Btn_SelectPic", "CHOOSE FROM DEVICE", new Vector2(80, -80), new Vector2(250, 60), SelectProfilePicture, troskiLightAsphalt, themeColor, 18, true, true);

        CreateButtonElement(ftBox.transform, "Btn_SaveName", "SAVE PROFILE", new Vector2(0, -180), new Vector2(400, 70), SaveCompanyProfile, themeColor, troskiDarkAsphalt, 24, true, true);
        
        CreateButtonElement(ftBox.transform, "Btn_CloseEdit", "CANCEL", new Vector2(260, 200), new Vector2(100, 40), CloseProfileEditor, troskiRed, textWhite, 16, true, true);

        profileEditPanel.SetActive(false);
        
        // --- NOTIFICATION MODAL ---
        noticeModal = CreateFullScreenPanel(canvasObj.transform, "NoticeModal", new Color(0, 0, 0, 0.9f), false);
        GameObject nmBox = CreatePanel(noticeModal.transform, "NMBox", Vector2.zero, new Vector2(800, 750), troskiDarkAsphalt, true);
        
        modalTitleText = CreateTextElement(nmBox.transform, "NMTitle", "NOTICE", new Vector2(0, 320), new Vector2(700, 50), 32, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, false);
        
        GameObject mmMaskObj = CreatePanel(nmBox.transform, "ModalMask", new Vector2(0, 100), new Vector2(700, 350), Color.white, true);
        Mask mmMask = mmMaskObj.AddComponent<Mask>();
        mmMask.showMaskGraphic = false;

        GameObject modalSlideContainer = new GameObject("ModalSlideContainer");
        modalSlideContainer.transform.SetParent(mmMaskObj.transform, false);
        modalPromoSlider = modalSlideContainer.AddComponent<RectTransform>();
        modalPromoSlider.anchorMin = new Vector2(0, 0.5f);
        modalPromoSlider.anchorMax = new Vector2(0, 0.5f);
        modalPromoSlider.pivot = new Vector2(0, 0.5f);
        modalPromoSlider.anchoredPosition = new Vector2(0, 0); 
        modalPromoSlider.sizeDelta = new Vector2(700, 350);

        modalDescText = CreateTextElement(nmBox.transform, "NMDesc", "Loading details...", new Vector2(0, -130), new Vector2(700, 100), 22, FontStyle.Normal, TextAnchor.UpperCenter, textWhite, false);
        
        GameObject actBtnObj = CreateButtonElement(nmBox.transform, "Btn_NMAction", "VIEW", new Vector2(-160, -280), new Vector2(300, 70), () => { if(notificationManager != null) notificationManager.OpenActionLink(); }, themeColor, troskiDarkAsphalt, 24, true, true);
        modalActionBtn = actBtnObj.GetComponent<Button>();
        modalActionText = actBtnObj.transform.Find("Text").GetComponent<Text>();
        
        CreateButtonElement(nmBox.transform, "Btn_CloseNM", "CLOSE", new Vector2(160, -280), new Vector2(300, 70), ToggleNoticeModal, troskiLightAsphalt, textWhite, 24, true, true);
        
        noticeModal.SetActive(false);

        loadingPanel = CreateFullScreenPanel(canvasObj.transform, "LoadingPanel", troskiDarkAsphalt, false);
        
        CanvasGroup loadCg = loadingPanel.AddComponent<CanvasGroup>();
        loadCg.alpha = 0f;

        GameObject loadImgObj = CreateFullScreenImage(loadingPanel.transform, "LoadingImage", loadingScreenImage, troskiDarkAsphalt);
        
        loadingText = CreateTextElement(loadingPanel.transform, "LoadingText", "DISPATCHING...", new Vector2(0, -260), new Vector2(800, 50), 36, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, true);
        
        GameObject pBarBg = CreatePanel(loadingPanel.transform, "ProgressBg", new Vector2(0, -320), new Vector2(800, 30), troskiDarkAsphalt, true);
        GameObject pBarFill = CreatePanel(pBarBg.transform, "ProgressFill", new Vector2(-400, 0), new Vector2(0, 30), themeColor, true);
        pBarFill.GetComponent<RectTransform>().pivot = new Vector2(0, 0.5f); 
        loadingFillRect = pBarFill.GetComponent<RectTransform>();
        
        loadingPercentText = CreateTextElement(loadingPanel.transform, "PercentText", "0%", new Vector2(0, -370), new Vector2(200, 50), 28, FontStyle.Bold, TextAnchor.MiddleCenter, textWhite, true);

        loadingPanel.SetActive(false);
    }

    private void CreateShopCard(Transform parent, string name, Vector2 pos, string topTitle, Texture2D bannerImage, string mainAmt, string subTxt, string priceText, UnityEngine.Events.UnityAction action, Color accentCol)
    {
        GameObject cardBtnObj = CreateButtonElement(parent, name, "", pos, new Vector2(320, 500), action, new Color(0.1f, 0.1f, 0.1f, 1f), Color.clear, 1, true, false);
        
        CreatePanel(cardBtnObj.transform, "AccentTop", new Vector2(0, 235), new Vector2(320, 30), accentCol, false);
        CreatePanel(cardBtnObj.transform, "AccentBot", new Vector2(0, -235), new Vector2(320, 30), accentCol, false);

        CreatePanel(cardBtnObj.transform, "InnerBg", new Vector2(0, 30), new Vector2(290, 350), new Color(0.2f, 0.2f, 0.2f, 1f), false);
        
        CreateTextElement(cardBtnObj.transform, "TopTitle", topTitle, new Vector2(0, 200), new Vector2(300, 40), 24, FontStyle.Bold, TextAnchor.MiddleCenter, textWhite, true);
        
        CreateImageElement(cardBtnObj.transform, "Banner", bannerImage, new Vector2(0, 60), new Vector2(290, 220), Color.white);
        
        CreateTextElement(cardBtnObj.transform, "MainAmt", mainAmt, new Vector2(0, -80), new Vector2(300, 30), 22, FontStyle.Normal, TextAnchor.MiddleCenter, textWhite, false);
        CreateTextElement(cardBtnObj.transform, "SubTxt", subTxt, new Vector2(0, -110), new Vector2(300, 30), 18, FontStyle.Bold, TextAnchor.MiddleCenter, accentCol, false);

        CreateTextElement(cardBtnObj.transform, "PriceTxt", priceText, new Vector2(0, -180), new Vector2(300, 40), 32, FontStyle.Bold, TextAnchor.MiddleCenter, textWhite, false);
    }

    private void BuildSettingsPanel()
    {
        bool wasActive = settingsPanel != null && settingsPanel.activeSelf;
        if (settingsPanel != null) Destroy(settingsPanel);

        settingsPanel = CreateFullScreenPanel(canvasObj.transform, "SettingsPanel", new Color(0, 0, 0, 0.85f), false);
        GameObject sBox = CreatePanel(settingsPanel.transform, "SettingsBox", Vector2.zero, new Vector2(650, 950), troskiDarkAsphalt, true);
        
        CreateTextElement(sBox.transform, "STitle", "GAME SETTINGS", new Vector2(0, 380), new Vector2(400, 50), 36, FontStyle.Bold, TextAnchor.MiddleCenter, themeColor, false);
        
        string audioText = isAudioMuted ? "🔇 AUDIO: MUTED" : "🔊 AUDIO: ON";
        CreateButtonElement(sBox.transform, "Btn_AudioToggle", audioText, new Vector2(0, 260), new Vector2(450, 70), ToggleAudio, troskiLightAsphalt, textWhite, 24, true, true);

        string controlsText = controlScheme == 0 ? "🎮 CONTROLS: BUTTONS" : "📱 CONTROLS: TILT";
        CreateButtonElement(sBox.transform, "Btn_Controls", controlsText, new Vector2(0, 160), new Vector2(450, 70), ToggleControls, troskiLightAsphalt, textWhite, 24, true, true);

        CreateTextElement(sBox.transform, "GraphTitle", "GRAPHICS QUALITY", new Vector2(0, 40), new Vector2(450, 30), 20, FontStyle.Bold, TextAnchor.MiddleCenter, textMuted, false);
        string[] qNames = QualitySettings.names;
        if(qNames.Length > 0)
        {
            float qSpacing = 500f / qNames.Length;
            float qStartX = -(500f / 2f) + (qSpacing / 2f);
            for (int i = 0; i < qNames.Length; i++)
            {
                int idx = i;
                Color btnBg = (i == currentGraphicsLevel) ? themeColor : troskiLightAsphalt;
                Color txtCol = (i == currentGraphicsLevel) ? troskiDarkAsphalt : textWhite;
                string shortName = qNames[i].Length > 7 ? qNames[i].Substring(0, 6) : qNames[i];
                CreateButtonElement(sBox.transform, "Btn_Q_" + i, shortName.ToUpper(), new Vector2(qStartX + (i * qSpacing), 0), new Vector2(qSpacing - 10, 50), () => SetGraphicsQuality(idx, true), btnBg, txtCol, 16, true, true);
            }
        }

        CreateTextElement(sBox.transform, "FPSTitle", "FRAME RATE (FPS)", new Vector2(0, -90), new Vector2(450, 30), 20, FontStyle.Bold, TextAnchor.MiddleCenter, textMuted, false);
        float fpsSpacing = 160f;
        float fpsStartX = -160f;
        for (int i = 0; i < availableFPS.Length; i++)
        {
            int fpsVal = availableFPS[i];
            Color btnBg = (currentFPS == fpsVal) ? themeColor : troskiLightAsphalt;
            Color txtCol = (currentFPS == fpsVal) ? troskiDarkAsphalt : textWhite;
            string fpsLabel = fpsVal == -1 ? "MAX FPS" : fpsVal.ToString() + " FPS";
            CreateButtonElement(sBox.transform, "Btn_FPS_" + i, fpsLabel, new Vector2(fpsStartX + (i * fpsSpacing), -140), new Vector2(140, 50), () => SetFPSTarget(fpsVal), btnBg, txtCol, 18, true, true);
        }

        CreateTextElement(sBox.transform, "ThemeTitle", "SELECT GAME THEME COLOR", new Vector2(0, -220), new Vector2(450, 30), 20, FontStyle.Bold, TextAnchor.MiddleCenter, textMuted, false);
        float tX = -120;
        for(int i = 0; i < availableThemes.Length; i++)
        {
            int idx = i;
            GameObject tBtn = CreateButtonElement(sBox.transform, "ThemeBtn_"+i, "", new Vector2(tX, -270), new Vector2(50, 50), () => SetTheme(idx), availableThemes[i], Color.clear, 10, true, true);
            
            if (i == selectedThemeIndex)
            {
                CreatePanel(tBtn.transform, "Highlight", Vector2.zero, new Vector2(15, 15), textWhite, true);
            }
            tX += 60;
        }

        CreateTextElement(sBox.transform, "XPText", $"CURRENT XP: {currentXP} / {xpToNextLevel}", new Vector2(0, -340), new Vector2(450, 40), 20, FontStyle.Bold, TextAnchor.MiddleCenter, textMuted, false);

        CreateButtonElement(sBox.transform, "Btn_CloseSettings", "CLOSE SETTINGS", new Vector2(0, -420), new Vector2(300, 60), ToggleSettings, troskiRed, textWhite, 24, true, true);
        
        settingsPanel.SetActive(wasActive);
    }

    private void RefreshTroskiStatsUI()
    {
        if (tStatsContainer == null) return;
        foreach (Transform child in tStatsContainer.transform) Destroy(child.gameObject);

        int bLvl = currentTroskiIndex + 1;
        float maxLvl = 15f; 

        CreateStatBarRow(tStatsContainer.transform, "Engine Power", "LVL " + (bLvl + 3), Mathf.Clamp01((bLvl + 3) / maxLvl), 130);
        CreateStatBarRow(tStatsContainer.transform, "Seat Comfort", "LVL " + (bLvl + 1), Mathf.Clamp01((bLvl + 1) / maxLvl), 50);
        CreateStatBarRow(tStatsContainer.transform, "Fuel Efficiency", "LVL " + (bLvl + 2), Mathf.Clamp01((bLvl + 2) / maxLvl), -30);
        CreateStatBarRow(tStatsContainer.transform, "Tire Grip", "LVL " + bLvl, Mathf.Clamp01(bLvl / maxLvl), -110);
        CreateStatBarRow(tStatsContainer.transform, "Brake Quality", "LVL " + (bLvl + 1), Mathf.Clamp01((bLvl + 1) / maxLvl), -190);
    }

    private void RefreshMateStatsUI()
    {
        if (mStatsContainer == null) return;
        foreach (Transform child in mStatsContainer.transform) Destroy(child.gameObject);

        int bLvl = currentMateIndex + 1;
        float maxLvl = 15f;

        CreateStatBarRow(mStatsContainer.transform, "Cash Math", "LVL " + (bLvl + 2), Mathf.Clamp01((bLvl + 2) / maxLvl), 80);
        CreateStatBarRow(mStatsContainer.transform, "Voice Volume", "LVL " + (bLvl + 1), Mathf.Clamp01((bLvl + 1) / maxLvl), 0);
        CreateStatBarRow(mStatsContainer.transform, "Agility", "LVL " + (bLvl + 3), Mathf.Clamp01((bLvl + 3) / maxLvl), -80);
        CreateStatBarRow(mStatsContainer.transform, "Charisma", "LVL " + bLvl, Mathf.Clamp01(bLvl / maxLvl), -160);

        if (mateHireBtnText != null)
        {
            int wage = GetMateWage(currentMateIndex);
            mateHireBtnText.text = $"HIRE SELECTED MATE\n<size=16>GHS {wage} / SHIFT</size>";
        }
    }

    private void CreateTaskRow(Transform parent, string taskName, string progress, bool completed, Vector2 position)
    {
        GameObject row = CreatePanel(parent, "Task", position, new Vector2(400, 45), troskiDarkAsphalt, true);
        Color txtCol = completed ? themeColor : textWhite; 
        CreateTextElement(row.transform, "Name", taskName, new Vector2(-30, 0), new Vector2(280, 45), 16, FontStyle.Normal, TextAnchor.MiddleLeft, txtCol, false);
        CreateTextElement(row.transform, "Prog", progress, new Vector2(150, 0), new Vector2(80, 45), 18, FontStyle.Bold, TextAnchor.MiddleRight, txtCol, false);
    }

    private void CreateNavBtn(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClickAction)
    {
        CreateButtonElement(parent, "Nav_" + label, label, position, new Vector2(220, 80), onClickAction, Color.clear, textWhite, 28, true, false);
    }

    private void CreateStatBarRow(Transform parent, string label, string value, float fillRatio, float yPos)
    {
        GameObject row = CreatePanel(parent, label + "_Row", new Vector2(0, yPos), new Vector2(360, 60), Color.clear, false);
        CreateTextElement(row.transform, "Label", label, new Vector2(-70, 15), new Vector2(180, 30), 18, FontStyle.Bold, TextAnchor.MiddleLeft, textWhite, false);
        CreateTextElement(row.transform, "Value", value, new Vector2(110, 15), new Vector2(100, 30), 18, FontStyle.Bold, TextAnchor.MiddleRight, themeColor, false);
        GameObject bgBar = CreatePanel(row.transform, "BgBar", new Vector2(0, -15), new Vector2(360, 12), troskiLightAsphalt, true);
        CreatePanel(bgBar.transform, "FillBar", new Vector2((fillRatio * 360 - 360)/2, 0), new Vector2(360 * fillRatio, 12), themeColor, true);
    }

    private GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color, bool rounded = true)
    {
        GameObject panelObj = new GameObject(name);
        panelObj.transform.SetParent(parent, false);
        RectTransform rect = panelObj.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = panelObj.AddComponent<Image>();
        image.color = color;
        
        if (color == Color.clear) image.raycastTarget = false; 
        
        if (rounded && roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }
        
        return panelObj;
    }

    private GameObject CreateFullScreenPanel(Transform parent, string name, Color color, bool rounded = false)
    {
        GameObject panelObj = new GameObject(name);
        panelObj.transform.SetParent(parent, false);
        RectTransform rect = panelObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        Image image = panelObj.AddComponent<Image>();
        image.color = color;
        
        if (color == Color.clear) image.raycastTarget = false; 
        
        if (rounded && roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }
        
        return panelObj;
    }

    private GameObject CreateImageElement(Transform parent, string name, Texture2D texture, Vector2 position, Vector2 size, Color color)
    {
        GameObject imgObj = new GameObject(name);
        imgObj.transform.SetParent(parent, false);
        RectTransform rect = imgObj.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = imgObj.AddComponent<Image>();
        
        if (texture != null) image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        else image.color = color; 
        
        image.preserveAspect = true;
        image.raycastTarget = false; 
        return imgObj;
    }

    private GameObject CreateFullScreenImage(Transform parent, string name, Texture2D texture, Color color)
    {
        GameObject imgObj = new GameObject(name);
        imgObj.transform.SetParent(parent, false);
        RectTransform rect = imgObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        Image image = imgObj.AddComponent<Image>();
        
        if (texture != null) image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        else image.color = color; 
        
        image.preserveAspect = false; 
        image.raycastTarget = false; 
        return imgObj;
    }

    private Text CreateTextElement(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize, FontStyle style, TextAnchor alignment, Color color, bool addShadow)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text textComp = textObj.AddComponent<Text>();
        textComp.text = content; textComp.font = defaultFont;
        textComp.fontSize = fontSize; textComp.fontStyle = style;
        textComp.alignment = alignment; textComp.color = color;
        textComp.raycastTarget = false; 
        if (addShadow) { Shadow shadow = textObj.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, 0.8f); shadow.effectDistance = new Vector2(2, -2); }
        return textComp;
    }

    private GameObject CreateButtonElement(Transform parent, string name, string labelText, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClickAction, Color bgColor, Color textColor, int fontSize, bool addHoverAnimation, bool rounded = true)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchoredPosition = position; rect.sizeDelta = size;
        
        Image image = buttonObj.AddComponent<Image>();
        image.color = bgColor;
        if (rounded && roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }
        
        Button buttonComp = buttonObj.AddComponent<Button>(); 
        buttonComp.targetGraphic = image;
        if (onClickAction != null) { 
            buttonComp.onClick.AddListener(onClickAction);
            buttonComp.onClick.AddListener(() => PlaySFX(btnClickSFX));
        }
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchoredPosition = Vector2.zero; textRect.sizeDelta = size;
        Text textComp = textObj.AddComponent<Text>();
        textComp.text = labelText; textComp.font = defaultFont;
        textComp.fontSize = fontSize; textComp.fontStyle = FontStyle.Bold;
        textComp.alignment = TextAnchor.MiddleCenter; textComp.color = textColor;
        textComp.raycastTarget = false;
        
        if (addHoverAnimation) buttonObj.AddComponent<UIHoverAnimator>();
        return buttonObj;
    }
}

public class UIHoverAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 originalScale;
    private Vector3 targetScale;
    private float smoothSpeed = 15f;

    void Start() { originalScale = transform.localScale; targetScale = originalScale; }
    void Update() { transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * smoothSpeed); }
    public void OnPointerEnter(PointerEventData eventData) { targetScale = originalScale * 1.05f; }
    public void OnPointerExit(PointerEventData eventData) { targetScale = originalScale; }
    public void OnPointerDown(PointerEventData eventData) { targetScale = originalScale * 0.95f; }
    public void OnPointerUp(PointerEventData eventData) { targetScale = originalScale * 1.05f; }
}

// --- TROSKI NOTIFICATION MANAGER ---
public class TroskiNotificationManager : MonoBehaviour
{
    private TroskiMenuManager menuManager;
    private string fetchUrl;
    
    private RectTransform miniSlider;
    private RectTransform modalSlider;
    private GameObject noticeModal;
    private Text fallbackText;
    
    private NotificationData currentData;
    public bool hasActiveNotice = false;
    
    private int currentIndex = 0;
    private Coroutine carouselCoroutine;
    
    private List<RenderTexture> videoTextures = new List<RenderTexture>();
    
    private float miniWidth = 450f;
    private float modalWidth = 700f;

    private string CacheDirectory
    {
        get
        {
            string path = Path.Combine(Application.persistentDataPath, "PromoCache");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }
    }

    private string GetCacheFileName(string url)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(url);
        return System.Convert.ToBase64String(bytes).Replace("/", "_").Replace("+", "-").Replace("=", "") + ".png";
    }

    public void Initialize(TroskiMenuManager manager, string url, RectTransform miniContainer, RectTransform modalContainer, GameObject modalObj, Text fbText)
    {
        menuManager = manager;
        fetchUrl = url;
        UpdateUIRefs(miniContainer, modalContainer, modalObj, fbText);
        
        StartCoroutine(FetchNoticeData());
    }

    public void UpdateUIRefs(RectTransform miniContainer, RectTransform modalContainer, GameObject modalObj, Text fbText)
    {
        miniSlider = miniContainer;
        modalSlider = modalContainer;
        noticeModal = modalObj;
        fallbackText = fbText;
        
        if (hasActiveNotice && currentData != null)
        {
            UpdateModalText(currentIndex);
        }
    }

    private IEnumerator FetchNoticeData()
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(fetchUrl))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogWarning("Failed to fetch notifications: " + webRequest.error);
                if (fallbackText != null) fallbackText.text = "";
            }
            else
            {
                string jsonResponse = webRequest.downloadHandler.text;
                try
                {
                    currentData = JsonUtility.FromJson<NotificationData>(jsonResponse);
                    if (currentData != null && currentData.isActive && currentData.slides != null && currentData.slides.Length > 0)
                    {
                        hasActiveNotice = true;
                        StartCoroutine(ProcessMediaUrls());
                    }
                    else
                    {
                        if (fallbackText != null) fallbackText.text = "";
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Error parsing notification JSON: " + e.Message);
                    if (fallbackText != null) fallbackText.text = "";
                }
            }
        }
    }

    private IEnumerator ProcessMediaUrls()
    {
        int slideCount = currentData.slides.Length;
        HashSet<string> activeCacheFiles = new HashSet<string>();
        
        for (int i = 0; i < slideCount; i++)
        {
            PromoSlide slide = currentData.slides[i];
            Texture2D downloadedTex = null;
            bool isVideo = slide.mediaUrl.ToLower().EndsWith(".mp4");

            if (!isVideo)
            {
                string fileName = GetCacheFileName(slide.mediaUrl);
                string cachePath = Path.Combine(CacheDirectory, fileName);
                activeCacheFiles.Add(fileName);

                if (File.Exists(cachePath))
                {
                    byte[] fileData = File.ReadAllBytes(cachePath);
                    downloadedTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    downloadedTex.LoadImage(fileData);
                }
                else
                {
                    using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(slide.mediaUrl))
                    {
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success) 
                        {
                            downloadedTex = DownloadHandlerTexture.GetContent(req);
                            if (downloadedTex != null)
                            {
                                byte[] pngData = downloadedTex.EncodeToPNG();
                                if (pngData != null) File.WriteAllBytes(cachePath, pngData);
                            }
                        }
                    }
                }
            }

            if (miniSlider != null)
            {
                GameObject miniObj = new GameObject("Slide_" + i);
                miniObj.transform.SetParent(miniSlider, false);
                RectTransform mRect = miniObj.AddComponent<RectTransform>();
                mRect.anchorMin = new Vector2(0, 0.5f); mRect.anchorMax = new Vector2(0, 0.5f);
                mRect.pivot = new Vector2(0, 0.5f);
                mRect.sizeDelta = new Vector2(miniWidth, 130);
                mRect.anchoredPosition = new Vector2(i * miniWidth, 0);
                RawImage mImg = miniObj.AddComponent<RawImage>();
                mImg.color = Color.white;
                
                if (downloadedTex != null) mImg.texture = downloadedTex;
                else if (isVideo)
                {
                    RenderTexture miniRT = new RenderTexture((int)miniWidth, 130, 16, RenderTextureFormat.ARGB32);
                    mImg.texture = miniRT;
                    VideoPlayer vpMini = miniObj.AddComponent<VideoPlayer>();
                    vpMini.url = slide.mediaUrl;
                    vpMini.renderMode = VideoRenderMode.RenderTexture;
                    vpMini.targetTexture = miniRT;
                    vpMini.isLooping = true;
                    vpMini.SetDirectAudioMute(0, true);
                    vpMini.Play();
                    videoTextures.Add(miniRT);
                }
            }

            if (modalSlider != null)
            {
                GameObject modObj = new GameObject("Slide_" + i);
                modObj.transform.SetParent(modalSlider, false);
                RectTransform modRect = modObj.AddComponent<RectTransform>();
                modRect.anchorMin = new Vector2(0, 0.5f); modRect.anchorMax = new Vector2(0, 0.5f);
                modRect.pivot = new Vector2(0, 0.5f);
                modRect.sizeDelta = new Vector2(modalWidth, 350);
                modRect.anchoredPosition = new Vector2(i * modalWidth, 0);
                RawImage modImg = modObj.AddComponent<RawImage>();
                modImg.color = Color.white;
                
                if (downloadedTex != null) modImg.texture = downloadedTex;
                else if (isVideo)
                {
                    RenderTexture modRT = new RenderTexture((int)modalWidth, 350, 16, RenderTextureFormat.ARGB32);
                    modImg.texture = modRT;
                    VideoPlayer vpMod = modObj.AddComponent<VideoPlayer>();
                    vpMod.url = slide.mediaUrl;
                    vpMod.renderMode = VideoRenderMode.RenderTexture;
                    vpMod.targetTexture = modRT;
                    vpMod.isLooping = true;
                    vpMod.SetDirectAudioMute(0, false);
                    vpMod.Play();
                    videoTextures.Add(modRT);
                }
            }
        }
        
        if (fallbackText != null) fallbackText.text = "";

        UpdateModalText(0);

        if (slideCount > 1)
        {
            if (carouselCoroutine != null) StopCoroutine(carouselCoroutine);
            carouselCoroutine = StartCoroutine(SlideCarouselRoutine(slideCount));
        }

        CleanupOldCache(activeCacheFiles);
    }

    private void CleanupOldCache(HashSet<string> activeFiles)
    {
        if (!Directory.Exists(CacheDirectory)) return;

        string[] existingFiles = Directory.GetFiles(CacheDirectory);
        foreach (string file in existingFiles)
        {
            string fileName = Path.GetFileName(file);
            if (!activeFiles.Contains(fileName))
            {
                try { File.Delete(file); } catch { /* Ignore locked files */ }
            }
        }
    }

    private IEnumerator SlideCarouselRoutine(int slideCount)
    {
        while (true)
        {
            yield return new WaitForSeconds(4f);
            int nextIndex = (currentIndex + 1) % slideCount;
            
            float startXMini = miniSlider != null ? miniSlider.anchoredPosition.x : 0;
            float targetXMini = -nextIndex * miniWidth;
            
            float startXMod = modalSlider != null ? modalSlider.anchoredPosition.x : 0;
            float targetXMod = -nextIndex * modalWidth;
            
            float t = 0;
            float duration = 0.5f;
            
            UpdateModalText(nextIndex); 

            while (t < duration)
            {
                t += Time.deltaTime;
                float normalizedTime = t / duration;
                float easeTime = normalizedTime * normalizedTime * (3f - 2f * normalizedTime); 
                
                if (miniSlider != null) miniSlider.anchoredPosition = new Vector2(Mathf.Lerp(startXMini, targetXMini, easeTime), 0);
                if (modalSlider != null) modalSlider.anchoredPosition = new Vector2(Mathf.Lerp(startXMod, targetXMod, easeTime), 0);
                
                yield return null;
            }
            
            if (miniSlider != null) miniSlider.anchoredPosition = new Vector2(targetXMini, 0);
            if (modalSlider != null) modalSlider.anchoredPosition = new Vector2(targetXMod, 0);
            
            currentIndex = nextIndex;
        }
    }

    private void UpdateModalText(int index)
    {
        if (currentData == null || currentData.slides == null || index >= currentData.slides.Length) return;
        
        PromoSlide slide = currentData.slides[index];
        
        if (noticeModal != null)
        {
            Text[] texts = noticeModal.GetComponentsInChildren<Text>(true);
            foreach (Text t in texts)
            {
                if (t.gameObject.name == "NMTitle") t.text = string.IsNullOrEmpty(slide.title) ? "" : slide.title.ToUpper();
                if (t.gameObject.name == "NMDesc") t.text = slide.description;
            }
            
            Button[] buttons = noticeModal.GetComponentsInChildren<Button>(true);
            foreach (Button b in buttons)
            {
                if (b.gameObject.name == "Btn_NMAction")
                {
                    Text btnText = b.GetComponentInChildren<Text>();
                    if (btnText != null) btnText.text = string.IsNullOrEmpty(slide.actionBtnText) ? "OPEN" : slide.actionBtnText.ToUpper();
                    b.gameObject.SetActive(!string.IsNullOrEmpty(slide.actionUrl));
                }
            }
        }
    }

    public void CheckStartupPop()
    {
        if (hasActiveNotice && currentData != null && currentData.popOnStartup && currentData.slides != null && currentData.slides.Length > 0)
        {
            string firstTitle = currentData.slides[0].title;
            if (PlayerPrefs.GetString("LastSeenNoticeTitle", "") != firstTitle)
            {
                if (menuManager != null) menuManager.ToggleNoticeModal();
                PlayerPrefs.SetString("LastSeenNoticeTitle", firstTitle);
                PlayerPrefs.Save();
            }
        }
    }

    public void OpenActionLink()
    {
        if (currentData != null && currentData.slides != null && currentIndex < currentData.slides.Length)
        {
            string url = currentData.slides[currentIndex].actionUrl;
            if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
        }
    }

    void OnDestroy()
    {
        foreach (var rt in videoTextures)
        {
            if (rt != null) rt.Release();
        }
    }
}