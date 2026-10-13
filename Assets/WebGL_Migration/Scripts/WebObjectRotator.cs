using UnityEngine;

namespace WebGLMigration
{
    public class WebObjectRotator : MonoBehaviour
    {
        [Header("Rotation Settings")]
        public float rotationSpeed = 5f;
        public bool invertX = false;
        public bool invertY = false;

        private bool isDragging = false;
        private Vector3 lastMousePosition;

        void Update()
        {
            if (DeviceSynHandler.IsGamePaused || Mathf.Approximately(Time.timeScale, 0f))
            {
                isDragging = false;
                return;
            }
            if (Input.GetMouseButtonDown(0))
            {
                // Check if we clicked on THIS object
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    {
                        isDragging = true;
                        lastMousePosition = Input.mousePosition;
                    }
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            if (isDragging)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                lastMousePosition = Input.mousePosition;

                float xMod = invertX ? 1f : -1f;
                float yMod = invertY ? 1f : -1f;

                float rotX = delta.y * rotationSpeed * 0.1f * xMod;
                float rotY = delta.x * rotationSpeed * 0.1f * yMod;

                // Rotate the object around the camera's up and right vectors
                transform.Rotate(Camera.main.transform.up, rotY, Space.World);
                transform.Rotate(Camera.main.transform.right, rotX, Space.World);
            }
        }
    }
}
