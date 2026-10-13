using UnityEngine;
using UnityEngine.Events;

namespace WebGLMigration
{
    public class WebInteractable : MonoBehaviour
    {
        [Header("Events")]
        public UnityEvent onClick;
        public UnityEvent onHoverEnter;
        public UnityEvent onHoverExit;

        private bool isHovered = false;

        void Update()
        {
            if (DeviceSynHandler.IsGamePaused || Mathf.Approximately(Time.timeScale, 0f)) return;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            bool hitThis = Physics.Raycast(ray, out hit) && (hit.transform == transform || hit.transform.IsChildOf(transform));

            if (hitThis)
            {
                if (!isHovered)
                {
                    isHovered = true;
                    onHoverEnter?.Invoke();
                }

                if (Input.GetMouseButtonDown(0))
                {
                    onClick?.Invoke();
                }
            }
            else
            {
                if (isHovered)
                {
                    isHovered = false;
                    onHoverExit?.Invoke();
                }
            }
        }
    }
}
