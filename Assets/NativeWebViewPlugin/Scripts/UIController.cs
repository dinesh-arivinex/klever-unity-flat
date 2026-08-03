namespace NativeWebView
{
    using System.Collections.Generic;
    using System.Globalization;
    using UnityEngine;
    using UnityEngine.UI;

    public class UIController : MonoBehaviour
    {
        [Header("Color Picker")] [SerializeField]
        private FlexibleColorPicker _flexibleColorPicker;

        [Header("iOS Icons Json")] [SerializeField] TextAsset _iosIcons;
        [Header("Android Icons Json")] [SerializeField] TextAsset _androidIcons;
        [Header("WebGL Icons Json")] [SerializeField] TextAsset _webglIcons;

        private IconsModel iconsModel;

        [Header("Url")] [SerializeField] TMPro.TMP_InputField _urlInput;
        [SerializeField] Toggle _toggleUrl;

        [Header("Html string")] [SerializeField]
        TMPro.TMP_InputField _htmlInput;

        [SerializeField] Toggle _toggleHtml;

        [Header("Html file")] [SerializeField] TMPro.TMP_InputField _fileInput;
        [SerializeField] Toggle _toggleFile;

        [Header("Expand/Collapse Button")] [SerializeField]
        Sprite _expandActive;

        [SerializeField] Sprite _expandDeactive;

        [Header("Custom")] [Header("Miscellaneous")] [SerializeField]
        Button _collapseExpandMisc;
        
        [SerializeField] GameObject _misc;
        [SerializeField] AdvancedDropdown _fontDesign;
        [SerializeField] AdvancedDropdown _fontWeight;
        [SerializeField] Slider _timeout;
        [SerializeField] TMPro.TextMeshProUGUI _timeoutValue;
        
        [Header("Web Padding")]
        [SerializeField] private Slider _topPadding;
        [SerializeField] private Slider _bottomPadding;
        [SerializeField] private Slider _leadingPadding;
        [SerializeField] private Slider _trailingPadding;
        [SerializeField] private Toggle _roundCornersToggle;
        [SerializeField] private TMPro.TMP_InputField _roundCornerRadiusInput;
        [SerializeField] TMPro.TextMeshProUGUI _displayTopPaddingValue;
        [SerializeField] TMPro.TextMeshProUGUI _displayBottomPaddingValue;
        [SerializeField] TMPro.TextMeshProUGUI _displayLeadingPaddingValue;
        [SerializeField] TMPro.TextMeshProUGUI _displayTrailingPaddingValue;

        [Header("Toolbar")] [SerializeField] Button _collapseExpandToolbar;
        [SerializeField] GameObject _toolbar;
        [SerializeField] Toggle _toolbarToggle;
        [SerializeField] Slider _toolbarSize;
        [SerializeField] TMPro.TextMeshProUGUI _toolbarSizeValue;
        [SerializeField] TMPro.TMP_InputField _toolbarColor;
        [SerializeField] TMPro.TMP_InputField _toolbarTitle;
        [SerializeField] TMPro.TMP_InputField _toolbarTitleColor;
        [SerializeField] Slider _toolbarTitleSize;
        [SerializeField] TMPro.TextMeshProUGUI _titleSizeValue;
        [SerializeField] Slider _toolbarTitlePadding;
        [SerializeField] TMPro.TextMeshProUGUI _titlePaddingValue;
        [SerializeField] AdvancedDropdown _backButtonIcon;
        [SerializeField] TMPro.TMP_InputField _backButtonColor;
        [SerializeField] Slider _backPadding;
        [SerializeField] TMPro.TextMeshProUGUI _backPaddingValue;
        [SerializeField] Toggle _useBackToWebGoBackToggle;
        [SerializeField] Toggle _reloadToggle;
        [SerializeField] AdvancedDropdown _reloadButtonIcon;
        [SerializeField] TMPro.TMP_InputField _reloadButtonColor;
        [SerializeField] Slider _reloadPadding;
        [SerializeField] TMPro.TextMeshProUGUI _reloadPaddingValue;
        [SerializeField] Slider _buttonsSize;
        [SerializeField] TMPro.TextMeshProUGUI __buttonsSizeValue;

        [Header("Loading")] [SerializeField] Button _collapseExpandLoading;
        [SerializeField] GameObject _loading;
        [SerializeField] Toggle _barToggle;
        [SerializeField] TMPro.TMP_InputField _loadingBarColor;

        [Header("Error")] [SerializeField] Button _collapseExpandError;
        [SerializeField] GameObject _error;
        [SerializeField] Toggle _shouldShowErrorToggle;
        [SerializeField] AdvancedDropdown _errorImage;
        [SerializeField] Slider _errorImageSize;
        [SerializeField] TMPro.TextMeshProUGUI _errorImageSizeValue;
        [SerializeField] TMPro.TMP_InputField _errorImageColor;
        [SerializeField] TMPro.TMP_InputField _errorMessage;
        [SerializeField] Slider _errorMessageTextSize;
        [SerializeField] TMPro.TextMeshProUGUI _errorMessageTextSizeValue;
        [SerializeField] TMPro.TMP_InputField _errorTextColor;

        [Header("Open")] [SerializeField] Button _open;

        private void Start()
        {
            TextAsset currentIcons = _androidIcons;

#if UNITY_IOS
            currentIcons = _iosIcons != null ? _iosIcons : _androidIcons;
#elif UNITY_ANDROID
            currentIcons = _androidIcons;
#elif UNITY_WEBGL
            currentIcons = _webglIcons != null ? _webglIcons : _androidIcons;
#endif

            if (currentIcons == null)
            {
                Debug.LogError("[UIController] No icon definition TextAsset configured.");
                iconsModel = new IconsModel { icons = new List<string>() };
            }
            else
            {
                iconsModel = JsonUtility.FromJson<IconsModel>(currentIcons.text);
            }

            #region SIMPLE

            WebViewBehaviour.Singleton.OnFileSaved = OnFileSaved;

            _urlInput.onEndEdit.AddListener(SetUrl);
            _toggleUrl.onValueChanged.AddListener(WebViewBehaviour.Singleton.ToggleUrl);

            _htmlInput.onEndEdit.AddListener(SetHtml);
            _toggleHtml.onValueChanged.AddListener(WebViewBehaviour.Singleton.ToggleHtmlString);

            _fileInput.onEndEdit.AddListener(SetFilePath);
            _toggleFile.onValueChanged.AddListener(WebViewBehaviour.Singleton.ToggleHtmlFile);

            #endregion

            #region CUSTOM

            HandleMiscInput();
            HandleToolbarInput();
            HandleLoadingInput();
            HandleErrorInput();

            #endregion

            _open.onClick.AddListener(BuildWebView);

            InitializeValues();
        }

        public void OpenColorPicker(TMPro.TMP_InputField inputField)
        {
            _flexibleColorPicker.gameObject.SetActive(true);

            _flexibleColorPicker.OnHexChanged.AddListener(pikedColor =>
            {
                inputField.text = pikedColor;
                inputField.onEndEdit.Invoke(inputField.text);
            });
        }

        public void CloseColorPicker()
        {
            _flexibleColorPicker.onColorChange.RemoveAllListeners();
            _flexibleColorPicker.OnHexChanged.RemoveAllListeners();
            _flexibleColorPicker.gameObject.SetActive(false);
        }

        private void InitializeValues()
        {
            SetComponentsVisibility();
            SetUrl(_urlInput.text);
            SetHtml(_htmlInput.text);

            SetFontDesign(0);
            SetFontWeight(0);
            SetTimeout(_timeout.value);
            SetUpPadding();

            EnableToolbar(_toolbarToggle.isOn);
            SetToolbarSize(_toolbarSize.value);
            SetToolbarColor(_toolbarColor.text);
            SetToolbarTitle(_toolbarTitle.text);
            SetToolbarTitleColor(_toolbarTitleColor.text);
            SetToolbarTitleSize(_toolbarTitleSize.value);
            SetToolbarTitlePadding(_toolbarTitlePadding.value);
            SetBackIcon(0);
            SetToolbarTitlePadding(_toolbarTitlePadding.value);
            SetBackColor(_backButtonColor.text);
            SetUseBackToGoWebBack(_useBackToWebGoBackToggle.isOn);
            SetBackPadding(_backPadding.value);
            EnableReload(_reloadToggle.isOn);
            SetReloadIcon(0);
            SetReloadColor(_reloadButtonColor.text);
            SetReloadPadding(_reloadPadding.value);
            SetButtonsSize(_buttonsSize.value);

            SetLoadingBarColor(_loadingBarColor.text);

            SetErrorImageSize(_errorImageSize.value);
            SetErrorImage(0);
        }

        private void SetComponentsVisibility()
        {
#if UNITY_IOS
            _toolbarSize.gameObject.SetActive(false);
            _backPadding.gameObject.SetActive(false);
            _reloadPadding.gameObject.SetActive(false);
            _toolbarTitlePadding.gameObject.SetActive(false);
            _useBackToWebGoBackToggle.gameObject.SetActive(false);
#endif

#if !UNITY_WEBGL
            _topPadding.gameObject.SetActive(false);
            _bottomPadding.gameObject.SetActive(false);
            _leadingPadding.gameObject.SetActive(false);
            _trailingPadding.gameObject.SetActive(false);
            _roundCornersToggle.gameObject.SetActive(false);
            _roundCornerRadiusInput.gameObject.SetActive(false);
#endif
        }

        private void BuildWebView()
        {
            WebViewBehaviour.Singleton.BuildWebView();
        }

        private void HandleMiscInput()
        {
            _collapseExpandMisc.onClick.AddListener(delegate { CollapseOrExpand(_misc, _collapseExpandMisc); });

            _fontDesign.AddOptions(FontDesignOptions().ToArray());
            _fontDesign.onChangedValue += SetFontDesign;

            _fontWeight.AddOptions(FontWeightOptions().ToArray());
            _fontWeight.onChangedValue += SetFontWeight;

            _timeout.onValueChanged.AddListener(SetTimeout);

            _topPadding.onValueChanged.AddListener(padding =>
            {
                SetPadding(
                    PaddingId.Top,
                    padding,
                    _displayTopPaddingValue
                );
            });

            _bottomPadding.onValueChanged.AddListener(padding =>
            {
                SetPadding(
                    PaddingId.Bottom,
                    padding,
                    _displayBottomPaddingValue
                );
            });

            _leadingPadding.onValueChanged.AddListener(padding =>
            {
                SetPadding(
                    PaddingId.Leading,
                    padding,
                    _displayLeadingPaddingValue
                );
            });

            _trailingPadding.onValueChanged.AddListener(padding =>
            {
                SetPadding(
                    PaddingId.Trailing,
                    padding,
                    _displayTrailingPaddingValue
                );
            });

            if (_roundCornersToggle != null)
            {
                _roundCornersToggle.onValueChanged.AddListener(EnableRoundCorners);
            }

            if (_roundCornerRadiusInput != null)
            {
                _roundCornerRadiusInput.onEndEdit.AddListener(SetRoundCornerRadius);
            }
        }

        private void HandleToolbarInput()
        {
            _collapseExpandToolbar.onClick.AddListener(delegate
            {
                CollapseOrExpand(_toolbar, _collapseExpandToolbar);
            });

            _toolbarToggle.onValueChanged.AddListener(EnableToolbar);
            _toolbarSize.onValueChanged.AddListener(SetToolbarSize);
            _toolbarTitlePadding.onValueChanged.AddListener(SetToolbarTitlePadding);
            _toolbarColor.onEndEdit.AddListener(SetToolbarColor);

            _toolbarColor.onSelect.AddListener(arg0 => { OpenColorPicker(_toolbarColor); });

            _toolbarTitle.onEndEdit.AddListener(SetToolbarTitle);
            _toolbarTitleColor.onEndEdit.AddListener(SetToolbarTitleColor);

            _toolbarTitleColor.onSelect.AddListener(arg0 => { OpenColorPicker(_toolbarTitleColor); });

            _toolbarTitleSize.onValueChanged.AddListener(SetToolbarTitleSize);

            _backButtonIcon.AddOptions(GetIcons().ToArray());
            _backButtonIcon.onChangedValue += SetBackIcon;
            _backButtonColor.onEndEdit.AddListener(SetBackColor);

            _backButtonColor.onSelect.AddListener(arg0 => { OpenColorPicker(_backButtonColor); });

            _useBackToWebGoBackToggle.onValueChanged.AddListener(SetUseBackToGoWebBack);
            _backPadding.onValueChanged.AddListener(SetBackPadding);

            _reloadToggle.onValueChanged.AddListener(EnableReload);
            _reloadButtonIcon.AddOptions(GetIcons().ToArray());
            _reloadButtonIcon.onChangedValue += SetReloadIcon;
            _reloadButtonColor.onEndEdit.AddListener(SetReloadColor);

            _reloadButtonColor.onSelect.AddListener(arg0 => { OpenColorPicker(_reloadButtonColor); });

            _reloadPadding.onValueChanged.AddListener(SetReloadPadding);

            _buttonsSize.onValueChanged.AddListener(SetButtonsSize);
        }

        private void SetButtonsSize(float size)
        {
            WebViewBehaviour.Singleton.SetButtonsSize(size);

            SetTextValue(size, __buttonsSizeValue);
        }

        private void SetReloadColor(string color)
        {
            WebViewBehaviour.Singleton.SetReloadColor(color);
        }

        private void SetReloadIcon(int value)
        {
            var sfSymbol = GetIcons()[value];
            WebViewBehaviour.Singleton.SetReloadIcon(sfSymbol);
        }

        private void EnableReload(bool enabled)
        {
            WebViewBehaviour.Singleton.EnableReload(enabled);
        }

        private void SetBackColor(string color)
        {
            WebViewBehaviour.Singleton.SetBackColor(color);
        }

        private void SetBackIcon(int value)
        {
            var sfSymbol = GetIcons()[value];
            WebViewBehaviour.Singleton.SetBackIcon(sfSymbol);
        }

        private void SetToolbarTitleSize(float size)
        {
            WebViewBehaviour.Singleton.SetToolbarTitleSize(size);

            SetTextValue(size, _titleSizeValue);
        }

        private void SetToolbarTitlePadding(float padding)
        {
            WebViewBehaviour.Singleton.SetToolbarTitlePadding((int)padding);

            SetTextValue(padding, _titlePaddingValue);
        }

        private void SetToolbarTitleColor(string color)
        {
            WebViewBehaviour.Singleton.SetToolbarTitleColor(color);
        }

        private void SetToolbarTitle(string title)
        {
            WebViewBehaviour.Singleton.SetToolbarTitle(title);
        }

        private void SetToolbarColor(string color)
        {
            WebViewBehaviour.Singleton.SetToolbarColor(color);
        }

        private void SetToolbarSize(float barSize)
        {
            WebViewBehaviour.Singleton.SetToolbarSize((int)barSize);

            SetTextValue(barSize, _toolbarSizeValue);
        }

        private void EnableToolbar(bool enabled)
        {
            WebViewBehaviour.Singleton.EnableToolbar(enabled);
        }

        private void SetUseBackToGoWebBack(bool enabled)
        {
            WebViewBehaviour.Singleton.SetUseBackToWebGoBack(enabled);
        }

        private void SetBackPadding(float padding)
        {
            WebViewBehaviour.Singleton.SetBackPadding((int)padding);

            SetTextValue(padding, _backPaddingValue);
        }

        private void SetReloadPadding(float padding)
        {
            WebViewBehaviour.Singleton.SetReloadPadding((int)padding);

            SetTextValue(padding, _reloadPaddingValue);
        }

        private void HandleLoadingInput()
        {
            _collapseExpandLoading.onClick.AddListener(delegate
            {
                CollapseOrExpand(_loading, _collapseExpandLoading);
            });

            _barToggle.onValueChanged.AddListener(EnableLoadingBar);
            _loadingBarColor.onEndEdit.AddListener(SetLoadingBarColor);

            _loadingBarColor.onSelect.AddListener(arg0 => { OpenColorPicker(_loadingBarColor); });

        }

        private void SetLoadingBarColor(string color)
        {
            WebViewBehaviour.Singleton.SetLoadingBarColor(color);
        }

        private void EnableLoadingBar(bool enabled)
        {
            WebViewBehaviour.Singleton.EnableLoadingBar(enabled);
        }

        private void SetTextValue(float value, TMPro.TextMeshProUGUI text)
        {
            text.text = $"{value.ToString("f1")}";
        }

        private void HandleErrorInput()
        {
            _collapseExpandError.onClick.AddListener(delegate { CollapseOrExpand(_error, _collapseExpandError); });

            _errorImageSize.onValueChanged.AddListener(SetErrorImageSize);
            _errorImage.AddOptions(GetIcons().ToArray());
            _errorImage.onChangedValue += SetErrorImage;
            _shouldShowErrorToggle.onValueChanged.AddListener(SetShouldShowError);
            _errorImageColor.onEndEdit.AddListener(SetErrorImageColor);

            _errorImageColor.onSelect.AddListener(arg0 => { OpenColorPicker(_errorImageColor); });

            _errorMessage.onEndEdit.AddListener(SetErrorMessage);
            _errorMessageTextSize.onValueChanged.AddListener(SetErrorTextSize);
            _errorTextColor.onEndEdit.AddListener(SetErrorTextColor);

            _errorTextColor.onSelect.AddListener(arg0 => { OpenColorPicker(_errorTextColor); });
        }

        private void SetShouldShowError(bool shouldShow)
        {
            WebViewBehaviour.Singleton.SetShouldShowError(shouldShow);
        }

        private void SetErrorImage(int value)
        {
            var sfSymbol = GetIcons()[value];
            WebViewBehaviour.Singleton.SetErrorImage(sfSymbol);
        }

        private void SetErrorImageSize(float size)
        {
            WebViewBehaviour.Singleton.SetErrorImageSize(size);

            SetTextValue(size, _errorImageSizeValue);
        }

        private void SetErrorImageColor(string color)
        {
            WebViewBehaviour.Singleton.SetErrorImageColor(color);
        }

        private void SetErrorMessage(string message)
        {
            WebViewBehaviour.Singleton.SetErrorMessage(message);
        }

        private void SetErrorTextSize(float size)
        {
            WebViewBehaviour.Singleton.SetErrorTextSize((int)size);

            SetTextValue(size, _errorMessageTextSizeValue);
        }

        private void SetErrorTextColor(string color)
        {
            WebViewBehaviour.Singleton.SetErrorTextColor(color);
        }

        private void SetFontWeight(int fontWeight)
        {
            WebViewBehaviour.Singleton.SetFontWeight(fontWeight);
        }

        private void SetFontDesign(int fontDesign)
        {
            WebViewBehaviour.Singleton.SetFontDesign(fontDesign);
        }

        private void SetTimeout(float timeout)
        {
            WebViewBehaviour.Singleton.SetTimeout((double)timeout);

            SetTextValue(timeout, _timeoutValue);
        }

        private void SetPadding(PaddingId paddingId, float paddingValue, TMPro.TextMeshProUGUI paddingText)
        {
            WebViewBehaviour.Singleton.SetPadding(paddingId, paddingValue);

            SetTextValue(paddingValue, paddingText);
        }

        private void EnableRoundCorners(bool enabled)
        {
            WebViewBehaviour.Singleton.EnableRoundCorners(enabled);
        }

        private void SetRoundCornerRadius(string radiusText)
        {
            if (_roundCornerRadiusInput == null)
                return;

            if (!TryParseFloat(radiusText, out var radius))
            {
                return;
            }

            radius = Mathf.Max(0.0f, radius);
            WebViewBehaviour.Singleton.SetRoundCornerRadius(radius);
            _roundCornerRadiusInput.SetTextWithoutNotify(radius.ToString("0.##", CultureInfo.InvariantCulture));
        }

        private static bool TryParseFloat(string value, out float result)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
                return true;

            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private List<string> FontDesignOptions()
        {
            var tempL = new List<string>();

            for (int i = 0; i < 4; i++)
            {
                tempL.Add(((FontDesign)i).ToString());
            }

            return tempL;
        }

        private List<string> FontWeightOptions()
        {
            var tempL = new List<string>();

            for (int i = 0; i < 9; i++)
            {
                tempL.Add(((FontWeight)i).ToString());
            }

            return tempL;
        }

        private List<string> GetIcons()
        {
            var tempL = new List<string>();

            if (iconsModel != null && iconsModel.icons != null)
            {
                iconsModel.icons.ForEach(icon => tempL.Add(icon));
            }

            if (tempL.Count == 0)
            {
                tempL.Add("←");
                tempL.Add("✕");
            }

            return tempL;
        }

        private void CollapseOrExpand(GameObject target, Button button)
        {
            var isActive = target.activeInHierarchy;

            button.transform.GetChild(0).GetComponent<Image>().sprite = isActive ? _expandActive : _expandDeactive;
            target.SetActive(!isActive);
        }

        private void OnFileSaved(string filePath)
        {
            _fileInput.text = filePath;

            SetFilePath(filePath);
        }

        private void SetFilePath(string filePath)
        {
            WebViewBehaviour.Singleton.SetFilePath(filePath);
        }

        private void SetHtml(string htmlString)
        {
            WebViewBehaviour.Singleton.SetHtml(htmlString);
        }

        private void SetUrl(string url)
        {
            WebViewBehaviour.Singleton.SetUrl(url);
        }

        private void SetUpPadding()
        {
            WebViewBehaviour.Singleton.SetPadding(PaddingId.Top, _topPadding.value);
            WebViewBehaviour.Singleton.SetPadding(PaddingId.Bottom, _bottomPadding.value);
            WebViewBehaviour.Singleton.SetPadding(PaddingId.Leading, _leadingPadding.value);
            WebViewBehaviour.Singleton.SetPadding(PaddingId.Trailing, _trailingPadding.value);

            if (_roundCornersToggle != null)
            {
                EnableRoundCorners(_roundCornersToggle.isOn);
            }

            if (_roundCornerRadiusInput != null)
            {
                SetRoundCornerRadius(_roundCornerRadiusInput.text);
            }
        }
    }
}
