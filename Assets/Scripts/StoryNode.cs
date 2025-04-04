using UnityEngine;
using System.Collections;

public class StoryNode : MonoBehaviour
{
    [Header("Story Elements to Activate")]
    public GameObject[] storyElements;

    [Header("Objects to Temporarily Disable")]
    public GameObject[] objectsToDisable;

    [Header("Objects to Permanently Disable")]
    public GameObject[] objectsToPermanentlyDisable;

    [Header("Player Control Settings")]
    public bool allowPlayerMovementDuringNode = true;

    [Header("Chained Story Node (Optional)")]
    public StoryNode nextNode;

    [Header("Settings")]
    public bool triggerOnce = true;
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

        playerController = other.GetComponent<PlayerController>();

        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        ActivateNode();
    }

    public void ActivateNode()
    {
        // Activate story elements
        foreach (var element in storyElements)
            if (element != null)
                element.SetActive(true);

        // Temporarily disable objects
        foreach (var obj in objectsToDisable)
            if (obj != null)
                obj.SetActive(false);

        // Permanently disable objects
        foreach (var obj in objectsToPermanentlyDisable)
            if (obj != null)
                obj.SetActive(false);

        // Lock player movement if needed
        if (!allowPlayerMovementDuringNode && playerController != null)
            playerController.canMove = false;

        triggered = true;
        Debug.Log($"Story Node '{gameObject.name}' activated.");

        if (activeDuration > 0f)
            activeCoroutine = StartCoroutine(DeactivateAfterDuration(activeDuration));
        else
            DeactivateNode(); // If duration is 0, deactivate immediately
    }

    private IEnumerator DeactivateAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        DeactivateNode();

        // Activate next node (after delay)
        if (nextNode != null)
        {
            nextNode.gameObject.SetActive(true);
            Debug.Log($"Next Story Node '{nextNode.gameObject.name}' activated.");
        }
    }

    public void DeactivateNode()
    {
        // Deactivate story elements
        foreach (var element in storyElements)
            if (element != null)
                element.SetActive(false);

        // Reactivate temporarily disabled objects
        foreach (var obj in objectsToDisable)
            if (obj != null)
                obj.SetActive(true);

        // Restore player movement if it was locked
        if (!allowPlayerMovementDuringNode && playerController != null)
            playerController.canMove = true;

        Debug.Log($"Story Node '{gameObject.name}' deactivated.");
    }
}
