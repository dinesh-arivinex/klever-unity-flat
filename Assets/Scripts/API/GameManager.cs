using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Script Ref")]
    public ChangeClassRoom changeClassRoom;
    public ThreesixtyImageManager threesixtyImageManager;
    public AssetDownloader assetDownloader;
    public ClassVrManager vrManager;
    public AddingImage addingImage;
    public ServiceScript serviceScript;
    public VideoManager videoManager;
    public API api;
    public DeviceSynHandler deviceSynHandler;
    public AnnotationManager annotationManager;
    public RuntimeImportBehaviourHelper runtimeImportBehaviourHelper;
    public RuntimeImportBehaviour runtimeImportBehaviour;
    public GoogleTextToSpeech googleTextToSpeech;
    public GamificationManager gamificationManager;
    public ModelScale modelScale;

    [Header("Gameobject Ref")]
    public GameObject videoManagerObj;
    public GameObject _360ImageExperienceObj;
    public GameObject assetDownloaderObj;
    public GameObject simulationManagerObj;
    public GameObject gamificationManagerObj;
    public GameObject AssessmentParent;

    public UnityEvent EnableClassAndBoard;
    public UnityEvent DisableClassAndBoard;

    public bool isModelSkip;
    public bool isImageSkip;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
