using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class ClickDebugger : MonoBehaviour
{
    void Update()
    {
        if (DeviceSynHandler.IsGamePaused || Mathf.Approximately(Time.timeScale, 0f)) return;
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Mouse clicked at " + Input.mousePosition);

            // 1. Check UI elements
            if (EventSystem.current != null)
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };

                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);
                
                if (results.Count > 0)
                {
                    Debug.Log("--- UI Raycast Hits ---");
                    foreach (RaycastResult result in results)
                    {
                        Debug.Log("Hit UI Element: " + result.gameObject.name, result.gameObject);
                    }
                }
                else
                {
                    Debug.Log("UI Raycast hit nothing.");
                }
            }
            else
            {
                Debug.LogWarning("No EventSystem found in scene! UI clicks will not work.");
            }

            // 2. Check 3D Physics objects
            if (Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Debug.Log("--- 3D Physics Raycast Hit ---");
                    Debug.Log("Hit 3D Object: " + hit.collider.gameObject.name, hit.collider.gameObject);
                }
                else
                {
                    Debug.Log("3D Physics Raycast hit nothing.");
                }
            }
            else
            {
                Debug.LogWarning("No Main Camera found in scene!");
            }
        }
    }
}
