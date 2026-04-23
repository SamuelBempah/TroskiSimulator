using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

// --- TROSKI SIMULATOR: MAIN GAME MANAGER ---
public class TroskiGameManager : MonoBehaviour
{
    public static TroskiGameManager Instance { get; private set; }

    public enum GearState { Drive, Reverse }

    [Header("Spawning Settings")]
    public GameObject[] troskiPrefabs;
    public GameObject[] matePrefabs;
    public Transform spawnPoint;
    
    [Header("Procedural Generation Prefabs")]
    [Tooltip("Drag a simple visual prefab here. No colliders needed!")]
    public GameObject busStopPrefab;
    public GameObject fuelStationPrefab;
    public float spawnDistanceAhead = 150f;

    [Header("Auto-Generate Waypoints")]
    public bool autoGenerateWaypoints = true;
    public string roadTag = "Road";
    public float cityBoundsSize = 3000f; 
    public int generatedBusStops = 150;
    public int generatedFuelStations = 30;

    private List<Transform> possibleStopLocations = new List<Transform>();
    private List<Transform> possibleFuelLocations = new List<Transform>();

    [Header("Camera Configuration")]
    public Camera mainCamera;
    private Camera minimapCamera;
    private RenderTexture minimapTexture;

    [Header("Mobile Control Icons")]
    public Sprite steerLeftIcon;
    public Sprite steerRightIcon;
    public Sprite gasIcon;
    public Sprite brakeIcon;
    public Sprite gearDriveIcon;
    public Sprite gearReverseIcon;

    [Header("UI Image Placeholders")]
    public Sprite passengerIcon;
    public Sprite fuelIcon;
    public Sprite cashIcon;
    public Sprite speedoIcon;
    public Sprite dashBgIcon;

    [Header("Loading Screen Settings")]
    public Texture2D loadingScreenImage;

    [Header("Gameplay Settings")]
    public int farePerPassenger = 5;
    public float maxFuel = 100f;
    public float fuelConsumptionRate = 0.5f; 
    public int fuelCostPerTick = 10; 
    
    [Header("Audio & SFX")]
    public AudioClip btnClickSFX;
    public AudioClip notificationSFX;
    public AudioClip modalOpenSFX;
    public AudioClip shiftSuccessSFX;
    public AudioClip shiftFailSFX;
    public AudioClip crashSFX;
    public AudioClip coinTallyLoopSFX; 
    public AudioClip xpLevelUpSFX;     
    public AudioClip buyFuelSFX;       

    [Header("Continuous Audio Environment")]
    public AudioClip engineIdleSFX;    
    public AudioClip engineMovingSFX;  
    public AudioClip trafficAmbientSFX;
    public AudioClip[] bgmTracks;
    public float bgmVolume = 0.3f;

    [Header("Engine Audio Loop Smoothing")]
    [Range(0f, 0.4f)] public float idleLoopStartPct = 0.1f;
    [Range(0.6f, 1f)] public float idleLoopEndPct = 0.9f;
    [Range(0f, 0.4f)] public float movingLoopStartPct = 0.1f;
    [Range(0.6f, 1f)] public float movingLoopEndPct = 0.9f;

    // Audio Sources
    private AudioSource sfxSource;
    private AudioSource tallyLoopSource;
    private AudioSource engineIdleSource;
    private AudioSource engineMovingSource;
    private AudioSource ambientSource;
    private AudioSource bgmSourceA;
    private AudioSource bgmSourceB;
    
    private bool useBgmSourceA = true;
    private bool isCrossfadingBgm = false;
    private List<int> bgmPlaylist = new List<int>();
    private int currentBgmIndex = 0;

    public static float SteerInput { get; private set; }
    public static float AccelInput { get; private set; }
    public static bool BrakeInput { get; private set; }

    private int selectedTroskiIndex;
    private int selectedMateIndex;
    private int selectedRouteIndex;
    private string gameMode;

    private int vehicleCondition = 100;
    private float currentFuel;
    private int playerLevel = 1;
    private int currentXP = 0;

    private GameObject playerTroski;
    public GameObject PlayerTroski => playerTroski; 
    private GameObject playerMate;
    private Rigidbody troskiRb;

    public float CurrentSpeedKmH => troskiRb != null ? troskiRb.velocity.magnitude * 3.6f : 0f;
    
    private int maxCapacity = 15;
    private int currentPassengers = 0;
    private int accumulatedCash = 0;
    
    private List<string> routeStops = new List<string>();
    private int currentStopIndex = 0;
    private bool isPaused = false;
    private bool isShiftOver = false;
    private float lastDamageTime = 0f; 

    private GearState currentGear = GearState.Drive;
    private bool isAtFuelStation = false;

    private float shiftTimer = 0f;
    private bool isTimerActive = false;
    private bool isLoadingPassengers = false;
    
    private GameObject currentPhysicalTarget;
    private GameObject currentFuelTarget;
    
    private float distanceToTarget = 0f;
    private bool isInDropoffZone = false;
    
    private float distanceToNextStop = 0f;
    private bool isStopTriggerSpawned = false;
    private float distanceToNextFuel = 0f;
    private bool isFuelTriggerSpawned = false;

    [System.Serializable]
    public class TroskiMission { public string title; public int currentProgress; public int targetProgress; public int rewardGHS; public int rewardXP; public bool IsComplete => currentProgress >= targetProgress; }
    [System.Serializable]
    public class MissionWrapper { public List<TroskiMission> missions = new List<TroskiMission>(); }
    private MissionWrapper activeMissions;

    // UI
    private GameObject canvasObj;
    private GameObject mobileControlsPanel;
    private RectTransform notificationPanel;
    private Text notificationText;
    private Text hudCashText;
    private Text hudPassengerText;
    private Text hudRouteText;
    private Text hudTimerText;
    private RectTransform fuelBarFill;
    private Text gearText;
    private Text speedText;
    private RectTransform speedBarFill;
    private Text unionTasksText;
    private GameObject refuelButtonObj;
    private GameObject loadingPhasePanel;
    private Text loadingPhaseText;
    private Sprite roundedSprite;
    
    // Minimap UI
    private RectTransform playerMapIconRect;
    private RectTransform targetMapIconRect;

    private GameObject pausePanel;
    private GameObject successModal;
    private GameObject loadingPanel;
    private Text smTitleText, smGrossText, smMateCutText, smMissionText, smNetText, smLevelText, smEarnedXPText;
    private RectTransform smXPBarFill;
    private RectTransform loadingFillRect;
    private Text loadingPercentText, loadingDescText;

    private bool isUsingKeyboard = false;
    private float mobileSteer = 0f;
    private float mobileGasPedal = 0f;
    private bool mobileBrake = false;

