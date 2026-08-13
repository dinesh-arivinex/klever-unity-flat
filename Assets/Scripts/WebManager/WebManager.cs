using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Vuplex.WebView;
using static ChangeClassRoom;
using static UnityEngine.Rendering.DebugUI;

public class WebManager : MonoBehaviour
{
    private GamificationManager gamificationManager;
    public GameObject webView;
    public GameObject[] disableitems;
    public CanvasWebViewPrefab _webViewPrefab;
    private string url;
    private float simulationTimer;
    private ServiceScript service;
    public CanvasWebViewLoader canvasWebViewLoader;
    private string simulationScript;
    private GoogleTextToSpeech googleTextToSpeech;
    public TextMeshProUGUI timer;
    private Coroutine timerCoroutine;
    public GameObject buttonNext;
    private bool oneMinutePassed;
    public UnityEvent startingevent;
    public UnityEvent endevent;
    public GameObject simulation;
    public GameObject temp;
    private bool firstTime;
    private bool urlChangedHandlerAttached;
    public bool isobjectXR;
    // Start is called before the first frame update
    void Start()
    {
        googleTextToSpeech = GameManager.Instance.googleTextToSpeech;
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
    public void StopWebViewAudio()
    {
        if (_webViewPrefab != null && _webViewPrefab.WebView != null && _webViewPrefab.WebView.IsInitialized)
        {
            // Execute JavaScript to stop audio playback within the WebView
            string jsCommand = "var audioElements = document.getElementsByTagName('audio');" +
                               "for(var i = 0; i < audioElements.length; i++) {" +
                               "  audioElements[i].pause();" +
                               "}";

            _webViewPrefab.WebView.ExecuteJavaScript(jsCommand);
        }
    }

    public void AssignSimulationAsync(string time, string newUrl, string script)
    {
        simulationScript = script;
        url = newUrl;
        simulationTimer = ConvertTimeToSeconds(time);

        StartLoading(url);

        StartSimulation();
    }
    public void StartSimulation()
    {
        if (!string.IsNullOrEmpty(url))
        {
            var liveDetails = new SimpleJSON.JSONObject();
            liveDetails["isObjectXR"] = isobjectXR;
            liveDetails["durationSeconds"] = simulationTimer;
            Klass.Net.LiveViewPublisher.Instance?.ShowContent("simulation", url, url, liveDetails);


            if (firstTime)
            {
                ChangeSkyBox.SetSkybox(ChangeSkyBox.skyBoxMaterialNew[ChangeClassRoom.Instance.classRoom]);
                EnableWebViewRaycasts();
                simulation.transform.position = temp.transform.position;
                simulation.transform.rotation = temp.transform.rotation;
                startingevent.Invoke();
                timerCoroutine = StartCoroutine(WebViewDuration(simulationTimer));
                ToggleItem(false);
                googleTextToSpeech.StopAudio();
                if (!string.IsNullOrEmpty(simulationScript))
                {
                    googleTextToSpeech.StartTalking(simulationScript);
                }
            }
            else
            {
                ChangeSkyBox.SetSkybox(ChangeSkyBox.skyBoxMaterialNew[ChangeClassRoom.Instance.classRoom]);
                webView.SetActive(true);
                if (isobjectXR)
                {
                    EnableWebViewRaycasts();
                }
            }

            firstTime = true;

        }
        else
        {
            CheckTheStatus();
        }

    }

    private void EnableWebViewRaycasts()
    {
        if (GamificationControllerHandSwitcher.Instance == null)
        {
            return;
        }

        if (GamificationControllerHandSwitcher.Instance.leftHandRaycast != null) GamificationControllerHandSwitcher.Instance.leftHandRaycast.SetActive(true);
        if (GamificationControllerHandSwitcher.Instance.rightHandRaycast != null) GamificationControllerHandSwitcher.Instance.rightHandRaycast.SetActive(true);
        if (GamificationControllerHandSwitcher.Instance.leftControllerRaycast != null) GamificationControllerHandSwitcher.Instance.leftControllerRaycast.SetActive(true);
        if (GamificationControllerHandSwitcher.Instance.rightControllerRaycast != null) GamificationControllerHandSwitcher.Instance.rightControllerRaycast.SetActive(true);
    }

    public void ToggleItem(bool value)
    {

        foreach (GameObject item in disableitems)
        {
            item.SetActive(value);
        }



    }

    private async void StartLoading(string urlToLoad)
    {
        if (_webViewPrefab != null && !string.IsNullOrWhiteSpace(urlToLoad))
        {
            ConfigureWebViewCanvas();
            await _webViewPrefab.WaitUntilInitialized();
            if (url != urlToLoad)
            {
                return;
            }

            ConfigureWebViewCanvas();
            if (!urlChangedHandlerAttached)
            {
                _webViewPrefab.WebView.UrlChanged += (sender, eventArgs) => {
                    Debug.Log("[WebManager] URL changed: " + eventArgs.Url);
                    if (!string.IsNullOrEmpty(eventArgs.Url) && eventArgs.Url.Contains("klever://"))
                    {
                        Debug.Log("[WebManager] Deep link 'klever://' detected. Closing simulation.");
                        CheckTheStatus();
                    }
                };
                urlChangedHandlerAttached = true;
            }
            _webViewPrefab.WebView.LoadUrl(urlToLoad);
        }
    }

    private void ConfigureWebViewCanvas()
    {
        if (_webViewPrefab == null)
        {
            return;
        }

        var canvas = _webViewPrefab.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            var canvasHost = _webViewPrefab.transform.parent;
            if (canvasHost == null || canvasHost.GetComponent<RectTransform>() == null)
            {
                canvasHost = _webViewPrefab.transform;
            }

            canvas = canvasHost.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                                              AdditionalCanvasShaderChannels.Normal |
                                              AdditionalCanvasShaderChannels.Tangent;
        }

        if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
        {
            var eventCamera = Camera.main;
            if (eventCamera == null)
            {
                eventCamera = FindAnyObjectByType<Camera>();
            }

            if (eventCamera != null)
            {
                canvas.worldCamera = eventCamera;
            }
            else
            {
                Debug.LogWarning("[WebManager] No active camera was found for the WebView canvas.");
            }
        }

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
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

            // aterter reach one min
            if (timeRemaining <= duration - 60f && !oneMinutePassed)
            {
                oneMinutePassed = true;
                buttonNext.SetActive(true);
            }

            yield return new WaitForSeconds(1f); // Wait for 1 second
            timeRemaining -= 1f; // Decrease remaining time by 1 second
        }

        // After the countdown is complete, check the status
        //CheckTheStatus();
    }
    public void CheckTheStatus()

    {
        Klass.Net.LiveViewPublisher.Instance?.HideContent("simulation-completed");
        //PlayerPrefs.SetInt("InSimulation", 0);
        PlayerPrefs.Save();

        endevent.Invoke();
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
        StopWebViewAudio();
        // webView.SetActive(false );
        Destroy(webView);
        service.AssignTaskBasedOnCondition(GameStatus.Mcq);
        gamificationManager.AssignGames();
    }
    // Update is called once per frame

}
