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
        globalLight.transform.localRotation = Quaternion.Euler((Hours * 15) % 360, 0, 0); // Smooth rotation

        // Smoothly update the light color based on time of day
        UpdateLightColor();
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
        float t = (float)hours / 24f;
        if (hours < 6) globalLight.color = gradientSunsetToNight.Evaluate(t);
        else if (hours < 8) globalLight.color = gradientNightToSunrise.Evaluate(t);
        else if (hours < 18) globalLight.color = gradientSunriseToDay.Evaluate(t);
        else if (hours < 22) globalLight.color = gradientDayToSunset.Evaluate(t);
        else globalLight.color = gradientSunsetToNight.Evaluate(t);
    }

    private IEnumerator LerpSkybox(Texture2D from, Texture2D to, float duration)
    {
        RenderSettings.skybox.SetTexture("_Texture1", from);
        RenderSettings.skybox.SetTexture("_Texture2", to);
        for (float t = 0; t < 1; t += Time.deltaTime / duration)
        {
            RenderSettings.skybox.SetFloat("_Blend", t);
            yield return null;
        }
        RenderSettings.skybox.SetTexture("_Texture1", to);
        RenderSettings.skybox.SetFloat("_Blend", 1);
    }
}


