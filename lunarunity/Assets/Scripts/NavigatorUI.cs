using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NavigatorUI : MonoBehaviour
{
    [Header("System References")]
    public CelestialSystem celestialSystem;
    public LunarLOSAndLanding losSystem;
    public DualCameraController cameraController;

    [Header("Time & Date Controls")]
    public TMP_Text timeDisplayText;
    public Slider timeScrubSlider;
    public Slider timeSpeedSlider;
    public TMP_Text timeSpeedText;
    public Button playPauseButton;
    public TMP_Text playPauseButtonText;
    public Button resetDateButton;

    [Header("Site Telemetry HUD")]
    public TMP_Text siteStatusHeader;
    public TMP_Text coordinatesText;
    public TMP_Text playerCoordinatesText;
    public TMP_Text slopeText;
    public TMP_Text sunLosText;
    public TMP_Text earthLosText;

    [Header("Action & Help")]
    public Button viewSwitchButton;
    public TMP_Text viewSwitchButtonText;
    public Button returnToAstronautButton;
    public TMP_Text controlsHintText;

    private double scrubAnchorJ2000;
    private bool isScrubbingTime = false;

    void Start()
    {
        if (celestialSystem != null)
            scrubAnchorJ2000 = celestialSystem.j2000Days;

        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(TogglePlayPause);

        if (resetDateButton != null)
            resetDateButton.onClick.AddListener(OnResetDateClicked);

        if (viewSwitchButton != null)
            viewSwitchButton.onClick.AddListener(OnViewSwitchClicked);

        if (returnToAstronautButton != null)
            returnToAstronautButton.onClick.AddListener(OnReturnToAstronautClicked);

        if (timeSpeedSlider != null && celestialSystem != null)
        {
            timeSpeedSlider.minValue = 0f;
            timeSpeedSlider.maxValue = 2f;
            timeSpeedSlider.value = celestialSystem.timeSpeed;
            timeSpeedSlider.onValueChanged.AddListener(OnTimeSpeedChanged);
        }

        if (timeScrubSlider != null)
        {
            timeScrubSlider.minValue = -15f;
            timeScrubSlider.maxValue = 15f;
            timeScrubSlider.value = 0f;
            timeScrubSlider.onValueChanged.AddListener(OnTimeScrubChanged);
        }

        ApplyCleanTextFitting();
    }

    private void ApplyCleanTextFitting()
    {
        TMP_Text[] allTexts = {
            timeDisplayText, timeSpeedText, playPauseButtonText,
            siteStatusHeader, coordinatesText, playerCoordinatesText,
            slopeText, sunLosText, earthLosText, viewSwitchButtonText, controlsHintText
        };

        foreach (var t in allTexts)
        {
            if (t != null)
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = 11f;
                t.fontSizeMax = 17f;
                t.textWrappingMode = TextWrappingModes.Normal;
            }
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

        if (timeDisplayText != null)
            timeDisplayText.text = $"Epoch: {celestialSystem.GetFormattedUtcString()}";

        if (timeScrubSlider != null && !isScrubbingTime)
        {
            double offset = celestialSystem.j2000Days - scrubAnchorJ2000;
            if (Math.Abs(offset) > 15.0)
            {
                scrubAnchorJ2000 = celestialSystem.j2000Days;
                offset = 0.0;
            }
            timeScrubSlider.SetValueWithoutNotify((float)offset);
        }

        if (timeSpeedText != null)
        {
            float hoursPerSec = celestialSystem.timeSpeed * 24f;
            timeSpeedText.text = $"Time Warp: {hoursPerSec:F1} hrs/s";
        }

        if (playPauseButtonText != null)
            playPauseButtonText.text = celestialSystem.isPlaying ? "Pause" : "Play";
    }

    private void UpdateTelemetryUI()
    {
        if (losSystem == null) return;

        // 1. Selected Landing Site Coordinates
        if (!losSystem.hasSelectedSite)
        {
            if (siteStatusHeader != null) siteStatusHeader.text = "NO LANDING SITE SELECTED";
            if (coordinatesText != null) coordinatesText.text = "Landing Site: None (Left-click Moon)";
            if (slopeText != null) slopeText.text = "Slope: --";
            if (sunLosText != null) sunLosText.text = "Sun LOS: --";
            if (earthLosText != null) earthLosText.text = "Earth LOS: --";
        }
        else
        {
            if (siteStatusHeader != null) siteStatusHeader.text = "TELEMETRY & SITE DATA";
            if (coordinatesText != null)
                coordinatesText.text = $"Landing Site: {FormatSelenographicCoords(losSystem.selectedPoint)}";

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

        // 2. Current or Last Astronaut Coordinates
        if (playerCoordinatesText != null && cameraController != null)
        {
            if (cameraController.IsFPSActive && cameraController.fpsCamera != null)
            {
                playerCoordinatesText.text = $"Current Pos: {FormatSelenographicCoords(cameraController.fpsCamera.transform.position)}";
            }
            else if (cameraController.hasSavedSurfacePos)
            {
                playerCoordinatesText.text = $"Last Surface Pos: {FormatSelenographicCoords(cameraController.savedSurfacePoint)}";
            }
            else
            {
                playerCoordinatesText.text = "Current Pos: In Orbit";
            }
        }
    }

    private string FormatSelenographicCoords(Vector3 worldPoint)
    {
        Vector3 dir = (worldPoint - losSystem.moonCenter.position).normalized;
        float lat = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        float lon = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        string latHem = lat >= 0 ? "N" : "S";
        string lonHem = lon >= 0 ? "E" : "W";
        return $"{Mathf.Abs(lat):F2}° {latHem}, {Mathf.Abs(lon):F2}° {lonHem}";
    }

    private void UpdateModeUI()
    {
        if (cameraController == null || losSystem == null) return;

        bool inOrbit = !cameraController.IsFPSActive;

        if (viewSwitchButton != null)
            viewSwitchButton.interactable = !inOrbit || losSystem.hasSelectedSite;

        if (viewSwitchButtonText != null)
            viewSwitchButtonText.text = inOrbit ? "Land at Site [F]" : "Return to Orbit [Tab]";

        if (returnToAstronautButton != null)
        {
            returnToAstronautButton.gameObject.SetActive(inOrbit && cameraController.hasSavedSurfacePos);
        }

        if (controlsHintText != null)
        {
            if (inOrbit)
            {
                controlsHintText.text = "Orbit: Right-Drag | Zoom: Scroll | Select Site: Left-Click | Land: [F]";
            }
            else
            {
                string cursorState = cameraController.IsCursorUnlocked ? "UI Mode [Alt/C to Look]" : "Free-Look [Alt/C for UI]";
                controlsHintText.text = $"Move: [WASD] | Sprint: [Shift] | Jump: [Space] | Noclip: [G] | {cursorState} | Orbit: [Tab]";
            }
        }
    }

    public void TogglePlayPause()
    {
        if (celestialSystem != null)
            celestialSystem.isPlaying = !celestialSystem.isPlaying;
    }

    public void OnResetDateClicked()
    {
        if (celestialSystem != null)
        {
            celestialSystem.ResetToStartDate();
            scrubAnchorJ2000 = celestialSystem.j2000Days;
        }
    }

    public void OnTimeSpeedChanged(float newSpeed)
    {
        if (celestialSystem != null)
            celestialSystem.timeSpeed = newSpeed;
    }

    public void OnTimeScrubChanged(float dayOffset)
    {
        if (celestialSystem != null)
        {
            celestialSystem.j2000Days = scrubAnchorJ2000 + dayOffset;
            celestialSystem.UpdateCelestialPositions();
        }
    }

    public void OnViewSwitchClicked()
    {
        if (cameraController == null || losSystem == null) return;

        if (!cameraController.IsFPSActive)
            losSystem.EnterFPSAtSelectedSite();
        else
            cameraController.SwitchToOrbital();
    }

    public void OnReturnToAstronautClicked()
    {
        if (cameraController != null)
        {
            cameraController.ReturnToLastSurfacePosition();
        }
    }
}