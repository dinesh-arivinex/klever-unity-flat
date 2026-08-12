using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public class ColliderTriggerMatch : MonoBehaviour, IDropHandler
{
    public GameObject Answer1Pos, Answer2Pos, Answer3Pos, Answer4Pos;

    [HideInInspector] public Vector3 pos1, pos2, pos3, pos4;

    private QuizManagers quizManager;

    private void Start()
    {
        quizManager = FindAnyObjectByType<QuizManagers>();

        if (Answer1Pos != null) pos1 = Answer1Pos.transform.position;
        if (Answer2Pos != null) pos2 = Answer2Pos.transform.position;
        if (Answer3Pos != null) pos3 = Answer3Pos.transform.position;
        if (Answer4Pos != null) pos4 = Answer4Pos.transform.position;
    }

    private GameObject GetAnswerRoot(GameObject obj)
    {
        if (obj == null) return null;

        Transform checkTransform = obj.transform;
        UIDraggable draggable = obj.GetComponent<UIDraggable>();
        if (draggable != null && draggable.originalParent != null)
        {
            checkTransform = draggable.originalParent;
        }

        if (Answer1Pos != null && (obj == Answer1Pos || checkTransform == Answer1Pos.transform || checkTransform.IsChildOf(Answer1Pos.transform))) return Answer1Pos;
        if (Answer2Pos != null && (obj == Answer2Pos || checkTransform == Answer2Pos.transform || checkTransform.IsChildOf(Answer2Pos.transform))) return Answer2Pos;
        if (Answer3Pos != null && (obj == Answer3Pos || checkTransform == Answer3Pos.transform || checkTransform.IsChildOf(Answer3Pos.transform))) return Answer3Pos;
        if (Answer4Pos != null && (obj == Answer4Pos || checkTransform == Answer4Pos.transform || checkTransform.IsChildOf(Answer4Pos.transform))) return Answer4Pos;
        return null;
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObj = eventData.pointerDrag;
        if (droppedObj == null) return;

        GameObject draggedAnswer = GetAnswerRoot(droppedObj);
        GameObject targetAnswer = GetAnswerRoot(gameObject);

        // Fallback to raycast hovered object if the direct gameObject receiver is not one of the Answer slots
        if (targetAnswer == null && eventData.pointerCurrentRaycast.gameObject != null)
        {
            targetAnswer = GetAnswerRoot(eventData.pointerCurrentRaycast.gameObject);
        }

        if (draggedAnswer != null && targetAnswer != null && draggedAnswer != targetAnswer)
        {
            // Mark as successfully dropped to prevent UIDraggable from snapping it back
            UIDraggable draggable = droppedObj.GetComponent<UIDraggable>();
            if (draggable != null) draggable.isDroppedSuccessfully = true;

            SwapPositions(targetAnswer, draggedAnswer);
        }
    }

    private void SwapPositions(GameObject objA, GameObject objB)
    {
        Vector3 slotA = GetCurrentSlotPosition(objA);
        Vector3 slotB = GetCurrentSlotPosition(objB);

        objA.transform.position = slotB;
        objB.transform.position = slotA;

        UpdateSlotPosition(objA, slotB);
        UpdateSlotPosition(objB, slotA);

        if (quizManager != null)
        {
            quizManager.SwapMatchItAnswers(objA, objB);
        }

        Debug.Log($"Swapped {objA.name} and {objB.name}");
    }

    private Vector3 GetCurrentSlotPosition(GameObject obj)
    {
        if (obj == Answer1Pos) return pos1;
        if (obj == Answer2Pos) return pos2;
        if (obj == Answer3Pos) return pos3;
        if (obj == Answer4Pos) return pos4;
        return obj.transform.position;
    }

    private void UpdateSlotPosition(GameObject obj, Vector3 newPos)
    {
        UpdateLocalSlot(obj, newPos);

        ColliderTriggerMatch[] allMatches = FindObjectsOfType<ColliderTriggerMatch>();
        foreach (var match in allMatches)
        {
            if (match != this)
            {
                match.UpdateLocalSlot(obj, newPos);
            }
        }
    }

    private void UpdateLocalSlot(GameObject obj, Vector3 newPos)
    {
        if (obj == Answer1Pos) pos1 = newPos;
        else if (obj == Answer2Pos) pos2 = newPos;
        else if (obj == Answer3Pos) pos3 = newPos;
        else if (obj == Answer4Pos) pos4 = newPos;
    }
}
