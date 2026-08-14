using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MouseClickToButton : MonoBehaviour
{
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void OnMouseDown()
    {
        // When the 3D collider is clicked with the mouse, trigger the UI Button's onClick event!
        if (button != null && button.interactable && button.enabled)
        {
            button.onClick.Invoke();
        }
    }
}
