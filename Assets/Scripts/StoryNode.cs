/*
 * UltimateStoryNode.cs
 * Created by: OmniDev951
 * Created on: 2025-04-11 19:43:06 UTC
 * 
 * A comprehensive story node system for Unity games
 * Handles dialogue, sequences, animations, and story progression
 * Requires: TextMeshPro, Unity 2022.3 or newer
 * Optional: Cinemachine
 */

// Check for required packages
#if UNITY_2022_3_OR_NEWER
#define SUPPORTS_TMP
#endif

#if ENABLE_CINEMACHINE
#define CINEMACHINE_PRESENT
#endif

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
#if SUPPORTS_TMP
using TMPro;
#endif
#if CINEMACHINE_PRESENT
using Cinemachine;
#endif

[AddComponentMenu("Story System/Ultimate Story Node")]
public class UltimateStoryNode : MonoBehaviour
{
    #region Serializable Classes
    [System.Serializable]
    public class StorySequence
    {
        public string sequenceName;
        public GameObject[] elementsToShow;
        public float duration;
        public bool waitForInput;
        public AnimationPreset animationPreset;
        public AudioEvent audioEvent;
        public UnityEvent onSequenceStart;
        public UnityEvent onSequenceEnd;
    }

    [System.Serializable]
    public class AnimationPreset
    {
        public enum AnimationType
        {
            None, Fade, Slide, Scale, Rotate, Custom
        }

        public AnimationType type = AnimationType.Fade;
        public float duration = 1f;
        public AnimationCurve curve = new AnimationCurve(
            new Keyframe(0, 0, 0, 1),
            new Keyframe(1, 1, 1, 0)
        );
        public Vector3 startPosition;
        public Vector3 endPosition;
        public Vector3 rotation;
        public bool useCustomPath;
        public Transform[] customPathPoints;
    }

    [System.Serializable]
    public class AudioEvent
    {
        public AudioClip[] clips;
        public bool randomize;
        public float volume = 1f;
        public float pitch = 1f;
        public bool fadeIn;
        public bool fadeOut;
        public float fadeTime = 1f;
        public bool loop;
        public AudioMixerGroup mixerGroup;
    }

    [System.Serializable]
    public class DialogueSettings
    {
#if SUPPORTS_TMP
        public string[] dialogueLines;
        public float typingSpeed = 0.05f;
        public AudioClip typingSound;
        public Color textColor = Color.white;
        public TMP_FontAsset font;
#else
        public string[] dialogueLines;
        public float typingSpeed = 0.05f;
        public AudioClip typingSound;
        public Color textColor = Color.white;
        public Font font;
#endif
        public float autoAdvanceDelay = 2f;
        public bool waitForInput;
        public KeyCode[] advanceKeys = { KeyCode.Space, KeyCode.Return };
        public string[] characterNames;
        public Sprite[] characterPortraits;
    }

    [System.Serializable]
    public class CameraSettings
    {
#if CINEMACHINE_PRESENT
        public bool useCinemachine = true;
        public CinemachineVirtualCamera virtualCamera;
        public Transform lookTarget;
        public float blendTime = 1f;
        public CinemachineBlendDefinition.Style blendStyle;
#else
        public bool useCinemachine = false;
        public Transform virtualCamera;
        public Transform lookTarget;
        public float blendTime = 1f;
#endif
        public bool returnToPlayerAfter = true;
        public Vector3 cameraOffset;
        public bool enableDutchAngle;
        public float dutchAngle;
        public bool enableShake;
        public float shakeIntensity;
        public float shakeDuration;
    }
    [System.Serializable]
    public class BranchingChoice
    {
        public string choiceText;
        public UltimateStoryNode targetNode;
        public bool requireCondition;
        public string conditionVariable;
        public enum ConditionType { Equals, GreaterThan, LessThan }
        public ConditionType conditionType;
        public float conditionValue;
        public UnityEvent onChoiceMade;
    }

    [System.Serializable]
    public class EnvironmentEffect
    {
        public enum EffectType
        {
            Weather, TimeOfDay, Particles, PostProcessing, Lightning, Custom
        }

        public EffectType type;
        public GameObject effectPrefab;
        public float duration;
        public float intensity;
        public bool fadeIn;
        public bool fadeOut;
        public Color colorTint;
        public AnimationCurve intensityCurve = new AnimationCurve(
            new Keyframe(0, 0),
            new Keyframe(1, 1)
        );
    }
    #endregion

    #region Main Settings
    [Header("Story Node Identity")]
    public string nodeID;
    public string nodeName;
    [TextArea(3, 10)]
    public string nodeDescription;
    public bool startOnAwake;
    public bool triggerOnce = true;
    public bool saveProgress = true;
    public UltimateStoryNode nextNode;

