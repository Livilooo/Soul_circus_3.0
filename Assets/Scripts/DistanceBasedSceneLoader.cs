using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
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

    // Internal state flags
    private bool isSceneLoaded = false;
    private bool isLoading = false;
    private bool isUnloading = false;
    private Coroutine unloadCoroutine = null;

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                player = p.transform;
            else
                Debug.LogError("Player not found! Please assign the player's transform.");
        }
    }

    private void Update()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(transform.position, player.position);

        // When player is within loadDistance, start loading if not already loaded
        if (distance < loadDistance && !isSceneLoaded)
        {
            if (unloadCoroutine != null)
            {
                StopCoroutine(unloadCoroutine);
                unloadCoroutine = null;
            }
            if (!isLoading)
            {
                StartCoroutine(LoadSceneAsync());
            }
        }
        // When player is beyond unloadDistance, start unloading if loaded
        else if (distance > unloadDistance && isSceneLoaded)
        {
            if (unloadCoroutine == null && !isUnloading)
            {
                unloadCoroutine = StartCoroutine(UnloadSceneAfterDelay());
            }
        }
    }

    private IEnumerator LoadSceneAsync()
    {
        if (isLoading || isSceneLoaded)
            yield break;
        isLoading = true;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Scene name is empty. Please assign a valid scene asset in the Inspector.");
            isLoading = false;
            yield break;
        }
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Scene '" + sceneName + "' cannot be loaded. Ensure it's added to the Build Settings.");
            isLoading = false;
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
        isLoading = false;
        Debug.Log("Scene loaded: " + sceneName);
    }

    private IEnumerator UnloadSceneAfterDelay()
    {
        if (isUnloading || !isSceneLoaded)
            yield break;
        isUnloading = true;
        yield return new WaitForSeconds(unloadDelay);

        Debug.Log("Unloading scene: " + sceneName);
        AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneName);
        while (unloadOp != null && !unloadOp.isDone)
        {
            yield return null;
        }
        isSceneLoaded = false;
        isUnloading = false;
        unloadCoroutine = null;
        Debug.Log("Scene unloaded: " + sceneName);
    }

    // --- Editor Debug Buttons ---
    [ContextMenu("Load Scene Now")]
    private void LoadSceneNow()
    {
        if (Application.isPlaying)
        {
            // Play mode: use runtime loading
            if (!isSceneLoaded && !isLoading)
            {
                StartCoroutine(LoadSceneAsync());
            }
        }
        else
        {
#if UNITY_EDITOR
            // Edit mode: use EditorSceneManager to open the scene additively
            if (!string.IsNullOrEmpty(sceneName))
            {
                string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                if (!string.IsNullOrEmpty(scenePath))
                {
                    Debug.Log("Editor: Loading scene " + sceneName);
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }
                else
                {
                    Debug.LogError("Editor: Scene path is invalid. Make sure the scene asset is set correctly.");
                }
            }
#endif
        }
    }

    [ContextMenu("Unload Scene Now")]
    private void UnloadSceneNow()
    {
        if (Application.isPlaying)
        {
            // Play mode: use runtime unloading
            if (isSceneLoaded && !isUnloading)
            {
                StartCoroutine(UnloadSceneImmediate());
            }
        }
        else
        {
#if UNITY_EDITOR
            // Edit mode: use EditorSceneManager to close the scene
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                Debug.Log("Editor: Unloading scene " + sceneName);
                EditorSceneManager.CloseScene(scene, true);
            }
            else
            {
                Debug.Log("Editor: Scene " + sceneName + " is not loaded.");
            }
#endif
        }
    }

    private IEnumerator UnloadSceneImmediate()
    {
        if (isUnloading || !isSceneLoaded)
            yield break;
        isUnloading = true;
        Debug.Log("Unloading scene immediately: " + sceneName);
        AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneName);
        while (unloadOp != null && !unloadOp.isDone)
        {
            yield return null;
        }
        isSceneLoaded = false;
        isUnloading = false;
        if (unloadCoroutine != null)
        {
            StopCoroutine(unloadCoroutine);
            unloadCoroutine = null;
        }
        Debug.Log("Scene unloaded: " + sceneName);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Automatically update sceneName from the assigned SceneAsset in the Editor
        if (sceneAsset != null)
            sceneName = sceneAsset.name;
        else
            sceneName = "";
    }
#endif
}



