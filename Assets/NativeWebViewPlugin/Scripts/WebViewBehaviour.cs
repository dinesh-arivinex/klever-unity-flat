using UnityEngine.Serialization;

namespace NativeWebView
{
    using System;
    using System.IO;
    using System.Text;
    using AOT;
    using UnityEngine;
        
    public enum PaddingId
    {
        Top,
        Bottom,
        Leading,
        Trailing
    }

    public class WebViewBehaviour : MonoBehaviour
    {
        [FormerlySerializedAs("_nativeBridge")]
        [Header("Handle WebView callbacks")]
        [SerializeField] private NativeBridge _nativeBridgePrefab;

        public static WebViewBehaviour Singleton = null;

        private const string CallbackTargetGameObjectName = "NativeBridge";

        private WebViewBuilder webViewBuilder = new WebViewBuilder();

        [Header("SIMPLE WEBVIEW")]
        [SerializeField] TextAsset _htmlFile;

        [Header("CUSTOM WEBVIEW")]
        private Miscellaneous miscellaneous = new Miscellaneous();
        private Toolbar toolbar = new Toolbar();
        private ProgressView progressView = new ProgressView();
        private ErrorView errorView = new ErrorView();

        private string url;
        private string htmlString;
        private string filePath;

        public Action<string> OnFileSaved;
        private bool useHtmlString;
        private bool useFile;

        private void Awake()
        {
            if (Singleton == null)
            {
                if (_nativeBridgePrefab != null)
                {
                    var bridge = Instantiate(_nativeBridgePrefab, parent: this.transform);
                    bridge.gameObject.name = CallbackTargetGameObjectName;
                }

                PluginWrapper.SetCallbackTarget(CallbackTargetGameObjectName);

                Singleton = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Singleton != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            WriteFileOnDisk();
        }

        private void WriteFileOnDisk()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL iframe cannot load local persistentDataPath files via file://.
            // Keep file mode URL/HTML based instead of writing index.html.
            filePath = string.Empty;

            if (string.IsNullOrWhiteSpace(htmlString) && _htmlFile != null)
            {
                htmlString = _htmlFile.text;
            }

            Debug.Log("[Unity] WebGL: skipping local index.html write; file mode expects http/https URL or htmlString.");
            return;
#else
            filePath = Path.Combine(Application.persistentDataPath, "index.html");
            File.WriteAllBytes(filePath, _htmlFile.bytes);

            Debug.Log(new StringBuilder($"[Unity] Html file saved at: {filePath}"));

            OnFileSaved?.Invoke(filePath);
#endif
        }

        #region SIMPLE WEBVIEW

        public void ToggleUrl(bool value)
        {
            webViewBuilder.ToggleUrl();
            useHtmlString = false;
            useFile = false;
        }

        public void ToggleHtmlString(bool value)
        {
            webViewBuilder.ToggleHtmlString();
            useHtmlString = true;
            useFile = false;
        }

        public void ToggleHtmlFile(bool value)
        {
            webViewBuilder.ToggleHtmlFile();
            useHtmlString = false;
            useFile = true;
        }

        #endregion

        #region CUSTOM WEBVIEW

        public void BuildWebView()
        {
            var resolvedHtmlString = htmlString;
            var resolvedFilePath = filePath;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (useFile)
            {
                // WebGL local file paths are not iframe-loadable; keep file mode without persistentDataPath dependency.
                resolvedFilePath = string.Empty;

                if (string.IsNullOrWhiteSpace(resolvedHtmlString) && _htmlFile != null)
                {
                    resolvedHtmlString = _htmlFile.text;
                }
            }

            webViewBuilder.SetParams(url, useHtmlString, resolvedHtmlString, useFile, resolvedFilePath)
                .CustomMiscellaneous(miscellaneous)
                .CustomToolbar(toolbar)
                .CustoProgressView(progressView)
                .CustomPadding(miscellaneous.padding)
                .CustoErrorView(errorView)
                .HandleCallbacks(OnError, OnSuccess)
                .BuildWebView();
#endif

            webViewBuilder.SetParams(url, useHtmlString, resolvedHtmlString, useFile, resolvedFilePath)
                .CustomMiscellaneous(miscellaneous)
                .CustomToolbar(toolbar)
                .CustoProgressView(progressView)
                .CustoErrorView(errorView)
                .HandleCallbacks(OnError, OnSuccess)
                .BuildWebView();
        }

        public void CloseWebView()
        {
            PluginWrapper.CloseWebView();
        }

        [MonoPInvokeCallback(typeof(PluginWrapper.OnError))]
        private static void OnError(string error)
        {
            Debug.LogError(new StringBuilder($"[Unity] Failed to load Webview! Error: {error}"));
        }

        [MonoPInvokeCallback(typeof(PluginWrapper.OnSuccess))]
        private static void OnSuccess()
        {
            Debug.Log(new StringBuilder("[Unity] Webview loaded successfully!"));
        }

        internal void SetUrl(string url)
        {
            this.url = url;
        }

        internal void SetHtml(string htmlString)
        {
            this.htmlString = htmlString;
        }

