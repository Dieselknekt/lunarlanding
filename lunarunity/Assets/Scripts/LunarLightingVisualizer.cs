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

    [Header("Stylized High-Contrast LOS Heatmap")]
    public float stylizedSunIntensity = 4.2f;
    public Color stylizedSunColor = new Color(1f, 0.85f, 0.0f);       // Pure Vivid Yellow (0 Blue)
    public float stylizedEarthIntensity = 4.2f;
    public Color stylizedEarthColor = new Color(0.0f, 0.55f, 1.0f);    // Pure Vivid Blue-Cyan (0 Red)

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

        if (sunDirectionalLight != null)
        {
            sunDirectionalLight.transform.rotation = Quaternion.LookRotation(-moonToSun, Vector3.up);
        }
        earthDirectionalLight.transform.rotation = Quaternion.LookRotation(-moonToEarth, Vector3.up);

        // Physical Earthshine phase factor
        float cosAngle = Vector3.Dot(moonToSun, moonToEarth);
        float earthPhaseIllumination = Mathf.Clamp01((1f - cosAngle) * 0.5f);
        float currentRealEarthIntensity = maxEarthshineIntensity * Mathf.Pow(earthPhaseIllumination, 1.2f);

        // Apply non-linear boost in stylized mode so colors pop dramatically
        float curveBlend = Mathf.SmoothStep(0f, 1f, stylizedBlend);

        if (sunDirectionalLight != null)
        {
            sunDirectionalLight.color = Color.Lerp(realSunColor, stylizedSunColor, curveBlend);
            sunDirectionalLight.intensity = Mathf.Lerp(realSunIntensity, stylizedSunIntensity, curveBlend);
        }

        earthDirectionalLight.color = Color.Lerp(realEarthshineColor, stylizedEarthColor, curveBlend);
        earthDirectionalLight.intensity = Mathf.Lerp(currentRealEarthIntensity, stylizedEarthIntensity, curveBlend);
    }
}