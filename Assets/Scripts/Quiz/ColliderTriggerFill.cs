using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ColliderTriggerFill : MonoBehaviour, IDropHandler
{
    public GameObject Answer1, Answer2, Answer3, Answer4, Answer5;
    public GameObject PositionGetter;
    public TextMeshProUGUI textfromRes;
    private Image targetImage;
    private Color correctColor = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
    private Color wrongColor = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);

    public QuizManagers fillItQuizManagers;
    private GamificationManager gamificationManager;

    private void Start()
    {
        gamificationManager = FindAnyObjectByType<GamificationManager>();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (DeviceSynHandler.IsGamePaused || Mathf.Approximately(Time.timeScale, 0f)) return;
        GameObject droppedObj = eventData.pointerDrag;
        if (droppedObj == null) return;

        // Check if the dragged object or its children match any of the answers
        if (IsMatchingAnswer(droppedObj, Answer1)) HandleAnswerDrop(Answer1, droppedObj);
        else if (IsMatchingAnswer(droppedObj, Answer2)) HandleAnswerDrop(Answer2, droppedObj);
        else if (IsMatchingAnswer(droppedObj, Answer3)) HandleAnswerDrop(Answer3, droppedObj);
        else if (IsMatchingAnswer(droppedObj, Answer4)) HandleAnswerDrop(Answer4, droppedObj);
        else if (IsMatchingAnswer(droppedObj, Answer5)) HandleAnswerDrop(Answer5, droppedObj);
    }

    private bool IsMatchingAnswer(GameObject draggedObj, GameObject answerObj)
    {
        if (answerObj == null) return false;
        return draggedObj == answerObj || draggedObj.transform.IsChildOf(answerObj.transform) || answerObj.transform.IsChildOf(draggedObj.transform);
    }

    private void HandleAnswerDrop(GameObject answer, GameObject draggedObj)
    {
        // Mark as successfully dropped to prevent UIDraggable from snapping it back
        UIDraggable draggable = draggedObj.GetComponent<UIDraggable>();
        if (draggable != null) draggable.isDroppedSuccessfully = true;

        GameObject grandParent = answer.transform.gameObject;

        Debug.Log(textfromRes.text);
        Debug.Log(answer.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

        TextMeshProUGUI childText = answer.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        targetImage = answer.GetComponent<Image>();

        if (this.GetComponent<BoxCollider>() != null) this.GetComponent<BoxCollider>().enabled = false;
        if (this.gameObject.GetComponent<Image>() != null) this.gameObject.GetComponent<Image>().enabled = false;
        if (answer.GetComponent<UIDraggable>() != null) answer.GetComponent<UIDraggable>().enabled = false;
        if (answer.GetComponent<Button>() != null) answer.GetComponent<Button>().enabled = false;

        //if (grandParent.transform.childCount > 1) 
        //{
        //    grandParent.transform.GetChild(1).gameObject.SetActive(false);
        //}

        if (childText.text == textfromRes.text)
        {
            Debug.Log("Correct");
            ApplyColor(correctColor, targetImage);
            gamificationManager.mcqScore++;
        }
        else
        {
            Debug.Log("Wrong");
            ApplyColor(wrongColor, targetImage);
        }

        // Snap to the target position
        grandParent.transform.position = PositionGetter.transform.position;
        grandParent.transform.rotation = PositionGetter.transform.rotation;

        fillItQuizManagers.answerPlacedCount++;

        if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
        {
            fillItQuizManagers.nextButton.enabled = true;
            if (fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>() != null)
            {
                fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }
    }

    private void ApplyColor(Color color, Image Image)
    {
        if (targetImage != null)
        {
            targetImage.color = color;
        }
    }

    IEnumerator CallIfWrong()
    {
        yield return new WaitForSeconds(2f);
    }
}
