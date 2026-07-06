using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class NumpadFixer
{
    [MenuItem("Tools/Clean And Fix Numpad Buttons")]
    public static void CleanNumpad()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Numpad Fixer: Please select the Numpad parent object in the hierarchy first!");
            return;
        }

        int missingRemovedCount = 0;
        int buttonAddedCount = 0;

        // Iterate through all children of the selected numpad
        foreach (Transform child in selected.GetComponentsInChildren<Transform>(true))
        {
            // 1. Remove all "Missing (Mono Script)" components that were left behind when Meta/Oculus was deleted
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            missingRemovedCount += removed;

            // 2. Identify if this object is a button by its name
            string objName = child.name.ToLower();
            bool isNumber = false;
            foreach (char c in objName) 
            { 
                if (char.IsDigit(c)) { isNumber = true; break; } 
            }
            
            bool isBackspace = objName.Contains("back") || objName.Contains("del") || objName.Contains("clear");
            bool isEnter = objName.Contains("enter") || objName.Contains("ok") || objName.Contains("submit");

            // If it's a number, backspace, or enter button
            if (isNumber || isBackspace || isEnter)
            {
                // Remove custom click handlers if they exist (we use standard buttons now)
                var oldClickHandler = child.GetComponent("NumpadButtonClickHandler");
                if (oldClickHandler != null)
                {
                    Object.DestroyImmediate(oldClickHandler);
                }

                // If it is a UI Graphic and is missing a standard Button, add one!
                Button btn = child.GetComponent<Button>();
                if (btn == null && child.GetComponent<Graphic>() != null)
                {
                    child.gameObject.AddComponent<Button>();
                    buttonAddedCount++;
                }
            }
        }

        Debug.Log($"Numpad Fixed Successfully! Removed {missingRemovedCount} missing/unwanted scripts and added {buttonAddedCount} standard UI Buttons to the children.");
    }
}
