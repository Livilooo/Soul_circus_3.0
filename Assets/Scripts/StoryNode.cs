using UnityEngine;
using System.Collections;

public class StoryNode : MonoBehaviour
{
    [Header("Story Elements to Activate")]
    [Tooltip("Drag and drop the GameObjects that represent this story point (e.g., dialogue, visual cues)")]
    public GameObject[] storyElements;

    [Header("Objects to Temporarily Disable")]
    [Tooltip("Drag and drop the GameObjects that you want to disable for the duration of this story node")]
    public GameObject[] objectsToDisable;

    [Header("Player Control Settings")]
    [Tooltip("If false, player control is disabled during this story node.")]
    public bool allowPlayerMovementDuringNode = true;

    [Header("Chained Story Node (Optional)")]
    [Tooltip("If set, this node will also activate the next node after triggering")]
    public StoryNode nextNode;

    [Header("Settings")]
    [Tooltip("If true, this node can only be triggered once")]
    public bool triggerOnce = true;

    [Tooltip("Time (in seconds) for which the story elements remain active. Set to 0 to disable auto-deactivation.")]
    public float activeDuration = 5f;

    private bool triggered = false;
    private Coroutine activeCoroutine;
    private PlayerController playerController;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (triggered && triggerOnce)
            return;

        // Get a reference to the PlayerController from the colliding object.
        playerController = other.GetComponent<PlayerController>();

        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        ActivateNode();
    }

    /// <summary>
    /// Activates story elements, disables specified objects, and optionally disables player movement.
    /// </summary>
    public void ActivateNode()
    {
        // Activate story elements.
        foreach (var element in storyElements)
        {
            if (element != null)
                element.SetActive(true);
        }
        // Deactivate specified objects.
        foreach (var obj in objectsToDisable)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        // Disable player movement if not allowed during this story point.
        if (!allowPlayerMovementDuringNode && playerController != null)
        {
            playerController.canMove = false;
        }

        triggered = true;
        Debug.Log($"Story Node '{gameObject.name}' activated.");

        if (nextNode != null)
        {
            nextNode.gameObject.SetActive(true);
            Debug.Log($"Next Story Node '{nextNode.gameObject.name}' activated.");
        }

        if (activeDuration > 0f)
            activeCoroutine = StartCoroutine(DeactivateAfterDuration(activeDuration));
    }

    /// <summary>
    /// Waits for the specified duration before deactivating this node.
    /// </summary>
    private IEnumerator DeactivateAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        DeactivateNode();
    }

    /// <summary>
    /// Deactivates story elements, re-enables any disabled objects, and restores player movement if it was disabled.
    /// </summary>
    public void DeactivateNode()
    {
        // Deactivate story elements.
        foreach (var element in storyElements)
        {
            if (element != null)
                element.SetActive(false);
        }
        // Reactivate objects that were disabled.
        foreach (var obj in objectsToDisable)
        {
            if (obj != null)
                obj.SetActive(true);
        }
        Debug.Log($"Story Node '{gameObject.name}' deactivated.");

        // Restore player movement if it was disabled.
        if (!allowPlayerMovementDuringNode && playerController != null)
        {
            playerController.canMove = true;
        }
    }
}
