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
    public Transform destinationHandMode;
    public Transform staringPoint;
    public Transform staringPointArrow;
    public Transform staringPointArrowHandMode;
    public Transform fallpoint;
    private bool isBasketBall;
    public bool isEntered;
    private GamificationController gamificationController;
    public InputActionReference inputActionController;
    //public OVRInput.RawButton inputActionController;
    [Header("Basket Ball details")]
    public AudioSource BasketBallAudioClip;
    public GameObject basketBall;
    public GameObject ballVisualize, ballVisualizeHandMode;
    public GameObject ballVisualizeChild, ballVisualizeChildHandMode;
    [Header("Archery details")]
    public AudioSource ArrowAudioClip;
    public GameObject arrow;
    public GameObject arrowVisualize;
    public GameObject arrowVisualizeHandMode;
    private  GamificationManager  gamificationManager;
    public BasketBallQuiz gameType;
    // public InteractableUnityEventWrapper interactableEvents;
    private bool wasHandSelected = false;
    private bool isCursorHandInside = false;

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
    private void OnTriggerStay(Collider other)
    {

        if (other.gameObject.CompareTag("Cursor"))
        {
          isEntered = true;
            if (GamificationController.isGamefication)
            {
              
                 if (Input.GetKeyDown(KeyCode.Space))  // play mode
                 {
                    
                    GamificationController.isGamefication  = false;
                    StartSpawning();
                }
                if (inputActionController != null && inputActionController.action.WasPressedThisFrame()) // build mode Controller
                {
                    GamificationController.isGamefication = false;
                    StartSpawning();
                }
            }
          
        }

        if (other.gameObject.CompareTag("CursorHand")) // build mode Hand
        {
            isEntered = true;
            isCursorHandInside = true;
        }

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


    void OnHandSelect()
    {
        if (isCursorHandInside && GamificationController.isGamefication)
        {
            GamificationController.isGamefication = false;
            StartSpawning();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        isEntered=false;
        if (other.gameObject.CompareTag("CursorHand"))
        {
            isCursorHandInside = false;
        }
    }
    public void StartSpawning()
    {
        gamificationController.UnchildStartPoint();
        SpawnBall();
    }

    private void SpawnBall()
    {
        GamificationControllerHandSwitcher handSwitcher = GamificationControllerHandSwitcher.Instance;
        bool isHandMode = handSwitcher != null && handSwitcher.isHandMode;

        if (gamificationManager.currentGameName == "Basketball")
        {
            BasketBallAudioClip.Play();
            if (!isHandMode)
            {
                ballVisualize.SetActive(false);
                SetActiveIfPresent(handSwitcher != null ? handSwitcher.handPoseBall : null, false);
                GameObject newBall = Instantiate(basketBall, destination.position, Quaternion.identity);
                SpawnObject spawn = FindAnyObjectByType<SpawnObject>();
                spawn.SetStartPointAndDestination(fallpoint, optionSelection.isBasketBall, staringPoint, destination);
            }
            else
            {
                ballVisualizeHandMode.SetActive(false);
                SetActiveIfPresent(handSwitcher.rightHandMesh, true);
                SetActiveIfPresent(handSwitcher.handPoseBallHandMode, true);
                GameObject newBall = Instantiate(basketBall, destinationHandMode.position, Quaternion.identity);
                SpawnObject spawn = FindAnyObjectByType<SpawnObject>();
                spawn.SetStartPointAndDestination(fallpoint, optionSelection.isBasketBall, staringPoint, destinationHandMode);
            }
            StartCoroutine(nextButtonEnable());
        }
        if (gamificationManager.currentGameName == "Archery")
        {
            ArrowAudioClip.Play();
            if (!isHandMode)
            {
                arrowVisualize.SetActive(false);
                destination = staringPointArrow;
                GameObject newBall = Instantiate(arrow, arrowVisualize.transform.position, arrowVisualize.transform.rotation);
                SpawnObject spawn = FindAnyObjectByType<SpawnObject>();
                spawn.SetStartPointAndDestination(fallpoint, optionSelection.isBasketBall, staringPoint, destination);
            }
            else
            {
                arrowVisualizeHandMode.SetActive(false);
                destinationHandMode = staringPointArrowHandMode;
                SetActiveIfPresent(handSwitcher.rightHandMesh, false);
                GameObject newBall = Instantiate(arrow, arrowVisualizeHandMode.transform.position, arrowVisualizeHandMode.transform.rotation);
                SpawnObject spawn = FindAnyObjectByType<SpawnObject>();
                spawn.SetStartPointAndDestination(fallpoint, optionSelection.isBasketBall, staringPoint, destinationHandMode);
            }
            StartCoroutine(nextButtonEnable());
        }

        IEnumerator nextButtonEnable()
        {
            yield return new WaitForSeconds(3);
            gameType.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
        }
    }

    private static void SetActiveIfPresent(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

}
