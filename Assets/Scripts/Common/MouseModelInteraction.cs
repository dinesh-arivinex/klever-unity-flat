using UnityEngine;

public class MouseModelInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float rotationSpeed = 1f;
    public float zoomSpeed = 0.5f;
    public float panSpeed = 0.005f;
    
    [Header("Zoom Constraints")]
    public float minScale = 1f;
    public float maxScale = 5f;

    private Vector3 lastMousePosition;
    private bool isDragging = false;
    private bool isPanning = false;

    void Update()
    {
        // Start Interaction
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;
        }
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            isPanning = true;
            lastMousePosition = Input.mousePosition;
        }

        // End Interaction
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
        if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
        {
            isPanning = false;
        }

        // Orbit (Rotate)
        if (isDragging)
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            
            // Rotate around the object's center based on world space axes
            transform.Rotate(Vector3.up, -delta.x * rotationSpeed, Space.World);
            transform.Rotate(Camera.main.transform.right, delta.y * rotationSpeed, Space.World);
            
            lastMousePosition = Input.mousePosition;
        }
        
        // Pan (Move)
        if (isPanning)
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            
            // Move relative to the camera's perspective
            Vector3 panTranslation = (-Camera.main.transform.right * delta.x * panSpeed) + 
                                     (-Camera.main.transform.up * delta.y * panSpeed);
            
            transform.position += panTranslation;
            
            lastMousePosition = Input.mousePosition;
        }
        
        // Zoom (Scale)
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0)
        {
            Vector3 newScale = transform.localScale + (Vector3.one * scroll * zoomSpeed);
            
            // Apply scale constraints uniformly
            float clampedX = Mathf.Clamp(newScale.x, minScale, maxScale);
            float clampedY = Mathf.Clamp(newScale.y, minScale, maxScale);
            float clampedZ = Mathf.Clamp(newScale.z, minScale, maxScale);
            
            transform.localScale = new Vector3(clampedX, clampedY, clampedZ);
        }
    }
}
