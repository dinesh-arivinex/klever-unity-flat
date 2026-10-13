using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight wrapper that lets a Unity UI Button or any UnityEvent target
/// toggle the student's hand-raise (Help) state on <see cref="DeviceSynHandler"/>.
///
/// Wire up either:
///   • A scene-authored Unity UI Button — set its OnClick() to call
///     <see cref="OnHelpButtonClicked"/> on this component.
///   • Or rely on <see cref="HelpButtonAutoMount"/> which spawns a help
///     button at runtime so no scene editing is required.
/// </summary>
[DisallowMultipleComponent]
public class HelpButton : MonoBehaviour
{
    private DeviceSynHandler _handler;
    [SerializeField] private GameObject helpButtonObj;
    [SerializeField] private GameObject unHelpButtonObj;

    void Awake()
    {
        ResolveButtonObjects();
    }

    void OnEnable()
    {
        ResolveHandler();
        DeviceSynHandler.OnHandRaisedChanged += OnHandRaisedStateChanged;
        UpdateVisuals(DeviceSynHandler.Instance != null && DeviceSynHandler.Instance.IsHandRaised);
    }

    void OnDisable()
    {
        DeviceSynHandler.OnHandRaisedChanged -= OnHandRaisedStateChanged;
    }

    private void OnHandRaisedStateChanged(bool raised)
    {
        UpdateVisuals(raised);
    }

    private void UpdateVisuals(bool handRaised)
    {
        ResolveButtonObjects();
        if (helpButtonObj != null) helpButtonObj.SetActive(!handRaised);
        if (unHelpButtonObj != null) unHelpButtonObj.SetActive(handRaised);
    }

    private void ResolveButtonObjects()
    {
        if (helpButtonObj == null || unHelpButtonObj == null)
        {
            var parent = transform.parent;
            if (parent != null)
            {
                var hb = parent.Find("HelpButton");
                if (hb != null && helpButtonObj == null) helpButtonObj = hb.gameObject;
                var ub = parent.Find("UnHelpButton");
                if (ub != null && unHelpButtonObj == null) unHelpButtonObj = ub.gameObject;
            }

            if (helpButtonObj == null || unHelpButtonObj == null)
            {
                var ap = GameObject.Find("AssessmentPanel");
                if (ap != null)
                {
                    var hb = ap.transform.Find("HelpButton");
                    if (hb != null && helpButtonObj == null) helpButtonObj = hb.gameObject;
                    var ub = ap.transform.Find("UnHelpButton");
                    if (ub != null && unHelpButtonObj == null) unHelpButtonObj = ub.gameObject;
                }
            }

            if (helpButtonObj == null || unHelpButtonObj == null)
            {
                var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in allGos)
                {
                    if (go != null && go.scene.isLoaded)
                    {
                        if (helpButtonObj == null && go.name == "HelpButton")
                            helpButtonObj = go;
                        else if (unHelpButtonObj == null && go.name == "UnHelpButton")
                            unHelpButtonObj = go;
                    }
                }
            }
        }
    }

    private void ResolveHandler()
    {
        if (_handler != null) return;
        _handler = FindObjectOfType<DeviceSynHandler>();
    }

    /// <summary>Toggle the hand-raise state — wire to a Button.onClick.</summary>
    public void OnHelpButtonClicked()
    {
        if (DeviceSynHandler.IsGamePaused || Mathf.Approximately(Time.timeScale, 0f))
        {
            Debug.Log("[HelpButton] Ignored click while game is paused.");
            return;
        }

        ResolveHandler();
        if (_handler == null)
        {
            Debug.LogWarning("[HelpButton] DeviceSynHandler not found in scene.");
            return;
        }
        _handler.ToggleHand();
    }
}
