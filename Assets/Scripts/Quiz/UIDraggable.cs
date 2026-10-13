using UnityEngine;
using UnityEngine.EventSystems;

public class UIDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [HideInInspector] public Vector3 originalPosition;
    [HideInInspector] public Transform originalParent;
    [HideInInspector] public bool isDroppedSuccessfully = false;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // Add CanvasGroup if not present (needed to ignore raycasts while dragging)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (DeviceSynHandler.IsGamePaused || Mathf.Approximately(Time.timeScale, 0f)) return;
        // Required by Unity EventSystem on some setups to begin tracking drag events!
        Debug.Log("UIDraggable: OnPointerDown fired for " + gameObject.name);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (DeviceSynHandler.IsGamePaused || Mathf.Approximately(Time.timeScale, 0f))
        {
            eventData.pointerDrag = null;
            return;
        }

        int dragLayer = LayerMask.NameToLayer("Drag");
        if (dragLayer != -1 && gameObject.layer != dragLayer)
        {
            eventData.pointerDrag = null;
            return;
        }

        Debug.Log("UIDraggable: OnBeginDrag fired for " + gameObject.name);
        originalPosition = transform.position;
        originalParent = transform.parent;

        // Escape layout groups by temporarily parenting to the Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            transform.SetParent(canvas.transform, true);
        }
        else
        {
            Debug.LogWarning("UIDraggable: No Canvas found in parent hierarchy!");
        }

        // Bring to front while dragging
        transform.SetAsLastSibling();

        // Disable raycasts so the drop zone underneath can receive the drop event
        canvasGroup.blocksRaycasts = false;
        isDroppedSuccessfully = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (DeviceSynHandler.IsGamePaused || Mathf.Approximately(Time.timeScale, 0f)) return;

        // Move the UI element with the mouse/touch
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out Vector3 worldPoint))
        {
            transform.position = worldPoint;
        }
        else
        {
            Debug.LogWarning("UIDraggable: ScreenPointToWorldPointInRectangle returned false!");
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (DeviceSynHandler.IsGamePaused || Mathf.Approximately(Time.timeScale, 0f)) return;

        Debug.Log("UIDraggable: OnEndDrag fired for " + gameObject.name);
        canvasGroup.blocksRaycasts = true;

        if (!isDroppedSuccessfully)
        {
            // Snap back if dropped in an invalid area
            if (originalParent != null)
            {
                transform.SetParent(originalParent, true);
            }
            transform.position = originalPosition;
        }
        else
        {
            // Dropped successfully - return to original parent keeping world position
            if (originalParent != null)
            {
                transform.SetParent(originalParent, true);
            }
        }
    }
}
