using UnityEngine;

namespace WebGLMigration
{
    public class WebCameraController : MonoBehaviour
    {
        [Header("Look Settings")]
        public float sensitivity = 2f;
        public bool invertY = false;
        public float minYAngle = -89f;
        public float maxYAngle = 89f;

        private float rotationX = 0f;
        private float rotationY = 0f;

        public static WebCameraController Instance;

        private void Awake()
        {
            Instance = this;
        }

        public void EnableController(bool state)
        {
            enabled = state;
            isDragging = false;
        }

        public void ResetCameraRotation()
        {
            rotationX = 0f;
            rotationY = 0f;
            transform.localRotation = Quaternion.identity;
        }

        private Vector3 lastMousePosition;
        private bool isDragging = false;

        void Start()
        {
            Vector3 angles = transform.eulerAngles;
            rotationX = angles.y;
            rotationY = angles.x;

            if (rotationY > 180f) rotationY -= 360f;
        }

        void Update()
        {
            if (DeviceSynHandler.IsGamePaused)
            {
                isDragging = false;
                return;
            }
            HandleInput();
        }

        private void HandleInput()
        {
            // Start drag
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
            }

            // End drag
            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            // During drag
            if (isDragging)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                lastMousePosition = Input.mousePosition;

                rotationX += delta.x * sensitivity * 0.1f;
                
                float invertModifier = invertY ? 1f : -1f;
                rotationY += delta.y * sensitivity * 0.1f * invertModifier;
                rotationY = Mathf.Clamp(rotationY, minYAngle, maxYAngle);

                transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0);
            }
        }
    }
}
