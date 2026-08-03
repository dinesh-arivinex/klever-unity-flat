namespace NativeWebView
{
    using UnityEngine;

    public class NativeBridge : MonoBehaviour
    {
        public void OnSuccess(string successMessage)
        {
            Debug.Log($"[NativeBridge] WebView loaded: {successMessage}");
            PluginWrapper.NotifySuccess(successMessage);
        }

        public void OnError(string errorMessage)
        {
            Debug.Log($"[NativeBridge] WebView error: {errorMessage}");
            PluginWrapper.NotifyError(errorMessage);
        }

        public void OnClosed(string closedMessage)
        {
            Debug.Log($"[NativeBridge] WebView closed: {closedMessage}");
        }

        public void OnReloaded(string reloadMessage)
        {
            Debug.Log($"[NativeBridge] WebView reloaded: {reloadMessage}");
        }
    }
}
