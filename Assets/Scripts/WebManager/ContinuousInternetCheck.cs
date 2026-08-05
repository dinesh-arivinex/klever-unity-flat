using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class ContinuousInternetCheck : MonoBehaviour
{
    // Time interval in seconds to check the internet connection.
    private float checkInterval = 10f; // Adjust the interval as needed.
    public GameObject internteCanva;

    void Start()
    {
        // Start checking the internet connection at regular intervals.
        InvokeRepeating(nameof(CheckInternetConnection), 0f, checkInterval);
    }

    private void CheckInternetConnection()
    {
        StartCoroutine(CheckInternetConnectionCoroutine());
    }

    private IEnumerator CheckInternetConnectionCoroutine()
    {
        // Probe the service the experience actually depends on. Both Klever API
        // hosts permit the WebGL origin, unlike arbitrary sites such as Google.
        string healthUrl = Klass.Net.KlassEndpoints.BackendBase + "/health";
        using (UnityWebRequest request = UnityWebRequest.Head(healthUrl))
        {
            yield return request.SendWebRequest();

            bool isOnline = request.result == UnityWebRequest.Result.Success;
            if (!isOnline)
            {
                DebugUtil.Log("No internet connection: " + request.error);
            }
            else
            {
                DebugUtil.Log("Internet connection is available.");
            }

            SetInternetStatus(isOnline);
        }
    }

    private void SetInternetStatus(bool isOnline)
    {
        bool shouldShowOfflineCanvas = !isOnline;
        if (internteCanva != null && internteCanva.activeSelf != shouldShowOfflineCanvas)
        {
            internteCanva.SetActive(shouldShowOfflineCanvas);
        }
    }
}
