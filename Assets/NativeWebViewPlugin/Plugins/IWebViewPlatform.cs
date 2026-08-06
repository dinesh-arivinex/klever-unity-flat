using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NativeWebView
{
    internal interface IWebViewPlatform
    {
        void Initialize(string callbackTargetGameObjectName);
        void BuildWebView(string json, PluginWrapper.OnError onError, PluginWrapper.OnSuccess onSuccess);
        void CloseWebView();
    }

    internal static class WebViewPlatformFactory
    {
        public static IWebViewPlatform Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidWebViewPlatform();
#elif UNITY_IOS && !UNITY_EDITOR
            return new IOSWebViewPlatform();
#elif UNITY_WEBGL && !UNITY_EDITOR
            return new WebGLWebViewPlatform();
#else
            return new UnsupportedWebViewPlatform();
#endif
        }
    }

    internal sealed class AndroidWebViewPlatform : IWebViewPlatform
    {
        private const string NativeMethodName = "showWebView";

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly AndroidJavaClass androidJavaClass =
            new AndroidJavaClass("com.mundosoftware.kotlinplugin.controllers.WebViewWrapper");
#endif

        public void Initialize(string callbackTargetGameObjectName)
        {
            // Android implementation handles callback routing in native side.
        }

        public void BuildWebView(string json, PluginWrapper.OnError onError, PluginWrapper.OnSuccess onSuccess)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            androidJavaClass.CallStatic(NativeMethodName, json);
#endif
        }

        public void CloseWebView()
        {
            // Android implementation
        }
    }

    internal sealed class IOSWebViewPlatform : IWebViewPlatform
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void buildWebView(string json, PluginWrapper.OnError onError, PluginWrapper.OnSuccess onSuccess);
#endif

        public void Initialize(string callbackTargetGameObjectName)
        {
            // iOS uses C# delegates directly in native bridge calls.
        }

        public void BuildWebView(string json, PluginWrapper.OnError onError, PluginWrapper.OnSuccess onSuccess)
        {
#if UNITY_IOS && !UNITY_EDITOR
            buildWebView(json, onError, onSuccess);
#endif
        }

        public void CloseWebView()
        {
            // iOS implementation
        }
    }

    internal sealed class WebGLWebViewPlatform : IWebViewPlatform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void NWV_Init(string callbackTargetGameObjectName);

        [DllImport("__Internal")]
        private static extern void NWV_ShowWebView(string json);
        
        [DllImport("__Internal")]
        private static extern void NWV_CloseWebView();
#endif

        private const string DefaultCallbackTarget = "NativeBridge";
        private string callbackTargetGameObjectName = DefaultCallbackTarget;

        public void Initialize(string callbackTargetGameObjectName)
        {
            if (!string.IsNullOrWhiteSpace(callbackTargetGameObjectName))
            {
                this.callbackTargetGameObjectName = callbackTargetGameObjectName;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            NWV_Init(this.callbackTargetGameObjectName);
#endif
        }

        public void BuildWebView(string json, PluginWrapper.OnError onError, PluginWrapper.OnSuccess onSuccess)
        {
            // Keep latest callbacks available for JS->Unity callback bridge.
            PluginWrapper.CacheCallbacks(onError, onSuccess);

#if UNITY_WEBGL && !UNITY_EDITOR
            NWV_Init(callbackTargetGameObjectName);
            NWV_ShowWebView(json);
#endif
        }

        public void CloseWebView()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            NWV_CloseWebView();
#endif
        }
    }

    internal sealed class UnsupportedWebViewPlatform : IWebViewPlatform
    {
        public void Initialize(string callbackTargetGameObjectName)
        {
        }

        public void BuildWebView(string json, PluginWrapper.OnError onError, PluginWrapper.OnSuccess onSuccess)
        {
            Debug.LogWarning("[PluginWrapper] Platform is not supported in this runtime. Use Android, iOS, or WebGL build.");
        }

        public void CloseWebView()
        {
        }
    }
}
