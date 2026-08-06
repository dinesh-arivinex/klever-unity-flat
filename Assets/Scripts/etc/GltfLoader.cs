using System;
using UnityEngine;
using GLTFast;

public class GltfLoader : MonoBehaviour
{
    public string gltfUrl;

    private void Start()
    {

        //EventManager.AddHandler(GameEvent.OnPlayerLanded, LoadGltf);
        LoadGltf();
    }

    private async void LoadGltf()
    {
        if (string.IsNullOrWhiteSpace(gltfUrl))
        {
            gltfUrl = "https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Duck/glTF/Duck.gltf";
        }

        try
        {
            GltfImport gltfImporter = new GltfImport();
            bool success = await gltfImporter.Load(gltfUrl);
            DebugUtil.Log($"After loading glTF. Success: {success}");

            if (success)
            {
                await gltfImporter.InstantiateMainSceneAsync(transform);
            }
            else
            {
                DebugUtil.LogError("Failed to load glTF.");
            }
        }
        catch (Exception ex)
        {
            DebugUtil.LogError($"An error occurred while loading glTF from '{gltfUrl}': {ex.Message}");
        }
    }

    private void ResetScene()
    {
        // Clear the existing scene content
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }
}
