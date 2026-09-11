using System;
using SimpleJSON;
using UnityEngine;

namespace Klass.Net
{
    /// <summary>Publishes the semantic state required by a live observer.</summary>
    public sealed class LiveViewPublisher : MonoBehaviour
    {
        public const int SchemaVersion = 1;
        public static LiveViewPublisher Instance { get; private set; }
        private JSONObject _snapshot;
        private long _sequence;
        private float _lastPlaybackEventAt = -10f;
        private float _lastTelemetryPlaybackAt = -20f;
        private bool? _lastTelemetryPlaying;
        private float _lastRetainedPlaybackAt = -10f;
        private float _lastTransformEventAt = -10f;
        private string _experienceId = string.Empty;
        private readonly string _clientSessionId = Guid.NewGuid().ToString("N");
        private string _currentMode = string.Empty;
        private string _currentContentId = string.Empty;
        private long _contentStartedAt;
        private long _questionViewedAt;
        private int _currentQuestionIndex = -1;
        private readonly System.Collections.Generic.Dictionary<int, int> _questionAttempts = new System.Collections.Generic.Dictionary<int, int>();
        private readonly System.Collections.Generic.HashSet<int> _answeredQuestions = new System.Collections.Generic.HashSet<int>();
        private bool _wasConnected;
        private bool _hasJoined;
        private bool _sessionPaused;
        private bool _sessionStarted;
        private long _pauseStartedAt;
        private float _telemetryTimer;
        private float _heartbeatFrameTime;
        private int _heartbeatFrameCount;
        private bool _publishingError;
        private bool _previousRunEndedUnexpectedly;
        private const string ActiveRunKey = "klever.telemetry.activeRun";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null || FindObjectOfType<LiveViewPublisher>() != null) return;
            new GameObject("[LiveViewPublisher]").AddComponent<LiveViewPublisher>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += OnLogMessage;
            _previousRunEndedUnexpectedly = PlayerPrefs.GetInt(ActiveRunKey, 0) == 1;
            PlayerPrefs.SetInt(ActiveRunKey, 1);
            PlayerPrefs.Save();
        }

        private void Update()
        {
            var connected = KlassMqttClient.Instance != null && KlassMqttClient.Instance.IsConnected;
            if (connected && !_wasConnected && !string.IsNullOrEmpty(_experienceId))
            {
                PublishLearningEventNow(_hasJoined ? "student_reconnected" : "student_joined");
                _hasJoined = true;
                PublishPreviousRunFailureIfNeeded();
            }
            _wasConnected = connected;
            if (!connected || string.IsNullOrEmpty(_experienceId)) return;
            _telemetryTimer += Time.unscaledDeltaTime;
            _heartbeatFrameTime += Time.unscaledDeltaTime;
            _heartbeatFrameCount++;
            if (_telemetryTimer < 30f) return;
            _telemetryTimer = 0f;
            var heartbeat = PublishLearningEvent("device_heartbeat");
            heartbeat["fps"] = _heartbeatFrameTime > 0f ? _heartbeatFrameCount / _heartbeatFrameTime : 0f;
            heartbeat["memoryMb"] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            heartbeat["batteryLevel"] = SystemInfo.batteryLevel;
            heartbeat["networkReachability"] = Application.internetReachability.ToString();
            PublishTelemetry(heartbeat);
            _heartbeatFrameTime = 0f;
            _heartbeatFrameCount = 0;
        }

        private void OnDestroy() { Application.logMessageReceived -= OnLogMessage; }

        private void OnApplicationQuit()
        {
            PlayerPrefs.SetInt(ActiveRunKey, 0);
            PlayerPrefs.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (string.IsNullOrEmpty(_experienceId)) return;
            PublishLearningEventNow(paused ? "student_left" : "student_reconnected");
            if (!paused) _hasJoined = true;
        }

        public void ShowContent(string mode, string contentId, string assetUrl, JSONNode details = null)
        {
            EndCurrentContent(false);
            _snapshot = Envelope("snapshot");
            _snapshot["mode"] = string.IsNullOrWhiteSpace(mode) ? "unknown" : mode;
            _snapshot["contentId"] = contentId ?? string.Empty;
            _snapshot["assetUrl"] = assetUrl ?? string.Empty;
            _snapshot["status"] = "visible";
            if (details != null) _snapshot["details"] = details;
            _currentMode = _snapshot["mode"].Value;
            _currentContentId = contentId ?? string.Empty;
            _contentStartedAt = Now();
            PublishSnapshot();
            var started = PublishLearningEvent("content_started");
            started["mode"] = _currentMode; started["contentId"] = _currentContentId; PublishTelemetry(started);
        }

        public void SetExperienceId(string experienceId)
        {
            var normalizedExperienceId = experienceId ?? string.Empty;
            var changed = _experienceId != normalizedExperienceId;
            if (changed)
            {
                _hasJoined = false; _sessionStarted = false; _sessionPaused = false; _currentQuestionIndex = -1;
                _questionAttempts.Clear(); _answeredQuestions.Clear();
            }
            _experienceId = normalizedExperienceId;
            if (changed && !string.IsNullOrEmpty(_experienceId) && KlassMqttClient.Instance != null && KlassMqttClient.Instance.IsConnected)
            {
                PublishLearningEventNow("student_joined"); _hasJoined = true; _wasConnected = true;
                PublishPreviousRunFailureIfNeeded();
            }
        }

        public void HideContent(string reason = "completed")
        {
            EndCurrentContent(reason != null && reason.ToLowerInvariant().Contains("completed"));
            _snapshot = Envelope("snapshot");
            _snapshot["mode"] = "idle";
            _snapshot["status"] = reason;
            PublishSnapshot();
        }

        public void PublishPlayback(double positionSeconds, double durationSeconds, bool isPlaying)
        {
            if (_snapshot == null || _snapshot["mode"].Value != "video") return;
            _snapshot["positionSeconds"] = positionSeconds;
            _snapshot["durationSeconds"] = durationSeconds;
            _snapshot["playState"] = isPlaying ? "playing" : "paused";
            if (Time.unscaledTime - _lastPlaybackEventAt >= 0.5f)
            {
                _lastPlaybackEventAt = Time.unscaledTime;
                var delta = Envelope("playback"); CopyIdentity(delta);
                delta["positionSeconds"] = positionSeconds; delta["durationSeconds"] = durationSeconds;
                delta["playState"] = isPlaying ? "playing" : "paused"; PublishEvent(delta);
            }
            if (_lastTelemetryPlaying != isPlaying || Time.unscaledTime - _lastTelemetryPlaybackAt >= 15f)
            {
                _lastTelemetryPlaying = isPlaying; _lastTelemetryPlaybackAt = Time.unscaledTime;
                var playback = PublishLearningEvent("playback_state"); playback["mode"] = _currentMode;
                playback["contentId"] = _currentContentId; playback["positionSeconds"] = positionSeconds;
                playback["mediaDurationSeconds"] = durationSeconds; playback["playState"] = isPlaying ? "playing" : "paused"; PublishTelemetry(playback);
            }
            if (Time.unscaledTime - _lastRetainedPlaybackAt >= 5f) { _lastRetainedPlaybackAt = Time.unscaledTime; PublishSnapshot(); }
        }

        public void PublishTransform(Transform target, string interaction = "transform")
        {
            if (target == null || _snapshot == null || Time.unscaledTime - _lastTransformEventAt < 0.1f) return;
            _lastTransformEventAt = Time.unscaledTime;
            var delta = Envelope(interaction); CopyIdentity(delta);
            delta["position"] = Vector(target.position); delta["rotation"] = QuaternionNode(target.rotation); delta["scale"] = Vector(target.localScale);
            PublishEvent(delta);
        }

        public void PublishAssessmentState(string assessmentType, int questionIndex, string question, JSONNode options)
        {
            PublishSkipIfNeeded();
            var details = new JSONObject(); details["assessmentType"] = assessmentType ?? string.Empty;
            details["questionIndex"] = questionIndex; details["question"] = question ?? string.Empty; details["options"] = options ?? new JSONArray();
            ShowContent("assessment", $"assessment-{questionIndex}", string.Empty, details);
            _currentQuestionIndex = questionIndex;
            _questionViewedAt = Now();
            var viewed = PublishLearningEvent("question_viewed"); viewed["mode"] = "assessment";
            viewed["contentId"] = $"assessment-{questionIndex}"; viewed["questionIndex"] = questionIndex; PublishTelemetry(viewed);
        }

        public void PublishAssessmentInteraction(int questionIndex, JSONNode response, bool submitted)
        {
            var delta = Envelope("assessment-response"); CopyIdentity(delta); delta["questionIndex"] = questionIndex;
            delta["response"] = response ?? new JSONNull(); delta["submitted"] = submitted; PublishEvent(delta);
            if ((response != null && !response.IsNull) || (submitted && !_answeredQuestions.Contains(questionIndex)))
            {
                var attempts = _questionAttempts.TryGetValue(questionIndex, out var currentAttempt) ? currentAttempt + 1 : 1;
                _questionAttempts[questionIndex] = attempts;
                _answeredQuestions.Add(questionIndex);
                var answer = PublishLearningEvent("answer_submitted"); answer["mode"] = "assessment";
                answer["contentId"] = $"assessment-{questionIndex}"; answer["questionIndex"] = questionIndex;
                answer["attemptNumber"] = attempts; answer["responseTimeMs"] = Math.Max(0, Now() - _questionViewedAt);
                if (response != null && !response.IsNull) answer["response"] = response; PublishTelemetry(answer);
            }
        }

        public void PublishAssessmentCompleted()
        {
            PublishSkipIfNeeded();
            _currentQuestionIndex = -1;
            PublishLearningEventNow("assessment_completed");
            var details = new JSONObject(); details["message"] = "Assessment completed";
            ShowContent("completion", "assessment-complete", string.Empty, details);
        }

        private void PublishSkipIfNeeded()
        {
            if (_currentQuestionIndex < 0 || _answeredQuestions.Contains(_currentQuestionIndex)) return;
            var skipped = PublishLearningEvent("question_skipped"); skipped["mode"] = "assessment";
            skipped["contentId"] = $"assessment-{_currentQuestionIndex}"; skipped["questionIndex"] = _currentQuestionIndex;
            skipped["responseTimeMs"] = Math.Max(0, Now() - _questionViewedAt); PublishTelemetry(skipped);
        }

        public void TrackSessionState(bool isStarted, bool isPaused, bool isStopped)
        {
            if (isStopped)
            {
                EndCurrentContent(false); PublishLearningEventNow("session_stopped");
                _sessionStarted = false; _sessionPaused = false; return;
            }
            if (isStarted && !_sessionStarted)
            {
                _sessionStarted = true; PublishLearningEventNow("session_started");
            }
            if (isStarted && !_sessionPaused && isPaused)
            {
                _sessionPaused = true; _pauseStartedAt = Now(); PublishLearningEventNow("session_paused");
            }
            else if (isStarted && _sessionPaused && !isPaused)
            {
                _sessionPaused = false; var resumed = PublishLearningEvent("session_resumed");
                resumed["durationMs"] = Math.Max(0, Now() - _pauseStartedAt); PublishTelemetry(resumed);
            }
        }

        private void EndCurrentContent(bool completed)
        {
            if (string.IsNullOrEmpty(_currentContentId) || _contentStartedAt <= 0) return;
            var ended = PublishLearningEvent(completed ? "content_completed" : "content_exited");
            ended["mode"] = _currentMode; ended["contentId"] = _currentContentId;
            ended["durationMs"] = Math.Max(0, Now() - _contentStartedAt); PublishTelemetry(ended);
            _currentMode = string.Empty; _currentContentId = string.Empty; _contentStartedAt = 0;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (_publishingError || (type != LogType.Exception && type != LogType.Error)) return;
            _publishingError = true;
            try
            {
                var error = PublishLearningEvent("device_error"); error["errorCode"] = type.ToString();
                error["errorMessage"] = string.IsNullOrEmpty(condition) ? "Unity runtime error" : condition.Substring(0, Math.Min(500, condition.Length));
                PublishTelemetry(error);
            }
            finally { _publishingError = false; }
        }

        private void PublishPreviousRunFailureIfNeeded()
        {
            if (!_previousRunEndedUnexpectedly) return;
            _previousRunEndedUnexpectedly = false;
            var error = PublishLearningEvent("device_error");
            error["errorCode"] = "UNCLEAN_PREVIOUS_EXIT";
            error["errorMessage"] = "The previous application run ended without a clean shutdown.";
            PublishTelemetry(error);
        }

        private JSONObject PublishLearningEvent(string eventType)
        {
            var node = new JSONObject(); node["schemaVersion"] = 1; node["eventType"] = eventType;
            node["experienceId"] = _experienceId; node["clientSessionId"] = _clientSessionId;
            node["sequence"] = (++_sequence).ToString(); node["timestamp"] = Now().ToString();
            node["platform"] = "webgl"; node["appVersion"] = Application.version; node["unityVersion"] = Application.unityVersion; return node;
        }

        private void PublishLearningEventNow(string eventType) => PublishTelemetry(PublishLearningEvent(eventType));
        private static void PublishTelemetry(JSONObject node) => KlassMqttClient.Instance?.PublishTelemetry(node);
        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private JSONObject Envelope(string eventType)
        {
            var node = new JSONObject(); node["schemaVersion"] = SchemaVersion; node["eventType"] = eventType; node["experienceId"] = _experienceId;
            node["sequence"] = (++_sequence).ToString(); node["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(); return node;
        }

        private void CopyIdentity(JSONObject target)
        {
            target["mode"] = _snapshot?["mode"]?.Value ?? "unknown"; target["contentId"] = _snapshot?["contentId"]?.Value ?? string.Empty;
        }

        private void PublishSnapshot()
        {
            if (_snapshot == null) return;
            _snapshot["sequence"] = (++_sequence).ToString(); _snapshot["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            KlassMqttClient.Instance?.PublishLiveViewState(_snapshot);
        }

        private static void PublishEvent(JSONObject node) => KlassMqttClient.Instance?.PublishLiveViewEvent(node);
        private static JSONObject Vector(Vector3 value) { var n = new JSONObject(); n["x"] = value.x; n["y"] = value.y; n["z"] = value.z; return n; }
        private static JSONObject QuaternionNode(Quaternion value) { var n = new JSONObject(); n["x"] = value.x; n["y"] = value.y; n["z"] = value.z; n["w"] = value.w; return n; }
    }
}
