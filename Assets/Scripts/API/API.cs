
// LATEST UPDATE: SECURE WEB GET PARSE LOGGING ACTIVE
using GLTFast.Schema;
using NativeWebView;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Threading.Tasks;
#endif

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

using UnityEngine.Video;


public class API : MonoBehaviour
{
    [System.Serializable]
    public class ApiResponse
    {
        public int code;
        public string message;
        // Include other fields as necessary
    }
    [HideInInspector]
    public string assessmentsString;
    public GameObject boardParent;
    public UnityEvent immediatActionApiDownload;
    private ServiceScript serviceScript;
    private JSONNode modelDetail;
    private string teacherCharacterGender;
    private bool isModelDownload;
    private bool isPlayingVideo;
    private JSONArray modelDetails;
    private int currentIndex = 0;
    public List<string> allModelCoordinates;
    [HideInInspector]
    public Dictionary<string, string> modelIdDatas;
    public List<string> allUrl;
    private ClassVrManager VrManager;
    private int modelDetailsCount;
    private string language;
    List<string> typeOfGames = new List<string>();
    private JSONArray allModelDetails;
    private List<int> modelCoordinateCount = new List<int>();
    private int annotationIncrimenter = 0;
    public GameObject DeviceID;
    public bool isLoadedResponse;
    private VideoManager videoManager;
    public VideoPlayer youtubevideoPlayer;
    public bool is360Image;

    public string youtubeURL;
    private AddingImage addingImage;
    private List<float> modelDisplayTime = new List<float>();
    private AssetDownloader assetDownloader;
    private bool isThreeSixty;
    private ThreesixtyImageManager threesixtyImageManager;
    private ChangeClassRoom room;
    private string youtubeUrlTest;
    //private YoutubeTestManager youtubeTestManager;

    public string YouTubeUrlTest
    {
        get { return youtubeUrlTest; }
        set { youtubeUrlTest = value; }
    }
    private void Start()
    {
        room = GameManager.Instance.changeClassRoom;
        threesixtyImageManager = GameManager.Instance.threesixtyImageManager;
        assetDownloader = GameManager.Instance.assetDownloader;
        VrManager = GameManager.Instance.vrManager;
        addingImage = GameManager.Instance.addingImage;
        serviceScript = GameManager.Instance.serviceScript;
        videoManager = GameManager.Instance.videoManager;

        CallingClassVrExperiance();
    }

    public void CallingClassVrExperiance()
    {
        StartCoroutine(WebGetRequest(Klass.Net.KlassEndpoints.V1("experience")));

    }
    public bool IsmodelDownload()
    {
        return isModelDownload;
    }
    public IEnumerator WebGetRequest(string api)
    {
        var apireq = api;
        string jsonData;
        var req = new UnityWebRequest(apireq, "GET");
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = 30;
        req.SetRequestHeader("Content-Type", "application/json");

        // Set device authentication header
        req.SetRequestHeader("device-id", Klass.Net.KlassDeviceIdentity.Value);
        UnityEngine.Debug.Log($"[API] Querying experience API: {apireq} with device-id header: {Klass.Net.KlassDeviceIdentity.Value}");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success && req.responseCode != 401)
        {
            var failureKind = req.result == UnityWebRequest.Result.ProtocolError
                ? $"HTTP {req.responseCode}"
                : "network/TLS";



            UnityEngine.Debug.LogError($"[API] WebGetRequest {failureKind} error for {apireq}: {req.error}");
            // Report download failure to teacher dashboard via MQTT.
            var synHandler = FindObjectOfType<DeviceSynHandler>();
            if (synHandler != null)
                synHandler.PublishDownloadError($"Experience API {failureKind} error: {req.error}");
            req.Dispose();
            yield break;
        }

