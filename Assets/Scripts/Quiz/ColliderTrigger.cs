using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public class ColliderTrigger : MonoBehaviour
{
    public List<string> targetObjects = new List<string>();

    public GameObject AnswerPlacePos;
    public List<GameObject> GrabInteractable;
    public string correctAnswerTag;

    private void OnTriggerEnter(Collider other)
    {
        foreach (string targetName in targetObjects)
        {
            if (other.gameObject.tag == targetName)
            {
                GameObject snapTarget = GameObject.Find(targetName);

                if (snapTarget != null)
                {
                    snapTarget.GetComponent<BoxCollider>().enabled = false;
                    GameObject grandParent = snapTarget.transform.parent.parent.gameObject;
                    
                    // --- ASSIGN COLOR BASED ON CORRECTNESS ---
                    Image answerImage = grandParent.GetComponentInChildren<Image>();
                    if (answerImage != null)
                    {
                        // Check if this specific object's tag matches the correct answer
                        if (other.gameObject.tag == correctAnswerTag)
                        {
                            // CORRECT -> GREEN
                            answerImage.color = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
                        }
                        else
                        {
                            // WRONG -> RED
                            answerImage.color = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);
                        }
                    }
                    
                    Transform handGrabInteractable = snapTarget.transform.parent.parent.Find("HandGrabInteractable");

                    if (handGrabInteractable != null)
                    {
                        handGrabInteractable.gameObject.SetActive(false);
                    }
                    grandParent.transform.localPosition = AnswerPlacePos.transform.localPosition;
                    this.gameObject.GetComponent<BoxCollider>().enabled = false;
                    this.gameObject.GetComponent<Image>().enabled=false;

                    foreach(var items in GrabInteractable)
                    {
                        if (items != null && items != grandParent)
                        {
                            items.gameObject.SetActive(false);
                        }
                    }
                }

                break;
            }
        }
    }
}