    [Header("Sequences")]
    public List<StorySequence> sequences = new List<StorySequence>();
    public bool playSequentially = true;
    public bool canSkipSequences;
    public KeyCode skipKey = KeyCode.Escape;

    [Header("Dialogue System")]
    public DialogueSettings dialogueSettings;
    public bool useDialogueSystem = true;
    public GameObject dialogueUI;
#if SUPPORTS_TMP
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI nameText;
#else
    public Text dialogueText;
    public Text nameText;
#endif
    public Image characterPortrait;

    [Header("Camera Control")]
    public CameraSettings cameraSettings;
    public bool useMultipleCameraPoints;
    public Transform[] cameraPoints;
    public float[] cameraDurations;

    [Header("Branching")]
    public List<BranchingChoice> choices = new List<BranchingChoice>();
    public bool showChoicesImmediately;
    public float choiceTimeout = -1f;
    public UnityEvent onChoiceTimeout;

    [Header("Environment")]
    public List<EnvironmentEffect> environmentEffects = new List<EnvironmentEffect>();
    public bool restoreEnvironmentAfter = true;

    [Header("Player Control")]
    public bool freezePlayer;
    public bool hidePlayer;
    public bool disablePlayerInput;
    public Transform playerPositionOverride;
    public bool smoothPlayerMovement;
    public float playerMovementDuration = 1f;

    [Header("Quest Integration")]
    public string[] questsToStart;
    public string[] questsToComplete;
    public string[] objectivesToUpdate;
    public bool trackQuestProgress;

    [Header("Item Management")]
    public GameObject[] itemsToGive;
    public GameObject[] itemsToRemove;
    public bool checkInventoryConditions;
    public string[] requiredItems;

    [Header("Save System")]
    public bool createCheckpoint;
    public string checkpointID;
    public bool persistNodeState;
    public string[] variablesToSave;
    #endregion

    #region Events
    public UnityEvent onNodeStart;
    public UnityEvent onNodeComplete;
    public UnityEvent<string> onVariableChanged;
    public UnityEvent<GameObject> onItemCollected;
    public UnityEvent<string> onQuestUpdated;
    public UnityEvent onPlayerInteract;
    public UnityEvent<BranchingChoice> onChoiceMade;
    public UnityEvent onSequenceComplete;
    public UnityEvent onDialogueComplete;
    public UnityEvent onCameraTransitionComplete;
    #endregion

    #region Runtime Variables
    private bool isActive;
    private bool isProcessing;
    private bool isInitialized;
    private bool isExecuting;
    private bool triggered;
    private int currentSequenceIndex;
    private Coroutine currentSequenceCoroutine;
    private Dictionary<string, float> variables = new Dictionary<string, float>();
    private Stack<StorySequence> sequenceHistory = new Stack<StorySequence>();
    private AudioSource[] audioSources;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private Vector3 originalPlayerPosition;
    private Quaternion originalPlayerRotation;
    private Dictionary<GameObject, bool> originalObjectStates = new Dictionary<GameObject, bool>();

    public enum NodeState
    {
        Inactive,
        Initializing,
        Active,
        Processing,
        Completing,
        Completed
    }
    private NodeState currentState = NodeState.Inactive;

    // Package support flags
#if CINEMACHINE_PRESENT
    private bool hasCinemachine = true;
#else
    private bool hasCinemachine = false;
#endif

#if SUPPORTS_TMP
    private bool hasTMP = true;
#else
    private bool hasTMP = false;
#endif
    #endregion


    #region Core Methods
    private void Awake()
    {
        ValidateRequirements();
        ValidateReferences();
        InitializeNode();
        if (startOnAwake) StartCoroutine(StartNodeDelayed());
    }

    private void ValidateRequirements()
    {
        if (!hasTMP)
        {
            LogDebugInfo("TextMeshPro is not available. Text functionality will be limited.", LogType.Warning);
        }

        if (!hasCinemachine && cameraSettings.useCinemachine)
        {
            LogDebugInfo("Cinemachine is not available. Advanced camera features will be disabled.", LogType.Warning);
            cameraSettings.useCinemachine = false;
        }
    }

    private void ValidateReferences()
    {
#if CINEMACHINE_PRESENT
        if (cameraSettings.useCinemachine && cameraSettings.virtualCamera == null)
        {
            cameraSettings.virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
        }
#endif

#if SUPPORTS_TMP
        if (dialogueText == null)
        {
            dialogueText = GetComponentInChildren<TextMeshProUGUI>();
        }
        if (nameText == null)
        {
            nameText = GetComponentInChildren<TextMeshProUGUI>();
        }
#else
        if (dialogueText == null)
        {
            dialogueText = GetComponentInChildren<Text>();
        }
        if (nameText == null)
        {
            nameText = GetComponentInChildren<Text>();
        }
#endif

        if (characterPortrait == null)
        {
            characterPortrait = GetComponentInChildren<Image>();
        }
    }

