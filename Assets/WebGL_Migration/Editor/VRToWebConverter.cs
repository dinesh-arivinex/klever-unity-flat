using UnityEngine;
using UnityEditor;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WebGLMigration.Editor
{
    public class VRToWebConverter : EditorWindow
    {
        [MenuItem("KleverVR/Convert Scene to WebGL")]
        public static void ShowWindow()
        {
            GetWindow<VRToWebConverter>("VR to WebGL Converter");
        }

        private void OnGUI()
        {
            GUILayout.Label("Scene Converter for WebGL", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This tool will disable VR components in the active scene and replace them with WebGL 2D interaction equivalents.", MessageType.Info);

            if (GUILayout.Button("Convert Active Scene"))
            {
                ConvertScene();
            }
        }

        private void ConvertScene()
        {

            // 1. Disable VR Camera Rigs
            DisableVRRigs();

            // 2. Setup Web Camera
            SetupWebCamera();

            // 3. Setup UI Canvases for Web
            SetupUICanvases();

            // 4. Setup Event System
            SetupEventSystem();

            Debug.Log("Scene conversion to WebGL completed!");
        }

        private void DisableVRRigs()
        {
            // Search for common VR rigs
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject root in rootObjects)
            {
                var rigs = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in rigs)
                {
                    if (t.name.Contains("OVRCameraRig") || t.name.Contains("OVRPlayerController") || t.name.Contains("XROrigin") || t.name.Contains("XR Rig"))
                    {
                        Undo.RecordObject(t.gameObject, "Disable VR Rig");
                        t.gameObject.SetActive(false);
                        Debug.Log($"Disabled VR Rig: {t.name}");
                    }
                }
            }
        }

        private void SetupWebCamera()
        {
            WebCameraController existingCam = FindObjectOfType<WebCameraController>();
            if (existingCam != null) return;

            GameObject webCamObj = new GameObject("WebPlayerCamera");
            Camera cam = webCamObj.AddComponent<Camera>();
            cam.tag = "MainCamera";
            
            webCamObj.AddComponent<AudioListener>();
            webCamObj.AddComponent<WebCameraController>();
            webCamObj.AddComponent<PhysicsRaycaster>(); // Needed to click 3D objects with EventSystem

            Undo.RegisterCreatedObjectUndo(webCamObj, "Create Web Camera");
            Debug.Log("Created WebPlayerCamera with WebCameraController.");
        }

        private void SetupUICanvases()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                Undo.RecordObject(canvas.gameObject, "Update Canvas");
                
                // If the user wants to keep world space canvases, we just make sure they have a graphic raycaster
                // We also disable any OVRRaycasters
                var components = canvas.GetComponents<MonoBehaviour>();
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name.Contains("OVRRaycaster"))
                    {
                        Undo.RecordObject(comp, "Disable OVRRaycaster");
                        (comp as Behaviour).enabled = false;
                    }
                }

                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
                }
            }
        }

        private void SetupEventSystem()
        {
            EventSystem evt = FindObjectOfType<EventSystem>();
            if (evt == null)
            {
                GameObject evtObj = new GameObject("EventSystem");
                evt = evtObj.AddComponent<EventSystem>();
                evtObj.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(evtObj, "Create Event System");
            }
            else
            {
                // Ensure it has standard input module, remove OVRInputModule
                var ovrInput = evt.GetComponent("OVRInputModule");
                if (ovrInput != null)
                {
                    Undo.DestroyObjectImmediate(ovrInput);
                    if (evt.GetComponent<StandaloneInputModule>() == null)
                    {
                        Undo.AddComponent<StandaloneInputModule>(evt.gameObject);
                    }
                }
            }
        }
    }
}