        {
            jsonData = req.downloadHandler.text;
            UnityEngine.Debug.Log($"[API] WebGetRequest response code: {req.responseCode}, raw payload: {jsonData}");
            bool parseAsArray = false;
            try
            {
                // Attempt to parse the response JSON as an object (e.g. registration error or configuration)
                var responseData = JsonUtility.FromJson<ApiResponse>(jsonData);
                UnityEngine.Debug.Log($"[API] Parsed as ApiResponse object: code={responseData.code}, message={responseData.message}");
                DeviceID.SetActive(true);
                DeviceIDManager deviceIDManager = DeviceID.GetComponent<DeviceIDManager>();
                deviceIDManager.OneTimeDeviceConfiguration();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log($"[API] JsonUtility object parsing threw (expected for array payloads): {ex.Message}. Treating as experience array.");
                parseAsArray = true;
            }

            if (parseAsArray)
            {
                // Parse experience data defensively first so we can check if it holds a valid session/experience (i.e. starts enabled)
                JSONNode spaceData = null;
                try
                {
                    spaceData = JSONNode.Parse(jsonData);
                }
                catch
                {
                    spaceData = null;
                }

                bool hasValidExperience = (spaceData != null && spaceData.Count > 0 && spaceData[0] != null && spaceData[0]["session"] != null);

                // Determine if we should auto-start on WebGL
                bool isWebGl = false;
#if UNITY_WEBGL
                isWebGl = true;
#endif
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    isWebGl = true;
                }

                bool shouldBypassForWebGl = isWebGl && hasValidExperience;

                UnityEngine.Debug.Log($"[API] State Validation check: isOpenWelcomeScreen={DeviceSynHandler.isOpenWelcomeScreen} (needed: false), isLoadedResponse={isLoadedResponse} (needed: false), shouldBypassForWebGl={shouldBypassForWebGl}");
                if ((DeviceSynHandler.isOpenWelcomeScreen == false || shouldBypassForWebGl) && isLoadedResponse != true)
                {
                    UnityEngine.Debug.Log("[API] State validation succeeded. Parsing experience data...");
                    if (spaceData == null || spaceData.Count == 0 || spaceData[0] == null)
                    {
                        DebugUtil.Log("[API] Experience response was empty or unparseable — aborting load.");
                        var synHandler = FindObjectOfType<DeviceSynHandler>();
                        if (synHandler != null)
                            synHandler.PublishDownloadError("Empty experience response from server");
                        req.Dispose();
                        yield break;
                    }

                    isLoadedResponse = true;
                    var synHandlerMQTT = GameManager.Instance.deviceSynHandler != null ? GameManager.Instance.deviceSynHandler : FindObjectOfType<DeviceSynHandler>();
                    if (synHandlerMQTT != null)
                    {
                        synHandlerMQTT.BootMqtt();
                    }
                    DeviceIDManager deviceIDManager = DeviceID.GetComponent<DeviceIDManager>();
                    deviceIDManager.ToggleFunction(true);
                    deviceIDManager.gameObject.SetActive(false);
                    // LoadingScene.SetActive(true);
                    JSONNode session = spaceData[0]["session"];
                    string sessionId = session != null ? session["_id"].Value : "";
                    string sessionTimeAndDate = session != null ? session["sessionTimeAndDate"].Value : "";

                    // If we bypassed validation because of WebGL, we trigger the autostart UI flow on the sync handler
                    if (shouldBypassForWebGl && DeviceSynHandler.isOpenWelcomeScreen)
                    {
                        var synHandler = GameManager.Instance.deviceSynHandler != null ? GameManager.Instance.deviceSynHandler : FindObjectOfType<DeviceSynHandler>();
                        if (synHandler != null)
                        {
                            UnityEngine.Debug.Log("[API] Bypassing lobby waiting state on WebGL. Triggering autostart UI flow.");
                            synHandler.TriggerWebGLAutoStart();
                        }
                    }

                    // Initialize StudentPerformance on WebGL if not already done
                    if (shouldBypassForWebGl && GameManager.Instance.gamificationManager != null)
                    {
                        string schoolId = (session != null && session["teacherId"] != null) ? session["teacherId"]["schoolId"].Value : "";
                        string gradeId = session != null ? session["grade"].Value : "";
                        string sectionId = ""; // No section ID in the REST response session payload

                        UnityEngine.Debug.Log($"[API] WebGL auto-start: Initializing Student Performance references with SessionID={sessionId}, SchoolID={schoolId}, GradeID={gradeId}");
                        GameManager.Instance.gamificationManager.StudentPerformance(sessionId, schoolId, sectionId, gradeId);
                    }

                    JSONArray assessments = spaceData[0]["assessments"].AsArray;
                    if (assessments != null && assessments.Count > 0)
                    {
                        foreach (JSONNode assessment in assessments)
                        {
                            string typeOfGame = assessment["typeOfGame"];
                            typeOfGames.Add(typeOfGame);
                        }

                        if (assessments != null && assessments.Count > 0)
                        {
                            assessmentsString = assessments.ToString();
                        }
                    }


                    JSONArray contents = spaceData[0]["content"].AsArray;
                    if (contents == null)
                    {
                        DebugUtil.Log("[API] No 'content' array in experience response — aborting load.");
                        var synHandler2 = FindObjectOfType<DeviceSynHandler>();
                        if (synHandler2 != null)
                            synHandler2.PublishDownloadError("Experience has no content");
                        req.Dispose();
                        yield break;
                    }


                    foreach (JSONNode content in contents)
                    {
                        string script = content["script"];
                        language = content["language"];
                        //  YouTube details


                        string youTubeUrl = content["youTubeUrl"];
                        youTubeUrl = AddingURL(youTubeUrl);
                        YouTubeUrlTest = youTubeUrl;

                        string youTubeDownloadableUrl = content["youTubeDownloadedUrl"];

                        //uncomment this for checking youtube status
                        //    youtubeTestManager.CheckVideostatus();


                        bool youTubeVideoAudio = content["youTubeVideoAudio"].AsBool;
                        string youTubeVideoScript = content["youTubeVideoScript"];
                        string youTubeStartTimer = content["youTubeStartTimer"];
                        string youTubeEndTimer = content["youTubeEndTimer"];
                        string classEnvironment = content["classEnvironment"].Value;
                        string youTubeVideoStatus = content["youTubeDownloadStatus"];
                        //uncomment this for assign different room
                        room.AssignClassRoom(classEnvironment);

                        JSONArray modelDetails = content["modelDetails"].AsArray;
                        if (modelDetails != null)
                        {

                        }
                        modelDetailsCount = modelDetails != null ? modelDetails.Count : 0;
                        VrManager.AddDownloadManager(modelDetailsCount);
                        List<string> allAnnotations = new List<string>();
                        bool isPlayingSimulation = false;
                        JSONArray selectedSimulationDetails = null;

                        JSONArray simulationDetails = content["simulationDetails"].AsArray;
                        JSONArray scholarLabSimulationDetails = content["scholarlabSimulationDetails"].AsArray;
                        JSONArray objectXRDetails = content["objectxrSimulationDetails"].AsArray;

                        // Priority:
                        // 1. simulationDetails
                        // 2. scholarlabSimulationDetails
                        // 3. objectXRDetails

                        if (simulationDetails != null && simulationDetails.Count > 0)
                        {
                            selectedSimulationDetails = simulationDetails;
                        }
                        else if (scholarLabSimulationDetails != null && scholarLabSimulationDetails.Count > 0)
                        {
                            selectedSimulationDetails = scholarLabSimulationDetails;
                        }
                        else if (objectXRDetails != null && objectXRDetails.Count > 0)
                        {
                            selectedSimulationDetails = objectXRDetails;
                        }

                        if (selectedSimulationDetails != null)
                        {
                            foreach (JSONNode simulationDetail in selectedSimulationDetails)
                            {
                                string simulationScript = simulationDetail["script"];
                                string displayTime = simulationDetail["displayTime"];
                                string simulationURL = string.Empty;

                                // Normal Simulation
                                if (simulationDetail["simulationId"] != null &&
                                    !string.IsNullOrEmpty(simulationDetail["simulationId"]["simulationURL"]))
                                {
                                    simulationURL = simulationDetail["simulationId"]["simulationURL"];
                                }
                                // ScholarLab Simulation
                                else if (!string.IsNullOrEmpty(simulationDetail["simulationURL"]))
                                {
                                    simulationURL = simulationDetail["simulationURL"];
                                }
                                // Object XR Simulation (direct url)
                                else if (!string.IsNullOrEmpty(simulationDetail["simulationUrl"]))
                                {
                                    simulationURL = simulationDetail["simulationUrl"];
                                }

                                if (!string.IsNullOrEmpty(simulationURL))
                                {
                                    if (WebViewController.Instance == null)
                                    {
                                        GameManager.Instance.simulationManagerObj.SetActive(true);
                                    }

                                    if (WebViewController.Instance != null)
                                    {
                                        WebViewController.Instance.displayTimeStr = displayTime;
                                        WebViewController.Instance._fallbackUrl = simulationURL;
                                    }
                                    isPlayingSimulation = true;
                                    DebugUtil.Log("Simulation URL: " + simulationURL);
                                    DebugUtil.Log("Display Time: " + displayTime);
                                }
                            }
                        }

                        JSONArray imageDetails = content["imageDetails"].AsArray;


                        // Check if imageDetails is not null and has elements
                        if (imageDetails != null && imageDetails.Count > 0)
                        {
                            isThreeSixty = true;
                            GameManager.Instance.isImageSkip = false;
                            // Access the imageURL field from the first element of imageDetails
                            string imageTime = imageDetails[0]["displayTime"];
                            string imageUrl = imageDetails[0]["ImageId"]["imageURL"];
                            string imageScript = imageDetails[0]["script"];
                            DebugUtil.Log("Image URL: " + imageUrl);
                            if (!string.IsNullOrEmpty(imageUrl))
                            {
                                is360Image = true;
                                addingImage.LoadAndAssignImage(imageUrl, imageScript, imageTime);
                            }
                        }
                        // Iterate over the modelDetails array
                        if (modelDetails != null) foreach (JSONNode modelDetail in modelDetails)
                            {
                                string modelTimer = modelDetail["displayTime"];
                                if (modelTimer != null)
                                {
                                    string[] timeParts = modelTimer.Split(':');

                                    int minutes = int.Parse(timeParts[0]);
                                    int seconds = int.Parse(timeParts[1]);


                                    int totalSeconds = minutes * 60 + seconds;
                                    float totalSecondsFloat = (float)totalSeconds;
                                    modelDisplayTime.Add(totalSecondsFloat);
                                    PlayerPrefs.SetFloat("totslTime", totalSecondsFloat);

                                }
                                //   float floatTimer = float.Parse(modelTimer);
                                // PlayerPrefs.SetFloat(floatTimer);
                                if (modelDetail["modelId"] != null)
                                {
                                    string url = modelDetail["modelId"]["modelUrl"];
                                    allUrl.Add(url);
                                }
                                // Check if modelCoordinates exists and is not null
                                if (modelDetail["modelCoordinates"] != null)
                                {
                                    // Now check if annotations exist within modelCoordinates
                                    JSONNode annotationsNode = modelDetail["modelCoordinates"];
                                    if (annotationsNode != null && annotationsNode.AsArray.Count > 0) // Making sure annotations is not null and has elements
                                    {
                                        string annotationsJson = annotationsNode.ToString();
                                        modelCoordinateCount.Add(annotationIncrimenter);
                                        allAnnotations.Add(annotationsJson);
                                    }
                                }
                                annotationIncrimenter++;
                            }
                        assetDownloader.FetchingTimer(modelDisplayTime);


                        if (modelDetails != null && modelDetails.Count > 0)
                        {

                            isModelDownload = true;
                            threesixtyImageManager.ModelStatus(isModelDownload);
                            allModelDetails = modelDetails;
                            ProcessNextModelDetail();

                        }
                        else
                        {
                            isModelDownload = false;

                        }

                        JSONNode videoDetail = content["videoDetails"][0];
                        if (videoDetail != null && videoDetail.Count > 0)
                        {
                            isPlayingVideo = true;
                            UpdateVideoPreferences(videoDetail);
                        }
                        else if (!string.IsNullOrEmpty(youTubeUrl) && !string.IsNullOrEmpty(youTubeDownloadableUrl))
                        {
                            isPlayingVideo = true;


                            //  youTubeUrl = youtubevideoPlayer.url;
                            UpdateYoutubePreferance(youTubeVideoAudio, youTubeVideoScript, youTubeStartTimer, youTubeEndTimer, youTubeUrl, youTubeDownloadableUrl, youTubeVideoStatus);

                        }
                        else
                        {
                            isPlayingVideo = false;

                        }

                        teacherCharacterGender = content["teacherCharacterGender"];
                        PlayerPrefs.SetString("Speak", script);
                        PlayerPrefs.SetString("ImageScript", content["imageScript"]); // Check if this is correct
                                                                                      //  StartCoroutine(WaitUntil(allAnnotations));
                        if (PlayerPrefs.HasKey("InSimulation"))
                        {
                            PlayerPrefs.DeleteKey("InSimulation");
                            PlayerPrefs.Save();
                        }

                        bool isResuming = DeepLinkManager.isResumingFromDeepLink;
                        if (isResuming)
                        {
                            DeepLinkManager.ResumeExperience();
                        }
                        else
                        {
                            if (!isPlayingVideo && !isModelDownload && !isPlayingSimulation)
                            {
                                serviceScript.AssignTaskBasedOnCondition(GameStatus.SkipVideoAndModel);
                            }
                            else
                            {
                                if (isModelDownload)
                                {
                                    GameManager.Instance.isModelSkip = true;
                                    serviceScript.AssignAnnotation(allAnnotations, modelCoordinateCount);
                                }
                                else
                                {
                                    GameManager.Instance.isModelSkip = false;
                                    serviceScript.AssignTaskBasedOnCondition(GameStatus.DisbleModelScript);
                                }

                                if (isPlayingVideo)
                                {
                                    serviceScript.AssignTaskBasedOnCondition(GameStatus.AssignVideoStatusAsNotSkip);
                                }
                                else
                                {
                                    serviceScript.AssignTaskBasedOnCondition(GameStatus.AssignVideoStatusAsSkip);
                                }
                            }
                        }

                        // Yield to the main thread to render the VR frame before processing the next content
                        yield return null;
                    }

                    serviceScript.QueryApiAndPassResult(teacherCharacterGender);
                    VrManager.PreferredLanguage(language);
                    //boardParent.SetActive(true);
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[API] Experience response ignored because either isLoadedResponse is true ({isLoadedResponse}) or the welcome screen is still considered open (isOpenWelcomeScreen={DeviceSynHandler.isOpenWelcomeScreen}).");
                }
                // If parsing as an object fails, it's likely a plain string response

                //  VrManager.AssignValues(typeOfGames);


            }
        }
        req.Dispose();
    }






    private void UpdateModelPreferences(List<Dictionary<string, string>> modelIds)
    {
        string modelScript = modelDetail["script"];
        JSONNode modelId = modelDetail["modelId"];
        string modelName = modelId["modelName"];
        string modelUrl = modelId["modelUrl"].Value.Trim();
        string thumbnailUrl = modelId["thumbnailUrl"];
        PlayerPrefs.SetString(StaticStrings.modelName, modelName);
        PlayerPrefs.SetString(StaticStrings.modelUrl, modelUrl);
        PlayerPrefs.SetString(StaticStrings.thumbnailUrl, thumbnailUrl);
        PlayerPrefs.SetString("ModelScript", modelScript);
    }


    public string AddingURL(string url)
    {
        youtubeURL = url;
        return url;
    }

    //Assign all modewl details here
    public void ProcessNextModelDetail()
    {
        if (allModelDetails != null && currentIndex < allModelDetails.Count)
        {
            modelDetail = allModelDetails[currentIndex];
            Dictionary<string, string> modelIdData = new Dictionary<string, string>();

            // Extract modelId node
            JSONNode modelIdNode = modelDetail["modelId"];
            modelIdData["id"] = modelIdNode["_id"];
            modelIdData["modelName"] = modelIdNode["modelName"];
            modelIdData["description"] = modelIdNode["description"];
            modelIdData["modelUrl"] = modelIdNode["modelUrl"];
            modelIdData["thumbnailUrl"] = modelIdNode["thumbnailUrl"];
            modelIdData["script"] = modelDetail["script"];

            UpdateModelPreferences(modelIdData);
            modelIdDatas = modelIdData;
            currentIndex++; // Increment the index for the next call
        }
        else
        {
            DebugUtil.Log("No more modelDetails to process or modelDetails is null.");
        }
    }
    public void MultipleModelCall()
    {
        // UpdateModelPreferences(modelIdDatas);
        ProcessNextModelDetail();
    }
    private void UpdateModelPreferences(Dictionary<string, string> modelId)
    {
        string modelName = modelId["modelName"];
        string modelUrl = modelId["modelUrl"].Trim();
        string thumbnailUrl = modelId["thumbnailUrl"];
        string modelScript = modelId["script"];

        PlayerPrefs.SetString(StaticStrings.modelName, modelName);
        PlayerPrefs.SetString(StaticStrings.modelUrl, modelUrl);
        PlayerPrefs.SetString(StaticStrings.thumbnailUrl, thumbnailUrl);
        PlayerPrefs.SetString("ModelScript", modelScript);
        PlayerPrefs.Save(); // Save the changes to PlayerPrefs
    }
    private void UpdateVideoPreferences(JSONNode videoDetail)
    {
        string videoScript = videoDetail["script"];
        JSONNode videoId = videoDetail["VideoId"];
        string videoURL = videoId["videoURL"].Value.Trim();
        string videoTitle = videoId["title"];
        string videoDescription = videoId["description"];
        string videoThumbnail = videoId["thumbnail"];
        string videoType = videoId["typeOfVideo"];
        string videoSound = videoDetail["videoSound"];
        // VrManager.UpdateVideoType(videoType);
        PlayerPrefs.SetString(StaticStrings.videoUrl, videoURL);
        PlayerPrefs.SetString(StaticStrings.videoScript, videoScript);
        PlayerPrefs.SetString(StaticStrings.videoSound, videoSound);
    }
    public void UpdateYoutubePreferance(bool youTubeVideoAudio, string youTubeVideoScript, string youTubeStartTimer, string youTubeEndTimer, string youTubeUrl, string youTubeDownloadableUrl, string status)
    {
        PlayerPrefs.SetString(StaticStrings.videoUrl, youTubeDownloadableUrl);
        PlayerPrefs.SetString(StaticStrings.videoScript, youTubeVideoScript);
        videoManager.AssignYoutubeProperty(youTubeStartTimer, youTubeEndTimer, youTubeVideoAudio, youTubeUrl, youTubeDownloadableUrl, youTubeVideoScript, status);

    }

}
