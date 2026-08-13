using System;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Events;

namespace Klass.Net
{
    [Serializable]
    public sealed class LiveViewJsonEvent : UnityEvent<string> { }

    /// <summary>
    /// WebGL bridge entry point for observer builds. The web console calls
    /// ApplyLiveViewSnapshot / ApplyLiveViewEvent through Unity SendMessage.
    /// Scene-specific renderers can subscribe without depending on MQTT.
    /// </summary>
    public sealed class LiveViewObserver : MonoBehaviour
    {
        public static LiveViewObserver Instance { get; private set; }
        public LiveViewJsonEvent onSnapshot = new LiveViewJsonEvent();
        public LiveViewJsonEvent onEvent = new LiveViewJsonEvent();

        public JSONNode CurrentSnapshot { get; private set; }
        public long LastSequence { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null || FindObjectOfType<LiveViewObserver>() != null) return;
            new GameObject("LiveViewObserver").AddComponent<LiveViewObserver>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            gameObject.name = "LiveViewObserver";
            DontDestroyOnLoad(gameObject);
        }

        public void ApplyLiveViewSnapshot(string json)
        {
            if (!TryAccept(json, out var node)) return;
            CurrentSnapshot = node;
            onSnapshot.Invoke(node.ToString());
        }

        public void ApplyLiveViewEvent(string json)
        {
            if (!TryAccept(json, out var node)) return;
            onEvent.Invoke(node.ToString());
        }

        private bool TryAccept(string json, out JSONNode node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try { node = JSONNode.Parse(json); }
            catch (Exception exception) { Debug.LogWarning($"[LiveViewObserver] Invalid JSON: {exception.Message}"); return false; }
            if (node == null || node["schemaVersion"].AsInt != LiveViewPublisher.SchemaVersion) return false;
            long sequence;
            if (!long.TryParse(node["sequence"].Value, out sequence)) sequence = 0;
            if (sequence > 0 && sequence < LastSequence) return false;
            LastSequence = Math.Max(LastSequence, sequence);
            return true;
        }
    }
}
