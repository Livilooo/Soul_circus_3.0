using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DistanceBasedSceneLoader : MonoBehaviour
{
    [Header("Scene Settings")]
#if UNITY_EDITOR
    [Tooltip("Assign the Scene Asset you want to load/unload additively.")]
    public SceneAsset sceneAsset;
#endif

    [SerializeField]
    [Tooltip("Scene name derived from the scene asset. Do not modify manually.")]
    private string sceneName;

    [Header("Distance Thresholds")]
    [Tooltip("Distance from this object at which the scene begins to load.")]
    public float loadDistance = 50f;

    [Tooltip("Distance from this object at which the scene will unload.")]
    public float unloadDistance = 60f;

    [Header("Unload Delay")]
    [Tooltip("Delay (in seconds) before unloading the scene after the player moves away.")]
    public float unloadDelay = 2f;

    [Tooltip("Reference to the player's transform. If left empty, the script will attempt to find an object tagged 'Player'.")]
    public Transform player;

    // Internal state
    private bool isSceneLoaded = false;
    private Coroutine unloadCoroutine = null;

    private void Start()
    {
        // If no player reference is provided, try to find it by tag
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                player = p.transform;
            }
            else
            {
                Debug.LogError("Player not found! Please assign the player's transform.");
            }
        }
    }

    private void Update()
    {
        if (player == null)
            return;

        // Check the distance between the player and this loader's position
        float distance = Vector3.Distance(transform.position, player.position);

        // If player is within the load distance and the scene isn't loaded, load it
        if (distance < loadDistance && !isSceneLoaded)
        {
            // Cancel any pending unload
            if (unloadCoroutine != null)
            {
                StopCoroutine(unloadCoroutine);
                unloadCoroutine = null;
            }
            StartCoroutine(LoadSceneAsync());
        }
        // If the player moves beyond the unload distance and the scene is loaded, start unloading it
        else if (distance > unloadDistance && isSceneLoaded)
        {
            if (unloadCoroutine == null)
            {
                unloadCoroutine = StartCoroutine(UnloadSceneAfterDelay());
            }
        }
    }

    private IEnumerator LoadSceneAsync()
    {
        // Ensure the scene name is valid
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Scene name is empty. Please assign a valid scene asset in the Inspector.");
            yield break;
        }

        // Check that the scene is available in Build Settings
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Scene '" + sceneName + "' cannot be loaded. Ensure it's added to the Build Settings.");
            yield break;
        }

        Debug.Log("Loading scene: " + sceneName);
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        loadOp.allowSceneActivation = true;

        while (!loadOp.isDone)
        {
            yield return null;
        }
        isSceneLoaded = true;
        Debug.Log("Scene loaded: " + sceneName);
    }

    private IEnumerator UnloadSceneAfterDelay()
    {
        yield return new WaitForSeconds(unloadDelay);

        Debug.Log("Unloading scene: " + sceneName);
        AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneName);

        while (unloadOp != null && !unloadOp.isDone)
        {
            yield return null;
        }
        isSceneLoaded = false;
        unloadCoroutine = null;
        Debug.Log("Scene unloaded: " + sceneName);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Automatically update the sceneName from the scene asset assigned in the Inspector
        if (sceneAsset != null)
        {
            sceneName = sceneAsset.name;
        }
        else
        {
            sceneName = "";
        }
    }
#endif
}


