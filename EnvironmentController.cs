using System;
using UnityEngine;
using TMPro;

public class EnvironmentController : MonoBehaviour
{
    public enum Weather { Clear, Cloudy, Rain, Fog, Storm }
    public enum DayPhase { Dawn, Day, Dusk, Night }

    [Header("Local daylight settings - prototype defaults")]
    [Range(0, 23)] [SerializeField] private int sunriseHour = 6;
    [Range(0, 23)] [SerializeField] private int sunsetHour = 18;

    [Header("Scene lighting")]
    [SerializeField] private Light sceneSun;
    [SerializeField] private float dayIntensity = 1.1f;
    [SerializeField] private float nightIntensity = 0.12f;
    [SerializeField] private Color dayLightColor = new Color(1f, 0.93f, 0.78f);
    [SerializeField] private Color nightLightColor = new Color(0.35f, 0.43f, 0.68f);
    [SerializeField] private Color dayAmbient = new Color(0.60f, 0.60f, 0.60f);
    [SerializeField] private Color nightAmbient = new Color(0.10f, 0.12f, 0.20f);

    [Header("Weather fog (affects virtual objects only, not the camera image)")]
    [SerializeField] private bool useFogForWeather = true;

    [Header("Optional HUD")]
    [SerializeField] private TMP_Text environmentText;

    [Header("Prototype weather setting")]
    [SerializeField] private Weather currentWeather = Weather.Clear;

    [Header("Ambient light input")]
    [Range(0f, 1f)] [SerializeField] private float ambientBrightness = 0.5f;
    [SerializeField] private bool useAmbientBrightness = false;

    private float refreshTimer;

    public DayPhase CurrentPhase { get; private set; }
    public Weather CurrentWeather => currentWeather;

    // More monsters at night and in bad weather. Used by MonsterSpawner.
    public float MonsterSpawnMultiplier
    {
        get
        {
            float phase;
            switch (CurrentPhase)
            {
                case DayPhase.Dawn: phase = 1.15f; break;
                case DayPhase.Day: phase = 0.8f; break;
                case DayPhase.Dusk: phase = 1.5f; break;
                default: phase = 1.8f; break;
            }

            float weather;
            switch (currentWeather)
            {
                case Weather.Cloudy: weather = 1.05f; break;
                case Weather.Rain: weather = 1.15f; break;
                case Weather.Fog: weather = 1.25f; break;
                case Weather.Storm: weather = 1.35f; break;
                default: weather = 1f; break;
            }
            return phase * weather;
        }
    }

    public void Configure(Light sun, TMP_Text text)
    {
        if (sun != null) sceneSun = sun;
        if (text != null) environmentText = text;
    }

    private void Start()
    {
        EnsureSun();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        UpdateEnvironment();
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= 1f)
        {
            refreshTimer = 0f;
            UpdateEnvironment();
        }
    }

    public void SetWeather(Weather weather)
    {
        currentWeather = weather;
        UpdateEnvironment();
    }

    // Future sensor integration can call this with a normalized 0..1 reading.
    public void SetAmbientBrightness(float normalizedBrightness)
    {
        ambientBrightness = Mathf.Clamp01(normalizedBrightness);
        useAmbientBrightness = true;
        UpdateEnvironment();
    }

    public void SetDaylightHours(int localSunriseHour, int localSunsetHour)
    {
        sunriseHour = Mathf.Clamp(localSunriseHour, 0, 23);
        sunsetHour = Mathf.Clamp(localSunsetHour, 0, 23);
        UpdateEnvironment();
    }

    private void EnsureSun()
    {
        if (sceneSun != null) return;

        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) { sceneSun = l; return; }
        }

        GameObject go = new GameObject("Sun");
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sceneSun = go.AddComponent<Light>();
        sceneSun.type = LightType.Directional;
    }

    private void UpdateEnvironment()
    {
        float hour = (float)DateTime.Now.TimeOfDay.TotalHours;
        CurrentPhase = GetPhase(hour);

        float blend = CurrentPhase == DayPhase.Day ? 1f :
                      CurrentPhase == DayPhase.Night ? 0f :
                      CurrentPhase == DayPhase.Dawn ? 0.45f : 0.35f;

        if (useAmbientBrightness)
            blend = Mathf.Lerp(blend, ambientBrightness, 0.45f);

        switch (currentWeather)
        {
            case Weather.Cloudy: blend *= 0.78f; break;
            case Weather.Rain: blend *= 0.62f; break;
            case Weather.Fog: blend *= 0.7f; break;
            case Weather.Storm: blend *= 0.42f; break;
        }

        if (sceneSun != null)
        {
            sceneSun.intensity = Mathf.Lerp(nightIntensity, dayIntensity, blend);
            sceneSun.color = Color.Lerp(nightLightColor, dayLightColor, blend);
        }

        // Ambient light keeps monsters visible at night instead of pure black silhouettes.
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, blend);

        ApplyWeatherFog(blend);

        if (environmentText != null)
            environmentText.text = Loc.Phase(CurrentPhase) + " | " + Loc.Weather(currentWeather);
    }

    private void ApplyWeatherFog(float blend)
    {
        if (!useFogForWeather) return;

        float density;
        switch (currentWeather)
        {
            case Weather.Rain: density = 0.02f; break;
            case Weather.Fog: density = 0.06f; break;
            case Weather.Storm: density = 0.035f; break;
            default: density = 0f; break;
        }

        RenderSettings.fog = density > 0f;
        if (density > 0f)
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = density;
            RenderSettings.fogColor = Color.Lerp(new Color(0.08f, 0.09f, 0.12f), new Color(0.65f, 0.67f, 0.70f), blend);
        }
    }

    // Dawn/dusk last one hour before and after sunrise/sunset. Works across midnight too.
    private DayPhase GetPhase(float hour)
    {
        if (InWindow(hour, sunriseHour - 1f, sunriseHour + 1f)) return DayPhase.Dawn;
        if (InWindow(hour, sunsetHour - 1f, sunsetHour + 1f)) return DayPhase.Dusk;
        if (InWindow(hour, sunriseHour + 1f, sunsetHour - 1f)) return DayPhase.Day;
        return DayPhase.Night;
    }

    private static float Wrap24(float x) { return ((x % 24f) + 24f) % 24f; }

    private static bool InWindow(float hour, float start, float end)
    {
        start = Wrap24(start);
        end = Wrap24(end);
        return start <= end ? (hour >= start && hour < end) : (hour >= start || hour < end);
    }
}
