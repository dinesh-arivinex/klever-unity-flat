using UnityEngine;

namespace NativeWebView
{
    public static class PluginWrapper
    {
        public delegate void OnSuccess();

        public delegate void OnError(string error);

        private static readonly IWebViewPlatform platform;
        private static OnError pendingOnError;
        private static OnSuccess pendingOnSuccess;

        static PluginWrapper()
        {
            platform = WebViewPlatformFactory.Create();
        }

        public static void SetCallbackTarget(string callbackTargetGameObjectName)
        {
            platform.Initialize(callbackTargetGameObjectName);
        }

        public static void BuildWebView(string json, OnError onError, OnSuccess onSuccess)
        {
            Debug.Log($"[PluginWrapper] Json: {json}");
            CacheCallbacks(onError, onSuccess);
            platform.BuildWebView(json, onError, onSuccess);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void NWV_SetSessionPaused(int paused);
#endif

        public static void SetSessionPaused(bool paused)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            NWV_SetSessionPaused(paused ? 1 : 0);
#endif
        }

        public static void CloseWebView()
        {
            platform.CloseWebView();
        }

        internal static void CacheCallbacks(OnError onError, OnSuccess onSuccess)
        {
            pendingOnError = onError;
            pendingOnSuccess = onSuccess;
        }

        public static void NotifySuccess(string message)
        {
            pendingOnSuccess?.Invoke();
        }

        public static void NotifyError(string error)
        {
            pendingOnError?.Invoke(error);
        }
    }
}
