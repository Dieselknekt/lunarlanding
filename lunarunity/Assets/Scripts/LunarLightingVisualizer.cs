using UnityEngine;

public class LunarLightingVisualizer : MonoBehaviour
{
    [Header("Celestial References")]
    public Transform moonCenter;
    public Transform sunVisual;
    public Transform earthVisual;

    [Header("Light Sources")]
    [Tooltip("Drag your scene's Main Directional Light (the Sun) here.")]
    public Light sunDirectionalLight;
    private Light earthDirectionalLight;

    [Header("Visualization Mode (0 = Real, 1 = Stylized)")]
    [Range(0f, 1f)]
    public float stylizedBlend = 0f;

    [Header("Real Mode Settings")]
    public float realSunIntensity = 2.6f;
    public Color realSunColor = new Color(1f, 0.98f, 0.95f);
    public float maxEarthshineIntensity = 0.22f;
    public Color realEarthshineColor = new Color(0.55f, 0.72f, 1f);

    [Header("Stylized LOS Heatmap Settings")]
    public float stylizedSunIntensity = 2.2f;
    public Color stylizedSunColor = new Color(1f, 0.82f, 0.1f);      // Warm Yellow
    public float stylizedEarthIntensity = 1.9f;
    public Color stylizedEarthColor = new Color(0.05f, 0.65f, 1f);    // Deep Cyan-Blue (Yellow + Cyan = Green!)

    void Start()
    {
        CreateEarthLightIfMissing();
        UpdateLighting();
    }

    private void CreateEarthLightIfMissing()
    {
        if (earthDirectionalLight != null) return;

        GameObject earthLightObj = new GameObject("EarthshineDirectionalLight");
        earthLightObj.transform.SetParent(transform);
        earthDirectionalLight = earthLightObj.AddComponent<Light>();
        earthDirectionalLight.type = LightType.Directional;
        earthDirectionalLight.shadows = LightShadows.Hard;
        earthDirectionalLight.shadowStrength = 1f;
        earthDirectionalLight.shadowBias = 0.05f;
        earthDirectionalLight.shadowNormalBias = 0.4f;
    }

    void LateUpdate()
    {
        UpdateLighting();
    }

    public void SetStylizedBlend(float value)
    {
        stylizedBlend = Mathf.Clamp01(value);
        UpdateLighting();
    }

    public void UpdateLighting()
    {
        if (moonCenter == null || sunVisual == null || earthVisual == null) return;
        if (earthDirectionalLight == null) CreateEarthLightIfMissing();

        Vector3 moonToSun = (sunVisual.position - moonCenter.position).normalized;
        Vector3 moonToEarth = (earthVisual.position - moonCenter.position).normalized;

        // Orient both directional lights toward the Moon's center
        if (sunDirectionalLight != null)
        {
            sunDirectionalLight.transform.rotation = Quaternion.LookRotation(-moonToSun, Vector3.up);
        }
        earthDirectionalLight.transform.rotation = Quaternion.LookRotation(-moonToEarth, Vector3.up);

        // Calculate physical Earthshine phase factor:
        // When Sun and Earth are opposite from Moon's perspective (angle ~180 deg, i.e., New Moon), Earth appears "Full" to the Moon (factor = 1.0).
        // When Sun and Earth are in the same direction (angle ~0 deg, i.e., Full Moon), Earth appears "New" (factor = 0.0).
        float cosAngle = Vector3.Dot(moonToSun, moonToEarth);
        float earthPhaseIllumination = Mathf.Clamp01((1f - cosAngle) * 0.5f);

        // Real Earthshine intensity scales smoothly with Earth's illuminated phase
        float currentRealEarthIntensity = maxEarthshineIntensity * Mathf.Pow(earthPhaseIllumination, 1.2f);

        // Blend between Real (0.0) and Stylized LOS Heatmap (1.0)
        if (sunDirectionalLight != null)
        {
            sunDirectionalLight.color = Color.Lerp(realSunColor, stylizedSunColor, stylizedBlend);
            sunDirectionalLight.intensity = Mathf.Lerp(realSunIntensity, stylizedSunIntensity, stylizedBlend);
        }

        earthDirectionalLight.color = Color.Lerp(realEarthshineColor, stylizedEarthColor, stylizedBlend);
        earthDirectionalLight.intensity = Mathf.Lerp(currentRealEarthIntensity, stylizedEarthIntensity, stylizedBlend);
    }
}