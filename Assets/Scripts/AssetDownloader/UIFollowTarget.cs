using UnityEngine;

public class UIFollowTarget : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    private RectTransform rectTransform;
    private Canvas canvas;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
    }

    void Update()
    {
        if (target != null && Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(target.position);
            
            // If the target is behind the camera, we might want to hide it
            if (screenPos.z < 0)
            {
                // Hide or move off screen
                rectTransform.anchoredPosition = new Vector2(-10000, -10000);
                return;
            }

            // Apply screen space position
            rectTransform.position = screenPos + offset;
        }
    }
}
