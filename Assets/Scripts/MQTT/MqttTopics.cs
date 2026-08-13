namespace Klass.Net
{
    /// <summary>
    /// MQTT topic taxonomy mirror of the backend src/mqtt/topics.js.
    /// klass/v1/schools/{schoolId}/...
    /// </summary>
    public static class MqttTopics
    {
        public const string Ns = "klass/v1";

        public static string Status(string schoolId, string deviceId)    => $"{Ns}/schools/{schoolId}/devices/{deviceId}/status";
        public static string State(string schoolId, string deviceId)     => $"{Ns}/schools/{schoolId}/devices/{deviceId}/state";
        public static string Telemetry(string schoolId, string deviceId) => $"{Ns}/schools/{schoolId}/devices/{deviceId}/telemetry";
        public static string LiveViewState(string schoolId, string deviceId) => $"{Ns}/schools/{schoolId}/devices/{deviceId}/live-view/state";
        public static string LiveViewEvents(string schoolId, string deviceId) => $"{Ns}/schools/{schoolId}/devices/{deviceId}/live-view/events";
        public static string Cmd(string schoolId, string deviceId)       => $"{Ns}/schools/{schoolId}/devices/{deviceId}/cmd";
        public static string CmdAck(string schoolId, string deviceId)    => $"{Ns}/schools/{schoolId}/devices/{deviceId}/cmd/ack";

        public static string SessionControl(string schoolId) => $"{Ns}/schools/{schoolId}/session/control";
        public static string SessionEvents(string schoolId)  => $"{Ns}/schools/{schoolId}/session/events";
        public static string Broadcast(string schoolId)      => $"{Ns}/schools/{schoolId}/broadcast";
    }
}
