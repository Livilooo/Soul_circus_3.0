/*
 * StoryNode.cs
 * Last Modified: 2025-04-15 20:07:29 UTC
 * Modified By: OmniDev951
 * 
 * This script handles story node sequences in Unity, managing camera movements,
 * player interactions, and state persistence with proper chain sequence handling.
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
        SaveNodeState();
        RestorePlayerState();
        CleanupNode();
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
            DebugLog("Node already processing, ignoring activation request.");
            return;
        }

        isProcessing = true;
        try
        {
            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
            }

            // Only store camera position at the start of a new sequence
            if (!isInChainedSequence || !isPartOfChainedSequence)
            {
                StoreOriginalCameraTransform();
                isInChainedSequence = isPartOfChainedSequence;
            }

            if (playerController != null)
            {
                wasPlayerMovementEnabled = playerController.canMove;
                playerController.canMove = allowPlayerMovementDuringNode;
                DebugLog($"Set player movement to: {allowPlayerMovementDuringNode}");
            }

            foreach (var element in storyElements)
            {
                if (element != null)
                {
                    element.SetActive(true);
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
            Debug.LogError($"[StoryNode] Error in ActivateNode: {e.Message}");
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
            continueButton.gameObject.SetActive(true);
            timeElapsed = true;
            while (!timeElapsed)
            {
                yield return null;
            }
        }
        else if (activeDuration > 0f)
        {
            yield return StartCoroutine(DeactivateAfterDuration(activeDuration));
        }

        if (nextNode != null)
        {
            ActivateNextNode();
        }
        else
        {
            // Only restore states if this is the end of the sequence
            if (isLastNodeInChain || !isPartOfChainedSequence)
            {
                yield return StartCoroutine(RestoreStatesAtEndOfSequence());
            }
        }

        DeactivateNode();
        isProcessing = false;
    }

    private IEnumerator ProcessCameraPoints()
    {
        if (mainCamera == null || cameraPoints == null || cameraPoints.Length == 0)
            yield break;

        foreach (var point in cameraPoints)
        {
            if (point.point == null)
                continue;

            Vector3 startPos = mainCamera.transform.position;
            Quaternion startRot = mainCamera.transform.rotation;
            float elapsedTime = 0f;

            while (elapsedTime < point.transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = point.transitionCurve.Evaluate(elapsedTime / point.transitionDuration);

                mainCamera.transform.position = Vector3.Lerp(startPos, point.point.position, t);
                mainCamera.transform.rotation = Quaternion.Lerp(startRot, point.point.rotation, t);

                yield return null;
            }

            mainCamera.transform.position = point.point.position;
            mainCamera.transform.rotation = point.point.rotation;
        }
    }

    private IEnumerator RestoreStatesAtEndOfSequence()
    {
        DebugLog("Restoring states at end of sequence");
        RestorePlayerState();
        if (returnCameraToPlayer && mainCamera != null)
        {
            float returnDuration = 1f;
            Vector3 startPos = mainCamera.transform.position;
            Quaternion startRot = mainCamera.transform.rotation;
            float elapsedTime = 0f;

            while (elapsedTime < returnDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = AnimationCurve.EaseInOut(0, 0, 1, 1).Evaluate(elapsedTime / returnDuration);

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
        isInChainedSequence = false;
    }

    private IEnumerator DeactivateAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        timeElapsed = true;
    }

    #endregion

    #region Node Deactivation & State Management

    private void DeactivateNode()
    {
        foreach (var element in storyElements)
        {
            if (element != null)
            {
                element.SetActive(false);
            }
        }

        if (triggerOnce)
        {
            foreach (var obj in objectsToPermanentlyDisable)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            gameObject.SetActive(false);
        }

        ResetObjects();
        onNodeDeactivate?.Invoke();
        SaveNodeState();
    }

    private void ActivateNextNode()
    {
        if (nextNode != null)
        {
            nextNode.gameObject.SetActive(true);
            nextNode.ActivateNode();
        }
    }

    private void RestorePlayerState()
    {
        if (playerController != null)
        {
            DebugLog($"Restoring player movement from {playerController.canMove} to {wasPlayerMovementEnabled}");
            playerController.canMove = wasPlayerMovementEnabled;
        }
    }

    private void DisableObjects()
    {
        foreach (var obj in objectsToDisable)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
        foreach (var obj in objectsToPermanentlyDisable)
        {
            if (obj != null)
            {
                obj.SetActive(false);
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
            }
        }
    }

    private void OnContinueButtonPressed()
    {
        if (!isActiveAndEnabled || !triggered || !timeElapsed)
            return;

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
        }
        timeElapsed = true;
    }

    #endregion

    #region Save/Load State

    private void SaveNodeState()
    {
        if (devMode)
            return;

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
    }

    private void LoadNodeState()
    {
        if (devMode)
        {
            PlayerPrefs.DeleteKey($"StoryNode_{nodeId}");
            return;
        }

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
            }
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

    #region Debug Methods

    [ContextMenu("Clear Save Data")]
    public void ClearSaveData()
    {
        PlayerPrefs.DeleteKey($"StoryNode_{nodeId}");
        triggered = false;
        variables.Clear();
        DebugLog("Save data cleared");
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