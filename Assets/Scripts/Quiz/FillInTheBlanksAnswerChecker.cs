using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// This script can be attached to Drag and Drop interactables for Fill in the Blanks.
/// It sets the color to Green (if correct) or Red (if wrong) with the exact alpha
/// used in QuizManagers (MCQ).
/// </summary>
public class FillInTheBlanksAnswerChecker : MonoBehaviour
{
    [Header("Answer Status")]
    [Tooltip("Check this if this object represents the correct answer.")]
    public bool isCorrect;

    [Header("Color Targets (Optional)")]
    [Tooltip("Drag the UI Image component here. If left blank, it tries to find one on this GameObject.")]
    public Image targetImage;
    [Tooltip("Drag the MeshRenderer component here if it's a 3D object. If left blank, it tries to find one on this GameObject.")]
    public MeshRenderer targetMeshRenderer;

    [Header("Events (Optional)")]
    public UnityEvent OnCorrectAnswer;
    public UnityEvent OnWrongAnswer;

    // Exact colors from QuizManagers.cs
    private Color correctColor = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
    private Color wrongColor = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);

    private void Awake()
    {
        // Try to automatically find components if they are not assigned
        if (targetImage == null) targetImage = GetComponent<Image>();
        if (targetMeshRenderer == null) targetMeshRenderer = GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// Call this method from your Drag and Drop Drop Zone or Snap Interactable events 
    /// (e.g. InteractableUnityEventWrapper.WhenSelect) when the object is dropped/snapped.
    /// </summary>
    public void CheckAnswer()
    {
        if (isCorrect)
        {
            ApplyColor(correctColor);
            OnCorrectAnswer?.Invoke();
        }
        else
        {
            ApplyColor(wrongColor);
            OnWrongAnswer?.Invoke();
        }
    }

    /// <summary>
    /// Applies the specified color to any found render components.
    /// </summary>
    private void ApplyColor(Color color)
    {
        if (targetImage != null)
        {
            targetImage.color = color;
        }

        if (targetMeshRenderer != null && targetMeshRenderer.material != null)
        {
            // Note: For 3D materials, the material needs to support transparency to see the alpha effect.
            targetMeshRenderer.material.color = color;
        }
    }

    /// <summary>
    /// Resets the color back to white.
    /// </summary>
    public void ResetColor()
    {
        ApplyColor(Color.white);
    }
}
