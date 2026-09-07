using System;
using UnityEngine;

namespace Klass.Net
{
    /// <summary>
    /// One stable, broker-safe identity shared by registration, REST and MQTT.
    /// WebGL may report SystemInfo.unsupportedIdentifier; persist a generated
    /// fallback so reloads continue to authenticate as the same device.
    /// </summary>
    public static class KlassDeviceIdentity
    {
        private const string PlayerPrefsKey = "klass.device_id";
        private static string _value;

        public static string Value
        {
            get
            {
                if (IsValid(_value)) return _value;

                var persisted = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty).Trim();
                if (IsValid(persisted)) return _value = persisted;

                var platformId = (SystemInfo.deviceUniqueIdentifier ?? string.Empty).Trim();
                _value = IsValid(platformId) ? platformId : $"device_{Guid.NewGuid():N}";
                PlayerPrefs.SetString(PlayerPrefsKey, _value);
                PlayerPrefs.Save();
                return _value;
            }
        }

        private static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64) return false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var asciiLetter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                var asciiDigit = c >= '0' && c <= '9';
                if (!(asciiLetter || asciiDigit || c == '_' || c == '-')) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Centralised endpoint configuration for the headset.
    ///
    /// Reads from PlayerPrefs first (key: "klass.backend_base_url" / "klass.mqtt_broker_host" /
    /// "klass.mqtt_broker_port"), then from compile-time defaults.
    /// This removes the hard-coded IPs that were previously scattered across scripts
    /// (DeviceSynHandler / API / GamificationManager / DeviceIDManager).
    /// </summary>
    public static class KlassEndpoints
    {
        // Production remains the safe default. Development builds can opt in with    //https://api.klever-edu.com
        // the KLEVER_DEVELOPMENT scripting define or with the PlayerPrefs overrides. //api.klever-edu.com
        private const string ProductionBackendBase = "http://44.222.108.144";
        private const string ProductionMqttHost = "44.222.108.144";
        private const string DevelopmentBackendBase = "http://44.222.108.144";
        private const string DevelopmentMqttHost = "44.222.108.144";

#if KLEVER_DEVELOPMENT
        private const string DefaultBackendBase = DevelopmentBackendBase;
        private const string DefaultMqttHost = DevelopmentMqttHost;
#else
        private const string DefaultBackendBase = ProductionBackendBase;
        private const string DefaultMqttHost = ProductionMqttHost;
#endif
        private const int DefaultMqttPort = 1883;
        private const bool DefaultMqttUseTls = false;

        public static string BackendBase
        {
            get
            {
                var configured = PlayerPrefs.GetString("klass.backend_base_url", string.Empty);
                if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim().TrimEnd('/');

#if UNITY_WEBGL && !UNITY_EDITOR
                // A WebGL build served by the development frontend must use that
                // same origin for device registration and MQTT credentials. This
                // prevents a dev player from silently registering against production.
                if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var pageUri))
                {
                    if (pageUri.Host.Equals(DevelopmentMqttHost, StringComparison.OrdinalIgnoreCase))
                        return pageUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');

                    // Unity's Build And Run uses a localhost static server which
                    // does not proxy /v1. Send those builds to the development API.
                    if (IsLocalHost(pageUri.Host)) return DevelopmentBackendBase;
                }
#endif
                return DefaultBackendBase;
            }
        }

        public static string MqttHost =>
            PlayerPrefs.GetString("klass.mqtt_broker_host", DefaultMqttHost);

        public static int MqttPort =>
            PlayerPrefs.GetInt("klass.mqtt_broker_port", DefaultMqttPort);

        public static bool MqttUseTls =>
            PlayerPrefs.GetInt("klass.mqtt_use_tls", DefaultMqttUseTls ? 1 : 0) == 1;

        private static bool IsLocalHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("::1", StringComparison.OrdinalIgnoreCase);
        }

        public static string V1(string path)
        {
            if (string.IsNullOrEmpty(path)) return BackendBase + "/v1/";
            return BackendBase + "/v1/" + path.TrimStart('/');
        }
    }
}
