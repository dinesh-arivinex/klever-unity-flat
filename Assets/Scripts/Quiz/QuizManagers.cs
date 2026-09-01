// using Oculus.Interaction;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Networking;

public class QuizManagers : MonoBehaviour
{
    public TextMeshProUGUI questionText;
    public TextMeshProUGUI MCQquestionText;
    public TextMeshProUGUI questionPanelText;
    public Button[] answerButtons;
    public Button nextButton;
    private bool buttonClicked = false;
    public UnityEvent reloadSceneEvent;
    private API apiManager;
    public UnityEvent StartEvent;
    public UnityEvent EndEvent;
    public GameObject elementWantToDelete;
    private int MCQstudetScore = 0;
    private GamificationManager gamificationManager;
    // public List<InteractableUnityEventWrapper> interactableWrappers = new List<InteractableUnityEventWrapper>();
    public AudioSource audioSource;

    public GameObject[] questionsFillIt, answersFillIt, answerPlacedTextFillIt;
    public GameObject[] questionsMatchIt, answersMatchIt, answerPlacedTextMatchIt;
    [HideInInspector]
    public int answerPlacedCount;
    [HideInInspector]
    public int totQuestionFillItCount;
    [HideInInspector]
    public int totQuestionMatchItCount;

    private bool hasDisplayedAllFillInTheBlanks = false;

    public FillItReferenceManager fillItReferenceManager;
    public MatchItReferenceManager matchItReferenceManager;


