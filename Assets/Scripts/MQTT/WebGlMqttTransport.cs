using System.Runtime.InteropServices;

namespace Klass.Net
{
    /// <summary>
    /// Browser-only MQTT transport. Unity WebGL cannot open raw TCP sockets, so
    /// this delegates MQTT 3.1.1 over secure WebSocket to a .jslib bridge.
    /// </summary>
    internal static class WebGlMqttTransport
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] internal static extern void KleverMqttConnect(
            string gameObjectName, string brokerUrl, string username, string password,
            string clientId, string willTopic, string willPayload, int keepaliveSeconds);
        [DllImport("__Internal")] internal static extern void KleverMqttSubscribe(string topic, int qos);
        [DllImport("__Internal")] internal static extern void KleverMqttPublish(string topic, string payload, int qos, int retain);
        [DllImport("__Internal")] internal static extern void KleverMqttDisconnect();
#else
        internal static void KleverMqttConnect(string gameObjectName, string brokerUrl, string username, string password,
            string clientId, string willTopic, string willPayload, int keepaliveSeconds) { }
        internal static void KleverMqttSubscribe(string topic, int qos) { }
        internal static void KleverMqttPublish(string topic, string payload, int qos, int retain) { }
        internal static void KleverMqttDisconnect() { }
#endif
    }
}
