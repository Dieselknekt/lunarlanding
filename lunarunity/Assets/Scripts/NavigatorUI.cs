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

    [Header("Landing Site Telemetry")]
    public TMP_Text siteStatusHeader;
    public TMP_Text coordinatesText;
    public TMP_Text slopeText;
    public TMP_Text sunLosText;
    public TMP_Text earthLosText;

    [Header("Astronaut / Player Telemetry")]
    public TMP_Text playerCoordinatesText;
    public TMP_Text playerSunLosText;
    public TMP_Text playerEarthLosText;

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
            siteStatusHeader, coordinatesText, slopeText, sunLosText, earthLosText,
            playerCoordinatesText, playerSunLosText, playerEarthLosText,
            viewSwitchButtonText, controlsHintText
        };

        foreach (var t in allTexts)
        {
            if (t != null)
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = 10f;
                t.fontSizeMax = 16f;
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

        // 1. Selected Landing Site Telemetry
        if (!losSystem.hasSelectedSite)
        {
            if (siteStatusHeader != null) siteStatusHeader.text = "LANDING SITE & ASTRONAUT HUD";
            if (coordinatesText != null) coordinatesText.text = "Site Pos: None (Left-click Moon)";
            if (slopeText != null) slopeText.text = "Site Slope: --";
            if (sunLosText != null) sunLosText.text = "Site Sun LOS: --";
            if (earthLosText != null) earthLosText.text = "Site Earth LOS: --";
        }
        else
        {
            if (siteStatusHeader != null) siteStatusHeader.text = "LANDING SITE & ASTRONAUT HUD";
            if (coordinatesText != null)
                coordinatesText.text = $"Site Pos: {FormatSelenographicCoords(losSystem.selectedPoint)}";

            if (slopeText != null)
                slopeText.text = $"Site Slope: {losSystem.surfaceSlopeDegrees:F1}°";

            if (sunLosText != null)
                sunLosText.text = losSystem.hasSunLOS
                    ? "Site Sun LOS: <color=#4ADE80>CLEAR</color>"
                    : "Site Sun LOS: <color=#F87171>BLOCKED</color>";

            if (earthLosText != null)
                earthLosText.text = losSystem.hasEarthLOS
                    ? "Site Earth LOS: <color=#4ADE80>CLEAR</color>"
                    : "Site Earth LOS: <color=#F87171>BLOCKED</color>";
        }

        // 2. Astronaut / Player Telemetry (Active in FPS or saved at PlayerMarker in Orbit)
        if (cameraController != null)
        {
            if (cameraController.IsFPSActive && cameraController.fpsCamera != null)
            {
                if (playerCoordinatesText != null)
                    playerCoordinatesText.text = $"Player Pos: {FormatSelenographicCoords(cameraController.fpsCamera.transform.position)}";
            }
            else if (cameraController.hasSavedSurfacePos)
            {
                if (playerCoordinatesText != null)
                    playerCoordinatesText.text = $"Player Marker: {FormatSelenographicCoords(cameraController.savedSurfacePoint)}";
            }
            else
            {
                if (playerCoordinatesText != null)
                    playerCoordinatesText.text = "Player Pos: In Orbit";
            }

            if (losSystem.hasPlayerTelemetry)
            {
                if (playerSunLosText != null)
                    playerSunLosText.text = losSystem.playerHasSunLOS
                        ? "Player Sun LOS: <color=#4ADE80>CLEAR</color>"
                        : "Player Sun LOS: <color=#F87171>BLOCKED</color>";

                if (playerEarthLosText != null)
                    playerEarthLosText.text = losSystem.playerHasEarthLOS
                        ? "Player Earth LOS: <color=#4ADE80>CLEAR</color>"
                        : "Player Earth LOS: <color=#F87171>BLOCKED</color>";
            }
            else
            {
                if (playerSunLosText != null) playerSunLosText.text = "Player Sun LOS: --";
                if (playerEarthLosText != null) playerEarthLosText.text = "Player Earth LOS: --";
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
                string resumeHint = cameraController.hasSavedSurfacePos ? " | Resume Pos: [Tab]" : "";
                controlsHintText.text = $"Orbit: Right-Drag | Zoom: Scroll (+Shift Fast) | Select Site: Left-Click | Land: [F]{resumeHint}";
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