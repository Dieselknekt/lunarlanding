using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NavigatorUI : MonoBehaviour
{
    [Header("System References")]
    public CelestialSystem celestialSystem;
    public LunarLOSAndLanding losSystem;
    public DualCameraController cameraController;

    [Header("Time Controls")]
    public TMP_Text timeDisplayText;
    public Slider timeScrubSlider;
    public Slider timeSpeedSlider;
    public TMP_Text timeSpeedText;
    public Button playPauseButton;
    public TMP_Text playPauseButtonText;

    [Header("Site Telemetry HUD")]
    public TMP_Text siteStatusHeader;
    public TMP_Text coordinatesText;
    public TMP_Text slopeText;
    public TMP_Text sunLosText;
    public TMP_Text earthLosText;

    [Header("Action & Help")]
    public Button viewSwitchButton;
    public TMP_Text viewSwitchButtonText;
    public TMP_Text controlsHintText;

    private bool isScrubbingTime = false;

    void Start()
    {
        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(TogglePlayPause);

        if (viewSwitchButton != null)
            viewSwitchButton.onClick.AddListener(OnViewSwitchClicked);

        if (timeSpeedSlider != null && celestialSystem != null)
        {
            timeSpeedSlider.minValue = 0f;
            timeSpeedSlider.maxValue = 5f;
            timeSpeedSlider.value = celestialSystem.timeSpeed;
            timeSpeedSlider.onValueChanged.AddListener(OnTimeSpeedChanged);
        }

        if (timeScrubSlider != null)
        {
            timeScrubSlider.minValue = 0f;
            timeScrubSlider.maxValue = 29.53f;
            timeScrubSlider.onValueChanged.AddListener(OnTimeScrubChanged);
        }
    }

    void Update()
    {
        UpdateTimeUI();
        UpdateTelemetryUI();
        UpdateModeUI();
    }

    private void UpdateTimeUI()
    {
        if (celestialSystem == null) return;

        float cycleDay = celestialSystem.simulationDays % 29.53f;
        if (cycleDay < 0f) cycleDay += 29.53f;

        if (timeDisplayText != null)
            timeDisplayText.text = $"Lunar Cycle: Day {cycleDay:F2} / 29.53 (Total: {celestialSystem.simulationDays:F1}d)";

        if (timeScrubSlider != null && !isScrubbingTime)
            timeScrubSlider.SetValueWithoutNotify(cycleDay);

        if (timeSpeedText != null)
            timeSpeedText.text = $"Speed: {celestialSystem.timeSpeed:F2} d/s";

        if (playPauseButtonText != null)
            playPauseButtonText.text = celestialSystem.isPlaying ? "Pause" : "Play";
    }

    private void UpdateTelemetryUI()
    {
        if (losSystem == null) return;

        if (!losSystem.hasSelectedSite)
        {
            if (siteStatusHeader != null) siteStatusHeader.text = "NO LANDING SITE SELECTED";
            if (coordinatesText != null) coordinatesText.text = "Left-click on the Moon to inspect a site";
            if (slopeText != null) slopeText.text = "Slope: --";
            if (sunLosText != null) sunLosText.text = "Sun LOS: --";
            if (earthLosText != null) earthLosText.text = "Earth LOS: --";
            return;
        }

        if (siteStatusHeader != null) siteStatusHeader.text = "SELECTED LANDING SITE";

        // Convert surface position to Lunar Latitude / Longitude
        Vector3 dir = (losSystem.selectedPoint - losSystem.moonCenter.position).normalized;
        float lat = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        float lon = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        string latHem = lat >= 0 ? "N" : "S";
        string lonHem = lon >= 0 ? "E" : "W";

        if (coordinatesText != null)
            coordinatesText.text = $"Coords: {Mathf.Abs(lat):F1}° {latHem}, {Mathf.Abs(lon):F1}° {lonHem}";

        if (slopeText != null)
            slopeText.text = $"Terrain Slope: {losSystem.surfaceSlopeDegrees:F1}°";

        if (sunLosText != null)
            sunLosText.text = losSystem.hasSunLOS
                ? "Sun LOS: <color=#4ADE80>CLEAR (Solar Active)</color>"
                : "Sun LOS: <color=#F87171>BLOCKED (Shadow)</color>";

        if (earthLosText != null)
            earthLosText.text = losSystem.hasEarthLOS
                ? "Earth LOS: <color=#4ADE80>CLEAR (Comms Active)</color>"
                : "Earth LOS: <color=#F87171>BLOCKED (No Signal)</color>";
    }

    private void UpdateModeUI()
    {
        if (cameraController == null || losSystem == null) return;

        bool inOrbit = cameraController.orbitalCamera != null && cameraController.orbitalCamera.gameObject.activeInHierarchy;

        if (viewSwitchButton != null)
            viewSwitchButton.interactable = !inOrbit || losSystem.hasSelectedSite;

        if (viewSwitchButtonText != null)
            viewSwitchButtonText.text = inOrbit ? "Land at Site [F]" : "Return to Orbit [Tab]";

        if (controlsHintText != null)
        {
            controlsHintText.text = inOrbit
                ? "Orbit: Right-Drag | Zoom: Scroll | Select Site: Left-Click | Land: [F]"
                : "Look: Mouse | Move: [WASD] | Jump: [Space] | Noclip: [G] | Orbit: [Tab]";
        }
    }

    public void TogglePlayPause()
    {
        if (celestialSystem != null)
            celestialSystem.isPlaying = !celestialSystem.isPlaying;
    }

    public void OnTimeSpeedChanged(float newSpeed)
    {
        if (celestialSystem != null)
            celestialSystem.timeSpeed = newSpeed;
    }

    public void OnTimeScrubChanged(float newCycleDay)
    {
        if (celestialSystem != null)
        {
            float currentCycleBase = Mathf.Floor(celestialSystem.simulationDays / 29.53f) * 29.53f;
            celestialSystem.simulationDays = currentCycleBase + newCycleDay;
            celestialSystem.UpdateCelestialPositions();
        }
    }

    public void OnViewSwitchClicked()
    {
        if (cameraController == null || losSystem == null) return;

        bool inOrbit = cameraController.orbitalCamera != null && cameraController.orbitalCamera.gameObject.activeInHierarchy;
        if (inOrbit)
            losSystem.EnterFPSAtSelectedSite();
        else
            cameraController.SwitchToOrbital();
    }
}