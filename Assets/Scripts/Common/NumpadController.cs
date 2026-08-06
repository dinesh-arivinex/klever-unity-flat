using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class NumpadController : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent<string> onEnterPressed;

    // These will be found automatically
    private TMP_InputField inputField;
    private TextMeshProUGUI textDisplay; 
    private TextMeshPro textDisplay3D; 

    private string currentText = "";

    void Start()
    {
        // 1. Automatically find the "Screen" object for the display anywhere in the children
        Transform screenObj = FindChildRecursive(this.transform, "Screen");
        
        if (screenObj != null)
        {
            inputField = screenObj.GetComponent<TMP_InputField>();
            textDisplay = screenObj.GetComponent<TextMeshProUGUI>();
            textDisplay3D = screenObj.GetComponent<TextMeshPro>();

            if (inputField != null) currentText = inputField.text;
            else if (textDisplay != null) currentText = textDisplay.text;
            else if (textDisplay3D != null) currentText = textDisplay3D.text;
        }
        else
        {
            Debug.LogWarning("NumpadController: Could not find a child object named 'Screen'!");
        }

        // 2. Automatically set up all buttons
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            // Skip self and the screen object
            if (child == this.transform || child == screenObj) continue;

            // CRITICAL FIX: Skip Text labels inside buttons so they don't intercept rays and cause double clicks!
            if (child.GetComponent<TextMeshProUGUI>() != null || child.GetComponent<Text>() != null || child.GetComponent<TextMeshPro>() != null)
            {
                NumpadButtonClickHandler accidentalHandler = child.GetComponent<NumpadButtonClickHandler>();
                if (accidentalHandler != null) Destroy(accidentalHandler);
                continue;
            }

            string objName = child.gameObject.name.ToLower();
            
            // Determine the button's purpose from its name
            int number = -1;
            bool isNumber = false;
            foreach (char c in objName)
            {
                if (char.IsDigit(c))
                {
                    number = int.Parse(c.ToString());
                    isNumber = true;
                    break;
                }
            }
            bool isBackspace = objName.Contains("back") || objName.Contains("del") || objName.Contains("clear");
            bool isEnter = objName.Contains("enter") || objName.Contains("ok") || objName.Contains("submit");

            if (isNumber || isBackspace || isEnter)
            {
                // Prefer standard Unity UI Buttons
                Button btn = child.GetComponent<Button>();
                
                if (btn != null)
                {
                    // Prevent double firing: remove the custom handler if it was added previously or manually
                    NumpadButtonClickHandler oldHandler = child.GetComponent<NumpadButtonClickHandler>();
                    if (oldHandler != null) Destroy(oldHandler);

                    // Hook into the standard Button's onClick event ONLY if it hasn't been set up in the inspector
                    if (btn.onClick.GetPersistentEventCount() == 0)
                    {
                        btn.onClick.RemoveAllListeners();
                        if (isNumber) btn.onClick.AddListener(() => OnNumberClicked(number.ToString()));
                        else if (isBackspace) btn.onClick.AddListener(OnBackspaceClicked);
                        else if (isEnter) btn.onClick.AddListener(OnEnterClicked);
                    }
                }
                else
                {
                    // Fallback for 3D objects without UI graphics: add BoxCollider and our custom click handler
                    if (child.GetComponent<Collider>() == null && child.GetComponent<Graphic>() == null)
                    {
                        child.gameObject.AddComponent<BoxCollider>();
                    }

                    NumpadButtonClickHandler clickHandler = child.gameObject.GetComponent<NumpadButtonClickHandler>();
                    if (clickHandler == null) clickHandler = child.gameObject.AddComponent<NumpadButtonClickHandler>();
                    
                    clickHandler.numpad = this;
                    clickHandler.isNumber = isNumber;
                    clickHandler.numberValue = isNumber ? number.ToString() : "";
                    clickHandler.isBackspace = isBackspace;
                    clickHandler.isEnter = isEnter;
                }
            }
        }

        // 3. Self-Healing: Fix missing EventSystem or GraphicRaycaster (common when removing VR SDKs)
        if (EventSystem.current == null && FindObjectOfType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
            Debug.Log("NumpadController: Auto-created missing EventSystem.");
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.GetComponent<GraphicRaycaster>() == null)
        {
            parentCanvas.gameObject.AddComponent<GraphicRaycaster>();
            Debug.Log("NumpadController: Auto-added missing GraphicRaycaster to Canvas.");
        }
    }
    
    // Helper to find a child by name anywhere in the hierarchy
    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private int lastClickFrame = -1;

    public void OnNumberClicked(string number)
    {
        // If this function was already called this exact frame, ignore it!
        if (Time.frameCount == lastClickFrame) return;
        lastClickFrame = Time.frameCount;

        currentText += number;
        UpdateDisplay();
    }

    public void OnBackspaceClicked()
    {
        if (Time.frameCount == lastClickFrame) return;
        lastClickFrame = Time.frameCount;

        if (currentText.Length > 0)
        {
            currentText = currentText.Substring(0, currentText.Length - 1);
            UpdateDisplay();
        }
    }

    public void OnEnterClicked()
    {
        if (Time.frameCount == lastClickFrame) return;
        lastClickFrame = Time.frameCount;

        onEnterPressed?.Invoke(currentText);
    }

    public void Clear()
    {
        currentText = "";
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (inputField != null) inputField.text = currentText;
        if (textDisplay != null) textDisplay.text = currentText;
        if (textDisplay3D != null) textDisplay3D.text = currentText;
    }
}

// Helper component to catch clicks universally on both 3D objects and UI Canvases
public class NumpadButtonClickHandler : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
{
    public NumpadController numpad;
    public bool isNumber;
    public string numberValue;
    public bool isBackspace;
    public bool isEnter;

    private float lastClickTime = 0f;

    // Handles UI and PhysicsRaycaster clicks
    public void OnPointerClick(PointerEventData eventData)
    {
        HandleClick();
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        // Required for IPointerClickHandler to work properly in Unity UI
    }

    // Handles standard 3D physics clicks (fallback if no EventSystem Physics Raycaster is present)
    void OnMouseDown()
    {
        HandleClick();
    }

    private void HandleClick()
    {
        if (numpad == null) return;

        // Prevent accidental double-clicks within a fraction of a second
        if (Time.time - lastClickTime < 0.1f) return; 
        lastClickTime = Time.time;

        if (isNumber) numpad.OnNumberClicked(numberValue);
        else if (isBackspace) numpad.OnBackspaceClicked();
        else if (isEnter) numpad.OnEnterClicked();
    }
}
