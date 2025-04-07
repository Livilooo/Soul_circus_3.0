using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UICanvasActivator : MonoBehaviour
{
    // Reference to the Canvas component (assign in Inspector)
    [SerializeField] private Canvas storyCanvas;

    // Activates the canvas (shows the UI)
    public void ActivateCanvas()
    {
        if (storyCanvas != null)
        {
            storyCanvas.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Story Canvas is not assigned!");
        }
    }

    // Deactivates the canvas (hides the UI)
    public void DeactivateCanvas()
    {
        if (storyCanvas != null)
        {
            storyCanvas.gameObject.SetActive(false);
            storyCanvas.gameObject.IsDestroyed();
        }
        else
        {
            Debug.LogWarning("Story Canvas is not assigned!");
        }
    }
}