    private void InitializeNode()
    {
        if (isInitialized) return;
        isInitialized = true;

        // Initialize audio sources
        audioSources = new AudioSource[3];
        for (int i = 0; i < audioSources.Length; i++)
        {
            audioSources[i] = gameObject.AddComponent<AudioSource>();
            audioSources[i].playOnAwake = false;
        }

        // Initialize dialogue UI
        if (useDialogueSystem && dialogueUI != null)
        {
            dialogueUI.SetActive(false);
        }

        // Reset states
        isActive = false;
        isProcessing = false;
        currentSequenceIndex = 0;

        // Cache player state if exists
        if (GameObject.FindGameObjectWithTag("Player") is GameObject player)
        {
            originalPlayerPosition = player.transform.position;
            originalPlayerRotation = player.transform.rotation;
        }

        UpdateNodeState(NodeState.Inactive);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggered && triggerOnce) return;
        if (isExecuting) return;

        StartNode();
    }

    private IEnumerator StartNodeDelayed()
    {
        yield return new WaitForEndOfFrame();
        StartNode();
    }

    public void StartNode()
    {
        if (isExecuting || isActive || isProcessing) return;
        
        isExecuting = true;
        try
        {
            UpdateNodeState(NodeState.Initializing);
            isProcessing = true;
            onNodeStart?.Invoke();

            HandlePlayerState(true);
            StartEnvironmentalEffects();

            if (sequences != null && sequences.Count > 0)
            {
                currentSequenceIndex = 0;
                StartCoroutine(ProcessSequences());
            }
            else if (useDialogueSystem && dialogueSettings.dialogueLines != null && 
                     dialogueSettings.dialogueLines.Length > 0)
            {
                StartCoroutine(ProcessDialogue());
            }
            else
            {
                CompleteNode();
            }

            isActive = true;
            UpdateNodeState(NodeState.Active);
        }
        finally
        {
            isProcessing = false;
            isExecuting = false;
        }
    }

    private void CompleteNode()
    {
        if (!isActive) return;

        UpdateNodeState(NodeState.Completing);

        // Cache next node reference
        UltimateStoryNode nextNodeRef = nextNode;

        // Handle quests
        foreach (string quest in questsToComplete)
        {
            onQuestUpdated?.Invoke(quest);
        }

        // Handle items
        foreach (GameObject item in itemsToGive)
        {
            if (item != null)
            {
                onItemCollected?.Invoke(item);
            }
        }

        // Restore player state
        HandlePlayerState(false);

        // Clean up environmental effects
        if (restoreEnvironmentAfter)
        {
            foreach (GameObject obj in spawnedObjects)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedObjects.Clear();
        }

        // Save progress if needed
        if (saveProgress)
        {
            SaveNodeState();
        }

        isActive = false;
        isProcessing = false;
        triggered = true;
        UpdateNodeState(NodeState.Completed);
        onNodeComplete?.Invoke();

        // Trigger next node if exists
        if (nextNodeRef != null)
        {
            nextNodeRef.StartNode();
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        CleanupNode();
        
        // Clear event listeners
        onNodeStart.RemoveAllListeners();
        onNodeComplete.RemoveAllListeners();
        onVariableChanged.RemoveAllListeners();
        onItemCollected.RemoveAllListeners();
        onQuestUpdated.RemoveAllListeners();
        onPlayerInteract.RemoveAllListeners();
        onChoiceMade.RemoveAllListeners();
        onSequenceComplete.RemoveAllListeners();
        onDialogueComplete.RemoveAllListeners();
        onCameraTransitionComplete.RemoveAllListeners();
    }

    private void CleanupNode()
    {
        if (currentSequenceCoroutine != null)
        {
            StopCoroutine(currentSequenceCoroutine);
        }

        CleanupAudioSources();

        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        spawnedObjects.Clear();

        HandlePlayerState(false);
    }

    #region Sequence Processing
    private IEnumerator ProcessSequences()
    {
        if (sequences == null || sequences.Count == 0)
        {
            LogDebugInfo("No sequences found", LogType.Warning);
            CompleteNode();
            yield break;
        }

        while (currentSequenceIndex < sequences.Count)
        {
            var sequence = sequences[currentSequenceIndex];
            sequence.onSequenceStart?.Invoke();

            if (sequence.animationPreset != null)
            {
                yield return StartCoroutine(ProcessAnimations(sequence));
            }

            if (sequence.audioEvent != null)
            {
                ProcessAudio(sequence.audioEvent);
            }

            foreach (var element in sequence.elementsToShow)
            {
                if (element != null)
                {
                    element.SetActive(true);
                    if (sequence.animationPreset != null && 
                        sequence.animationPreset.type != AnimationPreset.AnimationType.None)
                    {
                        yield return StartCoroutine(AnimateElement(element, sequence.animationPreset));
                    }
                }
            }

            if (sequence.waitForInput)
            {
                yield return StartCoroutine(WaitForInput());
            }
            else if (sequence.duration > 0)
            {
                yield return new WaitForSeconds(sequence.duration);
            }

            sequence.onSequenceEnd?.Invoke();
            currentSequenceIndex++;

            if (!playSequentially) break;
        }

        onSequenceComplete?.Invoke();

        if (useDialogueSystem && dialogueSettings.dialogueLines != null && 
            dialogueSettings.dialogueLines.Length > 0)
        {
            yield return StartCoroutine(ProcessDialogue());
        }
        else if (choices != null && choices.Count > 0 && showChoicesImmediately)
        {
            ShowChoices();
        }
        else
        {
            CompleteNode();
        }
    }

    private IEnumerator WaitForInput()
    {
        bool inputReceived = false;
        while (!inputReceived)
        {
            if (Input.anyKeyDown || (canSkipSequences && Input.GetKeyDown(skipKey)))
            {
                inputReceived = true;
            }
            yield return null;
        }
    }
    #endregion

    #region Animation Methods
    private IEnumerator ProcessAnimations(StorySequence sequence)
    {
        if (sequence.animationPreset == null) yield break;

        foreach (var element in sequence.elementsToShow)
        {
            if (element != null)
            {
                yield return StartCoroutine(AnimateElement(element, sequence.animationPreset));
            }
        }
    }

    private IEnumerator AnimateElement(GameObject element, AnimationPreset animation)
    {
        if (element == null) yield break;

        float elapsed = 0f;
        Vector3 startPos = element.transform.position;
        Vector3 startScale = element.transform.localScale;
        Quaternion startRot = element.transform.rotation;

        // Cache component references
        CanvasGroup canvasGroup = null;
        if (animation.type == AnimationPreset.AnimationType.Fade)
        {
            canvasGroup = element.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = element.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
        }

        while (elapsed < animation.duration)
        {
            elapsed += Time.deltaTime;
            float t = animation.curve.Evaluate(elapsed / animation.duration);

            switch (animation.type)
            {
                case AnimationPreset.AnimationType.Fade:
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = t;
                    }
                    break;

                case AnimationPreset.AnimationType.Slide:
                    element.transform.position = Vector3.Lerp(animation.startPosition, animation.endPosition, t);
                    break;

                case AnimationPreset.AnimationType.Scale:
                    element.transform.localScale = Vector3.Lerp(startScale, animation.endPosition, t);
                    break;

                case AnimationPreset.AnimationType.Rotate:
                    element.transform.rotation = Quaternion.Lerp(startRot, Quaternion.Euler(animation.rotation), t);
                    break;

                case AnimationPreset.AnimationType.Custom:
                    if (animation.useCustomPath && animation.customPathPoints != null && 
                        animation.customPathPoints.Length > 0)
                    {
                        element.transform.position = GetPointOnPath(animation.customPathPoints, t);
                    }
                    break;
            }

            yield return null;
        }

        // Ensure final state is set
        switch (animation.type)
        {
            case AnimationPreset.AnimationType.Fade:
                if (canvasGroup != null) canvasGroup.alpha = 1f;
                break;
            case AnimationPreset.AnimationType.Slide:
                element.transform.position = animation.endPosition;
                break;
            case AnimationPreset.AnimationType.Scale:
                element.transform.localScale = animation.endPosition;
                break;
            case AnimationPreset.AnimationType.Rotate:
                element.transform.rotation = Quaternion.Euler(animation.rotation);
                break;
        }
    }

    private Vector3 GetPointOnPath(Transform[] points, float t)
    {
        if (points == null || points.Length == 0) return Vector3.zero;
        if (points.Length == 1) return points[0].position;

        int segment = Mathf.FloorToInt(t * (points.Length - 1));
        float segmentT = (t * (points.Length - 1)) - segment;
        
        segment = Mathf.Clamp(segment, 0, points.Length - 2);

        return Vector3.Lerp(points[segment].position, 
                           points[segment + 1].position, 
                           segmentT);
    }
    #endregion

    #region Audio Methods
    private void CleanupAudioSources()
    {
        if (audioSources != null)
        {
            foreach (var source in audioSources)
            {
                if (source != null)
                {
                    source.Stop();
                    Destroy(source);
                }
            }
        }
        audioSources = null;
    }

    private void ProcessAudio(AudioEvent audioEvent)
    {
        if (audioEvent == null || audioSources == null || audioSources.Length == 0) return;

        AudioSource source = audioSources[0];
        if (source == null) return;
        
        if (audioEvent.clips != null && audioEvent.clips.Length > 0)
        {
            AudioClip clip = audioEvent.randomize ? 
                audioEvent.clips[Random.Range(0, audioEvent.clips.Length)] : 
                audioEvent.clips[0];

            source.clip = clip;
            source.volume = audioEvent.volume;
            source.pitch = audioEvent.pitch;
            source.loop = audioEvent.loop;

            if (audioEvent.mixerGroup != null)
            {
                source.outputAudioMixerGroup = audioEvent.mixerGroup;
            }

            if (audioEvent.fadeIn)
            {
                StartCoroutine(FadeAudio(source, 0f, audioEvent.volume, audioEvent.fadeTime));
            }
            else
            {
                source.Play();
            }
        }
    }

    private IEnumerator FadeAudio(AudioSource source, float startVolume, float endVolume, float duration)
    {
        if (source == null) yield break;

        float elapsed = 0f;
        float startingVolume = source.volume;
        source.volume = startVolume;

        if (!source.isPlaying)
            source.Play();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, endVolume, elapsed / duration);
            yield return null;
        }

        source.volume = endVolume;

        if (Mathf.Approximately(endVolume, 0f))
            source.Stop();
    }
    #endregion

    #region Dialogue Methods
    private bool ValidateDialogueSystem()
    {
        if (!useDialogueSystem) return false;
        
        if (dialogueUI == null)
        {
            LogDebugInfo("DialogueUI is null", LogType.Error);
            return false;
        }
        
#if SUPPORTS_TMP
        if (dialogueText == null)
        {
            LogDebugInfo("TextMeshProUGUI component is null", LogType.Error);
            return false;
        }
#else
        if (dialogueText == null)
        {
            LogDebugInfo("Text component is null", LogType.Error);
            return false;
        }
#endif
        
        return true;
    }

    private IEnumerator ProcessDialogue()
    {
        if (!ValidateDialogueSystem()) 
        {
            CompleteNode();
            yield break;
        }

        dialogueUI.SetActive(true);

        for (int i = 0; i < dialogueSettings.dialogueLines.Length; i++)
        {
            if (dialogueText != null)
            {
                if (i < dialogueSettings.characterNames.Length && nameText != null)
                {
                    nameText.text = dialogueSettings.characterNames[i];
                }
                
                if (i < dialogueSettings.characterPortraits.Length && characterPortrait != null)
                {
                    characterPortrait.sprite = dialogueSettings.characterPortraits[i];
                }

                yield return StartCoroutine(TypeText(dialogueSettings.dialogueLines[i]));
            }

            if (dialogueSettings.waitForInput)
            {
                yield return StartCoroutine(WaitForInput());
            }
            else
            {
                yield return new WaitForSeconds(dialogueSettings.autoAdvanceDelay);
            }
        }

        dialogueUI.SetActive(false);
        onDialogueComplete?.Invoke();

        if (choices != null && choices.Count > 0)
        {
            ShowChoices();
        }
        else
        {
            CompleteNode();
        }
    }

    private IEnumerator TypeText(string text)
    {
        if (string.IsNullOrEmpty(text) || dialogueText == null) yield break;

#if SUPPORTS_TMP
        dialogueText.text = "";
        dialogueText.color = dialogueSettings.textColor;
        if (dialogueSettings.font != null)
        {
            dialogueText.font = dialogueSettings.font;
        }
#else
        dialogueText.text = "";
        dialogueText.color = dialogueSettings.textColor;
        if (dialogueSettings.font != null)
        {
            dialogueText.font = dialogueSettings.font;
        }
#endif

        foreach (char c in text)
        {
            dialogueText.text += c;
            if (dialogueSettings.typingSound != null && audioSources != null && 
                audioSources.Length > 1 && audioSources[1] != null)
            {
                audioSources[1].PlayOneShot(dialogueSettings.typingSound);
            }
            yield return new WaitForSeconds(dialogueSettings.typingSpeed);
        }
    }
    #endregion
    #region Camera Control
    private void UpdateCameraSettings()
    {
#if CINEMACHINE_PRESENT
        if (!cameraSettings.useCinemachine || cameraSettings.virtualCamera == null) return;

        try
        {
            if (cameraSettings.lookTarget != null)
            {
                cameraSettings.virtualCamera.LookAt = cameraSettings.lookTarget;
            }

            if (cameraSettings.enableDutchAngle)
            {
                cameraSettings.virtualCamera.m_Dutch = cameraSettings.dutchAngle;
            }

            if (cameraSettings.enableShake)
            {
                StartCoroutine(ShakeCamera());
            }

            var composer = cameraSettings.virtualCamera.GetCinemachineComponent<CinemachineComposer>();
            if (composer != null)
            {
                composer.m_TrackedObjectOffset = cameraSettings.cameraOffset;
            }
        }
        catch (System.Exception e)
        {
            LogDebugInfo($"Camera update failed: {e.Message}", LogType.Error);
        }
#else
        // Fallback camera behavior when Cinemachine is not available
        if (cameraSettings.virtualCamera != null && cameraSettings.lookTarget != null)
        {
            Camera cam = cameraSettings.virtualCamera.GetComponent<Camera>();
            if (cam != null)
            {
                cam.transform.LookAt(cameraSettings.lookTarget);
                if (cameraSettings.enableShake)
                {
                    StartCoroutine(ShakeCameraFallback(cam));
                }
            }
        }
#endif
    }

    private IEnumerator ShakeCamera()
    {
#if CINEMACHINE_PRESENT
        if (cameraSettings.virtualCamera == null) yield break;

        var noise = cameraSettings.virtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        if (noise != null)
        {
            float originalAmplitude = noise.m_AmplitudeGain;
            float originalFrequency = noise.m_FrequencyGain;

            noise.m_AmplitudeGain = cameraSettings.shakeIntensity;
            noise.m_FrequencyGain = 1f;

            float elapsed = 0f;
            while (elapsed < cameraSettings.shakeDuration)
            {
                elapsed += Time.deltaTime;
                float strength = 1f - (elapsed / cameraSettings.shakeDuration);
                noise.m_AmplitudeGain = strength * cameraSettings.shakeIntensity;
                yield return null;
            }

            noise.m_AmplitudeGain = originalAmplitude;
            noise.m_FrequencyGain = originalFrequency;
        }
#else
        yield break;
#endif
    }

    private IEnumerator ShakeCameraFallback(Camera camera)
    {
        if (camera == null) yield break;

        Vector3 originalPos = camera.transform.position;
        float elapsed = 0f;

        while (elapsed < cameraSettings.shakeDuration)
        {
            elapsed += Time.deltaTime;
            float strength = (1f - (elapsed / cameraSettings.shakeDuration)) * cameraSettings.shakeIntensity;

            camera.transform.position = originalPos + Random.insideUnitSphere * strength;
            yield return null;
        }

        camera.transform.position = originalPos;
    }

    private IEnumerator ProcessCameraPoints()
    {
        if (!useMultipleCameraPoints || cameraPoints == null || cameraPoints.Length == 0) yield break;

#if CINEMACHINE_PRESENT
        if (!cameraSettings.useCinemachine || cameraSettings.virtualCamera == null) yield break;

        for (int i = 0; i < cameraPoints.Length; i++)
        {
            if (cameraPoints[i] != null)
            {
                float duration = i < cameraDurations.Length ? cameraDurations[i] : 1f;
                
                cameraSettings.virtualCamera.transform.position = cameraPoints[i].position;
                cameraSettings.virtualCamera.transform.rotation = cameraPoints[i].rotation;
                
                yield return new WaitForSeconds(duration);
            }
        }
#else
        Camera mainCam = Camera.main;
        if (mainCam == null) yield break;

        for (int i = 0; i < cameraPoints.Length; i++)
        {
            if (cameraPoints[i] != null)
            {
                float duration = i < cameraDurations.Length ? cameraDurations[i] : 1f;
                Vector3 startPos = mainCam.transform.position;
                Quaternion startRot = mainCam.transform.rotation;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    
                    mainCam.transform.position = Vector3.Lerp(startPos, cameraPoints[i].position, t);
                    mainCam.transform.rotation = Quaternion.Lerp(startRot, cameraPoints[i].rotation, t);
                    
                    yield return null;
                }
            }
        }