        internal void SetFilePath(string filePath)
        {
            this.filePath = filePath;
        }

        internal void SetPadding(PaddingId paddingId, float value)
        {
#if UNITY_WEBGL
            switch (paddingId)
            {
                case PaddingId.Top:
                    miscellaneous.padding.top = value;
                    break;
                case PaddingId.Bottom:
                    miscellaneous.padding.bottom = value;
                    break;
                case PaddingId.Leading:
                    miscellaneous.padding.leading = value;
                    break;
                case PaddingId.Trailing:
                    miscellaneous.padding.trailing = value;
                    break;
            }
#endif
        }

        internal void EnableRoundCorners(bool enabled)
        {
#if UNITY_WEBGL
            miscellaneous.padding.enableRoundCorners = enabled;
#endif
        }

        internal void SetRoundCornerRadius(float radius)
        {
#if UNITY_WEBGL
            miscellaneous.padding.roundCornerRadius = Mathf.Max(0.0f, radius);
#endif
        }

        internal void SetTimeout(double timeout)
        {
            miscellaneous.timeout = timeout;
        }

        internal void SetFontDesign(int fontDesign)
        {
#if UNITY_IOS
            miscellaneous.font = (FontDesign)fontDesign;
#elif UNITY_ANDROID || UNITY_WEBGL
            miscellaneous.font = ((FontDesign)fontDesign).ToString();
#endif
        }

        internal void SetFontWeight(int fontWeight)
        {
#if UNITY_IOS
            miscellaneous.fontWeight = (FontWeight)fontWeight;
#elif UNITY_ANDROID || UNITY_WEBGL
            miscellaneous.fontWeight = ((FontWeight)fontWeight).ToString();
#endif
        }

        internal void EnableToolbar(bool enabled)
        {
            toolbar.isToolbarVisible = false;
        }

        internal void SetToolbarSize(int size)
        {
#if UNITY_ANDROID || UNITY_WEBGL
            toolbar.toolbarSize = size;
#endif
        }

        internal void SetToolbarColor(string color)
        {
            toolbar.toolbarColor = color;
        }

        internal void SetToolbarTitle(string title)
        {
            toolbar.title = title;
        }

        internal void SetToolbarTitleColor(string color)
        {
            toolbar.titleColor = color;
        }

        internal void SetToolbarTitleSize(float size)
        {
            toolbar.titleSize = size;
        }

        internal void SetToolbarTitlePadding(int padding)
        {
#if UNITY_ANDROID || UNITY_WEBGL
            toolbar.titlePadding = padding;
#endif
        }

        internal void SetBackIcon(string sfSymbol)
        {
            toolbar.backButton = sfSymbol;
        }

        internal void SetBackColor(string color)
        {
            toolbar.backButtonColor = color;
        }

        internal void SetBackPadding(int padding)
        {
#if UNITY_ANDROID || UNITY_WEBGL
            toolbar.backButtonPadding = padding;
#endif
        }

        internal void SetUseBackToWebGoBack(bool goBack)
        {
#if UNITY_ANDROID
            toolbar.useBackToWebGoBack = goBack;
#endif
        }

        internal void EnableReload(bool enabled)
        {
            toolbar.isReloadVisible = enabled;
        }

        internal void SetReloadIcon(string sfSymbol)
        {
            toolbar.reloadButton = sfSymbol;
        }

        internal void SetReloadColor(string color)
        {
            toolbar.reloadButtonColor = color;
        }

        internal void SetReloadPadding(int padding)
        {
#if UNITY_ANDROID || UNITY_WEBGL
            toolbar.reloadButtonPadding = padding;
#endif
        }

        internal void SetButtonsSize(float size)
        {
#if UNITY_IOS
            toolbar.buttonsSize = size;
#elif UNITY_ANDROID || UNITY_WEBGL
            toolbar.backButtonSize = size;
            toolbar.reloadButtonSize = size;
#endif
        }

        internal void EnableLoadingBar(bool enabled)
        {
            progressView.useBarProgressView = enabled;
        }

        internal void SetLoadingBarColor(string color)
        {
            progressView.loadingBarColor = color;
        }

        internal void SetShouldShowError(bool shouldShow)
        {
#if UNITY_ANDROID
            errorView.shouldShowErrorView = shouldShow;
#endif
        }

        internal void SetErrorImageSize(float size)
        {
            errorView.errorImageSize = size;
        }

        internal void SetErrorImageColor(string color)
        {
#if UNITY_ANDROID
            errorView.errorImageColor = color;
#endif
        }

        internal void SetErrorMessage(string message)
        {
            errorView.errorMessage = message;
        }

        internal void SetErrorTextSize(int size)
        {
#if UNITY_ANDROID
            errorView.textSize = size;
#endif
        }

        internal void SetErrorTextColor(string color)
        {
#if UNITY_ANDROID
            errorView.textColor = color;
#endif
        }

        internal void SetErrorImage(string sfSymbol)
        {
            if (sfSymbol == "Choose SF Symbol")
                return;

            errorView.errorImage = sfSymbol;
        }

        #endregion
    }
}
