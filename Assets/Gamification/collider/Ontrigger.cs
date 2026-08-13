// using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Ontrigger : MonoBehaviour
{
    private GamificationController optionSelection;
    public BoxCollider[] optionsCollider;
    public Button answer1Btn,answer2Btn,answer3Btn,answer4Btn;
    public Button answerCurrentButtons;
    public Transform destination;
    public Transform staringPoint;
    public Transform staringPointArrow;
    public Transform fallpoint;
    private bool isBasketBall;
    public bool isEntered;
    private GamificationController gamificationController;
    
    [Header("Basket Ball details")]
    public AudioSource BasketBallAudioClip;
    public GameObject basketBall;
    public GameObject ballVisualize;
    public GameObject ballVisualizeChild;
    
    [Header("Archery details")]
    public AudioSource ArrowAudioClip;
    public GameObject arrow;
    public GameObject arrowVisualize;
    
    private  GamificationManager  gamificationManager;
    public BasketBallQuiz gameType;

    private void Start()
    {
        gamificationManager=FindObjectOfType<GamificationManager>();
        gamificationController =FindAnyObjectByType<GamificationController>();
        optionSelection =FindObjectOfType<GamificationController>();
        GamificationController.isGamefication=true;

        /*if (interactableEvents != null)
        {
            interactableEvents.WhenSelect.AddListener(OnHandSelect); // CHANGED
        }*/
    }

    public void ArrowTouchSpawn()
    {
        if(gamificationController.arrowstatus)
        {
            GamificationController.isGamefication = false;
            gamificationController.ToggleCollider(true);
            StartSpawning();
        }
    }
    public void OnInteractionButtonClicked()
    {
        if (GamificationController.isGamefication)
        {
            GamificationController.isGamefication = false;
            if (answerCurrentButtons != null) answerCurrentButtons.onClick.Invoke();
            DisableCollider();
            UpdateReferencedButtonColors();
            StartSpawning();
        }
        else if (gamificationController != null && gamificationController.arrowstatus)
        {
            GamificationController.isGamefication = false;
            gamificationController.ToggleCollider(true);
            if (answerCurrentButtons != null) answerCurrentButtons.onClick.Invoke();
            DisableCollider();
            UpdateReferencedButtonColors();
            StartSpawning();
        }
    }

    private void DisableCollider()
    {
        Button[] refBtns = { answer1Btn, answer2Btn, answer3Btn, answer4Btn };
        foreach (var btn in refBtns)
        {
            if (btn != null)
            {
                Collider col = btn.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                
                Ontrigger trig = btn.GetComponent<Ontrigger>();
                if (trig != null)
                {
                    Collider trigCol = trig.GetComponent<Collider>();
                    if (trigCol != null) trigCol.enabled = false;
                }
            }
        }

        Collider thisCol = GetComponent<Collider>();
        if (thisCol != null)
        {
            thisCol.enabled = false;
        }

        if (optionsCollider != null)
        {
            foreach (var col in optionsCollider)
            {
                if (col != null) col.enabled = false;
            }
        }
    }

    private void OnMouseDown()
    {
        OnInteractionButtonClicked();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("BasketBall"))
        {
            Button temp = answerCurrentButtons.GetComponent<Button>();
            temp.onClick.Invoke();
            UpdateReferencedButtonColors();
        }
        if (other.gameObject.CompareTag("arrow"))
        {
            Button temp = answerCurrentButtons.GetComponent<Button>();
            temp.onClick.Invoke();
            UpdateReferencedButtonColors();
        }
    }

    private void UpdateReferencedButtonColors()
    {
        if (gameType == null || gameType.answers == null) return;
        
        Button[] refBtns = { answer1Btn, answer2Btn, answer3Btn, answer4Btn };
        Color green = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
        Color red = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);

        for (int i = 0; i < refBtns.Length; i++)
        {
            if (refBtns[i] != null && i < gameType.answers.Count)
            {
                if (gameType.answers[i].isCorrect)
                {
                    ChangeColor(refBtns[i], green);
                }
                else
                {
                    ChangeColor(refBtns[i], red);
                }
            }
        }
    }

    private void ChangeColor(Button button, Color color)
    {
        Graphic graphic = button.GetComponent<Graphic>();
        if (graphic != null) graphic.color = color;
        ColorBlock cb = button.colors;
        cb.normalColor = color;
        cb.disabledColor = color;
        cb.selectedColor = color;
        button.colors = cb;
    }

    private void OnTriggerExit(Collider other)
    {
        isEntered=false;
    }
    public void StartSpawning()
    {
        gamificationController.UnchildStartPoint();
        SpawnBall();
    }

    private void SpawnBall()
    {
        if (gamificationManager.currentGameName == "Basketball")
        {
            BasketBallAudioClip.Play();
            ballVisualize.SetActive(false);
            GameObject newBall = Instantiate(basketBall, destination.position, Quaternion.identity);
            SpawnObject spawn = newBall.GetComponent<SpawnObject>();
            if (spawn == null) spawn = newBall.GetComponentInChildren<SpawnObject>();
            spawn.SetStartPointAndDestination(fallpoint, optionSelection.isBasketBall, staringPoint, destination);
            StartCoroutine(nextButtonEnable());
        }
        if (gamificationManager.currentGameName == "Archery")
        {
            ArrowAudioClip.Play();
            arrowVisualize.SetActive(false);
            destination = staringPointArrow;
            GameObject newBall = Instantiate(arrow, arrowVisualize.transform.position, arrowVisualize.transform.rotation);
            SpawnObject spawn = newBall.GetComponent<SpawnObject>();
            if (spawn == null) spawn = newBall.GetComponentInChildren<SpawnObject>();
            spawn.SetStartPointAndDestination(fallpoint, optionSelection.isBasketBall, staringPoint, destination);
            StartCoroutine(nextButtonEnable());
        }

        IEnumerator nextButtonEnable()
        {
            yield return new WaitForSeconds(3);
            if (gameType != null && gameType.nextButton != null)
            {
                gameType.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
                gameType.nextButton.enabled = true; // Enable the UI Button component for mouse clicks
            }
        }
    }

}
