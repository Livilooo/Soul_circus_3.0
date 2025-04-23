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
        if (!devMode) LoadNodeState(); // Ensure LoadNodeState is called
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
        // Validation logic would go here
    }

    private bool IsValidTrigger(Collider other)
    {
        if (other?.CompareTag("Player") != true) return false;
        if (triggerOnce && triggered) return false;
        if (isProcessing) return false;
        return true;
    }

    private void InitializePlayerController(Collider other)
    {
        if (playerController == null)
            playerController = other.GetComponent<PlayerController>();
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

    #region Camera Management

    private IEnumerator ProcessCameraPoints()
    {
        if (mainCamera == null || cameraPoints == null) yield break;

        foreach (var cp in cameraPoints.Where(cp => cp.IsValid))
        {
            Transform oldParent = mainCamera.transform.parent;
            Vector3 startPos = mainCamera.transform.position;
            Quaternion startRot = mainCamera.transform.rotation;
            Vector3 targetPos = cp.point.position;
            Quaternion targetRot = cp.point.rotation;

            float elapsedTime = 0f;
            while (elapsedTime < cp.transitionDuration)
            {
                float t = cp.transitionCurve.Evaluate(elapsedTime / cp.transitionDuration);
                mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
                mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            mainCamera.transform.position = targetPos;
            mainCamera.transform.rotation = targetRot;
        }
    }

    private IEnumerator ReturnCameraToOriginalPosition()
    {
        if (mainCamera == null) yield break;

        float duration = 1f;
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            mainCamera.transform.position = Vector3.Lerp(startPos, originalCameraWorldPosition, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, originalCameraWorldRotation, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.parent = originalCameraParent;
        mainCamera.transform.localPosition = originalCameraLocalPosition;
        mainCamera.transform.localRotation = originalCameraLocalRotation;
        DebugLog("Camera returned to original position");
    }

    private IEnumerator ReturnCameraToFirstNodePosition()
    {
        if (mainCamera == null) yield break;

        float duration = 1f;
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            mainCamera.transform.position = Vector3.Lerp(startPos, firstNodeCameraPosition, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, firstNodeCameraRotation, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.parent = firstNodeCameraParent;
        mainCamera.transform.localPosition = firstNodeCameraLocalPosition;
        mainCamera.transform.localRotation = firstNodeCameraLocalRotation;
        DebugLog("Camera returned to first node position");
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
            UpdatePlayerMovement(false); // Disable movement at the start of the node [T7](2)
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
        //SaveNodeState();  //Save node state to save progress
        activeCoroutine = StartCoroutine(ProcessNodeSequence());
    }

    private void HandleActivationError(Exception e)
    {
        Debug.LogError($"[StoryNode] Activation error: {e.Message}");

        if (!isPartOfChainedSequence || isLastNodeInChain)
            RestorePlayerState();

        isProcessing = false;

        // Reset chain-related states if needed
        if (isLastNodeInChain)
        {
            isInChainedSequence = false;
            hasStoredFirstNodeCamera = false;
        }
    }

    private IEnumerator ProcessNodeSequence()
    {
        try
        {
            // 1) Process camera sequence
            if (cameraPoints != null && cameraPoints.Any(cp => cp.IsValid))
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
                if (playerController != null)
                {
                    playerController.RestoreMovement(); // Call the function to restore movement [T7](2)
                }
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

    // Placeholder for SaveNodeState.  Implement based on your save system.
    private void SaveNodeState()
    {
        // Implement saving of node state here.  Example:
        // NodeSaveData data = new NodeSaveData(nodeId);
        // data.hasBeenTriggered = triggered;
        // //... save variables, etc.
        // SaveSystem.Save(data);
    }

    // Placeholder for LoadNodeState.  Implement based on your save system.
    private void LoadNodeState()
    {
        if (devMode)
        {
            // If in devMode, clear the save data.  This is useful for testing.
            PlayerPrefs.DeleteKey($"StoryNode_{nodeId}");
            triggered = false;
            variables.Clear();
            return;
        }

        try
        {
            // Retrieve the saved JSON string from PlayerPrefs.
            string json = PlayerPrefs.GetString($"StoryNode_{nodeId}", "");

            // If no data was found, return.
            if (string.IsNullOrEmpty(json)) return;

            // Deserialize the JSON string back into a NodeSaveData object.
            var data = JsonUtility.FromJson<NodeSaveData>(json);

            // Apply the loaded data to the current node.
            triggered = data.hasBeenTriggered;
            variables = data.variables.ToDictionary(p => p.key, p => p.value);
        }
        catch (Exception e)
        {
            // Log any errors that occur during the loading process.
            Debug.LogError($"[StoryNode] Error loading state: {e.Message}");
        }
    }

    // Helper to safely write Debug.Log messages.
    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[StoryNode - {nodeId}] {message}");
        }
    }
}
