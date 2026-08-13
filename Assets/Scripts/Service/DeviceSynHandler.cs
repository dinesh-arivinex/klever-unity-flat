// LATEST UPDATE: MOCK CONTROL AND DETAILED LOGGING PIPELINE ACTIVE
using SimpleJSON;
using System;
using System.Collections;
using Klass.Net;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpaceData
{
    public string experienceId;
    public string schoolId;
    public string sectionId;
    public string gradeId;
}

/// <summary>
/// MQTT-driven replacement for the previous 2s polling loop.
/// Public surface (fields, methods, statics) is preserved so that
/// LoadingBar / ServiceScript / GameManager / API continue to compile.
/// </summary>
public class DeviceSynHandler : MonoBehaviour
{
    [System.Serializable]
    public class Data
    {
        public string schoolId;
        public bool isActive;
        public bool isSynced;
        public bool isCompleted;
    }

    [System.Serializable]
    public class Response
    {
        public Data data;
        public string message;
    }

    public class DeviceStatus
    {
        public bool isActive;
        public bool isSynced;
        public bool isCompleted;
    }

    public class SynchDevice
    {
        public string deviceId;
        public bool isCompleted;
    }

    // Preserved public surface
    private bool isSync;
    public bool isActiveDevice;
    public bool isSyncedDevice;
    public bool isCompletedDevice;
    public static bool isOpenWelcomeScreen;
    public GameObject WelcomeScene;
    private API apiManager;
    private bool isStartGame;
    public bool isOpenGame;
    public LoadingBar loadingBar;
    public static bool isGameOver;
    public bool isOpening;
    private bool isStartButtonPressed;
    public GamificationManager gamificationManager;
    private bool isStopCalling;
    private bool isFirstTime;
    private bool isLobby = true;
    private bool _isPaused;

    private bool _mqttBooted;
    private string _lastPublishedFingerprint;
    private const float HeartbeatSeconds = 30f;
    private float _heartbeatTimer;
    private int _downloadProgress; // 0..100, set by LoadingBar via PublishDownloadProgress
    // Hand-raise / help button state. Set by HelpButton via RaiseHand()/LowerHand();
    // cleared by the teacher dashboard via a `clearHand` MQTT command.
    private bool _handRaised;
    public bool IsHandRaised => _handRaised;

    void Start()
    {
        isFirstTime = true;
        apiManager = GameManager.Instance.api;
        isActiveDevice = false;
        isOpenWelcomeScreen = true;
        isGameOver = false;
        BootMqtt();
    }

    void OnDestroy()
    {
        if (KlassMqttClient.Instance != null)
        {
            KlassMqttClient.Instance.OnConnected -= HandleMqttConnected;
            KlassMqttClient.Instance.OnDisconnected -= HandleMqttDisconnected;
            KlassMqttClient.Instance.OnSessionControl -= HandleSessionControl;
            KlassMqttClient.Instance.OnCommand -= HandleCommand;
        }
    }

