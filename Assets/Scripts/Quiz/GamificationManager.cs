using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using static ChangeClassRoom;
using static GamificationManager;


[System.Serializable]
public struct MatchSideData
{
    public string text;
    public string imageUrl;
}

[System.Serializable]
public struct AnswerData
{
    public string text;
    public bool isCorrect;
    public string _id;
    public string imageUrl;
    public MatchSideData left;
    public MatchSideData right;
}
public class GamificationManager : MonoBehaviour
{
    public class StudentScore
    {
        public string studentID;
        public string experienceConductedID;
        public string schoolId;
        public string sectionID;
        public string gradeID;
        public int score;
        public int totalQuestions;
        public int answeredQuestions;
        public int correctAnswers;

    }
    public class DeviceStatus
    {
        public bool isCompleted;
    }
    public GameObject arrowGame;
    public GameObject basketballGame;
    public GameObject mcqGame;
    public GameObject fillitGame;
    public GameObject matchitGame;
    public GameObject gamificationCollider;
    private List<string> selectedGames = new List<string>(); // Array to store selected game names
    private int currentGameIndex = 0;
    public GameObject thankyouCanvas;
    public TextMeshProUGUI[] scoreText;
    public GameObject[] yourScoreText;
    private class Wrapper
    {
        public List<QuestionData> questions;
    }

    [System.Serializable]
    public struct QuestionData
    {
        public string question;
        public List<AnswerData> options;
        public string createdBy;
        public string sessionId;
        public string id;
        public string typeOfGame;
        public string questionImageUrl;
    }


    private int currentQuestionIndex = 0;
    public MeshRenderer[] meshRender;
    public API apiManager;
    private string gamificationType;
    private string questionPanelText;
    private string actualQuestion;
    private string answer;
    public List<QuestionData> questions;
    public List<List<AnswerData>> allOptions = new List<List<AnswerData>>();
    private List<string> typeOfGame = new List<string>();
    private List<string> questionImageUrlList = new List<string>();
    private int gameCount;
    //public  BasketBallQuiz[] gameType;
    private List<string> headingDetails = new List<string>();
    List<string[]> options = new List<string[]>();
    List<string> questionlist = new List<string>();
    public string currentGameName;
    public int arrowScore;
    public int basketBallScore;
    public int mcqScore;

    public GameObject[] disableObjects;
    public GameObject classRoom;
    public GameObject welcomeSessionScene;

    private GoogleTextToSpeech googleTextToSpeech;

    public BoxCollider arrowCollider1, arrowCollider2, arrowCollider3, arrowCollider4;

    private void Start()
    {
        googleTextToSpeech = GameManager.Instance.googleTextToSpeech;
    }