    private Color themeColor = new Color(0.98f, 0.75f, 0.05f, 1f); 
    private Color darkBg = new Color(0.05f, 0.05f, 0.08f, 0.85f); 
    private Color lightBg = new Color(0.15f, 0.15f, 0.18f, 0.85f);
    private Font defaultFont;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Force Landscape Orientation firmly
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }

    void Start()
    {
        Time.timeScale = 1f;
        defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        currentFuel = maxFuel;

        SetupAudio();
        GenerateRoundedSprite();
        LoadPreferences();
        LoadMissions();
        GenerateRouteData();
        SpawnEntities();
        BuildGameUI();

        if (autoGenerateWaypoints) AutoGenerateRoadWaypoints();

        shiftTimer = 120f + (selectedRouteIndex * 60f);
        distanceToNextStop = Random.Range(300f, 600f);
        distanceToNextFuel = Random.Range(1000f, 2500f);

        if (gameMode == "Load at Station")
        {
            StartCoroutine(LoadPassengersRoutine());
        }
        else
        {
            isTimerActive = true;
            SpawnNextStop(); 
            ShowNotification("SHIFT STARTED: " + GetRouteName(selectedRouteIndex));
        }
        
        UpdateHUD();
        UpdateUnionTasks();

        if (vehicleCondition <= 0) FinishRouteRoutine(true, true, false, false);
        else if (vehicleCondition <= 40) ShowNotification($"VEHICLE DAMAGE: {100 - vehicleCondition}%\nEarnings will be reduced!");
    }

    void Update()
    {
        if (isShiftOver) return;

        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();

        if (!isPaused)
        {
            DetectInputDevice();
            ProcessInputs();
            UpdateFuel();
            UpdateSpeedometer();
            UpdateEngineAudio();
            UpdateBGM();
            UpdateMinimapAndTarget();
            
            if (isLoadingPassengers && troskiRb != null)
            {
                troskiRb.velocity = Vector3.zero;
                troskiRb.angularVelocity = Vector3.zero;
            }
            else if (troskiRb != null)
            {
                float speed = troskiRb.velocity.magnitude;
                float distanceThisFrame = speed * Time.deltaTime;
                
                // Only count distance if moving forward
                if (speed > 1f && currentGear == GearState.Drive)
                {
                    UpdateProceduralGeneration(distanceThisFrame);
                }

                // --- PURE PROXIMITY STOP LOGIC ---
                if (currentPhysicalTarget != null)
                {
                    Vector3 p1 = playerTroski.transform.position;
                    Vector3 p2 = currentPhysicalTarget.transform.position;
                    p1.y = 0; p2.y = 0; // Ignore height differences
                    distanceToTarget = Vector3.Distance(p1, p2);

                    if (distanceToTarget <= 8f)
                    {
                        if (!isInDropoffZone)
                        {
                            isInDropoffZone = true;
                            ShowNotification("In Drop-off Zone: Stop the vehicle!");
                        }

                        if (CurrentSpeedKmH < 1f)
                        {
                            GameObject oldTarget = currentPhysicalTarget;
                            isInDropoffZone = false;
                            OnReachedStop(routeStops[currentStopIndex]);
                            if (oldTarget != null) Destroy(oldTarget);
                        }
                    }
                    else
                    {
                        isInDropoffZone = false;
                    }
                }

                // --- PURE PROXIMITY FUEL LOGIC ---
                if (isFuelTriggerSpawned && currentFuelTarget != null)
                {
                    Vector3 p1 = playerTroski.transform.position;
                    Vector3 p2 = currentFuelTarget.transform.position;
                    p1.y = 0; p2.y = 0;
                    float distFromFuel = Vector3.Distance(p1, p2);

                    if (distFromFuel <= 8f)
                    {
                        if (!isAtFuelStation) OnReachedFuelStation();
                    }
                    else
                    {
                        if (isAtFuelStation) OnLeftFuelStation();
                        if (distFromFuel > 200f)
                        {
                            Destroy(currentFuelTarget);
                            OnLeftFuelStation(); 
                        }
                    }
                }
            }
            
            if (isTimerActive)
            {
                shiftTimer -= Time.deltaTime;
                UpdateTimerUI();

                if (shiftTimer <= 0)
                {
                    shiftTimer = 0;
                    FinishRouteRoutine(true, false, false, true); 
                }
            }

            if (isUsingKeyboard && Input.GetKeyDown(KeyCode.LeftShift) && !isLoadingPassengers) ToggleGear();
        }
    }

    private void AutoGenerateRoadWaypoints()
    {
        GameObject waypointContainer = new GameObject("AutoGenerated_Waypoints");
        int maxAttempts = 5000; 
        
        int stopsCreated = 0;
        for (int i = 0; i < maxAttempts && stopsCreated < generatedBusStops; i++)
        {
            float randomX = Random.Range(-cityBoundsSize / 2f, cityBoundsSize / 2f);
            float randomZ = Random.Range(-cityBoundsSize / 2f, cityBoundsSize / 2f);
            Vector3 rayStart = new Vector3(randomX, 2000f, randomZ); 

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 3000f))
            {
                if (CheckIfRoad(hit.collider.transform))
                {
                    GameObject wp = new GameObject($"StopNode_{stopsCreated}");
                    wp.transform.position = hit.point;
                    wp.transform.SetParent(waypointContainer.transform);
                    possibleStopLocations.Add(wp.transform);
                    stopsCreated++;
                }
            }
        }

        int fuelCreated = 0;
        for (int i = 0; i < maxAttempts && fuelCreated < generatedFuelStations; i++)
        {
            float randomX = Random.Range(-cityBoundsSize / 2f, cityBoundsSize / 2f);
            float randomZ = Random.Range(-cityBoundsSize / 2f, cityBoundsSize / 2f);
            Vector3 rayStart = new Vector3(randomX, 2000f, randomZ); 

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 3000f))
            {
                if (CheckIfRoad(hit.collider.transform))
                {
                    GameObject wp = new GameObject($"FuelNode_{fuelCreated}");
                    wp.transform.position = hit.point;
                    wp.transform.SetParent(waypointContainer.transform);
                    possibleFuelLocations.Add(wp.transform);
                    fuelCreated++;
                }
            }
        }
        Debug.Log($"[Auto-Scout] Found {stopsCreated} Bus Stops and {fuelCreated} Fuel Stations on '{roadTag}'.");
    }

    private bool CheckIfRoad(Transform hitTransform)
    {
        Transform current = hitTransform;
        while (current != null)
        {
            if (current.CompareTag(roadTag)) return true;
            current = current.parent;
        }
        return false;
    }

    private void UpdateProceduralGeneration(float dist)
    {
        if (!isStopTriggerSpawned && currentStopIndex < routeStops.Count)
        {
            distanceToNextStop -= dist;
            if (distanceToNextStop <= spawnDistanceAhead)
            {
                isStopTriggerSpawned = true;
                SpawnNextStop(); 
            }
        }

        if (!isFuelTriggerSpawned)
        {
            distanceToNextFuel -= dist;
            if (distanceToNextFuel <= spawnDistanceAhead)
            {
                isFuelTriggerSpawned = true;
                SpawnFuelTrigger();
            }
        }
    }

    private void SpawnFuelTrigger()
    {
        if (fuelStationPrefab == null || playerTroski == null) return;

        Vector3 forwardPos = playerTroski.transform.position + (playerTroski.transform.forward * spawnDistanceAhead);
        Vector3 spawnPos = forwardPos + (playerTroski.transform.right * 4.5f); 
        Quaternion spawnRot = playerTroski.transform.rotation;

        if (possibleFuelLocations != null && possibleFuelLocations.Count > 0)
        {
            Transform closestWaypoint = GetValidWaypoint(possibleFuelLocations, 50f, spawnDistanceAhead + 50f);
            if (closestWaypoint != null)
            {
                spawnPos = closestWaypoint.position + (closestWaypoint.right * 4.5f); 
                spawnRot = closestWaypoint.rotation;
            }
        }

        if (Physics.Raycast(spawnPos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
        {
            spawnPos = hit.point;
        }

        GameObject newTrigger = Instantiate(fuelStationPrefab, spawnPos, spawnRot);
        
        Collider[] existingCols = newTrigger.GetComponentsInChildren<Collider>();
        foreach (var c in existingCols) { Destroy(c); } 

        currentFuelTarget = newTrigger;
        ShowNotification("FUEL STATION AHEAD!\nPull over to buy fuel.");
    }

    public void SpawnNextStop()
    {
        if (currentStopIndex >= routeStops.Count) return;

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;
        bool validSpawnFound = false;

        if (possibleStopLocations != null && possibleStopLocations.Count > 0)
        {
            Transform chosenWaypoint = GetValidWaypoint(possibleStopLocations, 150f, 600f);
            if (chosenWaypoint != null)
            {
                spawnPos = chosenWaypoint.position + (chosenWaypoint.right * 4.5f); 
                spawnRot = chosenWaypoint.rotation;
                validSpawnFound = true;
            }
        }

        if (!validSpawnFound && playerTroski != null)
        {
            spawnPos = playerTroski.transform.position + (playerTroski.transform.forward * 400f) + (playerTroski.transform.right * 4.5f);
            if (Physics.Raycast(spawnPos + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 200f))
            {
                spawnPos = hit.point;
            }
            spawnRot = playerTroski.transform.rotation;
            validSpawnFound = true;
        }
        
        if (validSpawnFound)
        {
            currentPhysicalTarget = new GameObject("TargetLocation_" + currentStopIndex);
            currentPhysicalTarget.transform.position = spawnPos;

            float dist = Vector3.Distance(playerTroski.transform.position, spawnPos);
            distanceToTarget = dist;
            shiftTimer += (dist / 12f) + 30f; 
            isStopTriggerSpawned = true;

            ShowNotification($"PASSENGER: \"Mate, bus stop!\"\nNext stop generated: {routeStops[currentStopIndex]}");
        }
    }

    private Transform GetValidWaypoint(List<Transform> waypoints, float minDist, float maxDist)
    {
        List<Transform> validPoints = new List<Transform>();
        Transform furthestPoint = null;
        float maxDistFound = 0;

        foreach (Transform t in waypoints)
        {
            if (t == null) continue;
            float d = Vector3.Distance(playerTroski.transform.position, t.position);
            if (d > minDist && d < maxDist) validPoints.Add(t);
            if (d > maxDistFound) { maxDistFound = d; furthestPoint = t; }
        }

        if (validPoints.Count > 0) return validPoints[Random.Range(0, validPoints.Count)];
        if (furthestPoint != null) return furthestPoint; 
        return waypoints.Count > 0 ? waypoints[0] : null; 
    }

    // --- MINIMAP & UI TARGET ---
    private void UpdateMinimapAndTarget()
    {
        if (playerTroski == null) return;

        if (minimapCamera != null)
        {
            minimapCamera.transform.position = new Vector3(playerTroski.transform.position.x, playerTroski.transform.position.y + 150f, playerTroski.transform.position.z);
            minimapCamera.transform.eulerAngles = new Vector3(90f, 0f, 0f);

            if (playerMapIconRect != null)
            {
                playerMapIconRect.localEulerAngles = new Vector3(0, 0, -playerTroski.transform.eulerAngles.y);
            }
        }

        if (currentPhysicalTarget != null && targetMapIconRect != null)
        {
            targetMapIconRect.gameObject.SetActive(true);

            Vector3 diff = currentPhysicalTarget.transform.position - playerTroski.transform.position;
            float uiX = diff.x * 1.25f; 
            float uiY = diff.z * 1.25f; 

            Vector2 uiPos = new Vector2(uiX, uiY);
            
            float maxDist = 115f; 
            if (uiPos.magnitude > maxDist)
            {
                uiPos = uiPos.normalized * maxDist;
            }

            targetMapIconRect.anchoredPosition = uiPos;

            if (hudRouteText != null)
            {
                if (isInDropoffZone) 
                    hudRouteText.text = $"<color=red>DROP-OFF ZONE! STOP VEHICLE!</color>";
                else 
                    hudRouteText.text = $"{GetRouteName(selectedRouteIndex)}  ➔  {GetNextExpectedStop()} ({Mathf.RoundToInt(distanceToTarget)}m)";
            }
        }
        else
        {
            if (targetMapIconRect != null) targetMapIconRect.gameObject.SetActive(false);
            if (hudRouteText != null) hudRouteText.text = $"{GetRouteName(selectedRouteIndex)}  ➔  WAITING...";
        }
    }

    public string GetNextExpectedStop()
    {
        if (currentStopIndex < routeStops.Count) return routeStops[currentStopIndex];
        return "DESTINATION";
    }

    // --- SETUP & DATA ---

    private void SetupAudio()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        
        tallyLoopSource = gameObject.AddComponent<AudioSource>();
        tallyLoopSource.playOnAwake = false;
        tallyLoopSource.loop = true;

        engineIdleSource = gameObject.AddComponent<AudioSource>();
        engineIdleSource.loop = true; 
        engineIdleSource.clip = engineIdleSFX;
        engineIdleSource.volume = 1f;
        if (engineIdleSFX != null) engineIdleSource.Play();

        engineMovingSource = gameObject.AddComponent<AudioSource>();
        engineMovingSource.loop = true;
        engineMovingSource.clip = engineMovingSFX;
        engineMovingSource.volume = 0f; 
        if (engineMovingSFX != null) engineMovingSource.Play();

        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.loop = true;
        ambientSource.volume = 0.5f;
        ambientSource.clip = trafficAmbientSFX;
        if (trafficAmbientSFX != null) ambientSource.Play();

        bgmSourceA = gameObject.AddComponent<AudioSource>();
        bgmSourceA.loop = false;
        bgmSourceA.volume = bgmVolume;

        bgmSourceB = gameObject.AddComponent<AudioSource>();
        bgmSourceB.loop = false;
        bgmSourceB.volume = 0f;

        if (bgmTracks != null && bgmTracks.Length > 0)
        {
            ShuffleBGMPlaylist();
            bgmSourceA.clip = bgmTracks[bgmPlaylist[0]];
            bgmSourceA.Play();
        }
    }

    private void ShuffleBGMPlaylist()
    {
        bgmPlaylist.Clear();
        for (int i = 0; i < bgmTracks.Length; i++) bgmPlaylist.Add(i);
        
        for (int i = 0; i < bgmPlaylist.Count; i++)
        {
            int temp = bgmPlaylist[i];
            int randomIndex = Random.Range(i, bgmPlaylist.Count);
            bgmPlaylist[i] = bgmPlaylist[randomIndex];
            bgmPlaylist[randomIndex] = temp;
        }
        currentBgmIndex = 0;
    }

    private void UpdateBGM()
    {
        if (bgmTracks == null || bgmTracks.Length == 0 || isCrossfadingBgm) return;

        AudioSource activeSource = useBgmSourceA ? bgmSourceA : bgmSourceB;

        if (activeSource.isPlaying && activeSource.clip != null)
        {
            if (activeSource.time >= activeSource.clip.length - 2f) StartCoroutine(CrossfadeBGM());
        }
        else if (!activeSource.isPlaying) StartCoroutine(CrossfadeBGM());
    }

    private IEnumerator CrossfadeBGM()
    {
        isCrossfadingBgm = true;
        currentBgmIndex++;
        if (currentBgmIndex >= bgmPlaylist.Count) ShuffleBGMPlaylist(); 

        AudioClip nextClip = bgmTracks[bgmPlaylist[currentBgmIndex]];
        AudioSource activeSource = useBgmSourceA ? bgmSourceA : bgmSourceB;
        AudioSource newSource = useBgmSourceA ? bgmSourceB : bgmSourceA;

        newSource.clip = nextClip;
        newSource.volume = 0f;
        newSource.Play();

        float fadeDuration = 2.0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            activeSource.volume = Mathf.Lerp(bgmVolume, 0f, elapsed / fadeDuration);
            newSource.volume = Mathf.Lerp(0f, bgmVolume, elapsed / fadeDuration);
            yield return null;
        }

        activeSource.Stop();
        activeSource.volume = 0f;
        newSource.volume = bgmVolume;

        useBgmSourceA = !useBgmSourceA;
        isCrossfadingBgm = false;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
    }

    private void UpdateEngineAudio()
    {
        if (troskiRb != null)
        {
            float speedKmH = troskiRb.velocity.magnitude * 3.6f;
            float targetIdleVol = speedKmH < 5f ? 1f : 0f;
            float targetMovingVol = speedKmH >= 1f ? 1f : 0f;

            if (engineIdleSource != null && engineIdleSFX != null)
            {
                engineIdleSource.volume = Mathf.Lerp(engineIdleSource.volume, targetIdleVol, Time.deltaTime * 5f);
                float currentIdleTime = engineIdleSource.time;
                float endIdleTime = engineIdleSFX.length * idleLoopEndPct;
                if (currentIdleTime >= endIdleTime) engineIdleSource.time = engineIdleSFX.length * idleLoopStartPct;
            }

            if (engineMovingSource != null && engineMovingSFX != null)
            {
                engineMovingSource.volume = Mathf.Lerp(engineMovingSource.volume, targetMovingVol, Time.deltaTime * 5f);
                float pitchRatio = Mathf.Clamp(speedKmH / 100f, 0f, 1.5f);
                engineMovingSource.pitch = 1.0f + pitchRatio; 

                float currentMovingTime = engineMovingSource.time;
                float endMovingTime = engineMovingSFX.length * movingLoopEndPct;
                if (currentMovingTime >= endMovingTime) engineMovingSource.time = engineMovingSFX.length * movingLoopStartPct;
            }
        }
    }

    private void GenerateRoundedSprite()
    {
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

    private void LoadPreferences()
    {
        selectedTroskiIndex = PlayerPrefs.GetInt("SelectedTroski", 0);
        selectedMateIndex = PlayerPrefs.GetInt("SelectedMate", 0);
        selectedRouteIndex = PlayerPrefs.GetInt("SelectedRoute", 0);
        gameMode = PlayerPrefs.GetString("SelectedGameMode", "Move & Pick");
        
        vehicleCondition = PlayerPrefs.GetInt("VehicleCondition", 100);
        playerLevel = PlayerPrefs.GetInt("PlayerLevel", 1);
        currentXP = PlayerPrefs.GetInt("CurrentXP", 0);

        int themeIdx = PlayerPrefs.GetInt("ThemeIndex", 0);
        Color[] themes = new Color[] {
            new Color(0.98f, 0.75f, 0.05f, 1f), new Color(0.15f, 0.6f, 0.95f, 1f),
            new Color(0.9f, 0.2f, 0.2f, 1f), new Color(0.6f, 0.2f, 0.8f, 1f), new Color(0.15f, 0.85f, 0.4f, 1f)
        };
        if (themeIdx >= 0 && themeIdx < themes.Length) themeColor = themes[themeIdx];
        
        maxCapacity = 12 + (selectedTroskiIndex * 3);
    }

    private void LoadMissions()
    {
        string missionJson = PlayerPrefs.GetString("Missions", "");
        if (!string.IsNullOrEmpty(missionJson)) activeMissions = JsonUtility.FromJson<MissionWrapper>(missionJson);
        if (activeMissions == null) activeMissions = new MissionWrapper();
    }

    private void SaveMissions()
    {
        if (activeMissions != null)
        {
            PlayerPrefs.SetString("Missions", JsonUtility.ToJson(activeMissions));
            PlayerPrefs.Save();
        }
    }

    private int GetXPToNextLevel(int level)
    {
        return Mathf.RoundToInt(500 * Mathf.Pow(1.5f, level - 1));
    }

    private void SpawnEntities()
    {
        GameObject existingTroski = GameObject.FindGameObjectWithTag("Player");
        if (existingTroski != null) Destroy(existingTroski);

        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        if (troskiPrefabs != null && troskiPrefabs.Length > selectedTroskiIndex && troskiPrefabs[selectedTroskiIndex] != null)
        {
            playerTroski = Instantiate(troskiPrefabs[selectedTroskiIndex], pos, rot);
        }
        else
        {
            playerTroski = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerTroski.transform.position = pos;
            playerTroski.transform.localScale = new Vector3(2, 2, 5);
            playerTroski.GetComponent<Renderer>().material.color = themeColor;
        }

        SetLayerAndTagRecursively(playerTroski, LayerMask.NameToLayer("Default"), "Player");

        if (playerTroski.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider bc = playerTroski.AddComponent<BoxCollider>();
            bc.size = new Vector3(2.5f, 2.5f, 6f);
            bc.center = new Vector3(0, 1.25f, 0);
        }

        playerTroski.tag = "Player";
        troskiRb = playerTroski.GetComponent<Rigidbody>();

        // Ensure Gley AI Detection Script is attached
        if (playerTroski.GetComponent<Gley.TrafficSystem.TrafficParticipant>() == null)
        {
            playerTroski.AddComponent<Gley.TrafficSystem.TrafficParticipant>();
        }

        if (matePrefabs != null && matePrefabs.Length > selectedMateIndex && matePrefabs[selectedMateIndex] != null)
        {
            playerMate = Instantiate(matePrefabs[selectedMateIndex]);
            Transform matePoint = null;
            foreach (Transform child in playerTroski.GetComponentsInChildren<Transform>())
            {
                if (child.name == "MateSpawnPoint") { matePoint = child; break; }
            }

            if (matePoint != null)
            {
                playerMate.transform.SetParent(matePoint);
                playerMate.transform.localPosition = Vector3.zero;
                playerMate.transform.localRotation = Quaternion.identity;
            }
            else playerMate.transform.SetParent(playerTroski.transform, false);
        }
        
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            var camFollow = mainCamera.GetComponent("CameraFollow");
            if (camFollow != null) camFollow.GetType().GetField("target").SetValue(camFollow, playerTroski.transform);
        }

        minimapTexture = new RenderTexture(256, 256, 16);
        GameObject minimapCamObj = new GameObject("MinimapCamera");
        minimapCamera = minimapCamObj.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = 100f; 
        minimapCamera.targetTexture = minimapTexture;
        minimapCamera.cullingMask = ~(1 << 5); 

        try
        {
            var trafficComp = FindObjectOfType<Gley.TrafficSystem.TrafficComponent>();
            if (trafficComp != null) trafficComp.player = playerTroski.transform;

            System.Type trafficApi = System.Type.GetType("Gley.TrafficSystem.API, Assembly-CSharp");
            if (trafficApi != null)
            {
                System.Reflection.MethodInfo setPlayerMethod = trafficApi.GetMethod("SetPlayer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (setPlayerMethod != null) setPlayerMethod.Invoke(null, new object[] { playerTroski.transform });
            }
        }
        catch { }
    }

    private void SetLayerAndTagRecursively(GameObject obj, int newLayer, string newTag)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        try { obj.tag = newTag; } catch { }
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerAndTagRecursively(child.gameObject, newLayer, newTag);
        }
    }

    private string GetRouteName(int index)
    {
        string[] routes = { "MADINA ⇌ CIRCLE", "LAPAZ ⇌ ACCRA", "DANSOMAN ⇌ CIRCLE", "KASOA ⇌ CIRCLE", "TEMA ⇌ ACCRA", "KUMASI ⇌ ACCRA" };
        if (index >= 0 && index < routes.Length) return routes[index];
        return "FREE ROAM";
    }

    private void GenerateRouteData()
    {
        routeStops.Clear();
        string routeName = GetRouteName(selectedRouteIndex);
        string[] locations = routeName.Split('⇌');
        string start = locations[0].Trim();
        string end = locations.Length > 1 ? locations[1].Trim() : "DESTINATION";

        routeStops.Add(start + " Station");
        int subStops = 3 + selectedRouteIndex; 
        for (int i = 1; i <= subStops; i++) routeStops.Add($"Junction {i}");
        routeStops.Add(end + " Terminal");
    }

    private IEnumerator LoadPassengersRoutine()
    {
        isLoadingPassengers = true;
        if (loadingPhasePanel != null) loadingPhasePanel.SetActive(true);

        int targetPassengers = Mathf.CeilToInt(maxCapacity * 0.9f);
        currentPassengers = 0;
        
        while (currentPassengers < targetPassengers)
        {
            UpdateHUD();
            if (loadingPhaseText != null) loadingPhaseText.text = $"WAITING AT STATION\nLoading Passengers... {currentPassengers} / {maxCapacity}";
            yield return new WaitForSeconds(Random.Range(1.0f, 3.0f));
            
            int boarding = Random.Range(1, 4);
            currentPassengers += boarding;
            if (currentPassengers > maxCapacity) currentPassengers = maxCapacity;
            PlaySFX(buyFuelSFX); 
        }

        UpdateHUD();
        if (loadingPhasePanel != null) loadingPhasePanel.SetActive(false);
        
        isLoadingPassengers = false;
        isTimerActive = true;
        SpawnNextStop(); 
        ShowNotification($"VEHICLE LOADED. SHIFT STARTED!\nTime Limit: {Mathf.FloorToInt(shiftTimer/60):00}:{Mathf.FloorToInt(shiftTimer%60):00}");
    }

    private void UpdateTimerUI()
    {
        if (hudTimerText != null)
        {
            int m = Mathf.FloorToInt(shiftTimer / 60f);
            int s = Mathf.FloorToInt(shiftTimer % 60f);
            
            if (shiftTimer < 30f)
            {
                hudTimerText.color = new Color(0.85f, 0.15f, 0.15f); 
                hudTimerText.text = $"<size=18>TIME LEFT</size>\n{m:00}:{s:00}";
            }
            else
            {
                hudTimerText.color = Color.white;
                hudTimerText.text = $"<size=18>TIME LEFT</size>\n{m:00}:{s:00}";
            }
        }
    }

    public void TogglePause()
    {
        if (isShiftOver) return;

        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;

        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
            if (isPaused) PlaySFX(modalOpenSFX);
        }

        if (isPaused)
        {
            SteerInput = 0f;
            AccelInput = 0f;
            BrakeInput = true;
            
            if (engineIdleSource != null) engineIdleSource.volume = 0f;
            if (engineMovingSource != null) engineMovingSource.volume = 0f;
            if (bgmSourceA != null) bgmSourceA.volume = 0.05f;
            if (bgmSourceB != null) bgmSourceB.volume = 0.05f;
        }
        else
        {
            if (bgmSourceA != null && useBgmSourceA) bgmSourceA.volume = bgmVolume;
            if (bgmSourceB != null && !useBgmSourceA) bgmSourceB.volume = bgmVolume;
        }
    }

    public void EndShiftPrematurely()
    {
        TogglePause();
        FinishRouteRoutine(true, false, false, false); 
    }

    public void ApplyDamage(int amount)
    {
        if (isShiftOver || Time.time - lastDamageTime < 1.0f) return;
        lastDamageTime = Time.time;
        vehicleCondition -= amount;
        PlaySFX(crashSFX); 
        
        if (vehicleCondition <= 0)
        {
            vehicleCondition = 0;
            FinishRouteRoutine(true, true, false, false); 
        }
        else
        {
            int damagePercent = 100 - vehicleCondition;
            ShowNotification($"⚠ COLLISION DETECTED ⚠\nTotal Damage: <color=red>{damagePercent}%</color>");
        }
    }

    public void BuyFuel()
    {
        if (!isAtFuelStation || troskiRb.velocity.magnitude > 1f) 
        {
            ShowNotification("You must STOP completely to refuel!");
            return;
        }

        if (accumulatedCash >= fuelCostPerTick && currentFuel < maxFuel)
        {
            accumulatedCash -= fuelCostPerTick;
            currentFuel = Mathf.Min(maxFuel, currentFuel + 25f);
            PlaySFX(buyFuelSFX);
            
            isFuelTriggerSpawned = false;
            distanceToNextFuel = Random.Range(1000f, 2500f);
            if (refuelButtonObj != null) refuelButtonObj.SetActive(false);
            
            UpdateHUD();
            ShowNotification("Purchased Fuel!");
        }
        else if (accumulatedCash < fuelCostPerTick) ShowNotification("Not enough cash to buy fuel!");
        else ShowNotification("Tank is already full.");
    }

    public void OnReachedStop(string stopName)
    {
        ArriveAtStop(stopName);
    }

    public void OnReachedFuelStation()
    {
        isAtFuelStation = true;
        if (refuelButtonObj != null) refuelButtonObj.SetActive(true);
        ShowNotification("FUEL STATION REACHED.\nStop to refuel.");
    }

    public void OnLeftFuelStation()
    {
        isAtFuelStation = false;
        if (refuelButtonObj != null) refuelButtonObj.SetActive(false);
        
        isFuelTriggerSpawned = false;
        distanceToNextFuel = Random.Range(1000f, 2500f);
    }

    private void ArriveAtStop(string stopName)
    {
        if (currentStopIndex >= routeStops.Count) return;

        vehicleCondition -= Random.Range(1, 4);
        if (vehicleCondition <= 0)
        {
            vehicleCondition = 0;
            FinishRouteRoutine(true, true, false, false);
            return;
        }

        int dropOffs = 0;
        if (currentPassengers > 0)
        {
            dropOffs = (currentStopIndex == routeStops.Count - 1) ? currentPassengers : Random.Range(0, currentPassengers + 1);
            currentPassengers -= dropOffs;
        }

        int pickups = 0;
        if (currentStopIndex < routeStops.Count - 1) 
        {
            int availableSeats = maxCapacity - currentPassengers;
            pickups = Random.Range(0, availableSeats + 1);
            currentPassengers += pickups;
            
            if (pickups > 0 && activeMissions != null)
            {
                foreach (var mission in activeMissions.missions)
                {
                    if (mission.title.Contains("Load") && !mission.IsComplete)
                    {
                        mission.currentProgress += pickups;
                        if (mission.currentProgress > mission.targetProgress) mission.currentProgress = mission.targetProgress;
                    }
                }
                SaveMissions();
            }
        }

        float conditionMultiplier = 1.0f;
        if (vehicleCondition < 60) conditionMultiplier = 0.4f + ((vehicleCondition / 60f) * 0.6f);

        float baseFare = dropOffs * farePerPassenger * (selectedRouteIndex + 1);
        int fareCollected = Mathf.RoundToInt(baseFare * conditionMultiplier); 
        accumulatedCash += fareCollected;

        isStopTriggerSpawned = false;
        distanceToNextStop = Random.Range(300f, 600f);
        currentStopIndex++;
        SpawnNextStop(); 

        UpdateHUD();
        UpdateUnionTasks();
        
        int damagePercent = 100 - vehicleCondition;
        string penaltyMsg = conditionMultiplier < 1.0f ? "\n<color=red>Damaged Vehicle Penalty Applied</color>" : "";
        string notif = $"STOP: {stopName}\nDropped: {dropOffs} | Picked: {pickups} | Fare: +{fareCollected} GHS\nCurrent Damage: {damagePercent}%{penaltyMsg}";
        ShowNotification(notif);

        if (currentStopIndex >= routeStops.Count)
        {
            FinishRouteRoutine(false, false, false, false);
        }
    }

    private void FinishRouteRoutine(bool isCancelled, bool isCrash, bool outOfFuel, bool outOfTime)
    {
        isShiftOver = true;
        SteerInput = 0f;
        AccelInput = 0f;
        BrakeInput = true;
        isTimerActive = false;

        if (mobileControlsPanel != null) mobileControlsPanel.SetActive(false);

        int grossEarnings = isCancelled ? 0 : accumulatedCash;
        int mateCut = 50 + (selectedMateIndex * 25); 
        int missionBonus = 0;
        int missionXPBonus = 0;

        if (!isCancelled && activeMissions != null)
        {
            foreach (var mission in activeMissions.missions)
            {
                if (mission.title.Contains("Complete") && !mission.IsComplete)
                {
                    mission.currentProgress += 1;
                    if (mission.currentProgress > mission.targetProgress) mission.currentProgress = mission.targetProgress;
                }

                if (mission.IsComplete && mission.rewardGHS > 0)
                {
                    missionBonus += mission.rewardGHS;
                    missionXPBonus += mission.rewardXP;
                    mission.rewardGHS = 0; 
                }
            }
            SaveMissions();
            UpdateUnionTasks();
        }

        int netEarnings = grossEarnings + missionBonus - mateCut;
        int earnedXP = isCancelled ? 0 : (150 * (selectedRouteIndex + 1)) + missionXPBonus;
        int cachedStartXP = currentXP;
        int cachedStartLevel = playerLevel;

        int savedCash = PlayerPrefs.GetInt("PlayerCash", 1000);
        PlayerPrefs.SetInt("PlayerCash", Mathf.Max(0, savedCash + netEarnings));
        PlayerPrefs.SetInt("VehicleCondition", vehicleCondition);
        
        if (!isCancelled)
        {
            int dailyEarning = PlayerPrefs.GetInt("DailyEarnings", 0);
            PlayerPrefs.SetInt("DailyEarnings", dailyEarning + grossEarnings);
            
            currentXP += earnedXP;
            while (currentXP >= GetXPToNextLevel(playerLevel))
            {
                currentXP -= GetXPToNextLevel(playerLevel);
                playerLevel++;
            }
            PlayerPrefs.SetInt("CurrentXP", currentXP);
            PlayerPrefs.SetInt("PlayerLevel", playerLevel);
        }

        PlayerPrefs.SetInt("SkipIntro", 1);
        PlayerPrefs.Save();

        ShowSuccessModal(grossEarnings, mateCut, missionBonus, netEarnings, cachedStartXP, earnedXP, cachedStartLevel, isCancelled, isCrash, outOfFuel, outOfTime);
    }

    private void ShowSuccessModal(int gross, int mateCut, int missionBonus, int net, int startXP, int earnedXP, int startLevel, bool isCancelled, bool isCrash, bool outOfFuel, bool outOfTime)
    {
        if (successModal != null)
        {
            successModal.SetActive(true);
            PlaySFX(modalOpenSFX);

            if (engineIdleSource != null) engineIdleSource.Stop();
            if (engineMovingSource != null) engineMovingSource.Stop();
            if (ambientSource != null) ambientSource.volume = 0.1f;
            if (bgmSourceA != null) bgmSourceA.volume = 0.1f;
            if (bgmSourceB != null) bgmSourceB.volume = 0.1f;

            if (smTitleText != null)
            {
                if (isCrash)
                {
                    smTitleText.text = "SHIFT FAILED: VEHICLE TOTALED";
                    smTitleText.color = new Color(0.85f, 0.15f, 0.15f);
                    PlaySFX(shiftFailSFX);
                }
                else if (outOfFuel)
                {
                    smTitleText.text = "SHIFT FAILED: OUT OF FUEL";
                    smTitleText.color = new Color(0.85f, 0.15f, 0.15f);
                    PlaySFX(shiftFailSFX);
                }
                else if (outOfTime)
                {
                    smTitleText.text = "SHIFT FAILED: OUT OF TIME";
                    smTitleText.color = new Color(0.85f, 0.15f, 0.15f);
                    PlaySFX(shiftFailSFX);
                }
                else if (isCancelled)
                {
                    smTitleText.text = "SHIFT CANCELLED";
                    smTitleText.color = new Color(0.85f, 0.15f, 0.15f);
                    PlaySFX(shiftFailSFX);
                }
                else
                {
                    smTitleText.text = "SHIFT COMPLETED";
                    smTitleText.color = themeColor;
                    PlaySFX(shiftSuccessSFX);
                }
            }

            StartCoroutine(AnimateModalStats(gross, mateCut, missionBonus, net, startXP, earnedXP, startLevel));
        }
        else
        {
            StartCoroutine(TransitionToMenu());
        }
    }

    private IEnumerator AnimateModalStats(int targetGross, int targetCut, int targetBonus, int targetNet, int startXP, int earnedXP, int startLevel)
    {
        if (tallyLoopSource != null && coinTallyLoopSFX != null)
        {
            tallyLoopSource.clip = coinTallyLoopSFX;
            tallyLoopSource.Play();
        }

        float duration = 2.0f; 
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easeT = t * (2f - t); 

            int currentGross = Mathf.RoundToInt(Mathf.Lerp(0, targetGross, easeT));
            int currentCut = Mathf.RoundToInt(Mathf.Lerp(0, targetCut, easeT));
            int currentBonus = Mathf.RoundToInt(Mathf.Lerp(0, targetBonus, easeT));
            int currentNet = Mathf.RoundToInt(Mathf.Lerp(0, targetNet, easeT));
            int animatedEarnedXP = Mathf.RoundToInt(Mathf.Lerp(0, earnedXP, easeT));

            if (smGrossText != null) smGrossText.text = $"+ {currentGross} GHS";
            if (smMateCutText != null) smMateCutText.text = $"- {currentCut} GHS";
            if (smMissionText != null) smMissionText.text = $"+ {currentBonus} GHS";
            if (smNetText != null) smNetText.text = $"{currentNet} GHS";
            if (smEarnedXPText != null) smEarnedXPText.text = $"+ {animatedEarnedXP} XP";

            int visualTotalXP = startXP + animatedEarnedXP;
            int visualLevel = startLevel;
            int reqXP = GetXPToNextLevel(visualLevel);

            while (visualTotalXP >= reqXP)
            {
                visualTotalXP -= reqXP;
                visualLevel++;
                reqXP = GetXPToNextLevel(visualLevel);
            }

            if (smLevelText != null) smLevelText.text = $"LVL {visualLevel}";
            if (smXPBarFill != null)
            {
                float fillRatio = Mathf.Clamp01((float)visualTotalXP / reqXP);
                smXPBarFill.sizeDelta = new Vector2(500f * fillRatio, 20f);
            }

            yield return null;
        }

        if (tallyLoopSource != null) tallyLoopSource.Stop();
        PlaySFX(xpLevelUpSFX); 

        if (smGrossText != null) smGrossText.text = $"+ {targetGross} GHS";
        if (smMateCutText != null) smMateCutText.text = $"- {targetCut} GHS";
        if (smMissionText != null) smMissionText.text = $"+ {targetBonus} GHS";
        if (smNetText != null) smNetText.text = $"{targetNet} GHS";
        if (smEarnedXPText != null) smEarnedXPText.text = $"+ {earnedXP} XP";
        
        int finalTotalXP = startXP + earnedXP;
        int finalLevel = startLevel;
        int finalReq = GetXPToNextLevel(finalLevel);
        while (finalTotalXP >= finalReq)
        {
            finalTotalXP -= finalReq;
            finalLevel++;
            finalReq = GetXPToNextLevel(finalLevel);
        }
        if (smLevelText != null) smLevelText.text = $"LVL {finalLevel}";
        if (smXPBarFill != null) smXPBarFill.sizeDelta = new Vector2(500f * Mathf.Clamp01((float)finalTotalXP / finalReq), 20f);
    }

    public void ReturnToTerminal()
    {
        StartCoroutine(TransitionToMenu());
    }

    private IEnumerator TransitionToMenu()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (successModal != null) successModal.SetActive(false);
        if (mobileControlsPanel != null) mobileControlsPanel.SetActive(false);

        CanvasGroup loadCg = null;
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            loadCg = loadingPanel.GetComponent<CanvasGroup>();
            if (loadCg != null) loadCg.alpha = 0f;
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
        float minLoadingTime = 2.5f;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("MainMenu");
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
            
            if (loadingDescText != null) loadingDescText.text = "RETURNING TO TERMINAL" + new string('.', (int)(timer * 3) % 4);

            yield return null; 
        }

        if (loadingFillRect != null) loadingFillRect.sizeDelta = new Vector2(800, 30);
        if (loadingPercentText != null) loadingPercentText.text = "100%";
        yield return new WaitForSeconds(0.2f); 

        if (asyncLoad != null) asyncLoad.allowSceneActivation = true;
    }

    private void DetectInputDevice()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (!isUsingKeyboard)
            {
                isUsingKeyboard = true;
                if (mobileControlsPanel != null) mobileControlsPanel.SetActive(false);
                ShowNotification("Keyboard Controls Active");
            }
        }
        
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            if (isUsingKeyboard)
            {
                isUsingKeyboard = false;
                if (mobileControlsPanel != null) mobileControlsPanel.SetActive(true);
                ShowNotification("Touch Controls Enabled");
            }
        }
    }

    public void ToggleGear()
    {
        if (isLoadingPassengers) return; 

        currentGear = currentGear == GearState.Drive ? GearState.Reverse : GearState.Drive;
        PlaySFX(btnClickSFX);
        if (gearText != null)
        {
            gearText.text = currentGear == GearState.Drive ? "D" : "R";
            gearText.color = currentGear == GearState.Drive ? themeColor : new Color(0.85f, 0.15f, 0.15f);
        }
        ShowNotification("Gear Shifted to: " + currentGear.ToString());
    }

    private void ProcessInputs()
    {
        if (isLoadingPassengers)
        {
            SteerInput = 0f;
            AccelInput = 0f;
            BrakeInput = true;
            return;
        }

        if (currentFuel <= 0)
        {
            AccelInput = 0f;
            BrakeInput = true;
            return;
        }

        float rawGas;
        
        if (isUsingKeyboard)
        {
            SteerInput = Input.GetAxis("Horizontal");
            rawGas = Input.GetAxis("Vertical"); 
            BrakeInput = Input.GetKey(KeyCode.Space);
        }
        else
        {
            SteerInput = mobileSteer;
            rawGas = mobileGasPedal;
            BrakeInput = mobileBrake;
        }

        if (rawGas > 0)
        {
            AccelInput = currentGear == GearState.Drive ? rawGas : -rawGas;
        }
        else
        {
            AccelInput = 0f;
        }
    }

    private void UpdateFuel()
    {
        if (Mathf.Abs(AccelInput) > 0.1f && !isLoadingPassengers)
        {
            currentFuel -= fuelConsumptionRate * Time.deltaTime;
            
            if (currentFuel <= 0)
            {
                currentFuel = 0;
                FinishRouteRoutine(true, false, true, false); 
            }
        }

        if (fuelBarFill != null)
        {
            float fillRatio = Mathf.Clamp01(currentFuel / maxFuel);
            fuelBarFill.sizeDelta = new Vector2(180f * fillRatio, 20f);
            
            Image fillImage = fuelBarFill.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = Color.Lerp(Color.red, themeColor, fillRatio);
            }
        }
    }

    private void UpdateSpeedometer()
    {
        if (speedText != null && troskiRb != null && playerTroski != null)
        {
            Vector3 velocity = troskiRb.velocity;
            float localVelocityZ = playerTroski.transform.InverseTransformDirection(velocity).z;
            
            float speedKmH = Mathf.Abs(localVelocityZ) * 3.6f;
            
            string hexTheme = ColorUtility.ToHtmlStringRGBA(themeColor);
            string gearStr = currentGear == GearState.Drive ? "D" : "R";
            if (speedKmH < 1f && AccelInput == 0f) gearStr = "P";
            
            if (vehicleCondition < 30) 
            {
                speedText.text = $"<color=red>{Mathf.RoundToInt(speedKmH)}</color>\n<size=18>KM/H</size>\n<color=#{hexTheme}><size=28>[ {gearStr} ]</size></color>";
            }
            else
            {
                speedText.text = $"{Mathf.RoundToInt(speedKmH)}\n<size=18>KM/H</size>\n<color=#{hexTheme}><size=28>[ {gearStr} ]</size></color>";
            }

            if (speedBarFill != null)
            {
                float maxSpeed = 120f; 
                float fillRatio = Mathf.Clamp01(speedKmH / maxSpeed);
                speedBarFill.sizeDelta = new Vector2(300f * fillRatio, 8f); 
                
                Image fillImage = speedBarFill.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.color = Color.Lerp(themeColor, Color.red, fillRatio);
                }
            }
        }
    }

    private void UpdateHUD()
    {
        if (hudPassengerText != null) hudPassengerText.text = $"PASS: {currentPassengers}/{maxCapacity}";
        if (hudCashText != null) hudCashText.text = $"{accumulatedCash} GHS";
    }

    private void UpdateUnionTasks()
    {
        if (unionTasksText == null) return;

        string hexTheme = ColorUtility.ToHtmlStringRGBA(themeColor);
        string tasks = $"<b><color=#{hexTheme}><size=22>UNION DIRECTIVES</size></color></b>\n\n";
        
        if (activeMissions != null && activeMissions.missions != null && activeMissions.missions.Count > 0)
        {
            foreach(var mission in activeMissions.missions)
            {
                string checkmark = mission.IsComplete ? "<color=#00FF00>✓</color>" : " ";
                tasks += $"[ {checkmark} ] {mission.title}\n      <color=#aaaaaa>{mission.currentProgress} / {mission.targetProgress}</color>\n\n";
            }
        }
        else
        {
            tasks += "No active directives.\nReport to station.";
        }

        unionTasksText.text = tasks;
    }

    public void ShowNotification(string message)
    {
        if (notificationPanel == null) return;
        PlaySFX(notificationSFX);
        StopAllCoroutines();
        StartCoroutine(AnimateNotification(message));
    }

    private IEnumerator AnimateNotification(string message)
    {
        if (notificationText != null) notificationText.text = message;
        
        float time = 0;
        while (time < 1f)
        {
            time += Time.deltaTime * 3f; 
            notificationPanel.anchoredPosition = Vector2.Lerp(new Vector2(0, 200), new Vector2(0, -30), Mathf.SmoothStep(0, 1, time));
            yield return null;
        }

        yield return new WaitForSeconds(3f);

        time = 0;
        while (time < 1f)
        {
            time += Time.deltaTime * 3f;
            notificationPanel.anchoredPosition = Vector2.Lerp(new Vector2(0, -30), new Vector2(0, 200), Mathf.SmoothStep(0, 1, time));
            yield return null;
        }
    }

    private void BuildGameUI()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        canvasObj = new GameObject("GameCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f; // Scaler fix: Match Height avoids vertical clipping on varying aspect ratios
        
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject minimapPanel = CreateUIPanel(canvasObj.transform, "MinimapPanel", new Vector2(20, -20), new Vector2(260, 260), darkBg, TextAnchor.UpperLeft, true);
        GameObject minimapMask = CreateUIPanel(minimapPanel.transform, "MinimapMask", Vector2.zero, new Vector2(250, 250), Color.white, TextAnchor.MiddleCenter, true);
        minimapMask.AddComponent<Mask>();
        
        GameObject mapImageObj = new GameObject("MapRender");
        mapImageObj.transform.SetParent(minimapMask.transform, false);
        RectTransform mapRect = mapImageObj.AddComponent<RectTransform>();
        mapRect.anchoredPosition = Vector2.zero;
        mapRect.sizeDelta = new Vector2(250, 250);
        RawImage ri = mapImageObj.AddComponent<RawImage>();
        ri.texture = minimapTexture;

        GameObject mapTargetIcon = new GameObject("MapTargetIcon");
        mapTargetIcon.transform.SetParent(minimapMask.transform, false);
        targetMapIconRect = mapTargetIcon.AddComponent<RectTransform>();
        targetMapIconRect.anchoredPosition = Vector2.zero;
        targetMapIconRect.sizeDelta = new Vector2(15, 15);
        Image tIcon = mapTargetIcon.AddComponent<Image>();
        tIcon.color = themeColor;
        if (roundedSprite != null) { tIcon.sprite = roundedSprite; tIcon.type = Image.Type.Sliced; }
        targetMapIconRect.gameObject.SetActive(false);

        GameObject mapPlayerIcon = new GameObject("MapPlayerIcon");
        mapPlayerIcon.transform.SetParent(minimapPanel.transform, false);
        playerMapIconRect = mapPlayerIcon.AddComponent<RectTransform>();
        playerMapIconRect.anchoredPosition = Vector2.zero;
        playerMapIconRect.sizeDelta = new Vector2(40, 40);
        Text pIcon = mapPlayerIcon.AddComponent<Text>();
        pIcon.text = "▲";
        pIcon.font = defaultFont;
        pIcon.fontSize = 32;
        pIcon.color = Color.red; 
        pIcon.alignment = TextAnchor.MiddleCenter;

        // TOP BAR
        GameObject topBar = CreateUIPanel(canvasObj.transform, "TopBar", Vector2.zero, new Vector2(0, 80), darkBg, TextAnchor.UpperCenter, true);
        RectTransform tbRect = topBar.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0, 1);
        tbRect.anchorMax = new Vector2(1, 1);
        tbRect.offsetMin = new Vector2(290, -100); // Pad from left to accommodate minimap
        tbRect.offsetMax = new Vector2(-20, -20);  // Pad from right
        
        GameObject routeBlock = CreateUIPanel(topBar.transform, "RouteBg", new Vector2(20, 0), new Vector2(500, 60), lightBg, TextAnchor.MiddleLeft, true);
        hudRouteText = CreateUIText(routeBlock.transform, "RouteTxt", GetRouteName(selectedRouteIndex), new Vector2(0, 0), new Vector2(480, 60), 18, FontStyle.Bold, themeColor, TextAnchor.MiddleCenter);

        GameObject passBlock = CreateUIPanel(topBar.transform, "PassBg", new Vector2(540, 0), new Vector2(200, 60), lightBg, TextAnchor.MiddleLeft, true);
        CreateImageElement(passBlock.transform, "PassIcon", passengerIcon, new Vector2(-70, 0), new Vector2(30, 30), themeColor);
        hudPassengerText = CreateUIText(passBlock.transform, "PassTxt", "0/15", new Vector2(20, 0), new Vector2(120, 60), 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        GameObject fuelBlock = CreateUIPanel(topBar.transform, "FuelBg", new Vector2(760, 0), new Vector2(260, 60), lightBg, TextAnchor.MiddleLeft, true);
        CreateImageElement(fuelBlock.transform, "FuelIcon", fuelIcon, new Vector2(-100, 0), new Vector2(30, 30), Color.white);
        GameObject fBg = CreateUIPanel(fuelBlock.transform, "FBg", new Vector2(20, 0), new Vector2(180, 20), darkBg, TextAnchor.MiddleCenter, true);
        GameObject fFill = CreateUIPanel(fBg.transform, "FFill", Vector2.zero, new Vector2(180, 20), themeColor, TextAnchor.MiddleLeft, true);
        fuelBarFill = fFill.GetComponent<RectTransform>();

        GameObject timerBlock = CreateUIPanel(topBar.transform, "TimerBg", new Vector2(1040, 0), new Vector2(150, 60), lightBg, TextAnchor.MiddleLeft, true);
        hudTimerText = CreateUIText(timerBlock.transform, "TimerTxt", "<size=18>TIME LEFT</size>\n00:00", Vector2.zero, new Vector2(150, 60), 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        GameObject cashBlock = CreateUIPanel(topBar.transform, "CashBg", new Vector2(-100, 0), new Vector2(260, 60), lightBg, TextAnchor.MiddleRight, true);
        CreateImageElement(cashBlock.transform, "CashIcon", cashIcon, new Vector2(-90, 0), new Vector2(35, 35), new Color(0.15f, 0.85f, 0.4f));
        hudCashText = CreateUIText(cashBlock.transform, "CashTxt", "0 GHS", new Vector2(20, 0), new Vector2(150, 60), 24, FontStyle.Bold, new Color(0.15f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

        CreateTextButton(topBar.transform, "PauseBtn", "II", new Vector2(-20, 0), new Vector2(60, 60), TogglePause, themeColor, darkBg, 24, TextAnchor.MiddleRight);

        GameObject unionBorder = CreateUIPanel(canvasObj.transform, "UnionBorder", new Vector2(20, -300), new Vector2(340, 320), themeColor, TextAnchor.UpperLeft, true);
        GameObject unionBg = CreateUIPanel(unionBorder.transform, "UnionBg", new Vector2(0, 0), new Vector2(334, 314), darkBg, TextAnchor.MiddleCenter, true);
        unionTasksText = CreateUIText(unionBg.transform, "UnionTxt", "TASKS", new Vector2(15, -15), new Vector2(304, 284), 18, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);

        refuelButtonObj = CreateTextButton(canvasObj.transform, "RefuelBtn", "BUY FUEL\n(10 GHS)", new Vector2(0, 300), new Vector2(250, 80), BuyFuel, themeColor, darkBg, 20, TextAnchor.LowerCenter);
        refuelButtonObj.SetActive(false);

        loadingPhasePanel = CreateUIPanel(canvasObj.transform, "LoadingPhasePanel", new Vector2(0, 100), new Vector2(600, 150), darkBg, TextAnchor.MiddleCenter, true);
        loadingPhaseText = CreateUIText(loadingPhasePanel.transform, "LoadPhaseTxt", "WAITING AT STATION\nLoading Passengers...", Vector2.zero, new Vector2(580, 130), 28, FontStyle.Bold, themeColor, TextAnchor.MiddleCenter);
        loadingPhasePanel.SetActive(false);

        GameObject dashboardLip = CreateUIPanel(canvasObj.transform, "DashLip", new Vector2(0, 0), new Vector2(0, 20), darkBg, TextAnchor.LowerCenter, false);
        RectTransform dlRect = dashboardLip.GetComponent<RectTransform>();
        dlRect.anchorMin = new Vector2(0, 0); dlRect.anchorMax = new Vector2(1, 0);
        dlRect.offsetMin = new Vector2(0, 0); dlRect.offsetMax = new Vector2(0, 20); // Stretch across bottom
        CreateUIPanel(dashboardLip.transform, "DashAccent", new Vector2(0, 10), new Vector2(0, 4), themeColor, TextAnchor.MiddleCenter, false).GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.5f);
        dashboardLip.transform.Find("DashAccent").GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.5f);
        dashboardLip.transform.Find("DashAccent").GetComponent<RectTransform>().offsetMin = new Vector2(0, -2);
        dashboardLip.transform.Find("DashAccent").GetComponent<RectTransform>().offsetMax = new Vector2(0, 2);

        GameObject speedConsole = CreateUIPanel(canvasObj.transform, "SpeedConsole", new Vector2(0, 40), new Vector2(350, 150), Color.clear, TextAnchor.LowerCenter, false);
        if (dashBgIcon != null) CreateImageElement(speedConsole.transform, "DashBG", dashBgIcon, Vector2.zero, new Vector2(350, 150), Color.white);
        
        speedText = CreateUIText(speedConsole.transform, "SpeedTxt", "0\n<size=18>KM/H</size>\n<color=#aaaaaa><size=28>[ P ]</size></color>", new Vector2(0, 20), new Vector2(300, 120), 64, FontStyle.BoldAndItalic, Color.white, TextAnchor.MiddleCenter);
        
        GameObject sBarBg = CreateUIPanel(speedConsole.transform, "SBarBg", new Vector2(0, 0), new Vector2(300, 8), new Color(0.1f, 0.1f, 0.1f, 0.5f), TextAnchor.LowerCenter, true);
        GameObject sBarFillObj = CreateUIPanel(sBarBg.transform, "SBarFill", new Vector2(-150, 0), new Vector2(0, 8), themeColor, TextAnchor.MiddleLeft, true);
        speedBarFill = sBarFillObj.GetComponent<RectTransform>();

        // MOBILE CONTROLS
        mobileControlsPanel = CreateUIPanel(canvasObj.transform, "MobileControls", new Vector2(0, 0), new Vector2(0, 300), Color.clear, TextAnchor.LowerCenter, false);
        RectTransform mcRect = mobileControlsPanel.GetComponent<RectTransform>();
        mcRect.anchorMin = new Vector2(0, 0); mcRect.anchorMax = new Vector2(1, 0);
        mcRect.offsetMin = new Vector2(0, 0); mcRect.offsetMax = new Vector2(0, 300);

        GameObject leftBtn = CreateIconButton(mobileControlsPanel.transform, "LeftBtn", steerLeftIcon, new Vector2(120, 40), new Vector2(160, 160), lightBg, TextAnchor.LowerLeft);
        AddPointerEvents(leftBtn, () => mobileSteer = -1f, () => mobileSteer = 0f);
        
        GameObject rightBtn = CreateIconButton(mobileControlsPanel.transform, "RightBtn", steerRightIcon, new Vector2(300, 40), new Vector2(160, 160), lightBg, TextAnchor.LowerLeft);
        AddPointerEvents(rightBtn, () => mobileSteer = 1f, () => mobileSteer = 0f);

        GameObject brakeBtn = CreateIconButton(mobileControlsPanel.transform, "BrakeBtn", brakeIcon, new Vector2(-300, 40), new Vector2(160, 140), new Color(0.85f, 0.15f, 0.15f, 0.85f), TextAnchor.LowerRight);
        AddPointerEvents(brakeBtn, () => mobileBrake = true, () => mobileBrake = false);
        
        GameObject gasBtn = CreateIconButton(mobileControlsPanel.transform, "GasBtn", gasIcon, new Vector2(-120, 40), new Vector2(160, 200), new Color(0.15f, 0.85f, 0.4f, 0.85f), TextAnchor.LowerRight);
        AddPointerEvents(gasBtn, () => mobileGasPedal = 1f, () => mobileGasPedal = 0f);

        GameObject gearBtn = CreateIconButton(mobileControlsPanel.transform, "GearBtn", null, new Vector2(-480, 40), new Vector2(100, 140), lightBg, TextAnchor.LowerRight);
        gearText = CreateUIText(gearBtn.transform, "GearTxt", "D", Vector2.zero, new Vector2(100, 140), 48, FontStyle.Bold, themeColor, TextAnchor.MiddleCenter);
        AddPointerEvents(gearBtn, ToggleGear, () => {});

        GameObject notifObj = CreateUIPanel(canvasObj.transform, "NotificationBanner", new Vector2(0, 200), new Vector2(800, 100), darkBg, TextAnchor.UpperCenter, true);
        CreateUIPanel(notifObj.transform, "NotifAccent", new Vector2(0, -48), new Vector2(780, 4), themeColor, TextAnchor.MiddleCenter, true);
        notificationPanel = notifObj.GetComponent<RectTransform>();
        notificationText = CreateUIText(notifObj.transform, "NotifText", "Task Completed!", new Vector2(0, 5), new Vector2(780, 100), 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        
        pausePanel = CreateFullScreenPanel(canvasObj.transform, "PausePanel", new Color(0, 0, 0, 0.9f));
        GameObject pBox = CreateUIPanel(pausePanel.transform, "PauseBox", Vector2.zero, new Vector2(500, 400), darkBg, TextAnchor.MiddleCenter, true);
        CreateUIText(pBox.transform, "PTitle", "GAME PAUSED", new Vector2(0, 120), new Vector2(400, 50), 36, FontStyle.Bold, themeColor, TextAnchor.MiddleCenter);
        CreateTextButton(pBox.transform, "ResumeBtn", "RESUME DRIVE", new Vector2(0, 10), new Vector2(350, 70), TogglePause, themeColor, darkBg, 24, TextAnchor.MiddleCenter);
        CreateTextButton(pBox.transform, "EndShiftBtn", "END SHIFT", new Vector2(0, -90), new Vector2(350, 70), EndShiftPrematurely, new Color(0.8f, 0.1f, 0.1f, 1f), Color.white, 24, TextAnchor.MiddleCenter);
        pausePanel.SetActive(false);

        successModal = CreateFullScreenPanel(canvasObj.transform, "SuccessModal", new Color(0, 0, 0, 0.95f));
        GameObject smBox = CreateUIPanel(successModal.transform, "SMBox", Vector2.zero, new Vector2(650, 750), darkBg, TextAnchor.MiddleCenter, true);
        
        smTitleText = CreateUIText(smBox.transform, "SMTitle", "SHIFT COMPLETED", new Vector2(0, 300), new Vector2(600, 100), 42, FontStyle.Bold, themeColor, TextAnchor.MiddleCenter);
        
        CreateUIText(smBox.transform, "LblGross", "Gross Fares Collected:", new Vector2(-120, 180), new Vector2(300, 40), 22, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        smGrossText = CreateUIText(smBox.transform, "ValGross", "+ 0 GHS", new Vector2(120, 180), new Vector2(300, 40), 26, FontStyle.Bold, new Color(0.15f, 0.85f, 0.4f), TextAnchor.MiddleRight);

        CreateUIText(smBox.transform, "LblMate", "Mate's Cut:", new Vector2(-120, 110), new Vector2(300, 40), 22, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        smMateCutText = CreateUIText(smBox.transform, "ValMate", "- 0 GHS", new Vector2(120, 110), new Vector2(300, 40), 26, FontStyle.Bold, new Color(0.85f, 0.15f, 0.15f), TextAnchor.MiddleRight);

        CreateUIText(smBox.transform, "LblMiss", "Mission Bonus:", new Vector2(-120, 40), new Vector2(300, 40), 22, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        smMissionText = CreateUIText(smBox.transform, "ValMiss", "+ 0 GHS", new Vector2(120, 40), new Vector2(300, 40), 26, FontStyle.Bold, themeColor, TextAnchor.MiddleRight);

        CreateUIPanel(smBox.transform, "Div", new Vector2(0, -20), new Vector2(550, 4), themeColor, TextAnchor.MiddleCenter, false);

        CreateUIText(smBox.transform, "LblNet", "NET EARNINGS:", new Vector2(-120, -70), new Vector2(300, 50), 28, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        smNetText = CreateUIText(smBox.transform, "ValNet", "0 GHS", new Vector2(120, -70), new Vector2(300, 50), 36, FontStyle.Bold, new Color(0.15f, 0.85f, 0.4f), TextAnchor.MiddleRight);

        smLevelText = CreateUIText(smBox.transform, "SMLvlTxt", "LVL 1", new Vector2(-200, -150), new Vector2(100, 40), 24, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        smEarnedXPText = CreateUIText(smBox.transform, "SMEarnedXP", "+ 0 XP", new Vector2(200, -150), new Vector2(150, 40), 24, FontStyle.Bold, themeColor, TextAnchor.MiddleRight);
        
        GameObject smXPBarBg = CreateUIPanel(smBox.transform, "SMXPBarBg", new Vector2(0, -190), new Vector2(500, 20), lightBg, TextAnchor.MiddleCenter, true);
        GameObject smXPBarFillObj = CreateUIPanel(smXPBarBg.transform, "SMXPBarFill", Vector2.zero, new Vector2(0, 20), themeColor, TextAnchor.MiddleLeft, true);
        smXPBarFill = smXPBarFillObj.GetComponent<RectTransform>();

        CreateTextButton(smBox.transform, "SMRtnBtn", "RETURN TO TERMINAL", new Vector2(0, -280), new Vector2(450, 70), ReturnToTerminal, themeColor, darkBg, 24, TextAnchor.MiddleCenter);
        successModal.SetActive(false);

        loadingPanel = CreateFullScreenPanel(canvasObj.transform, "LoadingPanel", darkBg);
        CanvasGroup loadCg = loadingPanel.AddComponent<CanvasGroup>();
        loadCg.alpha = 0f;

        GameObject loadImgObj = CreateFullScreenPanel(loadingPanel.transform, "LoadingImg", darkBg);
        Image imgComp = loadImgObj.GetComponent<Image>();
        if (loadingScreenImage != null)
        {
            imgComp.sprite = Sprite.Create(loadingScreenImage, new Rect(0, 0, loadingScreenImage.width, loadingScreenImage.height), new Vector2(0.5f, 0.5f));
            imgComp.color = Color.white;
            imgComp.preserveAspect = false;
        }

        // Loading states moved up to avoid clipping
        loadingDescText = CreateUIText(loadingPanel.transform, "LoadTxt", "RETURNING...", new Vector2(0, -260), new Vector2(800, 50), 36, FontStyle.Bold, themeColor, TextAnchor.MiddleCenter);
        
        GameObject pBarBg = CreateUIPanel(loadingPanel.transform, "ProgressBg", new Vector2(0, -320), new Vector2(800, 30), darkBg, TextAnchor.MiddleCenter, true);
        GameObject pBarFill = CreateUIPanel(pBarBg.transform, "ProgressFill", Vector2.zero, new Vector2(0, 30), themeColor, TextAnchor.MiddleLeft, true);
        loadingFillRect = pBarFill.GetComponent<RectTransform>();
        
        loadingPercentText = CreateUIText(loadingPanel.transform, "PercentText", "0%", new Vector2(0, -370), new Vector2(200, 50), 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        loadingPanel.SetActive(false);

        if (SystemInfo.deviceType == DeviceType.Desktop) isUsingKeyboard = true;
        mobileControlsPanel.SetActive(!isUsingKeyboard);
    }

    private GameObject CreateUIPanel(Transform parent, string name, Vector2 pos, Vector2 size, Color color, TextAnchor anchor, bool rounded)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        SetAnchor(rect, anchor);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image img = obj.AddComponent<Image>();
        img.color = color;
        
        if (color == Color.clear) img.raycastTarget = false;
        if (rounded && roundedSprite != null) { img.sprite = roundedSprite; img.type = Image.Type.Sliced; }
        return obj;
    }

    private GameObject CreateFullScreenPanel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        Image img = obj.AddComponent<Image>();
        img.color = color;
        return obj;
    }

    private GameObject CreateImageElement(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image img = obj.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return obj;
    }

    private Text CreateUIText(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, FontStyle style, Color color, TextAnchor align)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Text t = obj.AddComponent<Text>();
        t.text = text;
        t.font = defaultFont;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        
        Shadow s = obj.AddComponent<Shadow>();
        s.effectColor = new Color(0,0,0,0.8f);
        s.effectDistance = new Vector2(2, -2);
        return t;
    }

    private GameObject CreateIconButton(Transform parent, string name, Sprite icon, Vector2 pos, Vector2 size, Color bgColor, TextAnchor anchor)
    {
        GameObject obj = CreateUIPanel(parent, name, pos, size, bgColor, anchor, true);
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(obj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(size.x * 0.6f, size.y * 0.6f); 
        Image img = iconObj.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false; 
        
        if (icon != null) { img.sprite = icon; img.color = Color.white; }
        else { img.color = new Color(1, 1, 1, 0.1f); CreateUIText(iconObj.transform, "PlaceholderTxt", "IMG\n<size=12>Missing</size>", Vector2.zero, iconRect.sizeDelta, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter); }
        return obj;
    }

    private GameObject CreateTextButton(Transform parent, string name, string labelText, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClickAction, Color bgColor, Color textColor, int fontSize, TextAnchor anchor)
    {
        GameObject btnObj = CreateUIPanel(parent, name, pos, size, bgColor, anchor, true);
        Button buttonComp = btnObj.AddComponent<Button>();
        buttonComp.targetGraphic = btnObj.GetComponent<Image>();
        if (onClickAction != null) { buttonComp.onClick.AddListener(onClickAction); buttonComp.onClick.AddListener(() => PlaySFX(btnClickSFX)); }
        CreateUIText(btnObj.transform, "Text", labelText, Vector2.zero, size, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        return btnObj;
    }

    private void AddPointerEvents(GameObject obj, UnityEngine.Events.UnityAction onDown, UnityEngine.Events.UnityAction onUp)
    {
        MobileButton mb = obj.AddComponent<MobileButton>();
        mb.onDown = onDown;
        mb.onUp = onUp;
    }

    private void SetAnchor(RectTransform rect, TextAnchor anchor)
    {
        Vector2 min = Vector2.zero, max = Vector2.zero, pivot = Vector2.zero;
        switch (anchor)
        {
            case TextAnchor.UpperLeft: min = max = new Vector2(0, 1); pivot = new Vector2(0, 1); break;
            case TextAnchor.UpperCenter: min = max = new Vector2(0.5f, 1); pivot = new Vector2(0.5f, 1); break;
            case TextAnchor.UpperRight: min = max = new Vector2(1, 1); pivot = new Vector2(1, 1); break;
            case TextAnchor.MiddleLeft: min = max = new Vector2(0, 0.5f); pivot = new Vector2(0, 0.5f); break;
            case TextAnchor.MiddleCenter: min = max = new Vector2(0.5f, 0.5f); pivot = new Vector2(0.5f, 0.5f); break;
            case TextAnchor.MiddleRight: min = max = new Vector2(1, 0.5f); pivot = new Vector2(1, 0.5f); break;
            case TextAnchor.LowerLeft: min = max = new Vector2(0, 0); pivot = new Vector2(0, 0); break;
            case TextAnchor.LowerCenter: min = max = new Vector2(0.5f, 0); pivot = new Vector2(0.5f, 0); break;
            case TextAnchor.LowerRight: min = max = new Vector2(1, 0); pivot = new Vector2(1, 0); break;
        }
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
    }
}

public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public UnityEngine.Events.UnityAction onDown;
    public UnityEngine.Events.UnityAction onUp;
    private bool isPressed = false;

    public void OnPointerDown(PointerEventData eventData) { isPressed = true; if (onDown != null) onDown(); }
    public void OnPointerUp(PointerEventData eventData) { if (isPressed) { isPressed = false; if (onUp != null) onUp(); } }
    public void OnPointerExit(PointerEventData eventData) { if (isPressed) { isPressed = false; if (onUp != null) onUp(); } }
}