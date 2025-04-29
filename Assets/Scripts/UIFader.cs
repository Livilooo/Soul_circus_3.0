using System.Collections;
using UnityEngine;

public class UIFaderFadeInOut : MonoBehaviour
{
    [Tooltip("Total duration in seconds for the entire fade in/out cycle.")]
    public float fadeDuration = 4f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // Get the CanvasGroup component (or add one if missing)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        // Start fully transparent.
        canvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        // Start the fade in/out cycle when this UI element is enabled.
        StartCoroutine(FadeInOut());
    }

    private IEnumerator FadeInOut()
    {
        // Calculate the duration for each half of the cycle.
        float halfDuration = fadeDuration / 2f;
        float elapsedTime = 0f;

        // Fade In: Increase alpha from 0 to 1.
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsedTime / halfDuration);
            yield return null;
        }
        // Ensure alpha is exactly 1 at midpoint.
        canvasGroup.alpha = 1f;

        // Reset elapsed time for fade out.
        elapsedTime = 0f;
        // Fade Out: Decrease alpha from 1 to 0.
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsedTime / halfDuration);
            yield return null;
        }
        // Ensure alpha is exactly 0 after finishing.
        canvasGroup.alpha = 0f;
    }
}