    public void AssignGames()
    {
        if (googleTextToSpeech == null && GameManager.Instance != null)
        {
            googleTextToSpeech = GameManager.Instance.googleTextToSpeech;
        }

        if (googleTextToSpeech != null)
        {
            googleTextToSpeech.StopAudio();
        }
        if (string.IsNullOrEmpty(apiManager.assessmentsString))
        {
            CallingThankyouOutSide();
            thankyouCanvas.SetActive(true);
            // GameManager.Instance.AssessmentParent.SetActive(false); // Removed to prevent hiding thankyouCanvas
        }
        else
        {
            ChangeSkyBox.SetSkybox(ChangeSkyBox.skyBoxMaterialNew[ChangeClassRoom.Instance.classRoom]);
            //welcomeSessionScene.SetActive(true);
            Invoke("StartAfterLoadingCanvas", 1f);
        }

    }
    public void StartAfterLoadingCanvas()
    {

        Wrapper wrapper = JsonUtility.FromJson<Wrapper>("{\"questions\":" + apiManager.assessmentsString + "}");
        questions = wrapper.questions;

        List<AnswerData> currentOptions = new List<AnswerData>();
        foreach (var questionData in wrapper.questions)
        {
            DebugUtil.Log("Type of Game: " + questionData.typeOfGame);
        }

        gamificationType = $"MCQ{currentQuestionIndex + 1}";
        questionPanelText = $"Question{currentQuestionIndex + 1}";
        actualQuestion = $"{questions[currentQuestionIndex].question}";
        headingDetails.Add(gamificationType);
        headingDetails.Add(questionPanelText);
        headingDetails.Add(actualQuestion);

        foreach (var questionData in questions)
        {

            allOptions.Add(questionData.options);
            typeOfGame.Add(questionData.typeOfGame);
            questionlist.Add(questionData.question);
            questionImageUrlList.Add(questionData.questionImageUrl);


        }
        EnableNextGame(typeOfGame[gameCount]);

    }
    private void EnableNextGame(string currentGame)
    {
        if (currentGameIndex <= allOptions.Count)
        {
            // string nextGameName = selectedGames[currentGameIndex];
            currentGameName = currentGame;
            PublishCurrentAssessment();
            //if (!GamificationControllerHandSwitcher.Instance.isHandMode)
            //{
            //    GamificationControllerHandSwitcher.Instance.leftControllerRaycast.SetActive(false);
            //    GamificationControllerHandSwitcher.Instance.rightControllerRaycast.SetActive(true);
            //    GamificationControllerHandSwitcher.Instance.handPoseBall.SetActive(true);
            //    //GamificationControllerHandSwitcher.Instance.rightHandMesh.SetActive(true);
            //}
            ////else
            //{
            //    GamificationControllerHandSwitcher.Instance.leftHandRaycast.SetActive(false);
            //    GamificationControllerHandSwitcher.Instance.rightHandRaycast.SetActive(true);
            //    GamificationControllerHandSwitcher.Instance.handPoseBallHandMode.SetActive(true);
            //    GamificationControllerHandSwitcher.Instance.rightHandMesh.SetActive(false);
            //}
            switch (currentGameName)
            {
                case "Archery":
                    //   arrowScore++;
                    arrowCollider1.enabled = true;
                    arrowCollider2.enabled = true;
                    arrowCollider3.enabled = true;
                    arrowCollider4.enabled = true;
                    arrowGame.SetActive(true);
                    gamificationCollider.SetActive(true);
                    arrowGame.GetComponent<BasketBallQuiz>().DisplayQuestion(headingDetails, allOptions[gameCount], questionlist[gameCount], questionImageUrlList[gameCount]);
                    SetRightHandMeshActive(false);
                    break;
                case "Basketball":
                    //  basketBallScore++;
                    basketballGame.SetActive(true);
                    SetRightHandMeshActive(false);
                    gamificationCollider.SetActive(true);
                    basketballGame.GetComponent<BasketBallQuiz>().DisplayQuestion(headingDetails, allOptions[gameCount], questionlist[gameCount], questionImageUrlList[gameCount]);
                    break;
                case "MCQ":
                    SetRightHandMeshActive(true);
                    mcqGame.GetComponent<QuizManagers>().DisplayQuestion(headingDetails, allOptions[gameCount], questionlist[gameCount], questionImageUrlList[gameCount]);
                    mcqGame.SetActive(true);
                    break;
                case "FillIt":
                case "Fillit":
                case "Fill In The Blanks":
                case "FillInTheBlanks":
                    SetRightHandMeshActive(true);
                    if (fillitGame != null)
                    {
                        var qm = fillitGame.GetComponent<QuizManagers>();
                        if (qm != null)
                        {
                            List<string> groupedQuestions = new List<string>();
                            List<List<AnswerData>> groupedOptions = new List<List<AnswerData>>();
                            List<string> groupedImages = new List<string>();

                            int advanceCount = 0;
                            while (gameCount + advanceCount < typeOfGame.Count)
                            {
                                string peekGame = typeOfGame[gameCount + advanceCount];
                                if (peekGame == "FillIt" || peekGame == "Fillit" || peekGame == "Fill In The Blanks" || peekGame == "FillInTheBlanks")
                                {
                                    groupedQuestions.Add(questionlist[gameCount + advanceCount]);
                                    groupedOptions.Add(allOptions[gameCount + advanceCount]);
                                    groupedImages.Add(questionImageUrlList[gameCount + advanceCount]);
                                    advanceCount++;
                                }
                                else break;
                            }

                            qm.DisplayMultipleQuestions(headingDetails, groupedOptions, groupedQuestions, groupedImages, "FillIt");

                            gameCount += (advanceCount - 1);
                            currentGameIndex += (advanceCount - 1);
                        }
                        fillitGame.SetActive(true);
                    }
                    break;
                case "MatchIt":
                case "Matchit":
                case "Match The Following":
                case "MatchTheFollowing":
                case "MatchingFollowing":
                case "Matching Following":
                    SetRightHandMeshActive(true);
                    if (matchitGame != null)
                    {
                        var qm = matchitGame.GetComponent<QuizManagers>();
                        if (qm != null)
                        {
                            List<string> groupedQuestions = new List<string>();
                            List<List<AnswerData>> groupedOptions = new List<List<AnswerData>>();
                            List<string> groupedImages = new List<string>();

                            int advanceCount = 0;
                            while (gameCount + advanceCount < typeOfGame.Count)
                            {
                                string peekGame = typeOfGame[gameCount + advanceCount];
                                if (peekGame == "MatchIt" || peekGame == "Matchit" || peekGame == "Match The Following" || peekGame == "MatchTheFollowing" || peekGame == "MatchingFollowing" || peekGame == "Matching Following")
                                {
                                    groupedQuestions.Add(questionlist[gameCount + advanceCount]);
                                    groupedOptions.Add(allOptions[gameCount + advanceCount]);
                                    groupedImages.Add(questionImageUrlList[gameCount + advanceCount]);
                                    advanceCount++;
                                }
                                else break;
                            }

                            qm.DisplayMultipleQuestions(headingDetails, groupedOptions, groupedQuestions, groupedImages, "MatchIt");

                            gameCount += (advanceCount - 1);
                            currentGameIndex += (advanceCount - 1);
                        }
                        matchitGame.SetActive(true);
                    }
                    break;
                default:
                    DebugUtil.LogWarning("Unknown game name: " + currentGameName);
                    break;
            }
            currentGameIndex++; // Move to the next game
        }
        else
        {
            // Load scene

        }
    }