    private Image targetImage;
    private Color correctColor = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
    private Color wrongColor = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);

    public ColliderTriggerMatch colliderTriggerMatch;
    [System.Serializable]
    private class Wrapper
    {
        public List<QuestionData> questions;
    }

    [System.Serializable]
    private struct QuestionData
    {
        public string question;
        public List<AnswerData> options;
        public string createdBy;
        public string sessionId;
        public string id;
        public string typeOfGame;
        public string questionImageUrl;
    }


    public class Option
    {
        public string text;
        public bool isCorrect;
        public string id;
    }
    // private List<QuestionData> questions;
    private int currentQuestionIndex = 0;
    //public MeshRenderer[] meshRender;
    public List<Option> options;
    private List<AnswerData> answers = new List<AnswerData>();
    private bool isProcessingNext = false;

    private IEnumerator ResetProcessingNext()
    {
        yield return new WaitForEndOfFrame();
        isProcessingNext = false;
    }

    private void OnEnable()
    {
        ResolveReferenceManagers();
        StartCoroutine(ResetProcessingNext());
        // --- ADDED FOR PC/WEBGL UI SUPPORT ---
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            if (canvas.worldCamera == null && canvas.renderMode == RenderMode.WorldSpace)
            {
                canvas.worldCamera = Camera.main;
            }
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        // Auto-attach UIDraggable to FillIt and MatchIt answers for PC Drag/Drop
        if (answersFillIt != null)
        {
            foreach (var answerObj in answersFillIt)
            {
                if (answerObj != null && answerObj.GetComponent<UIDraggable>() == null)
                {
                    answerObj.AddComponent<UIDraggable>();
                }
            }
        }
        if (answersMatchIt != null)
        {
            int dragLayer = LayerMask.NameToLayer("Drag");
            ColliderTriggerMatch colTrigger = FindAnyObjectByType<ColliderTriggerMatch>();
            foreach (var answerObj in answersMatchIt)
            {
                if (answerObj != null)
                {
                    GameObject targetDraggable = answerObj;
                    Transform parentTrans = answerObj.transform.parent;
                    if (parentTrans != null && colTrigger != null)
                    {
                        if (parentTrans.gameObject == colTrigger.Answer1Pos ||
                            parentTrans.gameObject == colTrigger.Answer2Pos ||
                            parentTrans.gameObject == colTrigger.Answer3Pos ||
                            parentTrans.gameObject == colTrigger.Answer4Pos)
                        {
                            targetDraggable = parentTrans.gameObject;
                        }
                    }

                    if (dragLayer != -1)
                    {
                        SetLayerRecursively(targetDraggable, dragLayer);
                    }

                    if (targetDraggable.GetComponent<UIDraggable>() == null)
                    {
                        targetDraggable.AddComponent<UIDraggable>();
                    }
                    if (targetDraggable.GetComponent<CanvasGroup>() == null)
                    {
                        targetDraggable.AddComponent<CanvasGroup>();
                    }
                    CanvasGroup cg = targetDraggable.GetComponent<CanvasGroup>();
                    if (cg != null) cg.blocksRaycasts = true;

                    UnityEngine.UI.Graphic[] graphics = targetDraggable.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                    foreach (var g in graphics) g.raycastTarget = true;
                }
            }
        }
        // -------------------------------------

        gamificationManager = FindAnyObjectByType<GamificationManager>();
        apiManager = FindAnyObjectByType<API>();
        if (apiManager != null && !string.IsNullOrEmpty(apiManager.assessmentsString))
        {
            try
            {
                Wrapper wrapper = JsonUtility.FromJson<Wrapper>("{\"questions\":" + apiManager.assessmentsString + "}");
                //questions = wrapper.questions;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("QuizManagers: assessmentsString parsing failed or empty. " + ex.Message);
            }
        }

        //   DisplayQuestion();
        StartEvent.Invoke();
        nextButton.enabled = false;

        // Ensure listener is added only once by removing all previous listeners
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(NextButtonClick);

        Invoke("DeleteElement", 4f);
        //foreach (var element in meshRender) 
        //{
        //    element.enabled = true;
        //}

    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            NextButtonClick();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                var pointerEventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                pointerEventData.position = Input.mousePosition;
                var raycastResults = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerEventData, raycastResults);

                if (raycastResults.Count > 0)
                {
                    Debug.Log("UI Click Raycast Hit: " + raycastResults[0].gameObject.name);
                }
                else
                {
                    Debug.Log("UI Click Raycast Hit: NOTHING (Check if GraphicRaycaster is on Canvas)");
                }
            }
        }
    }



    private void DisplayQuestionImage(string url)
    {
        Transform parentTransform = questionText != null ? questionText.transform.parent : transform;
        if (parentTransform == null) return;
        Transform existing = parentTransform.Find("DynamicQuestionImage");
        RawImage rawImg = null;
        if (existing != null)
        {
            rawImg = existing.GetComponent<RawImage>();
        }
        else
        {
            GameObject go = new GameObject("DynamicQuestionImage");
            go.transform.SetParent(parentTransform, false);
            rawImg = go.AddComponent<RawImage>();

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1.2f, 0.8f); // set size in world space scale, since it's on a VR Canvas
            rect.anchoredPosition = new Vector3(0f, 150f, 0f); // adjust offset relative to question text parent
        }

        if (rawImg != null)
        {
            if (string.IsNullOrEmpty(url))
            {
                rawImg.gameObject.SetActive(false);
            }
            else
            {
                rawImg.gameObject.SetActive(true);
                StartCoroutine(LoadImageCoroutine(url, texture => {
                    rawImg.texture = texture;
                }));
            }
        }
    }

    private void SetOptionImage(Button button, string url)
    {
        Transform existing = button.transform.Find("DynamicOptionImage");
        RawImage rawImg = null;
        if (existing != null)
        {
            rawImg = existing.GetComponent<RawImage>();
        }
        else
        {
            GameObject imgGo = new GameObject("DynamicOptionImage");
            imgGo.transform.SetParent(button.transform, false);
            imgGo.transform.SetSiblingIndex(0); // Draw behind text
            rawImg = imgGo.AddComponent<RawImage>();

            RectTransform rect = imgGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(5, 5);
            rect.offsetMax = new Vector2(-5, -5);
        }

        if (rawImg != null)
        {
            if (string.IsNullOrEmpty(url))
            {
                rawImg.gameObject.SetActive(false);
            }
            else
            {
                rawImg.gameObject.SetActive(true);
                this.gameObject.SetActive(true);
                StartCoroutine(LoadImageCoroutine(url, texture => {
                    rawImg.texture = texture;
                }));
            }
        }
    }

    private IEnumerator LoadImageCoroutine(string url, Action<Texture2D> onSuccess)
    {
        if (string.IsNullOrEmpty(url)) yield break;
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                onSuccess?.Invoke(texture);
            }
            else
            {
                DebugUtil.LogError("Failed to load image from: " + url + " Error: " + webRequest.error);
            }
        }
    }

    public void DisplayQuestion(List<string> heading, List<AnswerData> answerOptions, string actualQuestion, string questionImageUrl)
    {
        ResolveReferenceManagers();
        answers = answerOptions;
        if (questionText != null) questionText.text = actualQuestion;
        DisplayQuestionImage(questionImageUrl);

        if (questionText != null && currentQuestionIndex < 1)
        {
            if (MCQquestionText != null && heading.Count > 0) MCQquestionText.text = heading[0];
            if (questionPanelText != null && heading.Count > 1) questionPanelText.text = heading[1];
        }

        // Setup MCQ buttons if present
        if (answerButtons != null && answerButtons.Length > 0)
        {
            for (int i = 0; i < answerButtons.Length; i++)
            {
                if (i < answerOptions.Count)
                {
                    if (answerButtons[i] != null)
                    {
                        answerButtons[i].gameObject.SetActive(true);
                        TextMeshProUGUI buttonText = answerButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (buttonText != null) buttonText.text = answerOptions[i].text;
                        SetOptionImage(answerButtons[i], answerOptions[i].imageUrl);
                        answerButtons[i].onClick.RemoveAllListeners();
                        int answerIndex = i;
                        answerButtons[i].onClick.AddListener(() => OnAnswerClick(answerIndex));
                    }
                }
                else if (answerButtons[i] != null)
                {
                    answerButtons[i].gameObject.SetActive(false);
                }
            }
        }

        // Setup FillIt options if present
        GameObject[] fAnswers = (fillItReferenceManager != null && fillItReferenceManager.Answer != null && fillItReferenceManager.Answer.Length > 0 && fillItReferenceManager.Answer[0] != null) ? fillItReferenceManager.Answer : answersFillIt;
        GameObject[] fQuestions = (fillItReferenceManager != null && fillItReferenceManager.Questions != null && fillItReferenceManager.Questions.Length > 0 && fillItReferenceManager.Questions[0] != null) ? fillItReferenceManager.Questions : questionsFillIt;
        GameObject[] fPlacements = (fillItReferenceManager != null && fillItReferenceManager.AnswerPlacement != null && fillItReferenceManager.AnswerPlacement.Length > 0 && fillItReferenceManager.AnswerPlacement[0] != null) ? fillItReferenceManager.AnswerPlacement : answerPlacedTextFillIt;

        if (fAnswers != null && fAnswers.Length > 0)
        {
            // Create a shuffled list of options to randomize their positions
            List<AnswerData> shuffledOptions = new List<AnswerData>(answerOptions);
            for (int i = 0; i < shuffledOptions.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, shuffledOptions.Count);
                AnswerData temp = shuffledOptions[i];
                shuffledOptions[i] = shuffledOptions[r];
                shuffledOptions[r] = temp;
            }

            for (int i = 0; i < fAnswers.Length; i++)
            {
                if (i < shuffledOptions.Count)
                {
                    if (fAnswers[i] != null)
                    {
                        fAnswers[i].SetActive(true);
                        TextMeshProUGUI txt = fAnswers[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (txt != null) txt.text = shuffledOptions[i].text;
                    }
                    if (fQuestions != null && i < fQuestions.Length && fQuestions[i] != null)
                    {
                        fQuestions[i].SetActive(true);
                        TextMeshProUGUI qTxt = fQuestions[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (qTxt != null) qTxt.text = actualQuestion;
                    }
                    if (fPlacements != null && i < fPlacements.Length && fPlacements[i] != null)
                    {
                        fPlacements[i].SetActive(true);
                        TextMeshProUGUI pTxt = fPlacements[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (pTxt != null) pTxt.text = answerOptions[i].text;
                    }
                }
                else
                {
                    if (fAnswers[i] != null) fAnswers[i].SetActive(false);
                    if (fQuestions != null && i < fQuestions.Length && fQuestions[i] != null) fQuestions[i].SetActive(false);
                    if (fPlacements != null && i < fPlacements.Length && fPlacements[i] != null) fPlacements[i].SetActive(false);
                }
            }
            totQuestionFillItCount = answerOptions.Count;
        }

        // Setup MatchIt options if present
        GameObject[] mAnswers = (matchItReferenceManager != null && matchItReferenceManager.Answer != null && matchItReferenceManager.Answer.Length > 0 && matchItReferenceManager.Answer[0] != null) ? matchItReferenceManager.Answer : answersMatchIt;
        GameObject[] mQuestions = (matchItReferenceManager != null && matchItReferenceManager.Questions != null && matchItReferenceManager.Questions.Length > 0 && matchItReferenceManager.Questions[0] != null) ? matchItReferenceManager.Questions : questionsMatchIt;
        GameObject[] mPlacements = (matchItReferenceManager != null && matchItReferenceManager.AnsweTextStore != null && matchItReferenceManager.AnsweTextStore.Length > 0 && matchItReferenceManager.AnsweTextStore[0] != null) ? matchItReferenceManager.AnsweTextStore : answerPlacedTextMatchIt;

        if (mAnswers != null && mAnswers.Length > 0)
        {
            // Create a shuffled list of options for MatchIt too!
            List<AnswerData> shuffledOptions = new List<AnswerData>(answerOptions);
            for (int i = 0; i < shuffledOptions.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, shuffledOptions.Count);
                AnswerData temp = shuffledOptions[i];
                shuffledOptions[i] = shuffledOptions[r];
                shuffledOptions[r] = temp;
            }

            for (int i = 0; i < mAnswers.Length; i++)
            {
                if (i < shuffledOptions.Count)
                {
                    if (mAnswers[i] != null)
                    {
                        if (mAnswers[i].transform.parent != null)
                        {
                            mAnswers[i].transform.parent.gameObject.SetActive(true);
                        }
                        mAnswers[i].SetActive(true);
                        TextMeshProUGUI txt = mAnswers[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (txt != null) txt.text = shuffledOptions[i].text;
                    }
                    if (mQuestions != null && i < mQuestions.Length && mQuestions[i] != null)
                    {
                        if (mQuestions[i].transform.parent != null)
                        {
                            mQuestions[i].transform.parent.gameObject.SetActive(true);
                        }
                        mQuestions[i].SetActive(true);
                        TextMeshProUGUI qTxt = mQuestions[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (qTxt != null) qTxt.text = actualQuestion; // In case MatchIt also needs this
                    }
                    if (mPlacements != null && i < mPlacements.Length && mPlacements[i] != null)
                    {
                        if (mPlacements[i].transform.parent != null)
                        {
                            mPlacements[i].transform.parent.gameObject.SetActive(true);
                        }
                        mPlacements[i].SetActive(true);
                        TextMeshProUGUI pTxt = mPlacements[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (pTxt != null) pTxt.text = answerOptions[i].text;
                    }
                }
                else
                {
                    if (mAnswers[i] != null)
                    {
                        if (mAnswers[i].transform.parent != null)
                        {
                            mAnswers[i].transform.parent.gameObject.SetActive(false);
                        }
                        mAnswers[i].SetActive(false);
                    }
                    if (mQuestions != null && i < mQuestions.Length && mQuestions[i] != null)
                    {
                        if (mQuestions[i].transform.parent != null)
                        {
                            mQuestions[i].transform.parent.gameObject.SetActive(false);
                        }
                        mQuestions[i].SetActive(false);
                    }
                    if (mPlacements != null && i < mPlacements.Length && mPlacements[i] != null)
                    {
                        if (mPlacements[i].transform.parent != null)
                        {
                            mPlacements[i].transform.parent.gameObject.SetActive(false);
                        }
                        mPlacements[i].SetActive(false);
                    }
                }
            }
            totQuestionMatchItCount = answerOptions.Count;
        }

        buttonClicked = false;
    }

    public void DisplayMultipleQuestions(List<string> heading, List<List<AnswerData>> allAnswerOptions, List<string> actualQuestions, List<string> questionImageUrls, string gameName)
    {
        ResolveReferenceManagers();

        Debug.Log($"[MatchIt Debug] DisplayMultipleQuestions called. gameName: {gameName}");
        Debug.Log($"[MatchIt Debug] actualQuestions.Count: {actualQuestions.Count}");
        for (int idx = 0; idx < actualQuestions.Count; idx++)
        {
            Debug.Log($"  Question {idx}: \"{actualQuestions[idx]}\"");
        }
        Debug.Log($"[MatchIt Debug] allAnswerOptions.Count: {allAnswerOptions.Count}");
        for (int idx = 0; idx < allAnswerOptions.Count; idx++)
        {
            var optList = allAnswerOptions[idx];
            Debug.Log($"  Options List {idx} has {optList?.Count ?? 0} options:");
            if (optList != null)
            {
                for (int j = 0; j < optList.Count; j++)
                {
                    Debug.Log($"    Option {j}: text=\"{optList[j].text}\", isCorrect={optList[j].isCorrect}, imageUrl=\"{optList[j].imageUrl}\"");
                }
            }
        }

        DisplayQuestionImage(questionImageUrls != null && questionImageUrls.Count > 0 ? questionImageUrls[0] : null);

        if (MCQquestionText != null && heading.Count > 0) MCQquestionText.text = heading[0];
        if (questionPanelText != null && heading.Count > 1) questionPanelText.text = heading[1];

        if (gameName == "FillIt")
        {
            GameObject[] fAnswers = (fillItReferenceManager != null && fillItReferenceManager.Answer != null && fillItReferenceManager.Answer.Length > 0 && fillItReferenceManager.Answer[0] != null) ? fillItReferenceManager.Answer : answersFillIt;
            GameObject[] fQuestions = (fillItReferenceManager != null && fillItReferenceManager.Questions != null && fillItReferenceManager.Questions.Length > 0 && fillItReferenceManager.Questions[0] != null) ? fillItReferenceManager.Questions : questionsFillIt;
            GameObject[] fPlacements = (fillItReferenceManager != null && fillItReferenceManager.AnswerPlacement != null && fillItReferenceManager.AnswerPlacement.Length > 0 && fillItReferenceManager.AnswerPlacement[0] != null) ? fillItReferenceManager.AnswerPlacement : answerPlacedTextFillIt;

            List<AnswerData> flatOptions = new List<AnswerData>();
            foreach (var opts in allAnswerOptions)
            {
                if (opts != null)
                {
                    AnswerData correctOpt = default;
                    bool found = false;
                    foreach (var o in opts)
                    {
                        if (o.isCorrect)
                        {
                            correctOpt = o;
                            found = true;
                            break;
                        }
                    }
                    if (!found && opts.Count > 0)
                    {
                        correctOpt = opts[0];
                    }
                    flatOptions.Add(correctOpt);
                }
            }

            List<AnswerData> shuffledOptions = new List<AnswerData>(flatOptions);
            for (int i = 0; i < shuffledOptions.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, shuffledOptions.Count);
                AnswerData temp = shuffledOptions[i];
                shuffledOptions[i] = shuffledOptions[r];
                shuffledOptions[r] = temp;
            }

            if (fAnswers != null && fAnswers.Length > 0)
            {
                for (int i = 0; i < fAnswers.Length; i++)
                {
                    if (i < shuffledOptions.Count)
                    {
                        if (fAnswers[i] != null)
                        {
                            fAnswers[i].SetActive(true);
                            if (fAnswers[i].transform.parent != null)
                            {
                                fAnswers[i].transform.parent.SetAsLastSibling();
                            }
                            if (fAnswers[i].GetComponent<UIDraggable>() == null)
                            {
                                fAnswers[i].AddComponent<UIDraggable>();
                            }
                            CanvasGroup cg = fAnswers[i].GetComponent<CanvasGroup>();
                            if (cg != null) cg.blocksRaycasts = true;

                            UnityEngine.UI.Graphic[] graphics = fAnswers[i].GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                            foreach (var g in graphics) g.raycastTarget = true;

                            TextMeshProUGUI txt = fAnswers[i].GetComponentInChildren<TextMeshProUGUI>();
                            if (txt != null) txt.text = shuffledOptions[i].text;
                        }
                    }
                    else
                    {
                        if (fAnswers[i] != null) fAnswers[i].SetActive(false);
                    }
                }
            }

            if (fQuestions != null)
            {
                int countToEnable = Mathf.Max(actualQuestions.Count, flatOptions.Count);
                for (int i = 0; i < fQuestions.Length; i++)
                {
                    if (i < countToEnable)
                    {
                        if (fQuestions[i] != null)
                        {
                            if (fQuestions[i].transform.parent != null)
                            {
                                fQuestions[i].transform.parent.gameObject.SetActive(true);
                            }
                            fQuestions[i].SetActive(true);
                            TextMeshProUGUI qTxt = fQuestions[i].GetComponentInChildren<TextMeshProUGUI>();
                            if (qTxt != null)
                            {
                                string textToAssign = i < actualQuestions.Count ? actualQuestions[i] : (actualQuestions.Count > 0 ? actualQuestions[0] : "");
                                qTxt.text = textToAssign;
                            }
                        }
                    }
                    else
                    {
                        if (fQuestions[i] != null)
                        {
                            if (fQuestions[i].transform.parent != null)
                            {
                                fQuestions[i].transform.parent.gameObject.SetActive(false);
                            }
                            fQuestions[i].SetActive(false);
                        }
                    }
                }
            }

            if (fPlacements != null)
            {
                for (int i = 0; i < fPlacements.Length; i++)
                {
                    if (i < flatOptions.Count)
                    {
                        if (fPlacements[i] != null)
                        {
                            fPlacements[i].SetActive(true);
                            TextMeshProUGUI pTxt = fPlacements[i].GetComponentInChildren<TextMeshProUGUI>();
                            if (pTxt != null) pTxt.text = flatOptions[i].text;
                        }
                    }
                    else
                    {
                        if (fPlacements[i] != null) fPlacements[i].SetActive(false);
                    }
                }
            }

            totQuestionFillItCount = flatOptions.Count;
        }
        else if (gameName == "MatchIt")
        {
            GameObject[] mAnswers = (matchItReferenceManager != null && matchItReferenceManager.Answer != null && matchItReferenceManager.Answer.Length > 0 && matchItReferenceManager.Answer[0] != null) ? matchItReferenceManager.Answer : answersMatchIt;
            GameObject[] mQuestions = (matchItReferenceManager != null && matchItReferenceManager.Questions != null && matchItReferenceManager.Questions.Length > 0 && matchItReferenceManager.Questions[0] != null) ? matchItReferenceManager.Questions : questionsMatchIt;
            GameObject[] mPlacements = (matchItReferenceManager != null && matchItReferenceManager.AnsweTextStore != null && matchItReferenceManager.AnsweTextStore.Length > 0 && matchItReferenceManager.AnsweTextStore[0] != null) ? matchItReferenceManager.AnsweTextStore : answerPlacedTextMatchIt;

            List<string> leftTexts = new List<string>();
            List<string> rightTexts = new List<string>();

            // 1. Gather all pairs from allAnswerOptions
            foreach (var opts in allAnswerOptions)
            {
                if (opts != null)
                {
                    foreach (var o in opts)
                    {
                        if (!string.IsNullOrEmpty(o.left.text))
                        {
                            leftTexts.Add(o.left.text);
                            rightTexts.Add(o.right.text);
                        }
                    }
                }
            }

            // Fallback: If no nested left/right objects were found, use standard option-based mapping
            if (leftTexts.Count == 0)
            {
                for (int qIdx = 0; qIdx < allAnswerOptions.Count; qIdx++)
                {
                    var opts = allAnswerOptions[qIdx];
                    if (opts != null)
                    {
                        AnswerData correctOpt = default;
                        bool found = false;
                        foreach (var o in opts)
                        {
                            if (o.isCorrect)
                            {
                                correctOpt = o;
                                found = true;
                                break;
                            }
                        }
                        if (!found && opts.Count > 0)
                        {
                            correctOpt = opts[0];
                        }

                        string qText = qIdx < actualQuestions.Count ? actualQuestions[qIdx] : "";
                        leftTexts.Add(qText);
                        rightTexts.Add(correctOpt.text);
                    }
                }
            }

            // Create shuffled list of right-hand side answers
            List<string> shuffledRight = new List<string>(rightTexts);
            for (int i = 0; i < shuffledRight.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, shuffledRight.Count);
                string temp = shuffledRight[i];
                shuffledRight[i] = shuffledRight[r];
                shuffledRight[r] = temp;
            }

            if (mAnswers != null)
            {
                for (int i = 0; i < mAnswers.Length; i++)
                {
                    if (i < shuffledRight.Count)
                    {
                        GameObject targetDraggable = mAnswers[i];
                        Transform parentTrans = mAnswers[i].transform.parent;
                        ColliderTriggerMatch colTrigger = FindAnyObjectByType<ColliderTriggerMatch>();
                        if (parentTrans != null && colTrigger != null)
                        {
                            if (parentTrans.gameObject == colTrigger.Answer1Pos ||
                                parentTrans.gameObject == colTrigger.Answer2Pos ||
                                parentTrans.gameObject == colTrigger.Answer3Pos ||
                                parentTrans.gameObject == colTrigger.Answer4Pos)
                            {
                                targetDraggable = parentTrans.gameObject;
                            }
                        }

                        if (targetDraggable != null)
                        {
                            if (targetDraggable.transform.parent != null)
                            {
                                targetDraggable.transform.parent.gameObject.SetActive(true);
                            }
                            targetDraggable.SetActive(true);

                            int dragLayer = LayerMask.NameToLayer("Drag");
                            if (dragLayer != -1)
                            {
                                SetLayerRecursively(targetDraggable, dragLayer);
                            }

                            targetDraggable.transform.SetAsLastSibling();
                            if (targetDraggable.GetComponent<UIDraggable>() == null)
                            {
                                targetDraggable.AddComponent<UIDraggable>();
                            }
                            CanvasGroup cg = targetDraggable.GetComponent<CanvasGroup>();
                            if (cg == null)
                            {
                                cg = targetDraggable.AddComponent<CanvasGroup>();
                            }
                            if (cg != null) cg.blocksRaycasts = true;

                            UnityEngine.UI.Graphic[] graphics = targetDraggable.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                            foreach (var g in graphics) g.raycastTarget = true;
                        }

                        mAnswers[i].SetActive(true);
                        TextMeshProUGUI txt = mAnswers[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (txt != null) txt.text = shuffledRight[i];
                    }
                    else
                    {
                        if (mAnswers[i] != null)
                        {
                            if (mAnswers[i].transform.parent != null)
                            {
                                mAnswers[i].transform.parent.gameObject.SetActive(false);
                            }
                            mAnswers[i].SetActive(false);
                        }
                    }
                }
            }

            if (mQuestions != null)
            {
                for (int i = 0; i < mQuestions.Length; i++)
                {
                    if (i < leftTexts.Count)
                    {
                        if (mQuestions[i] != null)
                        {
                            if (mQuestions[i].transform.parent != null)
                            {
                                mQuestions[i].transform.parent.gameObject.SetActive(true);
                            }
                            mQuestions[i].SetActive(true);
                            TextMeshProUGUI qTxt = mQuestions[i].GetComponentInChildren<TextMeshProUGUI>();
                            if (qTxt != null) qTxt.text = leftTexts[i];
                        }
                    }
                    else
                    {
                        if (mQuestions[i] != null)
                        {
                            if (mQuestions[i].transform.parent != null)
                            {
                                mQuestions[i].transform.parent.gameObject.SetActive(false);
                            }
                            mQuestions[i].SetActive(false);
                        }
                    }
                }
            }

            if (mPlacements != null)
            {
                for (int i = 0; i < mPlacements.Length; i++)
                {
                    if (i < rightTexts.Count)
                    {
                        if (mPlacements[i] != null)
                        {
                            if (mPlacements[i].transform.parent != null)
                            {
                                mPlacements[i].transform.parent.gameObject.SetActive(true);
                            }
                            mPlacements[i].SetActive(true);
                            TextMeshProUGUI pTxt = mPlacements[i].GetComponentInChildren<TextMeshProUGUI>();
                            if (pTxt != null) pTxt.text = rightTexts[i];
                        }
                    }
                    else
                    {
                        if (mPlacements[i] != null)
                        {
                            if (mPlacements[i].transform.parent != null)
                            {
                                mPlacements[i].transform.parent.gameObject.SetActive(false);
                            }
                            mPlacements[i].SetActive(false);
                        }
                    }
                }
            }

            totQuestionMatchItCount = rightTexts.Count;
        }

        buttonClicked = false;
    }

    private void OnAnswerClick(int selectedAnswerIndex)
    {
        if (!buttonClicked)
        {
            var liveResponse = new SimpleJSON.JSONObject();
            liveResponse["selectedIndex"] = selectedAnswerIndex;
            liveResponse["selectedText"] = selectedAnswerIndex >= 0 && selectedAnswerIndex < answers.Count ? answers[selectedAnswerIndex].text : string.Empty;
            Klass.Net.LiveViewPublisher.Instance?.PublishAssessmentInteraction(currentQuestionIndex, liveResponse, false);
            // Create colors with adjusted alpha values
            Color greenWithAlpha = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
            Color redWithAlpha = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);
            
            // Change button color immediately for all active buttons
            for (int i = 0; i < answers.Count; i++)
            {
                if (answerButtons[i] != null && answerButtons[i].gameObject.activeSelf)
                {
                    ChangeButtonColor(answerButtons[i], answers[i].isCorrect ? greenWithAlpha : redWithAlpha);
                }
            }
            if (answers[selectedAnswerIndex].isCorrect)
            {
                // MCQstudetScore++;
                gamificationManager.mcqScore++;
            }
            buttonClicked = true;
            ToggleButtonActivation(false);
            audioSource.Play();
        }
        nextButton.enabled = true;
    }

    public void ToggleButtonActivation(bool activated)
    {
        foreach (var button in answerButtons)
        {
            button.enabled = activated;
        }
    }

    private void NextButtonClick()
    {
        if (isProcessingNext) return;
        isProcessingNext = true;

        nextButton.enabled = false;
        for (int i = 0; i < answerButtons.Length; i++)
        {
            Color newColor = new Color(150f / 255f, 150f / 255f, 150f / 255f, 255f / 255f);
            StartCoroutine(RestoreButtonColor(answerButtons[i], newColor));
        }
        ToggleButtonActivation(true);
        buttonClicked = false;

        currentQuestionIndex++;
        if (currentQuestionIndex < 1)
        {
            // DisplayQuestion();
        }
        else
        {
            reloadSceneEvent.Invoke();
            DebugUtil.Log("Your score is" + MCQstudetScore);
            gamificationManager.OnGameCompleted();
        }
    }

    private void ChangeButtonColor(Button button, Color color)
    {
        Graphic graphic = button.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = color;
        }
        else
        {
            DebugUtil.LogError("Graphic component not found on the button.");
        }

        ColorBlock cb = button.colors;
        cb.normalColor = color;
        cb.disabledColor = color;
        cb.selectedColor = color;
        button.colors = cb;
    }

    private IEnumerator RestoreButtonColor(Button button, Color color)
    {
        // This is essentially not causing any delay, consider removing or increasing the time if needed

        // Create a new color with baseColor RGB values but with the Alpha set to 95/255
        // Color newColorWithAlpha = new Color(color.r, color.g, color.b, 95f / 255f);

        // Restore button color after the delay
        ChangeButtonColor(button, color);

        // If the button is the correct answer button, find its index and restore its color
        int correctAnswerIndex = answers.FindIndex(answers => answers.isCorrect);
        if (correctAnswerIndex != -1)
        {
            ChangeButtonColor(answerButtons[correctAnswerIndex], color);
        }
        yield return new WaitForSeconds(0f);
    }
    public void DeleteElement()
    {
        Destroy(elementWantToDelete);
    }
    public void SceneReLoad()
    {
        EndEvent.Invoke();
        // SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    private void DisableImagesInArray(GameObject[] arr)
    {
        if (arr == null) return;
        foreach (var obj in arr)
        {
            if (obj == null) continue;
            Transform img = obj.transform.Find("DynamicMatchItImage");
            if (img != null) img.gameObject.SetActive(false);

            if (obj.transform.parent != null)
            {
                img = obj.transform.parent.Find("DynamicMatchItImage");
                if (img != null) img.gameObject.SetActive(false);
            }
        }
    }
    public void MatchSubmit()
    {
        if (matchItReferenceManager != null)
        {
            DisableImagesInArray(matchItReferenceManager.Questions);
            DisableImagesInArray(matchItReferenceManager.Answer);
        }

        if (colliderTriggerMatch != null)
        {
            if (colliderTriggerMatch.Answer1HandGrab != null) colliderTriggerMatch.Answer1HandGrab.gameObject.SetActive(false);
            if (colliderTriggerMatch.Answer2HandGrab != null) colliderTriggerMatch.Answer2HandGrab.gameObject.SetActive(false);
            if (colliderTriggerMatch.Answer3HandGrab != null) colliderTriggerMatch.Answer3HandGrab.gameObject.SetActive(false);
            if (colliderTriggerMatch.Answer4HandGrab != null) colliderTriggerMatch.Answer4HandGrab.gameObject.SetActive(false);
        }

        int checkCount = Mathf.Min(totQuestionMatchItCount, answersMatchIt != null ? answersMatchIt.Length : 0);
        checkCount = Mathf.Min(checkCount, answerPlacedTextMatchIt != null ? answerPlacedTextMatchIt.Length : 0);

        for (int i = 0; i < checkCount; i++)
        {
            if (answersMatchIt[i] == null || answerPlacedTextMatchIt[i] == null) continue;

            TextMeshProUGUI ansTxt = answersMatchIt[i].GetComponentInChildren<TextMeshProUGUI>();
            TextMeshProUGUI placeTxt = answerPlacedTextMatchIt[i].GetComponentInChildren<TextMeshProUGUI>();

            if (ansTxt != null && placeTxt != null && ansTxt.text == placeTxt.text)
            {
                targetImage = answersMatchIt[i].transform.parent != null ? answersMatchIt[i].transform.parent.GetComponent<Image>() : answersMatchIt[i].GetComponent<Image>();
                ApplyColor(correctColor, targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                targetImage = answersMatchIt[i].transform.parent != null ? answersMatchIt[i].transform.parent.GetComponent<Image>() : answersMatchIt[i].GetComponent<Image>();
                ApplyColor(wrongColor, targetImage);
            }
        }

        StartCoroutine(NextEnable());
    }

    private int FindMatchItAnswerIndex(GameObject obj)
    {
        if (answersMatchIt == null) return -1;
        for (int i = 0; i < answersMatchIt.Length; i++)
        {
            if (answersMatchIt[i] == null) continue;
            // Check if answersMatchIt[i] is the exact object or any child within the obj's hierarchy
            if (answersMatchIt[i] == obj || answersMatchIt[i].transform.IsChildOf(obj.transform))
            {
                return i;
            }
        }
        return -1;
    }

    public void SwapMatchItAnswers(GameObject objA, GameObject objB)
    {
        if (answersMatchIt == null) return;

        int indexA = FindMatchItAnswerIndex(objA);
        int indexB = FindMatchItAnswerIndex(objB);

        if (indexA != -1 && indexB != -1)
        {
            // 1. Swap GameObject array order (sibling index)
            int siblingIndexA = objA.transform.GetSiblingIndex();
            int siblingIndexB = objB.transform.GetSiblingIndex();

            if (siblingIndexA < siblingIndexB)
            {
                objB.transform.SetSiblingIndex(siblingIndexA);
                objA.transform.SetSiblingIndex(siblingIndexB);
            }
            else if (siblingIndexB < siblingIndexA)
            {
                objA.transform.SetSiblingIndex(siblingIndexB);
                objB.transform.SetSiblingIndex(siblingIndexA);
            }

            // 2. Swap array elements
            GameObject temp = answersMatchIt[indexA];
            answersMatchIt[indexA] = answersMatchIt[indexB];
            answersMatchIt[indexB] = temp;

            DebugUtil.Log($"Swapped array elements at indices {indexA} and {indexB}");
        }
        else
        {
            DebugUtil.Log($"SwapMatchItAnswers failed to find objects in array: indexA={indexA}, indexB={indexB}");
        }
    }

    public void MoveMatchItAnswerToSlot(GameObject snappedObject, int newSlotIndex)
    {
        if (answersMatchIt == null || newSlotIndex < 0 || newSlotIndex >= answersMatchIt.Length) return;

        int currentIndex = System.Array.IndexOf(answersMatchIt, snappedObject);
        if (currentIndex != -1 && currentIndex != newSlotIndex)
        {
            // Swap with whatever is currently in the new slot
            GameObject objectInNewSlot = answersMatchIt[newSlotIndex];
            SwapMatchItAnswers(snappedObject, objectInNewSlot);
        }
    }

    public IEnumerator NextEnable()
    {
        yield return new WaitForSeconds(2f);
        nextButton.gameObject.SetActive(true);
        nextButton.enabled = true;
        nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
    }
    private void ApplyColor(Color color, Image Image)
    {
        if (targetImage != null)
        {
            targetImage.color = color;
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private void ResolveReferenceManagers()
    {
        if (matchItReferenceManager == null)
        {
            matchItReferenceManager = GetComponent<MatchItReferenceManager>();
        }
        if (matchItReferenceManager == null)
        {
            matchItReferenceManager = GetComponentInChildren<MatchItReferenceManager>(true);
        }
        if (matchItReferenceManager == null)
        {
            matchItReferenceManager = FindAnyObjectByType<MatchItReferenceManager>();
        }

        if (matchItReferenceManager != null)
        {
            if (matchItReferenceManager.Answer != null && matchItReferenceManager.Answer.Length > 0 && matchItReferenceManager.Answer[0] != null)
            {
                answersMatchIt = matchItReferenceManager.Answer;
            }
            if (matchItReferenceManager.Questions != null && matchItReferenceManager.Questions.Length > 0 && matchItReferenceManager.Questions[0] != null)
            {
                questionsMatchIt = matchItReferenceManager.Questions;
            }
            if (matchItReferenceManager.AnsweTextStore != null && matchItReferenceManager.AnsweTextStore.Length > 0 && matchItReferenceManager.AnsweTextStore[0] != null)
            {
                answerPlacedTextMatchIt = matchItReferenceManager.AnsweTextStore;
            }
        }

        if (fillItReferenceManager == null)
        {
            fillItReferenceManager = GetComponent<FillItReferenceManager>();
        }
        if (fillItReferenceManager == null)
        {
            fillItReferenceManager = GetComponentInChildren<FillItReferenceManager>(true);
        }
        if (fillItReferenceManager == null)
        {
            fillItReferenceManager = FindAnyObjectByType<FillItReferenceManager>();
        }

        if (fillItReferenceManager != null)
        {
            if (fillItReferenceManager.Answer != null && fillItReferenceManager.Answer.Length > 0 && fillItReferenceManager.Answer[0] != null)
            {
                answersFillIt = fillItReferenceManager.Answer;
            }
            if (fillItReferenceManager.Questions != null && fillItReferenceManager.Questions.Length > 0 && fillItReferenceManager.Questions[0] != null)
            {
                questionsFillIt = fillItReferenceManager.Questions;
            }
            if (fillItReferenceManager.AnswerPlacement != null && fillItReferenceManager.AnswerPlacement.Length > 0 && fillItReferenceManager.AnswerPlacement[0] != null)
            {
                answerPlacedTextFillIt = fillItReferenceManager.AnswerPlacement;
            }
        }
    }
}
