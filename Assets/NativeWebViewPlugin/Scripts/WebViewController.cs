namespace NativeWebView
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using TMPro;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;

    public class WebViewController : MonoBehaviour
    {
        public static WebViewController Instance { get; private set; }

        [Header("URL Settings")]
        public string _fallbackUrl = "https://example.com";
        public TextMeshProUGUI timer;
        public string displayTimeStr;
        public UnityEvent startingevent;
        public UnityEvent endevent;
        private Coroutine timerCoroutine;
        public float simulationTimer;
        public GameObject webViewChild;
        private GamificationManager gamificationManager;
        private ServiceScript service;

        [Header("Container Settings")]
        [Tooltip("Assign your Canvas (1) or a Panel here to make the WebView render inside it")]
        [SerializeField] RectTransform webViewContainer;
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        private void Start()
        {
            service = GameManager.Instance.serviceScript;
            gamificationManager = GameManager.Instance.gamificationManager;
        }
        public float ConvertTimeToSeconds(string displayTime)
        {
            if (string.IsNullOrEmpty(displayTime))
            {
                Debug.LogError("Input time string is null or empty.");
                return 0f;
            }

            // Split the time string into minutes and seconds
            string[] timeComponents = displayTime.Split(':');

            // Check if the time string has the correct format
            if (timeComponents.Length != 2)
            {
                Debug.LogError("Invalid time format. Expected format is MM:ss.");
                return 0f;
            }

            // Parse minutes and seconds from the split string
            if (!int.TryParse(timeComponents[0], out int minutes) || !int.TryParse(timeComponents[1], out int seconds))
            {
                Debug.LogError("Failed to parse minutes or seconds.");
                return 0f;
            }

            // Calculate total seconds
            float totalSeconds = minutes * 60f + seconds;

            return totalSeconds;
        }
        public void AssignSimulation(string time, string url)
        {
            _fallbackUrl = url;
            startingevent.Invoke();
            simulationTimer = ConvertTimeToSeconds(time);
            
            if (timerCoroutine != null) StopCoroutine(timerCoroutine);
            timerCoroutine = StartCoroutine(WebViewDuration(simulationTimer));
            
            if (webViewContainer != null)
            {
                Canvas.ForceUpdateCanvases();
                CalculateAndSetPadding();
            }
            
            if (this.gameObject.transform.childCount > 0)
            {
                this.gameObject.transform.GetChild(0).gameObject.SetActive(true);
            }
            
            BuildWebView();
        }

        public void StartSimulation()
        {
            if (string.IsNullOrEmpty(displayTimeStr) || string.IsNullOrEmpty(_fallbackUrl))
            {
                Debug.LogWarning("[WebViewController] Missing displayTimeStr or _fallbackUrl when starting simulation!");
                return;
            }
            AssignSimulation(displayTimeStr, _fallbackUrl);
        }

        private void CalculateAndSetPadding()
        {
            Vector3[] corners = new Vector3[4];
            webViewContainer.GetWorldCorners(corners);

            Canvas canvas = webViewContainer.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = canvas.worldCamera;
                if (cam == null) cam = Camera.main;
            }

            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

            float leading = Mathf.Max(0, min.x);
            float trailing = Mathf.Max(0, Screen.width - max.x);
            float bottom = Mathf.Max(0, min.y);
            float top = Mathf.Max(0, Screen.height - max.y);

            WebViewBehaviour.Singleton.SetPadding(PaddingId.Leading, leading);
            WebViewBehaviour.Singleton.SetPadding(PaddingId.Trailing, trailing);
            WebViewBehaviour.Singleton.SetPadding(PaddingId.Bottom, bottom);
            WebViewBehaviour.Singleton.SetPadding(PaddingId.Top, top);
        }

        public void BuildWebView()
        {
            WebViewBehaviour.Singleton.ToggleUrl(true);
            WebViewBehaviour.Singleton.SetUrl(_fallbackUrl);
            WebViewBehaviour.Singleton.BuildWebView();

        }

         IEnumerator WebViewDuration(float duration)
        {

        float timeRemaining = duration;
        while (timeRemaining > 0)
        {
            // Calculate minutes and seconds
            int minutes = Mathf.FloorToInt(timeRemaining / 60);
            int seconds = Mathf.FloorToInt(timeRemaining % 60);

            // Update the timer text to display in minutes and seconds format
            timer.text = string.Format("{0}:{1:00}", minutes, seconds);

            yield return new WaitForSeconds(1f); // Wait for 1 second
            timeRemaining -= 1f; // Decrease remaining time by 1 second
        }

        // After the countdown is complete, check the status
        CheckTheStatus();
    }
    public void CheckTheStatus()

    {
        //PlayerPrefs.SetInt("InSimulation", 0);
        PlayerPrefs.Save();

        endevent.Invoke();
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
        
        if (WebViewBehaviour.Singleton != null)
        {
            WebViewBehaviour.Singleton.CloseWebView();
        }

        Destroy(webViewChild);
        service.AssignTaskBasedOnCondition(GameStatus.Mcq);
        gamificationManager.AssignGames();
    }
    }
     

}
