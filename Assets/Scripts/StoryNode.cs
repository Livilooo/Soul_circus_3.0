using UnityEngine;
using UnityEngine.UI;
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
    public bool waitForUIButton = false;
    public Button continueButton;

    // Removed Fade Settings

    private bool triggered = false;
    private Coroutine activeCoroutine;
    private PlayerController playerController;
    private bool timeElapsed = false;

    private void Start()
    {
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
            continueButton.onClick.AddListener(OnContinueButtonPressed);
        }
    }

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
        // Immediately disable temporarily disabled objects.
        foreach (var obj in objectsToDisable)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        // Immediately disable permanently disabled objects.
        foreach (var obj in objectsToPermanentlyDisable)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        // Lock player movement if needed.
        if (!allowPlayerMovementDuringNode && playerController != null)
            playerController.canMove = false;

        triggered = true;
        timeElapsed = false;
        Debug.Log($"Story Node '{gameObject.name}' activated.");

        if (waitForUIButton && continueButton != null)
        {
            if (activeDuration > 0f)
            {
                activeCoroutine = StartCoroutine(DeactivateAfterDuration(activeDuration));
            }
            else
            {
                // If no duration is set, show the button immediately.
                continueButton.gameObject.SetActive(true);
                timeElapsed = true;
            }
        }
        else if (activeDuration > 0f)
        {
            activeCoroutine = StartCoroutine(DeactivateAfterDuration(activeDuration));
        }
        else
        {
            DeactivateNode();
            ActivateNextNode();
        }
    }

    private IEnumerator DeactivateAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        timeElapsed = true;

        if (waitForUIButton && continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
        }
        else
        {
            DeactivateNode();
            ActivateNextNode();
        }
    }

    public void OnContinueButtonPressed()
    {
        Debug.Log("Continue button pressed!");
        if (timeElapsed)
        {
            // Reactivate player movement if needed.
            if (!allowPlayerMovementDuringNode && playerController != null)
            {
                playerController.canMove = true;
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }

            DeactivateNode();
            ActivateNextNode();
        }
    }

    private void ActivateNextNode()
    {
        if (nextNode != null)
        {
            nextNode.gameObject.SetActive(true);
            nextNode.ActivateNode();
            Debug.Log($"Next Story Node '{nextNode.gameObject.name}' activated.");
        }
    }

    public void DeactivateNode()
    {
        // Immediately enable story elements.
        foreach (var element in storyElements)
        {
            if (element != null)
                element.SetActive(true);
        }

        // Immediately re-enable temporarily disabled objects.
        foreach (var obj in objectsToDisable)
        {
            if (obj != null)
                obj.SetActive(true);
        }

        // Permanently disable and destroy objects (if triggerOnce is true).
        if (triggerOnce)
        {
            foreach (var obj in objectsToPermanentlyDisable)
            {
                if (obj != null)
                {
                    // Skip destroying the player or any object with a PlayerController.
                    if (obj.CompareTag("Player") || obj.GetComponent<PlayerController>() != null)
                    {
                        Debug.Log($"Skipping destruction of player object: {obj.name}");
                        continue;
                    }

                    Debug.Log($"Destroying permanently disabled object: {obj.name}");
                    Destroy(obj);
                }
            }

            // Destroy this StoryNode.
            Debug.Log($"Destroying Story Node: {gameObject.name}");
            Destroy(gameObject);
        }
        else
        {
            // Ensure player movement is enabled if the StoryNode is not destroyed.
            if (!allowPlayerMovementDuringNode && playerController != null)
            {
                playerController.canMove = true;
            }
        }

        Debug.Log($"Story Node '{gameObject.name}' deactivated.");
    }

    private void OnDestroy()
    {
        // Ensure player movement is re-enabled when the node is destroyed.
        if (playerController != null && !allowPlayerMovementDuringNode)
        {
            playerController.canMove = true;
        }

        // Clean up button listener.
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueButtonPressed);
        }
    }
}
