using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonObject : MonoBehaviour
{
    private void OnMouseDown()
    {
        if (DeviceSynHandler.IsGamePaused || Mathf.Approximately(Time.timeScale, 0f)) return;

        // Perform button-like actions here
        DebugUtil.Log("Cube clicked!");

        // For example, you can call a method or invoke an event
        ExecuteButtonClick();
    }

    private void ExecuteButtonClick()
    {
        // Add your button-like actions here
        // For example, you could call a method, trigger an event, or perform any other action.
    }
}
