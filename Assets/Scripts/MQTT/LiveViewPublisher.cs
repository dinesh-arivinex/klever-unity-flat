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
        private float _lastRetainedPlaybackAt = -10f;
        private float _lastTransformEventAt = -10f;
        private string _experienceId = string.Empty;

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
        }

        public void ShowContent(string mode, string contentId, string assetUrl, JSONNode details = null)
        {
            _snapshot = Envelope("snapshot");
            _snapshot["mode"] = string.IsNullOrWhiteSpace(mode) ? "unknown" : mode;
            _snapshot["contentId"] = contentId ?? string.Empty;
            _snapshot["assetUrl"] = assetUrl ?? string.Empty;
            _snapshot["status"] = "visible";
            if (details != null) _snapshot["details"] = details;
            PublishSnapshot();
        }

        public void SetExperienceId(string experienceId) { _experienceId = experienceId ?? string.Empty; }

        public void HideContent(string reason = "completed")
        {
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
            var details = new JSONObject(); details["assessmentType"] = assessmentType ?? string.Empty;
            details["questionIndex"] = questionIndex; details["question"] = question ?? string.Empty; details["options"] = options ?? new JSONArray();
            ShowContent("assessment", $"assessment-{questionIndex}", string.Empty, details);
        }

        public void PublishAssessmentInteraction(int questionIndex, JSONNode response, bool submitted)
        {
            var delta = Envelope("assessment-response"); CopyIdentity(delta); delta["questionIndex"] = questionIndex;
            delta["response"] = response ?? new JSONNull(); delta["submitted"] = submitted; PublishEvent(delta);
        }

        public void PublishAssessmentCompleted()
        {
            var details = new JSONObject(); details["message"] = "Assessment completed";
            ShowContent("completion", "assessment-complete", string.Empty, details);
        }

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
