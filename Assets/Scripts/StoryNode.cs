/*
* StoryNode.cs
* Last Modified: 2025-04-15 20:07:29 UTC
* Modified By: OmniDev951 / Revised by Assistant
*
* This script handles story node sequences in Unity. It manages camera transitions,
* UI prompts, player interactions, and state persistence with proper chained sequence handling.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#region Serializable Surrogates for Save Data

[System.Serializable]
public class SerializableKeyValuePair
{
    public string key;
    public float value;
}

[System.Serializable]
public class NodeSaveData
{
    public string nodeId;
    public bool hasBeenTriggered;
    public List<SerializableKeyValuePair> variables;

    public NodeSaveData(string id)
    {
        nodeId = id;
        hasBeenTriggered = false;
        variables = new List<SerializableKeyValuePair>();
    }
}

#endregion

#region Additional Classes

[System.Serializable]
public class BranchingChoice
{
    public string choiceText;
    public StoryNode nextNode;
    public string conditionVariable;
    public float conditionValue;
    public enum ConditionType { Equals, GreaterThan, LessThan }
    public ConditionType conditionType;
    public bool requireCondition;
}

[System.Serializable]
public class CameraPoint
{
    public Transform point;
    public float transitionDuration = 1f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
}

#endregion

public class StoryNode : MonoBehaviour
{

    #region Variables & Settings

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;
    // True while this node’s sequence is running.
    private bool isProcessing = false;

    [Header("Identification")]
    [SerializeField] private string nodeId = "";

    [Header("Story Elements to Activate")]
    public GameObject[] storyElements;

    [Header("Objects to Temporarily Disable")]
    public GameObject[] objectsToDisable;

    [Header("Objects to Permanently Disable")]
    public GameObject[] objectsToPermanentlyDisable;

    [Header("Player Control Settings")]
    public bool allowPlayerMovementDuringNode = true;
    private bool wasPlayerMovementEnabled = true;
    private PlayerController playerController;

    [Header("Camera Settings")]
    public CameraPoint[] cameraPoints;
    public bool returnCameraToPlayer = true;
    private Camera mainCamera;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPosition;
    private Vector3 originalCameraWorldPosition;
    private Quaternion originalCameraLocalRotation;
    private Quaternion originalCameraWorldRotation;

    [Header("Chained Node Camera Settings")]
    [Tooltip("Set if this node is part of a chain.")]
    public bool isPartOfChainedSequence = false;
    [Tooltip("Set to true if this node is the last in a chain.")]
    public bool isLastNodeInChain = false;
    // Static flag ensures the camera state is stored only once per sequence.
    private static bool isInChainedSequence = false;

    [Header("Chained Story Node (Optional)")]
    public StoryNode nextNode;

    [Header("General Node Settings")]
    public bool triggerOnce = true;
    public float activeDuration = 5f;
    [Tooltip("If enabled, a Continue UI button will appear and the node waits for the player's input.")]
    public bool waitForUIButton = false;
    public Button continueButton;

    [Header("Choices")]
    public List<BranchingChoice> choices = new List<BranchingChoice>();

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onNodeActivate;
    public UnityEngine.Events.UnityEvent onNodeDeactivate;

    [Header("Development Settings")]
    [Tooltip("Right-click on the component (gear icon) and choose Clear Save Data to reset this node.")]
    public bool devMode = false;

    // Internal state flags.
    private bool triggered = false;
    private Coroutine activeCoroutine;
    private bool timeElapsed = false; // Set to true when the waiting period is over.
    private Dictionary<string, float> variables = new Dictionary<string, float>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            nodeId = System.Guid.NewGuid().ToString();
            DebugLog($"Generated new ID for node: {nodeId}");
        }
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            StoreOriginalCameraTransform();
        }
    }

    private void Start()
    {
        InitializeNode();
    }

    private void OnValidate()
    {
        if (activeDuration < 0)
        {
            Debug.LogWarning($"[StoryNode] Invalid duration in {gameObject.name}. Setting to 0.");
            activeDuration = 0;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player"))
            return;
        // If this node has already been triggered and is set to trigger once, ignore further triggers.
        if (triggered && triggerOnce)
            return;
        playerController = other.GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError($"[StoryNode] PlayerController not found on {other.name}");
            return;
        }
        wasPlayerMovementEnabled = playerController.canMove;
        ActivateNode();
    }

    private void OnDestroy()
    {
        SaveNodeState();
        RestorePlayerState();
        CleanupNode();
    }

    #endregion

    #region Initialization and Cleanup

    private void InitializeNode()
    {
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
            continueButton.onClick.AddListener(OnContinueButtonPressed);
        }
        // Load saved state if not in development mode.
        if (!devMode)
            LoadNodeState();
    }

    private void CleanupNode()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueButtonPressed);
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);
    }

    private void StoreOriginalCameraTransform()
    {
        if (mainCamera != null)
        {
            originalCameraParent = mainCamera.transform.parent;
            originalCameraLocalPosition = mainCamera.transform.localPosition;
            originalCameraWorldPosition = mainCamera.transform.position;
            originalCameraLocalRotation = mainCamera.transform.localRotation;
            originalCameraWorldRotation = mainCamera.transform.rotation;
            DebugLog("Stored original camera transform");
        }
    }

    #endregion

    #region Node Activation & Processing

    public void ActivateNode()
    {
        if (isProcessing)
        {
            DebugLog("Node already processing; ignoring activation request.");
            return;
        }
        isProcessing = true;
        try
        {
            // If this node isn't already in an active chain, store the camera.
            if (!isInChainedSequence || !isPartOfChainedSequence)
            {
                StoreOriginalCameraTransform();
                isInChainedSequence = isPartOfChainedSequence;
            }
            if (playerController != null)
            {
                wasPlayerMovementEnabled = playerController.canMove;
                // Set player movement as needed for this node.
                playerController.canMove = allowPlayerMovementDuringNode;
                DebugLog($"Player movement set to: {allowPlayerMovementDuringNode}");
            }
            // Activate all story elements.
            foreach (var element in storyElements)
            {
                if (element != null)
                    element.SetActive(true);
            }
            // Disable any objects that should be temporarily disabled.
            DisableObjects();
            triggered = true;
            timeElapsed = false;
            onNodeActivate?.Invoke();
            SaveNodeState();
            // Start processing the node sequence.
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(ProcessNodeSequence());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StoryNode] Error in ActivateNode: {e.Message}");
            RestorePlayerState();
            isProcessing = false;
            if (isPartOfChainedSequence && isLastNodeInChain)
                isInChainedSequence = false;
        }
    }

    private IEnumerator ProcessNodeSequence()
    {
        // 1. Process any camera transitions.
        if (cameraPoints != null && cameraPoints.Length > 0)
            yield return StartCoroutine(ProcessCameraPoints());
        // 2. Wait for input:
        if (waitForUIButton && continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            // Wait until the player presses the button (OnContinueButtonPressed sets timeElapsed to true).
            while (!timeElapsed)
                yield return null;
        }
        else if (activeDuration > 0f)
        {
            // Wait for the specified duration.
            yield return StartCoroutine(WaitThenSetTimeElapsed(activeDuration));
        }
        // 3. Chain to the next node if available.
        if (nextNode != null)
        {
            ActivateNextNode();
        }
        else
        {
            // 4. If this node is the end of a chain (or not in a chain), restore camera and player states.
            if (isLastNodeInChain || !isPartOfChainedSequence)
                yield return StartCoroutine(RestoreStatesAtEndOfSequence());
        }
        // 5. Finally, deactivate this node.
        DeactivateNode();
        isProcessing = false;
    }

    // Waits for a given duration and then marks time as elapsed.
    private IEnumerator WaitThenSetTimeElapsed(float duration)
    {
        yield return new WaitForSeconds(duration);
        timeElapsed = true;
    }

    private IEnumerator ProcessCameraPoints()
    {
        if (mainCamera == null || cameraPoints == null || cameraPoints.Length == 0)
            yield break;
        // Process each camera point in order.
        foreach (var point in cameraPoints)
        {
            if (point.point == null)
                continue;
            float elapsed = 0f;
            Vector3 startPos = mainCamera.transform.position;
            Quaternion startRot = mainCamera.transform.rotation;
            while (elapsed < point.transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = point.transitionCurve.Evaluate(elapsed / point.transitionDuration);
                mainCamera.transform.position = Vector3.Lerp(startPos, point.point.position, t);
                mainCamera.transform.rotation = Quaternion.Lerp(startRot, point.point.rotation, t);
                yield return null;
            }
            // Snap exactly to the target.
            mainCamera.transform.position = point.point.position;
            mainCamera.transform.rotation = point.point.rotation;
        }
        // Return the camera to its original position.
        if (returnCameraToPlayer)
        {
            float returnDuration = 1f;
            float elapsedReturn = 0f;
            Vector3 startPos = mainCamera.transform.position;
            Quaternion startRot = mainCamera.transform.rotation;
            while (elapsedReturn < returnDuration)
            {
                elapsedReturn += Time.deltaTime;
                float t = elapsedReturn / returnDuration;
                mainCamera.transform.position = Vector3.Lerp(startPos, originalCameraWorldPosition, t);
                mainCamera.transform.rotation = Quaternion.Lerp(startRot, originalCameraWorldRotation, t);
                yield return null;
            }
            // Restore parent and local transform if available.
            if (originalCameraParent != null)
            {
                mainCamera.transform.SetParent(originalCameraParent);
                mainCamera.transform.localPosition = originalCameraLocalPosition;
                mainCamera.transform.localRotation = originalCameraLocalRotation;
            }
            else
            {
                mainCamera.transform.position = originalCameraWorldPosition;
                mainCamera.transform.rotation = originalCameraWorldRotation;
            }
        }
    }

    // At the end of a chain, restore states.
    private IEnumerator RestoreStatesAtEndOfSequence()
    {
        DebugLog("Restoring end-of-chain states.");
        RestorePlayerState();
        if (returnCameraToPlayer && mainCamera != null)
        {
            float returnDuration = 1f;
            float elapsedTime = 0f;
            Vector3 startPos = mainCamera.transform.position;
            Quaternion startRot = mainCamera.transform.rotation;
            while (elapsedTime < returnDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / returnDuration;
                mainCamera.transform.position = Vector3.Lerp(startPos, originalCameraWorldPosition, t);
                mainCamera.transform.rotation = Quaternion.Lerp(startRot, originalCameraWorldRotation, t);
                yield return null;
            }
            if (originalCameraParent != null)
            {
                mainCamera.transform.SetParent(originalCameraParent);
                mainCamera.transform.localPosition = originalCameraLocalPosition;
                mainCamera.transform.localRotation = originalCameraLocalRotation;
            }
            else
            {
                mainCamera.transform.position = originalCameraWorldPosition;
                mainCamera.transform.rotation = originalCameraWorldRotation;
            }
        }
        // End the chained sequence.
        isInChainedSequence = false;
        yield return null;
    }

    // Called via the UI button.
    public void OnContinueButtonPressed()
    {
        if (!isActiveAndEnabled || !triggered || !timeElapsed)
        {
            DebugLog("Continue button pressed but state is invalid.");
            return;
        }
        // Hide the button and mark the waiting period complete.
        if (continueButton != null)
            continueButton.gameObject.SetActive(false);
        timeElapsed = true;
    }

    #endregion

    #region Node Deactivation & Chaining

    private void DeactivateNode()
    {
        // Deactivate all story elements.
        foreach (var element in storyElements)
        {
            if (element != null)
                element.SetActive(false);
        }
        // Reset temporarily disabled objects.
        ResetObjects();
        // Always restore player movement.
        RestorePlayerState();
        // If triggerOnce, permanently disable and hide this node.
        if (triggerOnce)
        {
            foreach (var obj in objectsToPermanentlyDisable)
            {
                if (obj != null && !obj.CompareTag("Player") && obj.GetComponent<PlayerController>() == null)
                    Destroy(obj);
            }
            gameObject.SetActive(false);
            DebugLog("Node deactivated and set inactive (triggerOnce).");
        }
        else
        {
            DebugLog("Node deactivated (repeatable).");
        }
        onNodeDeactivate?.Invoke();
        SaveNodeState();
    }

    private void ActivateNextNode()
    {
        if (nextNode != null)
        {
            if (!nextNode.gameObject.activeInHierarchy)
                nextNode.gameObject.SetActive(true);
            nextNode.ActivateNode();
        }
        else
        {
            DebugLog("No next node to activate.");
        }
    }

    private void RestorePlayerState()
    {
        if (playerController != null)
        {
            playerController.canMove = wasPlayerMovementEnabled;
            DebugLog($"Restored player movement to: {wasPlayerMovementEnabled}");
        }
    }

    private void DisableObjects()
    {
        foreach (var obj in objectsToDisable)
            if (obj != null)
                obj.SetActive(false);
        foreach (var obj in objectsToPermanentlyDisable)
            if (obj != null)
                obj.SetActive(false);
    }

    private void ResetObjects()
    {
        foreach (var obj in objectsToDisable)
            if (obj != null)
                obj.SetActive(true);
    }

    #endregion

    #region Save/Load State

    private void SaveNodeState()
    {
        if (devMode)
            return;
        try
        {
            NodeSaveData saveData = new NodeSaveData(nodeId)
            {
                hasBeenTriggered = triggered,
                variables = new List<SerializableKeyValuePair>()
            };
            foreach (var kvp in variables)
            {
                SerializableKeyValuePair pair = new SerializableKeyValuePair();
                pair.key = kvp.Key;
                pair.value = kvp.Value;
                saveData.variables.Add(pair);
            }
            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString($"StoryNode_{nodeId}", json);
            PlayerPrefs.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StoryNode] Error saving node state: {e.Message}");
        }
    }

    private void LoadNodeState()
    {
        if (devMode)
        {
            PlayerPrefs.DeleteKey($"StoryNode_{nodeId}");
            triggered = false;
            variables = new Dictionary<string, float>();
            return;
        }
        try
        {
            string json = PlayerPrefs.GetString($"StoryNode_{nodeId}", "");
            if (!string.IsNullOrEmpty(json))
            {
                NodeSaveData saveData = JsonUtility.FromJson<NodeSaveData>(json);
                if (saveData != null)
                {
                    triggered = saveData.hasBeenTriggered;
                    variables = new Dictionary<string, float>();
                    foreach (var pair in saveData.variables)
                        variables[pair.key] = pair.value;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StoryNode] Error loading node state: {e.Message}");
        }
    }

    #endregion

    #region Variable Methods

    public void SetVariable(string name, float value)
    {
        if (string.IsNullOrEmpty(name))
            return;
        variables[name] = value;
        SaveNodeState();
    }

    public float GetVariable(string name, float defaultValue = 0f)
    {
        if (string.IsNullOrEmpty(name))
            return defaultValue;
        return variables.TryGetValue(name, out float value) ? value : defaultValue;
    }

    #endregion

    #region Context Menu for Clearing Save Data

    [ContextMenu("Clear Save Data")]
    public void ClearSaveData()
    {
        PlayerPrefs.DeleteKey($"StoryNode_{nodeId}");
        triggered = false;
        variables.Clear();
        DebugLog("Save data cleared.");
    }

    #endregion

    #region Public Save Data Getter

    public NodeSaveData GetNodeSaveData()
    {
        NodeSaveData data = new NodeSaveData(nodeId);
        data.hasBeenTriggered = triggered;
        data.variables = new List<SerializableKeyValuePair>();
        foreach (KeyValuePair<string, float> kvp in variables)
        {
            SerializableKeyValuePair pair = new SerializableKeyValuePair();
            pair.key = kvp.Key;
            pair.value = kvp.Value;
            data.variables.Add(pair);
        }
        return data;
    }

    #endregion

    #region Debug Logging

    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[StoryNode - {gameObject.name}] {message}");
    }

    #endregion
}
