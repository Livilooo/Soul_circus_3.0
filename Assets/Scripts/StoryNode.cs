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
    public bool isPartOfChainedSequence = false;
    public bool isLastNodeInChain = false;
    private static bool isInChainedSequence = false;

    [Header("Chained Story Node (Optional)")]
    public StoryNode nextNode;

    [Header("General Node Settings")]
    public bool triggerOnce = true;
    public float activeDuration = 5f;
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

    private bool triggered = false;
    private Coroutine activeCoroutine;
    private bool timeElapsed = false;
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
        if (string.IsNullOrEmpty(nodeId))
        {
            nodeId = System.Guid.NewGuid().ToString();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
        if (nextNode != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, nextNode.transform.position);
        }
        if (cameraPoints != null && cameraPoints.Length > 0)
        {
            Gizmos.color = Color.blue;
            foreach (var point in cameraPoints)
            {
                if (point.point != null)
                {
                    Gizmos.DrawWireSphere(point.point.position, 0.5f);
                    Gizmos.DrawLine(transform.position, point.point.position);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player"))
            return;
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
        try
        {
            SaveNodeState();
            RestorePlayerState();
            CleanupNode();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StoryNode] Error during cleanup: {e.Message}");
        }
    }

    #endregion

    #region Node Initialization and Cleanup

    private void InitializeNode()
    {
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
            continueButton.onClick.AddListener(OnContinueButtonPressed);
        }
        if (!devMode)
        {
            LoadNodeState();
        }
        DebugLog($"Initialized node: {gameObject.name}");
    }

    private void CleanupNode()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueButtonPressed);
        }
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }
        ResetCamera();
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
            DebugLog($"Stored camera transforms - Local Pos: {originalCameraLocalPosition}, World Pos: {originalCameraWorldPosition}");
        }
    }

    #endregion

    #region Node Activation & Processing

    public void ActivateNode()
    {
        if (isProcessing)
        {
            DebugLog("Node already processing, ignoring activation request.");
            return;
        }
        isProcessing = true;
        try
        {
            DebugLog($"Activating node: {gameObject.name}");
            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
            }

            // Only store camera transform at the start of a new chain
            if (!isInChainedSequence || !isPartOfChainedSequence)
            {
                StoreOriginalCameraTransform();
            }

            if (playerController != null)
            {
                wasPlayerMovementEnabled = playerController.canMove;
                playerController.canMove = allowPlayerMovementDuringNode;
                DebugLog($"Player movement set to: {allowPlayerMovementDuringNode}");
            }

            foreach (var element in storyElements)
            {
                if (element != null)
                {
                    element.SetActive(true);
                    DebugLog($"Activated story element: {element.name}");
                }
            }
            DisableObjects();
            triggered = true;
            timeElapsed = false;
            onNodeActivate?.Invoke();
            SaveNodeState();
            activeCoroutine = StartCoroutine(ProcessNodeSequence());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StoryNode] Error in ActivateNode: {e.Message}\n{e.StackTrace}");
            RestorePlayerState();
            isProcessing = false;
        }
    }

    private IEnumerator ProcessNodeSequence()
    {
        if (cameraPoints != null && cameraPoints.Length > 0)
        {
            yield return StartCoroutine(ProcessCameraPoints());
        }
        if (waitForUIButton && continueButton != null)
        {
            if (activeDuration > 0f)
            {
                yield return StartCoroutine(DeactivateAfterDuration(activeDuration));
            }
            else
            {
                continueButton.gameObject.SetActive(true);
                timeElapsed = true;
            }
        }
        else if (activeDuration > 0f)
        {
            yield return StartCoroutine(DeactivateAfterDuration(activeDuration));
        }
        else
        {
            ActivateNextNode();
            DeactivateNode();
        }
        isProcessing = false;
    }

    private IEnumerator ProcessCameraPoints()
    {
        if (mainCamera == null || cameraPoints == null || cameraPoints.Length == 0)
            yield break;

        if (!isInChainedSequence)
        {
            isInChainedSequence = true;
            DebugLog("Starting new camera chain sequence");
        }

        foreach (var point in cameraPoints)
        {
            if (point.point == null)
                continue;

            DebugLog($"Moving camera to point: {point.point.name}");
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

            mainCamera.transform.position = point.point.position;
            mainCamera.transform.rotation = point.point.rotation;
        }

        if (returnCameraToPlayer && (isLastNodeInChain || !isPartOfChainedSequence))
        {
            DebugLog($"Returning camera to original position. IsLastNode: {isLastNodeInChain}");
            yield return StartCoroutine(ReturnCameraToOriginal());
        }
    }

    private IEnumerator ReturnCameraToOriginal()
    {
        if (mainCamera == null) yield break;

        float returnDuration = 1f;
        float elapsed = 0f;
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
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

        isInChainedSequence = false;
        DebugLog("Camera returned to original position");
    }

    private IEnumerator DeactivateAfterDuration(float duration)
    {
        DebugLog($"Waiting for {duration} seconds");
        yield return new WaitForSeconds(duration);
        timeElapsed = true;
        if (waitForUIButton && continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            DebugLog("Continue button enabled after duration");
        }
        else
        {
            ActivateNextNode();
            DeactivateNode();
        }
    }

    public void OnContinueButtonPressed()
    {
        if (!isActiveAndEnabled || !triggered || !timeElapsed)
        {
            DebugLog($"Invalid continue button press state - Active: {isActiveAndEnabled}, Triggered: {triggered}, TimeElapsed: {timeElapsed}");
            return;
        }
        DebugLog("Continue button pressed!");
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
        }
        ActivateNextNode();
        DeactivateNode();
    }

    #endregion

    #region Node Deactivation & Chaining

    private void DeactivateNode()
    {
        try
        {
            DebugLog($"Deactivating node: {gameObject.name}");
            foreach (var element in storyElements)
            {
                if (element != null)
                    element.SetActive(false);
            }
            ResetObjects();
            
            if (isLastNodeInChain || !isPartOfChainedSequence)
            {
                RestorePlayerState();
                if (returnCameraToPlayer && !isProcessing)
                {
                    StartCoroutine(ReturnCameraToOriginal());
                }
            }

            if (triggerOnce)
            {
                foreach (var obj in objectsToPermanentlyDisable)
                {
                    if (obj != null && !obj.CompareTag("Player") && 
                        obj.GetComponent<PlayerController>() == null)
                    {
                        Destroy(obj);
                        DebugLog($"Destroyed permanent object: {obj.name}");
                    }
                }
            }

            SaveNodeState();
            onNodeDeactivate?.Invoke();

            if (triggerOnce)
            {
                gameObject.SetActive(false);
                DebugLog("Node deactivated and set inactive (triggerOnce).");
            }
            else
            {
                DebugLog("Node deactivated (repeatable).");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StoryNode] Error in DeactivateNode: {e.Message}");
            RestorePlayerState();
        }
    }

    private void ActivateNextNode()
    {
        if (nextNode != null)
        {
            DebugLog($"Activating chained node: {nextNode.gameObject.name}");
            nextNode.gameObject.SetActive(true);
            nextNode.ActivateNode();
        }
        else
        {
            DebugLog("No next node to activate");
        }
    }

    #endregion

    #region Object & Camera Helpers

    private void DisableObjects()
    {
        foreach (var obj in objectsToDisable)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                DebugLog($"Temporarily disabled: {obj.name}");
            }
        }
        foreach (var obj in objectsToPermanentlyDisable)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                DebugLog($"Permanently disabled: {obj.name}");
            }
        }
    }

    private void ResetObjects()
    {
        foreach (var obj in objectsToDisable)
        {
            if (obj != null)
            {
                obj.SetActive(true);
                DebugLog($"Reset object: {obj.name}");
            }
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

    private void ResetCamera()
    {
        if (mainCamera != null)
        {
            StopAllCoroutines();
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
            DebugLog("Camera reset to original position");
        }
    }

    public void ForceReset()
    {
        try
        {
            StopAllCoroutines();
            triggered = false;
            timeElapsed = false;
            variables.Clear();
            RestorePlayerState();
            ResetObjects();
            ResetCamera();
            isInChainedSequence = false;
            SaveNodeState();
            DebugLog("Force reset completed");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StoryNode] Error in ForceReset: {e.Message}");
        }
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Debug Chain Status")]
    public void DebugChainStatus()
    {
        DebugLog($"Chain Setup for {gameObject.name}:");
        DebugLog($"Is Part of Chain: {isPartOfChainedSequence}");
        DebugLog($"Is Last in Chain: {isLastNodeInChain}");
        DebugLog($"Return Camera to Player: {returnCameraToPlayer}");
        DebugLog($"Has Next Node: {nextNode != null}");
        if (mainCamera != null)
        {
            DebugLog($"Current Camera Position: {mainCamera.transform.position}");
            DebugLog($"Original Local Position: {originalCameraLocalPosition}");
            DebugLog($"Original World Position: {originalCameraWorldPosition}");
        }
    }

    #endregion

    #region Save/Load Node State

    private void SaveNodeState()
    {
        if (devMode)
        {
            DebugLog("Dev mode enabled - skipping save.");
            return;
        }
        try
        {
            NodeSaveData saveData = new NodeSaveData(nodeId)
            {
                hasBeenTriggered = triggered,
                variables = new List<SerializableKeyValuePair>()
            };
            foreach (var kvp in variables)
            {
                saveData.variables.Add(new SerializableKeyValuePair { key = kvp.Key, value = kvp.Value });
            }
            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString($"StoryNode_{nodeId}", json);
            PlayerPrefs.Save();
            DebugLog("Node state saved");
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
            DebugLog("Dev mode enabled - skipping load and clearing saved state.");
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
                    foreach (var kvp in saveData.variables)
                    {
                        variables[kvp.key] = kvp.value;
                    }
                    DebugLog("Node state loaded");
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
        {
            Debug.LogError("[StoryNode] Attempted to set variable with null or empty name");
            return;
        }
        variables[name] = value;
        SaveNodeState();
        DebugLog($"Set variable {name} to {value}");
    }

    public float GetVariable(string name, float defaultValue = 0f)
    {
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError("[StoryNode] Attempted to get variable with null or empty name");
            return defaultValue;
        }
        return variables.TryGetValue(name, out float value) ? value : defaultValue;
    }

    #endregion

    #region Context Menu Methods

    [ContextMenu("Clear Save Data")]
    public void ClearSaveData()
    {
        PlayerPrefs.DeleteKey($"StoryNode_{nodeId}");
        triggered = false;
        variables = new Dictionary<string, float>();
        DebugLog("Save data cleared.");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[StoryNode - {gameObject.name}] {message}");
        }
    }
}