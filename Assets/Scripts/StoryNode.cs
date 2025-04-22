/*
* StoryNode.cs
* Last Modified: 2025-04-21 20:07:59 UTC
* Modified By: OmniDev951 / Debugged by Assistant
*
* Debug Fix: Movement restoration at end of node sequence
* - Explicit movement restoration in BeginNodeActivation
* - Corrected movement restoration timing in ProcessNodeSequence
* - Redundant movement checks throughout execution
* - Comprehensive debug logging for movement and camera states
* - Enhanced cleanup sequence for movement and chaining states
* - Fixed missing camera return to player at sequence end
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private bool isPartOfChainedSequence = false;
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
    private static bool hasStoredFirstNodeCamera = false;
    private static Vector3 firstNodeCameraPosition;
    private static Quaternion firstNodeCameraRotation;
    private static Transform firstNodeCameraParent;
    private static Vector3 firstNodeCameraLocalPosition;
    private static Quaternion firstNodeCameraLocalRotation;

    private bool isProcessing;
    private bool triggered;
    private bool timeElapsed;
    private bool hasStoredPlayerState;
    private bool hasRestoredMovement;

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
        if (!devMode) LoadNodeState();
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
        if (mainCamera != null) StoreOriginalCameraTransform();
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
            foreach (var p in cameraPoints)
            {
                if (p.transitionDuration <= 0f)
                {
                    Debug.LogWarning($"[StoryNode] Invalid camera duration in {gameObject.name}. Setting to 1.");
                    p.transitionDuration = 1f;
                }
            }
        }
    }

    private bool IsValidTrigger(Collider other)
    {
        return other.CompareTag("Player") && (!triggered || !triggerOnce);
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
        originalCameraParent = mainCamera.transform.parent;
        originalCameraLocalPosition = mainCamera.transform.localPosition;
        originalCameraWorldPosition = mainCamera.transform.position;
        originalCameraLocalRotation = mainCamera.transform.localRotation;
        originalCameraWorldRotation = mainCamera.transform.rotation;
        DebugLog("Stored original camera transform");
    }

    private IEnumerator ProcessCameraPoints()
    {
        foreach (var cp in cameraPoints.Where(cp => cp.IsValid))
            yield return StartCoroutine(TransitionCamera(cp));
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
        yield return ReturnCameraRoutine(originalCameraWorldPosition, originalCameraWorldRotation,
            originalCameraParent, originalCameraLocalPosition, originalCameraLocalRotation);
        DebugLog("Camera returned to original position");
    }

    private IEnumerator ReturnCameraToFirstNodePosition()
    {
        yield return ReturnCameraRoutine(firstNodeCameraPosition, firstNodeCameraRotation,
            firstNodeCameraParent, firstNodeCameraLocalPosition, firstNodeCameraLocalRotation);
        DebugLog("Camera returned to first node position");
    }

    private IEnumerator ReturnCameraRoutine(Vector3 targetPos, Quaternion targetRot,
        Transform parent, Vector3 localPos, Quaternion localRot)
    {
        float duration = 1f, elapsed = 0f;
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }

        if (parent != null)
        {
            mainCamera.transform.SetParent(parent);
            mainCamera.transform.localPosition = localPos;
            mainCamera.transform.localRotation = localRot;
        }
        else
        {
            mainCamera.transform.position = targetPos;
            mainCamera.transform.rotation = targetRot;
        }
    }
    #endregion

    #region Story Elements Helpers
    private void ActivateStoryElements()
    {
        if (storyElements == null) return;
        foreach (var element in storyElements)
        {
            if (element != null) element.SetActive(true);
        }
        DebugLog("Story elements activated");
    }
    #endregion

    #region Activation & Processing
    public void ActivateNode()
    {
        if (isProcessing)
        {
            DebugLog("Node already processing; ignoring activation.");
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
        hasRestoredMovement = false;

        if (!isInChainedSequence || !isPartOfChainedSequence)
        {
            StoreOriginalCameraTransform();
            if (isPartOfChainedSequence && !hasStoredFirstNodeCamera)
            {
                firstNodeCameraPosition = originalCameraWorldPosition;
                firstNodeCameraRotation = originalCameraWorldRotation;
                firstNodeCameraParent = originalCameraParent;
                firstNodeCameraLocalPosition = originalCameraLocalPosition;
                firstNodeCameraLocalRotation = originalCameraLocalRotation;
                hasStoredFirstNodeCamera = true;
                DebugLog("Stored first node camera state");
            }
            isInChainedSequence = isPartOfChainedSequence;
            StorePlayerState();
        }

        if (!allowPlayerMovementDuringNode || isPartOfChainedSequence)
        {
            UpdatePlayerMovement(false);
            DebugLog("Player movement disabled for node/chain");
        }
    }

    private void ProcessNodeActivation()
    {
        if ((!isInChainedSequence || !isPartOfChainedSequence) && allowPlayerMovementDuringNode)
        {
            UpdatePlayerMovement(true);
            DebugLog("Player movement enabled for standalone node");
        }

        ActivateStoryElements();
        DisableObjects();
        triggered = true;
        onNodeActivate?.Invoke();
        SaveNodeState();

        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(ProcessNodeSequence());
    }

    private void HandleActivationError(Exception e)
    {
        Debug.LogError($"[StoryNode] Activation error: {e}");
        if (!isPartOfChainedSequence || isLastNodeInChain) RestorePlayerState();
        isProcessing = false;
        if (isPartOfChainedSequence && isLastNodeInChain)
        {
            isInChainedSequence = false;
            hasStoredFirstNodeCamera = false;
            hasStoredPlayerState = false;
            hasRestoredMovement = false;
        }
    }

    private IEnumerator ProcessNodeSequence()
    {
        try
        {
            // 1) Camera transitions
            if (cameraPoints.Any(cp => cp.IsValid))
                yield return StartCoroutine(ProcessCameraPoints());

            // 2) Wait for input or duration
            if (waitForUIButton && continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
                while (!timeElapsed) yield return null;
                continueButton.gameObject.SetActive(false);
            }
            else if (activeDuration > 0f)
            {
                yield return new WaitForSeconds(activeDuration);
                timeElapsed = true;
            }

            // 3) Return camera if needed
            if (returnCameraToPlayer && (!isInChainedSequence || isLastNodeInChain))
            {
                if (returnToFirstNodeCamera && hasStoredFirstNodeCamera)
                    yield return StartCoroutine(ReturnCameraToFirstNodePosition());
                else
                    yield return StartCoroutine(ReturnCameraToOriginalPosition());
            }

            // 4) Restore movement if needed
            if ((!isPartOfChainedSequence || isLastNodeInChain)
                && restorePlayerMovementOnComplete && !hasRestoredMovement)
            {
                RestorePlayerState();
                DebugLog("Player movement restored post-wait");
            }

            // 5) Deactivate this node
            DeactivateNode();

            // 6) Chain next node
            ActivateNextNode();
        }
        finally
        {
            EnsureMovementRestoration();
            isProcessing = false;
            if (isLastNodeInChain)
            {
                isInChainedSequence = false;
                hasStoredFirstNodeCamera = false;
                hasStoredPlayerState = false;
                hasRestoredMovement = false;
                DebugLog("Chained sequence completed");
            }
        }
    }
    #endregion

    #region Player Movement Management
    private void StorePlayerState()
    {
        if (playerController != null && !hasStoredPlayerState)
        {
            wasPlayerMovementEnabled = playerController.canMove;
            hasStoredPlayerState = true;
            hasRestoredMovement = false;
            DebugLog($"Stored player movement: {wasPlayerMovementEnabled}");
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
        if (playerController != null && hasStoredPlayerState && !hasRestoredMovement)
        {
            playerController.canMove = wasPlayerMovementEnabled;
            hasRestoredMovement = true;
            DebugLog($"Restored player movement to: {wasPlayerMovementEnabled}");
        }
        hasStoredPlayerState = false;
    }

    private void EnsureMovementRestoration()
    {
        if (restorePlayerMovementOnComplete && !hasRestoredMovement && playerController != null)
        {
            playerController.canMove = true;
            hasRestoredMovement = true;
            DebugLog("Forced final movement restoration");
        }
    }
    #endregion

    #region Deactivation & Chaining
    private void DeactivateNode()
    {
        EnsureMovementRestoration();
        DeactivateStoryElements();
        ResetObjects();

        if (triggerOnce)
        {
            foreach (var obj in objectsToPermanentlyDisable)
            {
                if (obj != null && !obj.CompareTag("Player") && obj.GetComponent<PlayerController>() == null)
                    Destroy(obj);
            }
            gameObject.SetActive(false);
            DebugLog("Node deactivated and disabled (triggerOnce)");
        }
        else
        {
            DebugLog("Node deactivated (repeatable)");
        }

        onNodeDeactivate?.Invoke();
        SaveNodeState();
    }

    private void ActivateNextNode()
    {
        if (nextNode == null) return;
        if (!nextNode.gameObject.activeInHierarchy)
            nextNode.gameObject.SetActive(true);
        nextNode.ActivateNode();
    }

    private void DeactivateStoryElements()
    {
        foreach (var element in storyElements)
        {
            if (element != null)
                element.SetActive(false);
        }
    }
    #endregion

    #region Object Management
    private void DisableObjects()
    {
        foreach (var obj in objectsToDisable) obj?.SetActive(false);
        foreach (var obj in objectsToPermanentlyDisable) obj?.SetActive(false);
    }

    private void ResetObjects()
    {
        foreach (var obj in objectsToDisable) obj?.SetActive(true);
    }
    #endregion

    #region Cleanup
    private void CleanupNode()
    {
        SaveNodeState();
        EnsureMovementRestoration();
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueButtonPressed);
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);
    }
    #endregion

    #region Button Handling
    public void OnContinueButtonPressed()
    {
        if (!isActiveAndEnabled || !isProcessing) return;
        timeElapsed = true;
    }
    #endregion

    #region Save/Load State
    private void SaveNodeState()
    {
        if (devMode) return;
        try
        {
            var data = new NodeSaveData(nodeId)
            {
                hasBeenTriggered = triggered,
                variables = variables.Select(kv => new SerializableKeyValuePair { key = kv.Key, value = kv.Value }).ToList()
            };
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString($"StoryNode_{nodeId}", json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[StoryNode] Error saving state: {e.Message}");
        }
    }

    private void LoadNodeState()
    {
        if (devMode)
        {
            PlayerPrefs.DeleteKey($"StoryNode_{nodeId}");
            triggered = false;
            variables.Clear();
            return;
        }
        try
        {
            string json = PlayerPrefs.GetString($"StoryNode_{nodeId}", "");
            if (string.IsNullOrEmpty(json)) return;
            var data = JsonUtility.FromJson<NodeSaveData>(json);
            triggered = data.hasBeenTriggered;
            variables = data.variables.ToDictionary(p => p.key, p => p.value);
        }
        catch (Exception e)
        {
            Debug.LogError($"[StoryNode] Error loading state: {e.Message}");
        }
    }
    #endregion

    #region Public API
    public void SetVariable(string name, float value)
    {
        if (string.IsNullOrEmpty(name)) return;
        variables[name] = value;
        SaveNodeState();
    }

    public float GetVariable(string name, float defaultValue = 0f)
    {
        return variables.TryGetValue(name, out var val) ? val : defaultValue;
    }

    public NodeSaveData GetNodeSaveData()
    {
        var data = new NodeSaveData(nodeId)
        {
            hasBeenTriggered = triggered,
            variables = variables.Select(kv => new SerializableKeyValuePair { key = kv.Key, value = kv.Value }).ToList()
        };
        return data;
    }

    [ContextMenu("Clear Save Data")]
    public void ClearSaveData()
    {
        PlayerPrefs.DeleteKey($"StoryNode_{nodeId}");
        triggered = false;
        variables.Clear();
        DebugLog("Save data cleared.");
    }
    #endregion

    #region Utility
    private void DebugLog(string message)
    {
        if (showDebugLogs) Debug.Log($"[StoryNode - {gameObject.name}] {message}");
    }
    #endregion
}
