using System;
using UnityEngine;

namespace NativeWebView
{

    public enum FontDesign
    {
#if UNITY_IOS || UNITY_WEBGL
        defaultFont = 0,
        monospaced,
        rounded,
        serif
#elif UNITY_ANDROID
        Default = 0,
        Cursive,
        SansSerif,
        Serif,
        Monospace
#endif
    }

    public enum FontWeight
    {
#if UNITY_IOS || UNITY_WEBGL
        black = 0,
        bold,
        heavy,
        light,
        medium,
        regular,
        semibold,
        thin,
        ultraLight
#elif UNITY_ANDROID
        Normal = 0,
        SemiBold,
        Bold,
        Medium,
        Black,
        ExtraBold,
        ExtraLight,
        Light,
        Thin
#endif
    }

    public class WebViewBuilder
    {
        private WebViewParams webViewParams = new WebViewParams();

        public WebViewBuilder ToggleUrl()
        {
            webViewParams.shouldLoadHtmlString = false;
            webViewParams.shouldLoadFromFileUrl = false;

            return this;
        }

        public WebViewBuilder ToggleHtmlString()
        {
            webViewParams.shouldLoadHtmlString = true;
            webViewParams.shouldLoadFromFileUrl = false;

            return this;
        }

        public WebViewBuilder ToggleHtmlFile()
        {
            webViewParams.shouldLoadHtmlString = false;
            webViewParams.shouldLoadFromFileUrl = true;

            return this;
        }

        #region CUSTOMIZATION

        public WebViewBuilder CustomMiscellaneous(Miscellaneous misc)
        {
            webViewParams.miscellaneous = misc;

            return this;
        }

        public WebViewBuilder CustomToolbar(Toolbar toolbar)
        {
            webViewParams.toolbar = toolbar;

            return this;
        }

        public WebViewBuilder CustoProgressView(ProgressView progressView)
        {
            webViewParams.progressView = progressView;

            return this;
        }

        public WebViewBuilder CustomPadding(Padding padding)
        {
#if UNITY_WEBGL
            webViewParams.miscellaneous.padding = padding;
#endif

            return this;
        }

        public WebViewBuilder CustoErrorView(ErrorView errorView)
        {
            webViewParams.errorView = errorView;

            return this;
        }

        public WebViewBuilder HandleCallbacks(PluginWrapper.OnError onError, PluginWrapper.OnSuccess onSuccess)
        {
            webViewParams.OnError = onError;
            webViewParams.OnSuccess = onSuccess;

            return this;
        }

        #endregion

        public void BuildWebView()
        {
            var json = JsonUtility.ToJson(webViewParams);
            PluginWrapper.BuildWebView(json, webViewParams.OnError, webViewParams.OnSuccess);
        }

        internal WebViewBuilder SetParams(string url, bool useHtmlString, string htmlString, bool useFile, string filePath)
        {
            webViewParams.url = url;
            webViewParams.shouldLoadHtmlString = useHtmlString;
            webViewParams.htmlString = htmlString;
            webViewParams.shouldLoadFromFileUrl = useFile;
            webViewParams.filePath = filePath;

            return this;
        }
    }

    public class WebViewParams
    {
        public string url = "https://assetstore.unity.com/publishers/24932";
        public bool shouldLoadHtmlString = false;
        public string htmlString = "<html><body><p>Hello!</p></body></html>";
        public bool shouldLoadFromFileUrl = false;
        public string filePath = "Application.PersistenceDataPath";
        public Miscellaneous miscellaneous = new Miscellaneous();
        public Toolbar toolbar = new Toolbar();
        public ProgressView progressView = new ProgressView();
        public ErrorView errorView = new ErrorView();

        public PluginWrapper.OnSuccess OnSuccess;
        public PluginWrapper.OnError OnError;
    }

    [System.Serializable]
    public class Miscellaneous
    {
        public double timeout = 8.0;

#if UNITY_IOS
        public FontDesign font;
        public FontWeight fontWeight;
        public bool ignoreSafeArea = false;
        public bool isFullScreen = true;
        public bool isSheet = false;
#elif UNITY_ANDROID
        public String font;
        public String fontWeight;
#elif UNITY_WEBGL
        public String font;
        public String fontWeight;
        public Padding padding = new Padding();
#endif
    }

    [System.Serializable]
    public class Toolbar
    {
        public bool isToolbarVisible = true;
        public string toolbarColor = "#0066b2";
        public float titleSize = 20.0f;
        public string title = "Assets For Devs";
        public string titleColor = "#faf9f7";
        public string backButton = "chevron.left";
        public string backButtonColor = "##05b4ff";
        public bool isReloadVisible = true;
        public string reloadButton = "arrow.circlepath";
        public string reloadButtonColor = "##ff05d5";

#if UNITY_IOS
        public float buttonsSize = 20.0f;
#elif UNITY_ANDROID || UNITY_WEBGL
        public int toolbarSize = 56;
        public float titlePadding = 20;
        public float backButtonSize = 20.0f;
        public bool useBackToWebGoBack = false;
        public int backButtonPadding = 20;
        public float reloadButtonSize = 20;
        public float reloadButtonPadding = 20;
#endif
    }

    [System.Serializable]
    public class ProgressView
    {
        public bool useBarProgressView = true;
        public string progressViewMessage = "Loading";
        public string loadingBarColor = "#3EB489";
        public string loadingTextColor = "#3EB489";
        public FontDesign font;
        public FontWeight fontWeight;
    }

    [System.Serializable]
    public class Padding
    {
        public float top = 0.0f;
        public float bottom = 0.0f;
        public float leading = 0.0f;
        public float trailing = 0.0f;
        public bool enableRoundCorners = false;
        public float roundCornerRadius = 0.0f;
    }

    [System.Serializable]
    public class ErrorView
    {
        public string errorMessage = "Something went wrong!";
        public string errorImage = "figure.walk.motion.trianglebadge.exclamationmark";
        public double errorImageSize = 200.0;

#if UNITY_ANDROID
        public bool shouldShowErrorView = false;
        public string errorImageColor = "#ff0000";
        public int textSize = 20;
        public string textColor = "#ff0000";
#endif
    }

}
