using System;
using System.Collections;
using System.Text;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

namespace Klass.Net
{
    /// <summary>
    /// Fetches short-lived MQTT credentials from the backend using the headset's
    /// existing device-id authentication (POST /v1/mqtt/credentials/device).
    /// </summary>
    public class MqttCredentialsFetcher
    {
        public class Credentials
        {
            public string BrokerWsUrl;
            public string BrokerTcpUrl;
            public string BrokerHost;
            public int    BrokerPort;
            public bool   BrokerUseTls;
            public string Username;
            public string Password;
            public string ClientId;
            public long   ExpiresAt;          // unix seconds
            public int    KeepaliveSeconds = 30;
            public string SchoolId;           // parsed from username
            public string DeviceId;           // parsed from username

            public bool IsExpired => ExpiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt - 30;
        }

        public delegate void OnSuccessHandler(Credentials c);
        public delegate void OnFailureHandler(string error);

        public IEnumerator Fetch(OnSuccessHandler onSuccess, OnFailureHandler onFailure)
        {
            var url = KlassEndpoints.V1("mqtt/credentials/device");
            using (var req = new UnityWebRequest(url, "POST"))
            {
                // empty JSON body so server frameworks don't reject it
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 20;
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Accept", "application/json");
                req.SetRequestHeader("Cache-Control", "no-store");
                req.SetRequestHeader("device-id", KlassDeviceIdentity.Value);
#if UNITY_WEBGL && !UNITY_EDITOR
                req.SetRequestHeader("x-device-platform", "webgl");
#else
                req.SetRequestHeader("x-device-platform", "vr");
#endif
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    var response = req.downloadHandler == null ? "" : req.downloadHandler.text;
                    if (response.Length > 256) response = response.Substring(0, 256);
                    onFailure?.Invoke($"HTTP {req.responseCode}: {req.error} {response}".Trim());
                    yield break;
                }

                JSONNode json = null;
                try
                {
                    json = JSONNode.Parse(req.downloadHandler.text);
                }
                catch (Exception ex)
                {
                    onFailure?.Invoke($"JSON parsing failed: {ex.Message}");
                    yield break;
                }

                if (json == null)
                {
                    onFailure?.Invoke("invalid JSON from credential endpoint");
                    yield break;
                }
                
                Credentials c;
                try
                {
                    c = new Credentials
                    {
                        BrokerWsUrl  = json["brokerWsUrl"],
                        BrokerTcpUrl = json["brokerTcpUrl"],
                        Username     = json["username"],
                        Password     = json["password"],
                        ClientId     = json["clientId"],
                        ExpiresAt    = (long)json["expiresAt"].AsDouble,
                    };
                }
                catch (Exception ex)
                {
                    onFailure?.Invoke($"Failed to extract credential fields: {ex.Message}");
                    yield break;
                }
                if (json["keepaliveSeconds"] != null) c.KeepaliveSeconds = json["keepaliveSeconds"].AsInt;

                // Username pattern: device:{deviceId}@{schoolId}
                if (!string.IsNullOrEmpty(c.Username) && c.Username.StartsWith("device:"))
                {
                    var rest = c.Username.Substring("device:".Length);
                    var at = rest.IndexOf('@');
                    if (at > 0)
                    {
                        c.DeviceId = rest.Substring(0, at);
                        c.SchoolId = rest.Substring(at + 1);
                    }
                }
                if (string.IsNullOrEmpty(c.SchoolId) || string.IsNullOrEmpty(c.DeviceId))
                {
                    onFailure?.Invoke("malformed username from credential endpoint");
                    yield break;
                }

#if UNITY_WEBGL && !UNITY_EDITOR
                if (!Uri.TryCreate(c.BrokerWsUrl, UriKind.Absolute, out var brokerUri) ||
                    (brokerUri.Scheme != "ws" && brokerUri.Scheme != "wss") ||
                    string.IsNullOrEmpty(brokerUri.Host))
                {
                    onFailure?.Invoke("invalid brokerWsUrl from credential endpoint");
                    yield break;
                }

                if (KlassEndpoints.BackendBase.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    brokerUri.Scheme != "wss")
                {
                    onFailure?.Invoke("brokerWsUrl must use wss for an HTTPS WebGL build");
                    yield break;
                }
#else
                if (!Uri.TryCreate(c.BrokerTcpUrl, UriKind.Absolute, out var brokerUri) ||
                    (brokerUri.Scheme != "mqtt" && brokerUri.Scheme != "mqtts") ||
                    string.IsNullOrEmpty(brokerUri.Host))
                {
                    onFailure?.Invoke("invalid brokerTcpUrl from credential endpoint");
                    yield break;
                }

                c.BrokerHost = brokerUri.Host;
                c.BrokerUseTls = brokerUri.Scheme == "mqtts";
                c.BrokerPort = brokerUri.IsDefaultPort
                    ? (c.BrokerUseTls ? 8883 : 1883)
                    : brokerUri.Port;
#endif
                onSuccess?.Invoke(c);
            }
        }
    }
}