    private void PublishCurrentAssessment()
    {
        if (gameCount < 0 || gameCount >= questionlist.Count || gameCount >= allOptions.Count) return;
        var optionNodes = new JSONArray();
        foreach (var option in allOptions[gameCount])
        {
            var node = new JSONObject();
            node["text"] = option.text ?? string.Empty;
            node["imageUrl"] = option.imageUrl ?? string.Empty;
            if (!string.IsNullOrEmpty(option.left.text) || !string.IsNullOrEmpty(option.right.text))
            {
                var left = new JSONObject(); left["text"] = option.left.text ?? string.Empty; left["imageUrl"] = option.left.imageUrl ?? string.Empty;
                var right = new JSONObject(); right["text"] = option.right.text ?? string.Empty; right["imageUrl"] = option.right.imageUrl ?? string.Empty;
                node["left"] = left; node["right"] = right;
            }
            optionNodes.Add(node);
        }
        Klass.Net.LiveViewPublisher.Instance?.PublishAssessmentState(currentGameName, gameCount, questionlist[gameCount], optionNodes);
    }

    IEnumerator EnableThankYouCanvas(string experienceId, string schoolId, string sectionID, string gradeID)
    {
        SetRightHandMeshActive(true);
        SetVrRaycastsActive(false);
        DeviceSynHandler.isGameOver = true;
        thankyouCanvas.SetActive(true);
        // GameManager.Instance.AssessmentParent.SetActive(false); // Removed to prevent hiding thankyouCanvas
        StudentScore student = new StudentScore()
        {
            studentID = Klass.Net.KlassDeviceIdentity.Value,//deviceid
            experienceConductedID = experienceId,
            schoolId = schoolId,
            sectionID = sectionID,
            gradeID = gradeID,
            score = arrowScore + basketBallScore + mcqScore,
            totalQuestions = currentGameName == "MatchingFollowing" ? 5 : allOptions.Count,
            answeredQuestions = currentGameName == "MatchingFollowing" ? 5 : allOptions.Count,
            correctAnswers = arrowScore + basketBallScore + mcqScore,
            // optional, defaults to 60
        };
        string jsonData = JsonUtility.ToJson(student);
        foreach (var scoreTexts in scoreText)
        {
            if (allOptions.Count == 0)
            {
                foreach (GameObject yourscoreText in yourScoreText)
                {
                    yourscoreText.SetActive(false);
                }
                scoreTexts.text = null;
            }
            else
            {
                if(currentGameName== "MatchingFollowing")
                {
                    scoreTexts.text = student.score.ToString() + "/" + "4";
                }
                else
                {
                    scoreTexts.text = student.score.ToString() + "/" + allOptions.Count.ToString();
                }
                   
            }

        }
        StartCoroutine(PostRequest(Klass.Net.KlassEndpoints.V1("performance"), jsonData));
        PlayerPrefs.SetString("PlayerStatus", "Completed");
        yield return new WaitForSeconds(3.5f);


    }
    IEnumerator PostRequest(string api, string jsonData)
    {

        var req = new UnityWebRequest(api, "POST");
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);
        req.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);

        req.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("device-id", Klass.Net.KlassDeviceIdentity.Value);
        //Send the request then wait here until it returns
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            DebugUtil.Log("Error While Sending: " + req.error);
        }
        else // done
        {

            DebugUtil.Log("return" + req.downloadHandler.text);
            string value = req.downloadHandler.text;

        }

    }
    public void CallingThankYouCanvas()
    {
        Klass.Net.LiveViewPublisher.Instance?.PublishAssessmentCompleted();
        foreach (GameObject item in disableObjects)
        {
            item.SetActive(false);
        }
        classRoom.SetActive(true);
        ActiveDeviceStatus();
        StartCoroutine(EnableThankYouCanvas(exid, sid, sectionId, gradeId));
    }
    private string exid;
    private string sid;
    private string sectionId;
    private string gradeId;
    public void StudentPerformance(string experianceID, string schoolId, string sectId, string gradId)
    {
        exid = experianceID;
        sid = schoolId;
        sectionId = sectId;
        gradeId = gradId;
    }

    public void OnGameCompleted()
    {
        Klass.Net.LiveViewPublisher.Instance?.PublishAssessmentInteraction(gameCount, null, true);
        // Disable the completed game
        switch (currentGameName)
        {
            case "Archery":
                arrowGame.SetActive(false);
                break;
            case "Basketball":
                basketballGame.SetActive(false);
                break;
            case "MCQ":
                mcqGame.SetActive(false);
                break;
            case "FillIt":
            case "Fillit":
            case "Fill In The Blanks":
            case "FillInTheBlanks":
                if (fillitGame != null) fillitGame.SetActive(false);
                break;
            case "MatchIt":
            case "Matchit":
            case "Match The Following":
            case "MatchingFollowing":
                if (matchitGame != null) matchitGame.SetActive(false);
                break;
            default:
                DebugUtil.LogWarning("Unknown game name: " + currentGameName);
                break;
        }
        gameCount++;
        if (gameCount < typeOfGame.Count)
        {
            EnableNextGame(typeOfGame[gameCount]);
        }
        else
        {
            CallingThankYouCanvas();
            //StartCoroutine(EnableThankYouCanvas());
        }


    }

    private static void SetRightHandMeshActive(bool active)
    {
        GamificationControllerHandSwitcher switcher = GamificationControllerHandSwitcher.Instance;
        if (switcher != null && switcher.rightHandMesh != null)
        {
            switcher.rightHandMesh.SetActive(active);
        }
    }

    private static void SetVrRaycastsActive(bool active)
    {
        GamificationControllerHandSwitcher switcher = GamificationControllerHandSwitcher.Instance;
        if (switcher == null)
        {
            return;
        }

        SetActiveIfPresent(switcher.leftHandRaycast, active);
        SetActiveIfPresent(switcher.rightHandRaycast, active);
        SetActiveIfPresent(switcher.leftControllerRaycast, active);
        SetActiveIfPresent(switcher.rightControllerRaycast, active);
    }

    private static void SetActiveIfPresent(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
    public void CallingThankyouOutSide()
    {
        CallingThankYouCanvas();
    }
    public void AssignGames(List<string> selectedgames)
    {
        this.selectedGames = selectedgames;
    }
    // Method to enable selected games based on data from API
    public void ActiveDeviceStatus()
    {
        // Migrated from REST PATCH /device_sync to retained MQTT state publish.
        var mqtt = Klass.Net.KlassMqttClient.Instance;
        if (mqtt != null && mqtt.IsConnected)
            mqtt.PublishState(isActive: true, isSynced: true, isCompleted: true);
    }
    IEnumerator SendPatchRequest(string api, string jsonPayload)
    {
        string url = api;
        UnityWebRequest request = new UnityWebRequest(url, "PATCH");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("device-id", Klass.Net.KlassDeviceIdentity.Value);
        //  request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");

        // Send the request and await a response
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            DebugUtil.LogError("Error: " + request.error);
        }
        else
        {
            DebugUtil.Log("Success: " + request.downloadHandler.text);
            string value = request.downloadHandler.text;

        }

    }
}