    void Update()
    {
        _heartbeatTimer += Time.deltaTime;
        if (_heartbeatTimer >= HeartbeatSeconds)
        {
            _heartbeatTimer = 0f;
            PublishStateIfChanged(force: true);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.S))
        {
            UnityEngine.Debug.Log("[DeviceSynHandler] Simulated manual Start command via keyboard 'S'.");
            var mockPayload = new JSONObject();
            mockPayload["isStart"] = true;
            mockPayload["isStop"] = false;
            mockPayload["experienceId"] = "simulated_id";
            mockPayload["schoolId"] = "simulated_school";
            mockPayload["sectionId"] = "simulated_section";
            mockPayload["gradeId"] = "simulated_grade";
            HandleSessionControl(mockPayload);
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            UnityEngine.Debug.Log("[DeviceSynHandler] Simulated manual Stop command via keyboard 'X'.");
            var mockPayload = new JSONObject();
            mockPayload["isStart"] = false;
            mockPayload["isStop"] = true;
            mockPayload["experienceId"] = "simulated_id";
            HandleSessionControl(mockPayload);
        }
#endif
    }

    private void BootMqtt()
    {
        if (_mqttBooted) return;
        _mqttBooted = true;
        if (KlassMqttClient.Instance == null)
        {
            var go = new GameObject("[KlassMqttClient]");
            go.AddComponent<KlassMqttClient>();
        }
        var c = KlassMqttClient.Instance;
        c.OnConnected += HandleMqttConnected;
        c.OnDisconnected += HandleMqttDisconnected;
        c.OnSessionControl += HandleSessionControl;
        c.OnCommand += HandleCommand;
        c.Begin();
    }

    private void HandleMqttConnected()
    {
        isActiveDevice = true;
        PublishStateIfChanged(force: true);
    }

    private void HandleMqttDisconnected()
    {
        UnityEngine.Debug.Log("[DeviceSynHandler] MQTT Disconnected.");
        isActiveDevice = false;
    }

    private void HandleSessionControl(JSONNode payload)
    {
        UnityEngine.Debug.Log($"[DeviceSynHandler] HandleSessionControl called with payload: {payload.ToString()}");
        if (payload == null || payload.Count == 0)
        {
            UnityEngine.Debug.LogWarning("[DeviceSynHandler] HandleSessionControl: Payload empty/null. Reloading scene...");
            if (!isLobby) LoadScene();
            return;
        }
        bool isStart = payload["isStart"].AsBool;
        bool isStop = payload["isStop"].AsBool;

        UnityEngine.Debug.Log($"[DeviceSynHandler] Session Control Status: isStart={isStart}, isStop={isStop}, isOpenGame={isOpenGame}");

        if (isFirstTime && !string.IsNullOrEmpty(payload["gradeId"]))
        {
            experienceId = payload["experienceId"];
            string schoolId = payload["schoolId"];
            string sectionId = payload["sectionId"];
            string gradeId = payload["gradeId"];
            UnityEngine.Debug.Log($"[DeviceSynHandler] Initializing Student Performance references with ExperienceID={experienceId}, SchoolID={schoolId}, SectionID={sectionId}, GradeID={gradeId}");
            if (gamificationManager != null)
                gamificationManager.StudentPerformance(experienceId, schoolId, sectionId, gradeId);
            Klass.Net.LiveViewPublisher.Instance?.SetExperienceId(experienceId);
            isFirstTime = false;
        }

        isStartButtonPressed = isStart;
        if (isStart && !isOpenGame)
        {
            UnityEngine.Debug.Log("[DeviceSynHandler] Starting experience activation flow...");
            isOpening = true;
            isLobby = false;

            // Step 1: Enable the Welcome Scene panel immediately when Start is pressed.
            // This also sets isOpenWelcomeScreen = false, which is required by API.cs
            // to allow processing the experience response (breaking the circular
            // dependency where download couldn't start without the welcome screen
            // being shown, and the welcome screen couldn't show without isSync=true).
            isOpenWelcomeScreen = false;
            UnityEngine.Debug.Log($"[DeviceSynHandler] Enabling WelcomeScene. WelcomeScene ref: {WelcomeScene != null}, gamification welcomeSessionScene ref: {((gamificationManager != null) ? (gamificationManager.welcomeSessionScene != null).ToString() : "null")}");
            if (WelcomeScene != null) WelcomeScene.SetActive(true);
            if (gamificationManager != null
                && gamificationManager.welcomeSessionScene != null
                && gamificationManager.welcomeSessionScene != WelcomeScene)
                gamificationManager.welcomeSessionScene.SetActive(true);

            // Step 2: Show the loading bar UI (hides black sphere, reveals progress bar).
            if (loadingBar != null)
            {
                try
                {
                    UnityEngine.Debug.Log("[DeviceSynHandler] Calling loadingBar.SynchDevice()");
                    loadingBar.SynchDevice();
                }
                catch (System.Exception e) { UnityEngine.Debug.LogWarning("[DeviceSynHandler] LoadingBar.SynchDevice error (non-fatal): " + e.Message); }
            }
            else
            {
                UnityEngine.Debug.LogWarning("[DeviceSynHandler] loadingBar reference is null!");
            }

            // Step 3: Fetch experience data. Now allowed because isOpenWelcomeScreen=false.
            // API.cs will process the response, trigger asset downloads, run the loading
            // bar progress coroutine, then call SynDevice() → TTS → experience flow.
            if (apiManager != null)
            {
                if (!apiManager.isLoadedResponse)
                {
                    UnityEngine.Debug.Log("[DeviceSynHandler] Requesting experience details via apiManager.CallingClassVrExperiance()");
                    apiManager.CallingClassVrExperiance();
                }
                else
                {
                    UnityEngine.Debug.Log("[DeviceSynHandler] Experience already loaded (isLoadedResponse = true). skipping Web Request.");
                }
            }
            else
            {
                UnityEngine.Debug.LogError("[DeviceSynHandler] apiManager reference is null! Cannot fetch experience data.");
            }

            isOpenGame = true;
        }

        // Handle play / pause for a running session.
        bool isPaused = payload["isPaused"].AsBool;
        if (isStart && isOpenGame)
        {
            if (isPaused && !_isPaused)
            {
                UnityEngine.Debug.Log("[DeviceSynHandler] Pausing VR Experience.");
                _isPaused = true;
                _lastPublishedFingerprint = string.Empty; // force heartbeat republish
                PublishStateIfChanged(force: true);
                PauseVR();
            }
            else if (!isPaused && _isPaused)
            {
                UnityEngine.Debug.Log("[DeviceSynHandler] Resuming VR Experience.");
                _isPaused = false;
                _lastPublishedFingerprint = string.Empty;
                PublishStateIfChanged(force: true);
                ResumeVR();
            }
        }

        if (isStop && isOpening && !string.IsNullOrEmpty(payload["experienceId"]) && !isStopCalling)
        {
            UnityEngine.Debug.Log("[DeviceSynHandler] Stop session event received. Loading lobby scene...");
            isStartButtonPressed = false;
            isStopCalling = true;
        }
    }

    public void TriggerWebGLAutoStart()
    {
        if (isOpenGame) return;

        UnityEngine.Debug.Log("[DeviceSynHandler] Triggering WebGL auto-start experience activation UI flow.");
        isOpening = true;
        isLobby = false;
        isOpenWelcomeScreen = false;
        isStartButtonPressed = true;

        if (WelcomeScene != null) WelcomeScene.SetActive(true);
        if (gamificationManager != null
            && gamificationManager.welcomeSessionScene != null
            && gamificationManager.welcomeSessionScene != WelcomeScene)
            gamificationManager.welcomeSessionScene.SetActive(true);

        if (loadingBar != null)
        {
            try
            {
                UnityEngine.Debug.Log("[DeviceSynHandler] Calling loadingBar.SynchDevice() for WebGL auto-start.");
                loadingBar.SynchDevice();
            }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning("[DeviceSynHandler] LoadingBar.SynchDevice error (non-fatal): " + e.Message); }
        }
        else
        {
            UnityEngine.Debug.LogWarning("[DeviceSynHandler] loadingBar reference is null!");
        }

        isOpenGame = true;
    }

    private void HandleCommand(JSONNode cmd)
    {
        if (cmd == null) return;
        string action = cmd["action"];
        string msgId = cmd["msgId"];
        KlassMqttClient.Instance?.PublishCmdAck(msgId, ok: true);
        // "stop" reloads the scene (returns to lobby); "disconnect" does the same
        // and is sent when a teacher explicitly removes a device mid-session.
        if (action == "stop" || action == "disconnect") LoadScene();
        // "clearHand" is sent when the teacher acknowledges a hand raise from
        // the live dashboard. Lower the hand locally and re-publish state so
        // the retained MQTT message no longer carries handRaised:true.
        else if (action == "clearHand") LowerHand();
    }

    public void ActiveDeviceStatus() => PublishStateIfChanged(force: false);

    private void PauseVR()
    {
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (VideoManager.Instance != null)
        {
            if (VideoManager.Instance.mediaPlayer != null && VideoManager.Instance.mediaPlayer.Control != null)
            {
                if (VideoManager.Instance.mediaPlayer.Control.IsPlaying())
                {
                    VideoManager.Instance.mediaPlayer.Control.Pause();
                }
            }

            if (VideoManager.Instance.youtubePlayer != null)
            {
                var vp = VideoManager.Instance.youtubePlayer.GetComponent<UnityEngine.Video.VideoPlayer>();
                if (vp != null && vp.isPlaying)
                {
                    vp.Pause();
                }
            }
        }
    }

    private void ResumeVR()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (VideoManager.Instance != null)
        {
            if (VideoManager.Instance.mediaPlayer != null && VideoManager.Instance.mediaPlayer.Control != null)
            {
                if (VideoManager.Instance.mediaPlayer.Control.IsPaused())
                {
                    VideoManager.Instance.mediaPlayer.Control.Play();
                }
            }

            if (VideoManager.Instance.youtubePlayer != null)
            {
                var vp = VideoManager.Instance.youtubePlayer.GetComponent<UnityEngine.Video.VideoPlayer>();
                if (vp != null && vp.isPaused)
                {
                    vp.Play();
                }
            }
        }
    }
    public void SynDevice()
    {
        isSync = true;
        isStartGame = true;
        isSyncedDevice = true;
        PublishStateIfChanged(force: true);
        // Ensure timeScale and audio are reset when returning to lobby
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (isStartButtonPressed) TriggerWelcomeIfReady();
    }

    public void ApplicationStatus()
    {
        DebugUtil.Log("reg the account");
    }

    public void LoadScene()
    {
        isGameOver = true;
        isCompletedDevice = true;
        PublishStateIfChanged(force: true);
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }

    private void TriggerWelcomeIfReady()
    {
        if (!isStartButtonPressed || !isActiveDevice || !isSync || !isOpenWelcomeScreen) return;
        isLobby = false;
        isOpenWelcomeScreen = false;
        if (WelcomeScene != null) WelcomeScene.SetActive(true);
        if (gamificationManager != null && gamificationManager.welcomeSessionScene != null && gamificationManager.welcomeSessionScene != WelcomeScene)
            gamificationManager.welcomeSessionScene.SetActive(true);
        if (apiManager != null && apiManager.isLoadedResponse == false)
            apiManager.CallingClassVrExperiance();
    }

    private void PublishStateIfChanged(bool force)
    {
        var mqtt = KlassMqttClient.Instance;
        if (mqtt == null || !mqtt.IsConnected) return;

        bool active = true;
        bool synced = isSync;
        bool completed = isGameOver;
        int progress = synced ? 100 : Mathf.Clamp(_downloadProgress, 0, 100);
        string playState;
        if (completed) playState = "completed";
        else if (_isPaused) playState = "paused";
        else if (synced) playState = "playing";
        else if (progress > 0) playState = "downloading";
        else playState = "idle";

        // Include pause + progress in the fingerprint so any change triggers a publish.
        var fp = $"{active}|{synced}|{completed}|{progress}|{playState}|{_isPaused}|{_handRaised}|{experienceId}";
        if (!force && fp == _lastPublishedFingerprint) return;
        _lastPublishedFingerprint = fp;

        mqtt.PublishStateWithProgress(active, synced, completed, progress, playState, experienceId, _handRaised);

        isActiveDevice = active;
        isSyncedDevice = synced;
        isCompletedDevice = completed;
    }

    /// <summary>
    /// Called by LoadingBar (and any other asset pipeline) every time the
    /// download progress changes. Throttled internally: we only re-publish
    /// when the integer percent changes, to avoid flooding the broker.
    /// </summary>
    public void PublishDownloadProgress(float percent01)
    {
        int p = Mathf.Clamp(Mathf.RoundToInt(percent01 * 100f), 0, 100);
        if (p == _downloadProgress) return;
        _downloadProgress = p;
        PublishStateIfChanged(force: false);
    }

    /// <summary>
    /// Report a download failure to the teacher dashboard via MQTT.
    /// The error badge on the device card will show the message.
    /// </summary>
    public void PublishDownloadError(string errorMessage)
    {
        _lastPublishedFingerprint = string.Empty; // force next call to re-publish
        KlassMqttClient.Instance?.PublishDownloadError(errorMessage);
    }

    // ── Hand-raise / Help button API ───────────────────────────────────────
    /// <summary>
    /// Set the hand-raised state and immediately republish device state so
    /// the teacher dashboard sees the change in real time. No-op if the
    /// state is already what was requested.
    /// </summary>
    public void SetHandRaised(bool raised)
    {
        if (_handRaised == raised) return;
        _handRaised = raised;
        // Force fingerprint reset so the next publish always goes out.
        _lastPublishedFingerprint = string.Empty;
        PublishStateIfChanged(force: true);
    }

    /// <summary>Raise the hand — called by HelpButton.</summary>
    public void RaiseHand() => SetHandRaised(true);

    /// <summary>Lower the hand — called by HelpButton or by the teacher's clearHand command.</summary>
    public void LowerHand() => SetHandRaised(false);

    /// <summary>Toggle the hand-raised state — convenient single-button wiring.</summary>
    public void ToggleHand() => SetHandRaised(!_handRaised);
}
