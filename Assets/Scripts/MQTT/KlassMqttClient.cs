using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using SimpleJSON;
#if !UNITY_WEBGL || UNITY_EDITOR
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
#endif
using UnityEngine;
using UnityEngine.Scripting;

namespace Klass.Net
{
    /// <summary>
    /// Singleton MonoBehaviour wrapping M2Mqtt for the Klever_VR headset.
    ///
    /// Responsibilities:
    ///  - Fetch credentials (HTTP), connect to EMQX (TCP), publish LWT-aware status.
    ///  - Auto-reconnect with exponential backoff.
    ///  - Marshal incoming messages onto the Unity main thread so handlers can
    ///    safely touch GameObjects.
    ///  - Expose simple PublishState/PublishTelemetry/PublishCmdAck helpers and
    ///    OnSessionControl / OnCommand events.
    ///
    /// IMPORTANT: this script must live in a scene (or be instantiated with
    /// DontDestroyOnLoad) before any consumers call Instance.
    /// </summary>
    public class KlassMqttClient : MonoBehaviour
    {
        public static KlassMqttClient Instance { get; private set; }



        [Header("Tuning")]
        public float reconnectInitialSeconds = 2f;
        public float reconnectMaxSeconds = 30f;

        // ── Public state ───────────────────────────────────────────────────
        public bool IsConnected { get; private set; }
        public string SchoolId  { get; private set; }
        public string DeviceId  { get; private set; }

        private static string RuntimePlatformName
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return "webgl";
#else
                return "vr";
#endif
            }
        }

        // ── Public events (always fired on Unity main thread) ──────────────
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<JSONNode> OnSessionControl; // payload of session/control
        public event Action<JSONNode> OnCommand;        // payload of devices/{me}/cmd
        public event Action<JSONNode> OnBroadcast;      // payload of broadcast

        // ── Internals ──────────────────────────────────────────────────────
#if !UNITY_WEBGL || UNITY_EDITOR
        private MqttClient _client;
#endif
        private MqttCredentialsFetcher.Credentials _creds;
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private readonly object _lock = new object();
        private float _backoff;
        private bool _wantConnected;
        private Coroutine _reconnectLoop;
        private Coroutine _refreshLoop;
#if UNITY_WEBGL && !UNITY_EDITOR
        private int _webGlConnectResult;
