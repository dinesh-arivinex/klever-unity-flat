using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class EnsureEventSystem : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        // Wait for scene-authored systems before creating a fallback.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad() => EnsureReady();

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureReady();

    public static EventSystem EnsureReady()
    {
        var system = EventSystem.current;
        if (system == null)
        {
            // A disabled EventSystem clears EventSystem.current. Recover it
            // rather than creating a second system on every resume.
            foreach (var candidate in Object.FindObjectsOfType<EventSystem>())
            {
                if (system == null || candidate.isActiveAndEnabled) system = candidate;
                if (candidate.isActiveAndEnabled) break;
            }
        }

        if (system == null)
        {
            var go = new GameObject("EventSystem");
            system = go.AddComponent<EventSystem>();
            Object.DontDestroyOnLoad(go);
            Debug.Log("Created fallback EventSystem for UI interactions.");
        }

        system.enabled = true;
        if (system.GetComponent<BaseInputModule>() == null)
            system.gameObject.AddComponent<StandaloneInputModule>();
        return system;
    }
}
