/*
* StoryNode.cs
* Last Modified: 2025-04-18 19:45:30 UTC
* Modified By: OmniDev951
*
* This script handles story node sequences in Unity with fixes for:
* - Fixed movement restoration in chained sequences
* - Fixed temporary and permanent object disabling for chained nodes
* - Improved movement state handling in chains
* - Added null checks for object arrays
* - Added detailed debug logging
* - Fixed chain sequence completion states and ensured player movement restoration
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

#region Data Structures

[Serializable]
public class SerializableKeyValuePair
{
    public string key;
    public float value;
}

[Serializable]
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

[Serializable]
public class BranchingChoice
{
    public string choiceText;
    public StoryNode nextNode;
    public string conditionVariable;
    public float conditionValue;
    public ConditionType conditionType;
    public bool requireCondition;

    public enum ConditionType { Equals, GreaterThan, LessThan }

    public bool EvaluateCondition(float currentValue)
    {
        if (!requireCondition) return true;
        
        return conditionType switch
        {
            ConditionType.Equals => Mathf.Approximately(currentValue, conditionValue),
            ConditionType.GreaterThan => currentValue > conditionValue,
            ConditionType.LessThan => currentValue < conditionValue,
            _ => false
        };
    }
}

[Serializable]
public class CameraPoint
{
    public Transform point;
    public float transitionDuration = 1f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public bool IsValid => point != null && transitionDuration > 0f;
}

#endregion

public class StoryNode : MonoBehaviour
{
    #region Inspector Variables

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool devMode = false;

    [Header("Identification")]
    [SerializeField] private string nodeId = "";

    [Header("Story Elements")]
    [SerializeField] private GameObject[] storyElements;
    [SerializeField] private GameObject[] objectsToDisable;
    [SerializeField] private GameObject[] objectsToPermanentlyDisable;

    [Header("Player Control")]
    [SerializeField] private bool allowPlayerMovementDuringNode = true;
    [SerializeField] private bool restorePlayerMovementOnComplete = true;
    [SerializeField] private float activeDuration = 5f;
    [SerializeField] private bool waitForUIButton = false;
    [SerializeField] private Button continueButton;

    [Header("Camera Settings")]
    [SerializeField] private CameraPoint[] cameraPoints;
    [SerializeField] private bool returnCameraToPlayer = true;
    [SerializeField] private bool returnToFirstNodeCamera = false;

    [Header("Chain Settings")]
    [Tooltip("Set if this node is part of a chain")]
    [SerializeField] private bool isPartOfChainedSequence = false;
    [Tooltip("Set to true if this node is the last in a chain")]
    [SerializeField] private bool isLastNodeInChain = false;
    [SerializeField] private StoryNode nextNode;

    [Header("Node Behavior")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private List<BranchingChoice> choices = new List<BranchingChoice>();

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onNodeActivate;
    public UnityEngine.Events.UnityEvent onNodeDeactivate;

    #endregion

    #region Private Variables

    private static bool isInChainedSequence = false;
    private static Vector3 firstNodeCameraPosition;
    private static Quaternion firstNodeCameraRotation;
    private static Transform firstNodeCameraParent;
    private static Vector3 firstNodeCameraLocalPosition;
    private static Quaternion firstNodeCameraLocalRotation;
    private static bool hasStoredFirstNodeCamera = false;

    private bool isProcessing;
    private bool triggered;
    private bool timeElapsed;
    private bool hasStoredPlayerState;
    
    private PlayerController playerController;
    private bool wasPlayerMovementEnabled;
    
    private Camera mainCamera;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPosition;
    private Vector3 originalCameraWorldPosition;
    private Quaternion originalCameraLocalRotation;
    private Quaternion originalCameraWorldRotation;
    
    private Coroutine activeCoroutine;
    private Dictionary<string, float> variables = new Dictionary<string, float>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeNode();
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTrigger(other)) return;
        
        InitializePlayerController(other);
        ActivateNode();
    }

    private void OnDestroy()
    {
        CleanupNode();
    }

    #endregion

    #region Initialization and Validation

    private void InitializeNode()
    {
        GenerateNodeIdIfNeeded();
        InitializeCamera();
        SetupUI();
        if (!devMode)
        {
            LoadNodeState();
        }
    }

    private void GenerateNodeIdIfNeeded()
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            nodeId = Guid.NewGuid().ToString();
            DebugLog($"Generated new ID for node: {nodeId}");
        }
    }

    private void InitializeCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            StoreOriginalCameraTransform();
        }
    }

    private void SetupUI()
    {
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueButtonPressed);
        }
    }

    private void ValidateSettings()
    {
        if (activeDuration < 0)
        {
            Debug.LogWarning($"[StoryNode] Invalid duration in {gameObject.name}. Setting to 0.");
            activeDuration = 0;
        }

        if (cameraPoints != null)
        {
            foreach (var point in cameraPoints)
            {
                if (point.transitionDuration <= 0)
                {
                    Debug.LogWarning($"[StoryNode] Invalid camera transition duration in {gameObject.name}. Setting to 1.");
                    point.transitionDuration = 1f;
                }
            }
        }
    }

    private bool IsValidTrigger(Collider other)
    {
        return other != null && 
               other.CompareTag("Player") && 
               (!triggered || !triggerOnce);
    }

    private void InitializePlayerController(Collider other)
    {
        playerController = other.GetComponent<PlayerController>();
        if (playerController == null)
        {
            throw new MissingComponentException($"[StoryNode] PlayerController not found on {other.name}");
        }
    }

    #endregion

    #region Camera Management

    private void StoreOriginalCameraTransform()
    {
        if (mainCamera == null) return;

        originalCameraParent = mainCamera.transform.parent;
        originalCameraLocalPosition = mainCamera.transform.localPosition;
        originalCameraWorldPosition = mainCamera.transform.position;
        originalCameraLocalRotation = mainCamera.transform.localRotation;
        originalCameraWorldRotation = mainCamera.transform.rotation;
        DebugLog("Stored original camera transform");
    }

    private IEnumerator ProcessCameraPoints()
    {
        if (mainCamera == null) yield break;

        foreach (var point in cameraPoints)
        {
            if (!point.IsValid) continue;

            yield return StartCoroutine(TransitionCamera(point));
        }

        if (returnCameraToPlayer && (!isInChainedSequence || isLastNodeInChain))
        {
            yield return StartCoroutine(ReturnCameraToOriginalPosition());
        }
    }

    private IEnumerator TransitionCamera(CameraPoint point)
    {
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

    private IEnumerator ReturnCameraToOriginalPosition()
    {
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

        RestoreCameraTransform();
        DebugLog("Camera returned to original position");
    }

    private IEnumerator ReturnCameraToFirstNodePosition()
    {
        DebugLog("Returning camera to first node position");
        
        float returnDuration = 1f;
        float elapsed = 0f;
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            
            mainCamera.transform.position = Vector3.Lerp(startPos, firstNodeCameraPosition, t);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, firstNodeCameraRotation, t);
            
            yield return null;
        }

        if (firstNodeCameraParent != null)
        {
            mainCamera.transform.SetParent(firstNodeCameraParent);
            mainCamera.transform.localPosition = firstNodeCameraLocalPosition;
            mainCamera.transform.localRotation = firstNodeCameraLocalRotation;
        }
        else
        {
            mainCamera.transform.position = firstNodeCameraPosition;
            mainCamera.transform.rotation = firstNodeCameraRotation;
        }
        
        DebugLog("Camera returned to first node position");
    }

    private void RestoreCameraTransform()
    {
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

    #endregion

    #region Node Activation and Processing

    public void ActivateNode()
    {
        if (isProcessing)
        {
            DebugLog("Node already processing; ignoring activation request.");
            return;
        }

        try
        {
            BeginNodeActivation();
            ProcessNodeActivation();
        }
        catch (Exception e)
        {
            HandleActivationError(e);
        }
    }

    private void BeginNodeActivation()
    {
        isProcessing = true;
        timeElapsed = false;

        // Only store states and update movement at the start of a chain or for individual nodes
        if (!isInChainedSequence || !isPartOfChainedSequence)
        {
            StoreOriginalCameraTransform();
            
            if (isPartOfChainedSequence && !hasStoredFirstNodeCamera && mainCamera != null)
            {
                firstNodeCameraPosition = mainCamera.transform.position;
                firstNodeCameraRotation = mainCamera.transform.rotation;
                firstNodeCameraParent = mainCamera.transform.parent;
                firstNodeCameraLocalPosition = mainCamera.transform.localPosition;
                firstNodeCameraLocalRotation = mainCamera.transform.localRotation;
                hasStoredFirstNodeCamera = true;
                DebugLog("Stored first node camera position");
            }
            
            isInChainedSequence = isPartOfChainedSequence;
            StorePlayerState();
        }
        
        // Always update movement state based on current node settings
        if (!allowPlayerMovementDuringNode || isPartOfChainedSequence)
        {
            UpdatePlayerMovement(false);
            DebugLog("Disabled player movement for node or chain");
        }
    }

    private void ProcessNodeActivation()
    {
        // For standalone nodes, enforce movement state if allowed
        if ((!isInChainedSequence || !isPartOfChainedSequence) && allowPlayerMovementDuringNode)
        {
            UpdatePlayerMovement(true);
            DebugLog("Enabled player movement for standalone node");
        }

        ActivateStoryElements();
        DisableObjects();
        triggered = true;
        onNodeActivate?.Invoke();
        SaveNodeState();

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }
        activeCoroutine = StartCoroutine(ProcessNodeSequence());
    }

    private void HandleActivationError(Exception e)
    {
        Debug.LogError($"[StoryNode] Error in ActivateNode: {e.Message}");
        // For chained nodes, do not override stored player state until the final node completes.
        if (!isPartOfChainedSequence)
        {
            RestorePlayerState();
            EnsurePlayerMovementRestored();
        }
        isProcessing = false;
        
        if (isPartOfChainedSequence && isLastNodeInChain)
        {
            isInChainedSequence = false;
            hasStoredPlayerState = false;
        }
    }

    private void ActivateStoryElements()
    {
        if (storyElements != null)
        {
            foreach (var element in storyElements)
            {
                if (element != null)
                {
                    element.SetActive(true);
                }
            }
        }
    }

    private IEnumerator ProcessNodeSequence()
    {
        try
        {
            if (HasValidCameraPoints())
            {
                yield return StartCoroutine(ProcessCameraPoints());
            }

            yield return StartCoroutine(HandleNodeDuration());

            yield return StartCoroutine(ProcessSequenceEnd());

            DeactivateNode();

            // Final safety check for player movement (only for non-chained or final node)
            if (isLastNodeInChain || !isPartOfChainedSequence)
            {
                EnsurePlayerMovementRestored();
            }
        }
        finally
        {
            isProcessing = false;
            
            if (isLastNodeInChain)
            {
                isInChainedSequence = false;
                hasStoredPlayerState = false;
                EnsurePlayerMovementRestored();
                DebugLog("Node sequence completed and cleaned up.");
            }
        }
    }

    private bool HasValidCameraPoints()
    {
        return cameraPoints != null && cameraPoints.Length > 0 && cameraPoints.Any(p => p.IsValid);
    }

    private IEnumerator HandleNodeDuration()
    {
        if (waitForUIButton && continueButton != null)
        {
            yield return StartCoroutine(WaitForButtonPress());
        }
        else if (activeDuration > 0f)
        {
            yield return StartCoroutine(WaitThenSetTimeElapsed(activeDuration));
        }
    }

    private IEnumerator WaitForButtonPress()
    {
        timeElapsed = false;
        continueButton.gameObject.SetActive(true);
        while (!timeElapsed)
        {
            yield return null;
        }
        continueButton.gameObject.SetActive(false);
    }

    // Modified ProcessSequenceEnd to ensure proper restoration
    private IEnumerator ProcessSequenceEnd()
    {
        bool shouldActivateNext = nextNode != null;

        // For standalone nodes or the last node in a chain, restore player movement if configured
        if ((!isPartOfChainedSequence || isLastNodeInChain) && restorePlayerMovementOnComplete)
        {
            RestorePlayerState();
            DebugLog("Restored player movement at sequence end.");
        }

        // Only in non-chained or final chained node, perform end-of-sequence state restoration
        if (isLastNodeInChain || !isPartOfChainedSequence)
        {
            yield return StartCoroutine(RestoreStatesAtEndOfSequence());
            EnsurePlayerMovementRestored();
        }

        if (shouldActivateNext)
        {
            ActivateNextNode();
        }
    }

    #endregion

    #region Node Deactivation

    private void DeactivateNode()
    {
        DeactivateStoryElements();
        // For chained nodes that are not final, do not reset temporarily disabled objects
        if (!isPartOfChainedSequence || isLastNodeInChain)
        {
            ResetObjects();
            RestorePlayerState();
            EnsurePlayerMovementRestored();
        }
        else
        {
            DebugLog("Intermediate chained node deactivated; temporary object states preserved.");
        }

        HandleTriggerOnceCleanup();
        onNodeDeactivate?.Invoke();
        SaveNodeState();

        if (isLastNodeInChain)
        {
            isProcessing = false;
            isInChainedSequence = false;
            EnsurePlayerMovementRestored();
            DebugLog("Last node in chain deactivated.");
        }
    }

    private void DeactivateStoryElements()
    {
        if (storyElements != null)
        {
            foreach (var element in storyElements)
            {
                if (element != null)
                {
                    element.SetActive(false);
                }
            }
        }
    }

    private IEnumerator RestoreStatesAtEndOfSequence()
    {
        DebugLog("Restoring end-of-chain states.");
        
        RestorePlayerState();
        EnsurePlayerMovementRestored();
        
        if (mainCamera != null)
        {
            if (isLastNodeInChain && returnToFirstNodeCamera && hasStoredFirstNodeCamera)
            {
                yield return StartCoroutine(ReturnCameraToFirstNodePosition());
            }
            else if (returnCameraToPlayer)
            {
                yield return StartCoroutine(ReturnCameraToOriginalPosition());
            }
        }

        if (isLastNodeInChain)
        {
            isInChainedSequence = false;
            hasStoredPlayerState = false;
            hasStoredFirstNodeCamera = false;
            EnsurePlayerMovementRestored();
            DebugLog("Chain sequence completed - all states restored.");
        }
        
        yield return null;
    }

    #endregion

    #region Object Management

    private void DisableObjects()
    {
        // First disable temporary objects
        if (objectsToDisable != null)
        {
            foreach (var obj in objectsToDisable)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    DebugLog($"Temporarily disabled object: {obj.name}");
                }
            }
        }

        // Then handle permanent disables
        if (objectsToPermanentlyDisable != null)
        {
            foreach (var obj in objectsToPermanentlyDisable)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    DebugLog($"Permanently disabled object: {obj.name}");
                }
            }
        }
    }

    private void ResetObjects()
    {
        // Re-enable temporarily disabled objects only for standalone nodes or final chained node.
        if (objectsToDisable != null)
        {
            foreach (var obj in objectsToDisable)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    DebugLog($"Re-enabled temporary object: {obj.name}");
                }
            }
        }
    }

    private void HandleTriggerOnceCleanup()
    {
        if (!triggerOnce) return;

        if (objectsToPermanentlyDisable != null)
        {
            foreach (var obj in objectsToPermanentlyDisable)
            {
                if (obj != null && !obj.CompareTag("Player") && obj.GetComponent<PlayerController>() == null)
                {
                    Destroy(obj);
                }
            }
        }
        
        gameObject.SetActive(false);
        DebugLog("Node deactivated and set inactive (triggerOnce).");
    }

    #endregion

    #region Player Movement Management

    private void StorePlayerState()
    {
        if (playerController != null && !hasStoredPlayerState)
        {
            wasPlayerMovementEnabled = playerController.canMove;
            hasStoredPlayerState = true;
            DebugLog($"Stored player movement state: {wasPlayerMovementEnabled}");
        }
    }

    private void UpdatePlayerMovement(bool allowMovement)
    {
        if (playerController != null)
        {
            playerController.canMove = allowMovement;
            DebugLog($"Player movement set to: {allowMovement}");
        }
    }

    private void RestorePlayerState()
    {
        // Only restore if not in an intermediate chain node.
        if (playerController != null && hasStoredPlayerState)
        {
            playerController.canMove = wasPlayerMovementEnabled;
            if (!isPartOfChainedSequence)
            {
                hasStoredPlayerState = false;
            }
            DebugLog($"Restored player movement to: {wasPlayerMovementEnabled}");
        }
    }

    private void EnsurePlayerMovementRestored()
    {
        if (playerController != null)
        {
            if (!playerController.canMove)
            {
                playerController.canMove = true;
                DebugLog("Player movement forcefully restored by safety check");
            }
            hasStoredPlayerState = false;
        }
    }

    #endregion

    #region Save/Load State

    private void SaveNodeState()
    {
        if (devMode) return;

        try
        {
            var saveData = CreateSaveData();
            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString($"StoryNode_{nodeId}", json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[StoryNode] Error saving node state: {e.Message}");
        }
    }

    private NodeSaveData CreateSaveData()
    {
        var saveData = new NodeSaveData(nodeId)
        {
            hasBeenTriggered = triggered,
            variables = variables.Select(kvp => new SerializableKeyValuePair
            {
                key = kvp.Key,
                value = kvp.Value
            }).ToList()
        };
        return saveData;
    }

    private void LoadNodeState()
    {
        if (devMode)
        {
            ResetNodeState();
            return;
        }

        try
        {
            string json = PlayerPrefs.GetString($"StoryNode_{nodeId}", "");
            if (string.IsNullOrEmpty(json)) return;

            var saveData = JsonUtility.FromJson<NodeSaveData>(json);
            if (saveData != null)
            {
                ApplySaveData(saveData);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[StoryNode] Error loading node state: {e.Message}");
        }
    }

    private void ApplySaveData(NodeSaveData saveData)
    {
        triggered = saveData.hasBeenTriggered;
        variables = saveData.variables.ToDictionary(
            pair => pair.key,
            pair => pair.value
        );
    }

    private void ResetNodeState()
    {
        PlayerPrefs.DeleteKey($"StoryNode_{nodeId}");
        triggered = false;
        variables = new Dictionary<string, float>();
    }

    #endregion

    #region Public Methods

    public void OnContinueButtonPressed()
    {
        if (!isActiveAndEnabled || !isProcessing)
        {
            DebugLog("Continue button pressed but state is invalid.");
            return;
        }
        
        timeElapsed = true;
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
        }
    }

    public void SetVariable(string name, float value)
    {
        if (string.IsNullOrEmpty(name)) return;
        
        variables[name] = value;
        SaveNodeState();
    }

    public float GetVariable(string name, float defaultValue = 0f)
    {
        if (string.IsNullOrEmpty(name)) return defaultValue;
        return variables.TryGetValue(name, out float value) ? value : defaultValue;
    }

    public NodeSaveData GetNodeSaveData()
    {
        return CreateSaveData();
    }

    [ContextMenu("Clear Save Data")]
    public void ClearSaveData()
    {
        ResetNodeState();
        DebugLog("Save data cleared.");
    }

    #endregion

    #region Utility Methods

    private void ActivateNextNode()
    {
        if (nextNode == null)
        {
            DebugLog("No next node to activate.");
            return;
        }

        if (!nextNode.gameObject.activeInHierarchy)
        {
            nextNode.gameObject.SetActive(true);
        }
        nextNode.ActivateNode();
    }

    private IEnumerator WaitThenSetTimeElapsed(float duration)
    {
        yield return new WaitForSeconds(duration);
        timeElapsed = true;
    }

    private void CleanupNode()
    {
        SaveNodeState();
        
        RestorePlayerState();
        EnsurePlayerMovementRestored();
        
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueButtonPressed);
        }
        
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[StoryNode - {gameObject.name}] {message}");
        }
    }

    #endregion
}