using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TimeManager : MonoBehaviour
{
    [Header("Skybox Textures")]
    [SerializeField] private Texture2D skyboxNight;
    [SerializeField] private Texture2D skyboxSunrise;
    [SerializeField] private Texture2D skyboxDay;
    [SerializeField] private Texture2D skyboxSunset;

    [Header("Lighting")]
    [SerializeField] private Light globalLight;
    [SerializeField] private Gradient gradientNightToSunrise;
    [SerializeField] private Gradient gradientSunriseToDay;
    [SerializeField] private Gradient gradientDayToSunset;
    [SerializeField] private Gradient gradientSunsetToNight;
    [Tooltip("Maximum intensity of the sun at its peak (usually noon).")]
    [SerializeField] private float maxSunIntensity = 1.5f;

    [Header("Time Settings")]
    [Tooltip("Length of a full day (24 in‑game hours) in real‑time seconds.")]
    [SerializeField] private float dayDuration = 864f;
    [SerializeField] private bool startAtMidnight = true;

    [Header("Events")]
    public UnityEvent<int> OnHourChanged; // Invoked with the current hour when the hour changes

    // Represents time-of-day in hours (0 - 24)
    public float TimeOfDay { get; private set; }
    // Tracks real time elapsed
    private float elapsedTime;
    // Last recorded whole hour for event triggering
    private int lastHour;

    private void Start()
    {
        // Initialize time-of-day
        TimeOfDay = startAtMidnight ? 0f : TimeOfDay;
        lastHour = Mathf.FloorToInt(TimeOfDay);
        // Set up the skybox immediately to avoid a pop-in effect
        UpdateSkyboxImmediately(TimeOfDay);
        // Initial update for lighting properties
        UpdateLighting();
    }

    private void Update()
    {
        // Increase elapsed time scaled to the day duration
        elapsedTime += Time.deltaTime;
        // Compute passed hours as a fraction of dayDuration
        float hoursPassed = (elapsedTime / dayDuration) * 24f;
        // Update the continuous time-of-day; wrapping around at 24
        TimeOfDay = (startAtMidnight ? hoursPassed : (TimeOfDay + hoursPassed)) % 24f;

        // Check for a change in the whole hour value
        int currentHour = Mathf.FloorToInt(TimeOfDay);
        if (currentHour != lastHour)
        {
            lastHour = currentHour;
            OnHourChanged?.Invoke(currentHour);
            HandleSkyboxTransition(currentHour);
        }

        // Apply realistic lighting based on the updated time
        UpdateLighting();
    }

    /// <summary>
    /// Update lighting (color, intensity, ambient) and sun rotation based on realistic calculations.
    /// </summary>
    private void UpdateLighting()
    {
        // Calculate sun’s elevation using a sine function:
        // At 6 AM (TimeOfDay = 6) the sun is at the horizon,
        // at noon (12) it reaches maximum altitude,
        // and at 6 PM (18) it sets back on the horizon.
        // This returns a value in the range [-1, 1].
        float sunCycle = Mathf.Sin(((TimeOfDay - 6f) / 12f) * Mathf.PI);
        // Only allow positive intensity for daytime
        float sunIntensityFactor = Mathf.Clamp01(sunCycle);

        // Apply intensity based on the sun's elevation.
        globalLight.intensity = sunIntensityFactor * maxSunIntensity;
        // Optionally modify ambient intensity for a more realistic scene brightness.
        RenderSettings.ambientIntensity = Mathf.Lerp(0.2f, 1.0f, sunIntensityFactor);

        // Update the light color using gradients according to predefined time windows.
        if (TimeOfDay >= 6 && TimeOfDay < 8)
        {
            // Sunrise: remap time from 6 to 8 into 0..1 for the gradient.
            float t = (TimeOfDay - 6f) / 2f;
            globalLight.color = gradientNightToSunrise.Evaluate(t);
        }
        else if (TimeOfDay >= 8 && TimeOfDay < 18)
        {
            // Daytime transition.
            float t = (TimeOfDay - 8f) / 10f;
            globalLight.color = gradientSunriseToDay.Evaluate(t);
        }
        else if (TimeOfDay >= 18 && TimeOfDay < 22)
        {
            // Sunset transition.
            float t = (TimeOfDay - 18f) / 4f;
            globalLight.color = gradientDayToSunset.Evaluate(t);
        }
        else
        {
            // Night transition.
            // For times after 22 or before 6, map to the gradient.
            float t;
            if (TimeOfDay >= 22)
                t = (TimeOfDay - 22f) / 2f;
            else
                t = TimeOfDay / 6f;
            globalLight.color = gradientSunsetToNight.Evaluate(t);
        }

        // Set the sun's rotation to simulate its path across the sky.
        // The X-axis rotation is based on the TimeOfDay mapped to a full 360° circle.
        float sunAngle = ((TimeOfDay / 24f) * 360f) - 90f;
        globalLight.transform.localRotation = Quaternion.Euler(sunAngle, 0f, 0f);
    }

    /// <summary>
    /// Checks the whole hour and starts the skybox transition when key times are reached.
    /// </summary>
    private void HandleSkyboxTransition(int currentHour)
    {
        // You can adjust these key times to customize the look & feel.
        switch (currentHour)
        {
            case 6:
                StartCoroutine(LerpSkybox(skyboxNight, skyboxSunrise, 10f));
                break;
            case 8:
                StartCoroutine(LerpSkybox(skyboxSunrise, skyboxDay, 10f));
                break;
            case 18:
                StartCoroutine(LerpSkybox(skyboxDay, skyboxSunset, 10f));
                break;
            case 22:
                StartCoroutine(LerpSkybox(skyboxSunset, skyboxNight, 10f));
                break;
        }
    }

    /// <summary>
    /// Immediately sets the skybox texture based on the initial time-of-day.
    /// This avoids a pop-in effect when the scene starts.
    /// </summary>
    private void UpdateSkyboxImmediately(float time)
    {
        Material skyboxMaterial = RenderSettings.skybox;
        if (time >= 6 && time < 8)
            skyboxMaterial.SetTexture("_MainTex", skyboxSunrise);
        else if (time >= 8 && time < 18)
            skyboxMaterial.SetTexture("_MainTex", skyboxDay);
        else if (time >= 18 && time < 22)
            skyboxMaterial.SetTexture("_MainTex", skyboxSunset);
        else
            skyboxMaterial.SetTexture("_MainTex", skyboxNight);

        skyboxMaterial.SetFloat("_Blend", 1f);
    }

    /// <summary>
    /// Smoothly blends the skybox from one texture to another over the specified duration.
    /// Requires that the skybox material have a "_Blend" parameter.
    /// </summary>
    private IEnumerator LerpSkybox(Texture2D from, Texture2D to, float duration)
    {
        Material skyboxMaterial = RenderSettings.skybox;
        // Ensure we start with the initial texture.
        skyboxMaterial.SetTexture("_MainTex", from);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float blendValue = Mathf.Clamp01(elapsed / duration);
            skyboxMaterial.SetFloat("_Blend", blendValue);
            yield return null;
        }
        // Finalize the transition.
        skyboxMaterial.SetTexture("_MainTex", to);
        skyboxMaterial.SetFloat("_Blend", 1f);
    }
}