#endif

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            // Drain main-thread work queued from MQTT worker thread
            while (_mainThread.TryDequeue(out var act))
            {
                try { act(); } catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        void OnApplicationQuit()
        {
            _wantConnected = false;
            DisconnectInternal("app quit");
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            _wantConnected = false;
            DisconnectInternal("destroyed");
            Instance = null;
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>Begin connecting (idempotent). Will keep retrying.</summary>
        public void Begin()
        {
            if (_wantConnected) return;
            _wantConnected = true;
            _backoff = reconnectInitialSeconds;
            _reconnectLoop = StartCoroutine(ReconnectLoop());
        }

        public void End()
        {
            _wantConnected = false;
            if (_reconnectLoop != null) StopCoroutine(_reconnectLoop);
            if (_refreshLoop != null) StopCoroutine(_refreshLoop);
            DisconnectInternal("end");
        }

        public void PublishState(bool isActive, bool isSynced, bool isCompleted, string playState = null, string experienceId = null)
        {
            if (!IsConnected) return;
            var node = new JSONObject();
            node["isActive"]    = isActive;
            node["isSynced"]    = isSynced;
            node["isCompleted"] = isCompleted;
            node["platform"]    = RuntimePlatformName;
            if (!string.IsNullOrEmpty(playState))    node["playState"]    = playState;
            if (!string.IsNullOrEmpty(experienceId)) node["experienceId"] = experienceId;
            node["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            PublishJson(MqttTopics.State(SchoolId, DeviceId), node, retain: true, qos: 1);
        }

        /// <summary>
        /// Publish a richer state payload that includes the live asset-download
        /// progress (0-100). The teacher dashboard subscribes to retained state
        /// messages, so even a freshly-opened browser tab sees the latest value.
        /// </summary>
        public void PublishStateWithProgress(bool isActive, bool isSynced, bool isCompleted,
                                             int downloadProgress, string playState = null,
                                             string experienceId = null, bool handRaised = false)
        {
            if (!IsConnected) return;
            var node = new JSONObject();
            node["isActive"]         = isActive;
            node["isSynced"]         = isSynced;
            node["isCompleted"]      = isCompleted;
            node["platform"]         = RuntimePlatformName;
            node["downloadProgress"] = Mathf.Clamp(downloadProgress, 0, 100);
            if (!string.IsNullOrEmpty(playState))    node["playState"]    = playState;
            if (!string.IsNullOrEmpty(experienceId)) node["experienceId"] = experienceId;
            // Always include handRaised so it is preserved across retained-state
            // heartbeats and a refreshing teacher dashboard sees the latest value.
            node["handRaised"]   = handRaised;
            // Explicitly clear any previous downloadError when publishing normal state.
            node["downloadError"] = new JSONNull();
            node["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            PublishJson(MqttTopics.State(SchoolId, DeviceId), node, retain: true, qos: 1);
        }

        /// <summary>
        /// Publish a download-error state so the teacher dashboard can show
        /// a per-device error badge. The error is retained so page refreshes
        /// still see it until the device recovers or is removed.
        /// </summary>
        public void PublishDownloadError(string errorMessage)
        {
            if (!IsConnected) return;
            var node = new JSONObject();
            node["isActive"]         = true;
            node["isSynced"]         = false;
            node["isCompleted"]      = false;
            node["platform"]         = RuntimePlatformName;
            node["downloadProgress"] = 0;
            node["playState"]        = "idle";
            node["downloadError"]    = string.IsNullOrEmpty(errorMessage) ? "Unknown download error" : errorMessage;
            node["ts"]               = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            PublishJson(MqttTopics.State(SchoolId, DeviceId), node, retain: true, qos: 1);
        }

        public void PublishTelemetry(JSONObject node)
        {
            if (!IsConnected) return;
            // Learning events must survive transient packet loss. Event IDs make
            // QoS 1 redelivery safe because the backend stores them idempotently.
            PublishJson(MqttTopics.Telemetry(SchoolId, DeviceId), node, retain: false, qos: 1);
        }

        public void PublishLiveViewState(JSONObject node)
        {
            if (!IsConnected) return;
            PublishJson(MqttTopics.LiveViewState(SchoolId, DeviceId), node, retain: true, qos: 1);
        }

        public void PublishLiveViewEvent(JSONObject node)
        {
            if (!IsConnected) return;
            PublishJson(MqttTopics.LiveViewEvents(SchoolId, DeviceId), node, retain: false, qos: 0);
        }

        public void PublishCmdAck(string msgId, bool ok, string error = null)
        {
            if (!IsConnected) return;
            var node = new JSONObject();
            node["msgId"] = msgId ?? "";
            node["ok"] = ok;
            if (!string.IsNullOrEmpty(error)) node["error"] = error;
            node["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            PublishJson(MqttTopics.CmdAck(SchoolId, DeviceId), node, retain: false, qos: 1);
        }

        public void PublishJson(string topic, JSONNode body, bool retain, byte qos)
        {
            if (!IsConnected) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(body == null ? "{}" : body.ToString());
#if UNITY_WEBGL && !UNITY_EDITOR
                WebGlMqttTransport.KleverMqttPublish(topic, Encoding.UTF8.GetString(bytes), qos, retain ? 1 : 0);
#else
                _client.Publish(topic, bytes, qos, retain);
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] publish failed on {topic}: {ex.Message}");
            }
        }

        // ── Connect + reconnect loop ───────────────────────────────────────

        private IEnumerator ReconnectLoop()
        {
            while (_wantConnected)
            {
                if (!IsConnected)
                {
                    yield return ConnectOnce();
                    if (!IsConnected)
                    {
                        var wait = _backoff * UnityEngine.Random.Range(0.8f, 1.2f);
                        _backoff = Mathf.Min(reconnectMaxSeconds, _backoff * 2f);
                        yield return new WaitForSecondsRealtime(wait);
                        continue;
                    }
                    _backoff = reconnectInitialSeconds;
                }
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        private IEnumerator ConnectOnce()
        {
            // 1. (re)fetch creds if missing or expired
            if (_creds == null || _creds.IsExpired)
            {
                bool done = false;
                bool ok = false;
                string err = null;
                yield return new MqttCredentialsFetcher().Fetch(
                    c => { _creds = c; ok = true; done = true; },
                    e => { err = e; done = true; });
                if (!done) yield break;
                if (!ok) 
                { 
                    if (err != null && err.Contains("HTTP 401") && err.Contains("Device not found"))
                    {
                        Debug.Log("[KlassMqtt] Device not yet registered. Waiting for registration UI to complete before reconnecting...");
                    }
                    else
                    {
                        Debug.LogWarning($"[KlassMqtt] credential fetch failed: {err}"); 
                    }
                    yield break; 
                }
                SchoolId = _creds.SchoolId;
                DeviceId = _creds.DeviceId;
            }

            // 2. Open the transport advertised by the credential endpoint.
#if UNITY_WEBGL && !UNITY_EDITOR
            yield return ConnectWebGlOnce();
            if (!IsConnected) yield break;
#else
            try
            {
                var sslProtocol = _creds.BrokerUseTls ? MqttSslProtocols.TLSv1_2 : MqttSslProtocols.None;
                _client = new MqttClient(_creds.BrokerHost, _creds.BrokerPort, _creds.BrokerUseTls,
                    null, null, sslProtocol);
                _client.MqttMsgPublishReceived += HandleIncomingMessage;
                _client.ConnectionClosed += HandleConnectionClosed;

                // Last Will: broker auto-publishes when this client drops/keepalive-times-out
                var willTopic   = MqttTopics.Status(SchoolId, DeviceId);
                var willPayload = Encoding.UTF8.GetBytes("{\"online\":false,\"ts\":0}");
                byte code = _client.Connect(
                    _creds.ClientId,
                    _creds.Username,
                    _creds.Password,
                    /*willRetain*/ true,
                    /*willQos*/ (byte)1,
                    /*willFlag*/ true,
                    willTopic,
                    Encoding.UTF8.GetString(willPayload),
                    /*cleanSession*/ true,
                    /*keepAlivePeriod*/ (ushort)_creds.KeepaliveSeconds);

                if (code != 0)
                {
                    Debug.LogWarning($"[KlassMqtt] connect refused, code={code}");
                    SafeDisconnect();
                    yield break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] connect threw: {ex.GetType().Name}: {ex.Message} (host={_creds.BrokerHost}:{_creds.BrokerPort} tls={_creds.BrokerUseTls})");
                SafeDisconnect();
                yield break;
            }

            IsConnected = true;
#endif

            // 3. Subscribe to commands & session topics
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                WebGlMqttTransport.KleverMqttSubscribe(MqttTopics.Cmd(SchoolId, DeviceId), 1);
                WebGlMqttTransport.KleverMqttSubscribe(MqttTopics.SessionControl(SchoolId), 1);
                WebGlMqttTransport.KleverMqttSubscribe(MqttTopics.Broadcast(SchoolId), 1);
#else
                _client.Subscribe(
                    new[] {
                        MqttTopics.Cmd(SchoolId, DeviceId),
                        MqttTopics.SessionControl(SchoolId),
                        MqttTopics.Broadcast(SchoolId),
                    },
                    new[] { (byte)1, (byte)1, (byte)1 });
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] subscribe failed: {ex.Message}");
            }

            // 4. Publish online presence (retained)
            try
            {
                var online = new JSONObject();
                online["online"] = true;
                online["platform"] = RuntimePlatformName;
                online["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                PublishJson(MqttTopics.Status(SchoolId, DeviceId), online, retain: true, qos: 1);
            }
            catch (Exception ex) { Debug.LogWarning($"[KlassMqtt] presence publish failed: {ex.Message}"); }

            EnqueueMain(() => OnConnected?.Invoke());

            // 5. Schedule credential refresh
            if (_refreshLoop != null) StopCoroutine(_refreshLoop);
            _refreshLoop = StartCoroutine(CredentialRefreshLoop());
        }

        private IEnumerator CredentialRefreshLoop()
        {
            while (_wantConnected && IsConnected)
            {
                var ttl = _creds.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var sleep = Mathf.Max(60f, (float)(ttl - 60));
                yield return new WaitForSecondsRealtime(sleep);
                // Force reconnect with fresh creds (keeps things simple & robust)
                Debug.Log("[KlassMqtt] credential refresh triggered");
                _creds = null;
                SafeDisconnect();
                IsConnected = false;
                EnqueueMain(() => OnDisconnected?.Invoke());
                yield break;
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private IEnumerator ConnectWebGlOnce()
        {
            if (!Uri.TryCreate(_creds.BrokerWsUrl, UriKind.Absolute, out var brokerUri) ||
                (brokerUri.Scheme != "ws" && brokerUri.Scheme != "wss"))
            {
                Debug.LogWarning("[KlassMqtt] invalid brokerWsUrl from credential endpoint");
                yield break;
            }

            _webGlConnectResult = 0;
            var will = "{\"online\":false,\"ts\":0}";
            WebGlMqttTransport.KleverMqttConnect(
                gameObject.name, _creds.BrokerWsUrl, _creds.Username, _creds.Password,
                _creds.ClientId, MqttTopics.Status(SchoolId, DeviceId), will,
                Mathf.Max(1, _creds.KeepaliveSeconds));

            var timeoutAt = Time.realtimeSinceStartup + 15f;
            while (_webGlConnectResult == 0 && Time.realtimeSinceStartup < timeoutAt)
                yield return null;

            if (_webGlConnectResult != 1)
            {
                Debug.LogWarning("[KlassMqtt] WebGL MQTT connection failed or timed out");
                WebGlMqttTransport.KleverMqttDisconnect();
                yield break;
            }
            IsConnected = true;
        }

        // Called by KleverMqtt.jslib on Unity's main thread.
        [Preserve]
        public void OnKleverMqttWebGlConnected(string _) => _webGlConnectResult = 1;
        [Preserve]
        public void OnKleverMqttWebGlConnectFailed(string reason)
        {
            Debug.LogWarning($"[KlassMqtt] WebGL broker refused connection: {reason}");
            _webGlConnectResult = -1;
        }
        [Preserve]
        public void OnKleverMqttWebGlDisconnected(string _)
        {
            if (!IsConnected) return;
            IsConnected = false;
            OnDisconnected?.Invoke();
        }
        [Preserve]
        public void OnKleverMqttWebGlMessage(string envelope)
        {
            try
            {
                var json = JSONNode.Parse(envelope);
                DispatchIncomingMessage(json?["topic"]?.Value, json?["payload"]?.Value);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] WebGL message parse failed: {ex.Message}");
            }
        }
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        private void UnsubscribeMqttEvents(MqttClient client)
        {
            if (client == null) return;
            try
            {
                client.ConnectionClosed -= HandleConnectionClosed;
                client.MqttMsgPublishReceived -= HandleIncomingMessage;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] Failed to unsubscribe connection event: {ex.Message}");
            }
        }

        private void HandleConnectionClosed(object sender, EventArgs e)
        {
            try
            {
                EnqueueMain(() =>
                {
                    // A close from a retired connection must not mark a newer
                    // successfully reconnected client as offline.
                    if (!ReferenceEquals(sender, _client)) return;
                    IsConnected = false;
                    OnDisconnected?.Invoke();
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] Error in ConnectionClosed handler: {ex.Message}");
            }
        }

        [UnityEngine.Scripting.Preserve]
        private void HandleIncomingMessage(object sender, MqttMsgPublishEventArgs e)
        {
            // M2Mqtt fires this on a worker thread — wrap in try-catch to prevent application crash on background thread exception.
            try
            {
                if (e == null) return;
                string topic = e.Topic ?? "";
                byte[] msgBytes = e.Message;
                string payload = msgBytes != null ? Encoding.UTF8.GetString(msgBytes) : "";

                EnqueueMain(() => DispatchIncomingMessage(topic, payload));
            }
            catch (Exception ex)
            {
                // Background thread exceptions MUST be caught to prevent crash!
                Debug.LogWarning($"[KlassMqtt] HandleIncomingMessage exception: {ex.Message}");
            }
        }
#endif

        private void DispatchIncomingMessage(string topic, string payload)
        {
            JSONNode node = null;
            try { node = JSONNode.Parse(payload); } catch { /* not JSON */ }

            if (topic == MqttTopics.SessionControl(SchoolId)) OnSessionControl?.Invoke(node);
            else if (topic == MqttTopics.Broadcast(SchoolId)) OnBroadcast?.Invoke(node);
            else if (topic == MqttTopics.Cmd(SchoolId, DeviceId)) OnCommand?.Invoke(node);
        }

 #if UNITY_WEBGL && !UNITY_EDITOR
        private void CleanupClient()
        {
            WebGlMqttTransport.KleverMqttDisconnect();
        }
#else
        private void CleanupClient(MqttClient client)
        {
            if (client == null) return;
            UnsubscribeMqttEvents(client);
            try
            {
                if (client.IsConnected)
                {
                    // best-effort offline status before clean disconnect
                    try
                    {
                        var off = new JSONObject();
                        off["online"] = false;
                        off["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                        client.Publish(MqttTopics.Status(SchoolId, DeviceId),
                            Encoding.UTF8.GetBytes(off.ToString()),
                            (byte)1, true);
                    }
                    catch { /* swallow */ }
                    client.Disconnect();
                }
            }
            catch { /* swallow */ }
        }
#endif

        private void DisconnectInternal(string reason)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CleanupClient();
#else
            CleanupClient(_client);
            _client = null;
#endif
            IsConnected = false;
            Debug.Log($"[KlassMqtt] disconnected: {reason}");
        }

        private void SafeDisconnect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CleanupClient();
#else
            CleanupClient(_client);
            _client = null;
#endif
            IsConnected = false;
        }

        private void EnqueueMain(Action a) => _mainThread.Enqueue(a);
    }
}
