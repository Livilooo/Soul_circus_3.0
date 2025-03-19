using System.Collections;
using UnityEngine;

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

    [Header("Time Settings")]
    [SerializeField] private float dayDuration = 864f; // Length of a full day in real-time seconds
    private float timeElapsed;

    private int minutes;
    public int Minutes
    {
        get { return minutes; }
        private set
        {
            minutes = value;
            if (minutes >= 60) { Hours++; minutes = 0; }
        }
    }

    private int hours;
    public int Hours
    {
        get { return hours; }
        private set
        {
            hours = value;
            if (hours >= 24) { hours = 0; Days++; }
            HandleLightingTransitions();
        }
    }

    private int days;
    public int Days { get; private set; }

    private void Update()
    {
        timeElapsed += Time.deltaTime;
        float timeFactor = (24f / dayDuration) * Time.deltaTime; // Scales to real-time

        // Update time
        Minutes += Mathf.FloorToInt(timeFactor * 60);

        // Calculate the rotation angle based on the time of day
        float rotationAngle = (timeElapsed / dayDuration) * 360f;
        globalLight.transform.localRotation = Quaternion.Euler(rotationAngle - 90f, 0f, 0f);

        // Smoothly update the light color based on time of day
        UpdateLightColor();

        // Debug logs
        Debug.Log($"Time Elapsed: {timeElapsed}");
        Debug.Log($"Minutes: {Minutes}");
        Debug.Log($"Hours: {Hours}");
        Debug.Log($"Days: {Days}");
    }

    private void HandleLightingTransitions()
    {
        switch (Hours)
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

    private void UpdateLightColor()
    {
        float t = (float)Minutes / 60f + Hours;
        if (Hours < 6) globalLight.color = gradientSunsetToNight.Evaluate(t / 6);
        else if (Hours < 8) globalLight.color = gradientNightToSunrise.Evaluate((t - 6) / 2);
        else if (Hours < 18) globalLight.color = gradientSunriseToDay.Evaluate((t - 8) / 10);
        else if (Hours < 22) globalLight.color = gradientDayToSunset.Evaluate((t - 18) / 4);
        else globalLight.color = gradientSunsetToNight.Evaluate((t - 22) / 2);
    }

    private IEnumerator LerpSkybox(Texture2D from, Texture2D to, float duration)
    {
        Material skyboxMaterial = RenderSettings.skybox;
        skyboxMaterial.SetTexture("_MainTex", from);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            skyboxMaterial.SetFloat("_Blend", elapsedTime / duration);
            yield return null;
        }
        skyboxMaterial.SetTexture("_MainTex", to);
        skyboxMaterial.SetFloat("_Blend", 1);
    }
}