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
    public Button answerButtons;
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
            StartSpawning();
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
            Button temp = answerButtons.GetComponent<Button>();
            temp.onClick.Invoke();
        }
        if (other.gameObject.CompareTag("arrow"))
        {
            Button temp = answerButtons.GetComponent<Button>();
            temp.onClick.Invoke();
        }
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