#endif

        onCameraTransitionComplete?.Invoke();
    }
    #endregion
    #region Choice System
    private void ShowChoices()
    {
        if (choices == null || choices.Count == 0)
        {
            LogDebugInfo("No choices available", LogType.Warning);
            CompleteNode();
            return;
        }

        // Reset any existing choice timers
        StopCoroutine(nameof(ChoiceTimeoutRoutine));

        try
        {
            foreach (var choice in choices)
            {
                if (choice != null && (!choice.requireCondition || CheckCondition(choice)))
                {
                    GameObject choiceButton = CreateChoiceButton(choice);
                    spawnedObjects.Add(choiceButton);
                }
            }

            if (choiceTimeout > 0)
            {
                StartCoroutine(ChoiceTimeoutRoutine());
            }
        }
        catch (System.Exception e)
        {
            LogDebugInfo($"Error showing choices: {e.Message}", LogType.Error);
            CompleteNode();
        }
    }

    private GameObject CreateChoiceButton(BranchingChoice choice)
    {
        GameObject buttonObj = new GameObject($"Choice_{choices.IndexOf(choice)}");
        buttonObj.transform.SetParent(dialogueUI.transform, false);

        // Add required components
#if SUPPORTS_TMP
        var textComponent = buttonObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = choice.choiceText;
        textComponent.font = dialogueSettings.font;
        textComponent.color = dialogueSettings.textColor;
#else
        var textComponent = buttonObj.AddComponent<Text>();
        textComponent.text = choice.choiceText;
        textComponent.font = dialogueSettings.font;
        textComponent.color = dialogueSettings.textColor;
#endif

        var button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(() => MakeChoice(choice));

        return buttonObj;
    }

    private IEnumerator ChoiceTimeoutRoutine()
    {
        yield return new WaitForSeconds(choiceTimeout);
        
        foreach (var obj in spawnedObjects.Where(o => o.name.StartsWith("Choice_")))
        {
            if (obj != null) Destroy(obj);
        }
        
        onChoiceTimeout?.Invoke();
        CompleteNode();
    }

    private bool CheckCondition(BranchingChoice choice)
    {
        if (choice == null || string.IsNullOrEmpty(choice.conditionVariable)) return true;
        if (!variables.ContainsKey(choice.conditionVariable)) return false;

        float value = variables[choice.conditionVariable];
        
        switch (choice.conditionType)
        {
            case BranchingChoice.ConditionType.Equals:
                return Mathf.Approximately(value, choice.conditionValue);
            case BranchingChoice.ConditionType.GreaterThan:
                return value > choice.conditionValue;
            case BranchingChoice.ConditionType.LessThan:
                return value < choice.conditionValue;
            default:
                return false;
        }
    }
    #endregion

    #region Environment Effects
    private void StartEnvironmentalEffects()
    {
        if (environmentEffects == null) return;

        foreach (var effect in environmentEffects)
        {
            if (effect != null && effect.effectPrefab != null)
            {
                try
                {
                    GameObject effectObj = Instantiate(effect.effectPrefab, transform.position, Quaternion.identity);
                    spawnedObjects.Add(effectObj);

                    if (effect.fadeIn)
                    {
                        StartCoroutine(FadeEffect(effectObj, effect, true));
                    }

                    ApplyEffectSettings(effectObj, effect);
                }
                catch (System.Exception e)
                {
                    LogDebugInfo($"Failed to create effect: {e.Message}", LogType.Error);
                }
            }
        }
    }

    private void ApplyEffectSettings(GameObject effectObj, EnvironmentEffect effect)
    {
        switch (effect.type)
        {
            case EnvironmentEffect.EffectType.Particles:
                var particles = effectObj.GetComponent<ParticleSystem>();
                if (particles != null)
                {
                    var main = particles.main;
                    main.startColor = effect.colorTint;
                    main.duration = effect.duration;
                }
                break;

            case EnvironmentEffect.EffectType.Lightning:
                var light = effectObj.GetComponent<Light>();
                if (light != null)
                {
                    light.color = effect.colorTint;
                    light.intensity = effect.intensity;
                }
                break;

            case EnvironmentEffect.EffectType.PostProcessing:
#if UNITY_POST_PROCESSING_STACK_V2
                var volume = effectObj.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
                if (volume != null)
                {
                    volume.weight = effect.intensity;
                }
#endif
                break;

            case EnvironmentEffect.EffectType.Weather:
                // Implement weather system integration
                break;

            case EnvironmentEffect.EffectType.TimeOfDay:
                // Implement time of day system integration
                break;
        }
    }

    private IEnumerator FadeEffect(GameObject effectObj, EnvironmentEffect effect, bool fadeIn)
    {
        if (effectObj == null || effect == null) yield break;

        float elapsed = 0f;
        
        while (elapsed < effect.duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / effect.duration;
            
            if (!fadeIn) t = 1 - t;
            
            float intensity = effect.intensityCurve.Evaluate(t) * effect.intensity;
            
            UpdateEffectIntensity(effectObj, effect, intensity);
            
            yield return null;
        }
    }

    private void UpdateEffectIntensity(GameObject effectObj, EnvironmentEffect effect, float intensity)
    {
        switch (effect.type)
        {
            case EnvironmentEffect.EffectType.Particles:
                var particles = effectObj.GetComponent<ParticleSystem>();
                if (particles != null)
                {
                    var main = particles.main;
                    main.startLifetime = intensity;
                }
                break;

            case EnvironmentEffect.EffectType.Lightning:
                var light = effectObj.GetComponent<Light>();
                if (light != null)
                {
                    light.intensity = intensity;
                }
                break;

            case EnvironmentEffect.EffectType.PostProcessing:
#if UNITY_POST_PROCESSING_STACK_V2
                var volume = effectObj.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
                if (volume != null)
                {
                    volume.weight = intensity;
                }
#endif
                break;
        }
    }
    #endregion

    #region Save System
    private void SaveNodeState()
    {
        if (!persistNodeState) return;

        try
        {
            NodeSaveData saveData = new NodeSaveData
            {
                nodeID = nodeID,
                triggered = triggered,
                currentSequenceIndex = currentSequenceIndex,
                variables = variables,
                timestamp = System.DateTime.UtcNow.ToString("o"),
                position = transform.position,
                rotation = transform.rotation.eulerAngles
            };

            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString($"StoryNode_{nodeID}", json);

            // Save checkpoint if needed
            if (createCheckpoint)
            {
                PlayerPrefs.SetString($"Checkpoint_{checkpointID}", json);
            }

            foreach (string varName in variablesToSave)
            {
                if (variables.TryGetValue(varName, out float value))
                {
                    PlayerPrefs.SetFloat($"StoryVar_{varName}", value);
                }
            }

            PlayerPrefs.Save();
            LogDebugInfo($"Saved state for node: {nodeID}");
        }
        catch (System.Exception e)
        {
            LogDebugInfo($"Failed to save node state: {e.Message}", LogType.Error);
        }
    }

    private void LoadNodeState()
    {
        if (!persistNodeState) return;

        try
        {
            string json = PlayerPrefs.GetString($"StoryNode_{nodeID}", "");
            if (!string.IsNullOrEmpty(json))
            {
                NodeSaveData saveData = JsonUtility.FromJson<NodeSaveData>(json);
                if (saveData != null)
                {
                    triggered = saveData.triggered;
                    currentSequenceIndex = saveData.currentSequenceIndex;
                    variables = saveData.variables;
                    transform.position = saveData.position;
                    transform.rotation = Quaternion.Euler(saveData.rotation);
                }
            }

            // Load saved variables
            foreach (string varName in variablesToSave)
            {
                if (PlayerPrefs.HasKey($"StoryVar_{varName}"))
                {
                    float value = PlayerPrefs.GetFloat($"StoryVar_{varName}");
                    variables[varName] = value;
                }
            }

            LogDebugInfo($"Loaded state for node: {nodeID}");
        }
        catch (System.Exception e)
        {
            LogDebugInfo($"Failed to load node state: {e.Message}", LogType.Error);
        }
    }

    [System.Serializable]
    private class NodeSaveData
    {
        public string nodeID;
        public bool triggered;
        public int currentSequenceIndex;
        public Dictionary<string, float> variables;
        public string timestamp;
        public Vector3 position;
        public Vector3 rotation;
    }
    #endregion

    #region Variable Management
    public void SetVariable(string name, float value)
    {
        if (string.IsNullOrEmpty(name))
        {
            LogDebugInfo("Cannot set variable: name is null or empty", LogType.Error);
            return;
        }

        variables[name] = value;
        onVariableChanged?.Invoke(name);

        if (variablesToSave != null && variablesToSave.Contains(name))
        {
            PlayerPrefs.SetFloat($"StoryVar_{name}", value);
            PlayerPrefs.Save();
        }
    }

    public float GetVariable(string name, float defaultValue = 0f)
    {
        if (string.IsNullOrEmpty(name))
        {
            LogDebugInfo("Cannot get variable: name is null or empty", LogType.Error);
            return defaultValue;
        }

        return variables.TryGetValue(name, out float value) ? value : defaultValue;
    }

    public void IncrementVariable(string name, float amount = 1f)
    {
        float currentValue = GetVariable(name);
        SetVariable(name, currentValue + amount);
    }

    public void ClearVariables()
    {
        variables.Clear();
        foreach (string varName in variablesToSave)
        {
            PlayerPrefs.DeleteKey($"StoryVar_{varName}");
        }
        PlayerPrefs.Save();
    }
    #endregion

    #region Debug Methods
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Debug.isDebugBuild) return;

        // Draw node connections
        if (nextNode != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, nextNode.transform.position);
        }

        // Draw camera points
        if (useMultipleCameraPoints && cameraPoints != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < cameraPoints.Length; i++)
            {
                if (cameraPoints[i] != null)
                {
                    Gizmos.DrawWireSphere(cameraPoints[i].position, 0.5f);
                    if (i < cameraPoints.Length - 1 && cameraPoints[i + 1] != null)
                    {
                        Gizmos.DrawLine(cameraPoints[i].position, cameraPoints[i + 1].position);
                    }
                }
            }
        }
    }

    [ContextMenu("Reset Node State")]
    private void ResetNodeStateDebug()
    {
        triggered = false;
        currentSequenceIndex = 0;
        ClearVariables();
        PlayerPrefs.DeleteKey($"StoryNode_{nodeID}");
        PlayerPrefs.Save();
        LogDebugInfo("Node state reset (Debug)");
    }
#endif

    private void LogDebugInfo(string message, LogType type = LogType.Log)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string prefix = $"[StoryNode:{nodeID}] ";
        switch (type)
        {
            case LogType.Error:
                Debug.LogError(prefix + message);
                break;
            case LogType.Warning:
                Debug.LogWarning(prefix + message);
                break;
            default:
                Debug.Log(prefix + message);
                break;
        }
#endif
    }
    #endregion
}
