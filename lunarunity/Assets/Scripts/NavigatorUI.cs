using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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

    [Header("3D LOS Beams & Mission Bookmarks")]
    public Button toggleBeamsButton;
    public TMP_Text toggleBeamsButtonText;
    public Button bookmarkShackletonButton;
    public Button bookmarkApollo11Button;
    public Button bookmarkTychoButton;
    public Button bookmarkAristarchusButton;
    public LunarLightingVisualizer lightingVisualizer;
    public Slider stylizedLightingSlider;
    public TMP_Text stylizedLightingText;

    [Header("30-Day Mission Window Analysis")]
    public MissionWindowAnalyzer windowAnalyzer;
    public Button analyze30DayButton;
    public TMP_Text analysisReportText;

    [Header("Launch Window & Trajectory Planner")]
    public LaunchTrajectoryPlanner trajectoryPlanner;
    public Button cycleLaunchSiteButton;
    public TMP_Text launchSiteButtonText;
    public Button planTrajectoryButton;
    public Button nextOptimalWindowButton;
    public Button playTrajectoryAnimButton;
    public TMP_Text playTrajectoryAnimText;
    public TMP_Text trajectoryStatusText;

    [Header("Pause & Settings Menu [Esc]")]
    public GameObject pauseMenuPanel;
    public Slider sensitivitySlider;
    public TMP_Text sensitivityValueText;
    public Slider orbitSpeedSlider;
    public TMP_Text orbitSpeedValueText;
    public Slider zoomSpeedSlider;
    public TMP_Text zoomSpeedValueText;
    public Button scaleModeToggleButton;
    public TMP_Text scaleModeButtonText;
    public Button resumeButton;

    private double scrubAnchorJ2000;
    private bool isScrubbingTime = false;
    private bool wasPlayingBeforePause = true;

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

        SetupPauseMenu();
        ApplyCleanTextFitting();

        if (toggleBeamsButton != null)
            toggleBeamsButton.onClick.AddListener(OnToggleBeamsClicked);

        if (bookmarkShackletonButton != null && losSystem != null)
            bookmarkShackletonButton.onClick.AddListener(() => losSystem.SelectSiteByCoordinates(-89.54f, 0.00f, "Shackleton Rim (Artemis III)"));

        if (bookmarkApollo11Button != null && losSystem != null)
            bookmarkApollo11Button.onClick.AddListener(() => losSystem.SelectSiteByCoordinates(0.67f, 23.47f, "Apollo 11 (Tranquility Base)"));

        if (bookmarkTychoButton != null && losSystem != null)
            bookmarkTychoButton.onClick.AddListener(() => losSystem.SelectSiteByCoordinates(-43.31f, -11.36f, "Tycho Crater"));

        if (bookmarkAristarchusButton != null && losSystem != null)
            bookmarkAristarchusButton.onClick.AddListener(() => losSystem.SelectSiteByCoordinates(23.73f, -47.49f, "Aristarchus Plateau"));

        UpdateBeamsButtonLabel();
        if (stylizedLightingSlider != null && lightingVisualizer != null)
        {
            stylizedLightingSlider.minValue = 0f;
            stylizedLightingSlider.maxValue = 1f;
            stylizedLightingSlider.value = lightingVisualizer.stylizedBlend;
            stylizedLightingSlider.onValueChanged.AddListener(OnStylizedLightingChanged);
            OnStylizedLightingChanged(lightingVisualizer.stylizedBlend);
        }
        if (analyze30DayButton != null)
            analyze30DayButton.onClick.AddListener(OnAnalyze30DayClicked);

        if (analysisReportText != null)
            analysisReportText.text = "Select a site & click 'Analyze 30-Day Window'.";

        if (cycleLaunchSiteButton != null)
            cycleLaunchSiteButton.onClick.AddListener(OnCycleLaunchSiteClicked);

        if (planTrajectoryButton != null)
            planTrajectoryButton.onClick.AddListener(OnPlanTrajectoryClicked);

        if (nextOptimalWindowButton != null)
            nextOptimalWindowButton.onClick.AddListener(OnNextOptimalWindowClicked);

        if (playTrajectoryAnimButton != null)
            playTrajectoryAnimButton.onClick.AddListener(OnPlayTrajectoryAnimClicked);

        UpdateLaunchSiteLabel();
        if (trajectoryStatusText != null)
            trajectoryStatusText.text = "Select a Lunar site, choose Launch Pad & click 'Plan Launch'.";
    }

    private void SetupPauseMenu()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (cameraController != null)
        {
            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = 0.005f;
                sensitivitySlider.maxValue = 0.2f;
                sensitivitySlider.value = cameraController.lookSensitivity;
                sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
                OnSensitivityChanged(cameraController.lookSensitivity);
            }

            if (orbitSpeedSlider != null)
            {
                orbitSpeedSlider.minValue = 0.01f;
                orbitSpeedSlider.maxValue = 30f;
                cameraController.orbitSpeed = Mathf.Clamp(cameraController.orbitSpeed, 0.01f, 30f);
                orbitSpeedSlider.value = cameraController.orbitSpeed;
                orbitSpeedSlider.onValueChanged.AddListener(OnOrbitSpeedChanged);
                OnOrbitSpeedChanged(cameraController.orbitSpeed);
            }

            if (zoomSpeedSlider != null)
            {
                zoomSpeedSlider.minValue = 10f;
                zoomSpeedSlider.maxValue = 500f;
                cameraController.zoomSpeed = Mathf.Clamp(cameraController.zoomSpeed, 10f, 500f);
                zoomSpeedSlider.value = cameraController.zoomSpeed;
                zoomSpeedSlider.onValueChanged.AddListener(OnZoomSpeedChanged);
                OnZoomSpeedChanged(cameraController.zoomSpeed);
            }
        }

        if (scaleModeToggleButton != null)
            scaleModeToggleButton.onClick.AddListener(OnToggleScaleModeClicked);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(() => SetPauseMenuOpen(false));

        UpdateScaleButtonLabel();
    }

    private void ApplyCleanTextFitting()
    {
        TMP_Text[] allTexts = {
            timeDisplayText, timeSpeedText, playPauseButtonText,
            siteStatusHeader, coordinatesText, slopeText, sunLosText, earthLosText,
            playerCoordinatesText, playerSunLosText, playerEarthLosText,
            viewSwitchButtonText, controlsHintText,
            sensitivityValueText, orbitSpeedValueText, zoomSpeedValueText, scaleModeButtonText,
            toggleBeamsButtonText, stylizedLightingText, analysisReportText, launchSiteButtonText,
            playTrajectoryAnimText, trajectoryStatusText
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
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            bool nextOpen = pauseMenuPanel != null && !pauseMenuPanel.activeSelf;
            SetPauseMenuOpen(nextOpen);
        }

        UpdateTimeUI();
        UpdateTelemetryUI();
        UpdateModeUI();
    }

    public void SetPauseMenuOpen(bool open)
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(open);

        if (cameraController != null)
            cameraController.SetPaused(open);

        if (celestialSystem != null)
        {
            if (open)
            {
                wasPlayingBeforePause = celestialSystem.isPlaying;
                celestialSystem.isPlaying = false;
            }
            else
            {
                celestialSystem.isPlaying = wasPlayingBeforePause;
            }
        }
    }

    private void OnSensitivityChanged(float val)
    {
        if (cameraController != null) cameraController.lookSensitivity = val;
        if (sensitivityValueText != null) sensitivityValueText.text = $"Look Sensitivity: {val:F3}";
    }

    private void OnOrbitSpeedChanged(float val)
    {
        if (cameraController != null) cameraController.orbitSpeed = val;
        if (orbitSpeedValueText != null) orbitSpeedValueText.text = $"Orbit Speed: {val:F2}";
    }

    private void OnZoomSpeedChanged(float val)
    {
        if (cameraController != null) cameraController.zoomSpeed = val;
        if (zoomSpeedValueText != null) zoomSpeedValueText.text = $"Zoom Speed: {val:F2}";
    }

    private void OnToggleScaleModeClicked()
    {
        if (cameraController != null)
        {
            cameraController.ApplyScaleProfile(!cameraController.isDebugScale);
            UpdateScaleButtonLabel();
        }
    }

    private void UpdateScaleButtonLabel()
    {
        if (scaleModeButtonText != null && cameraController != null)
        {
            scaleModeButtonText.text = cameraController.isDebugScale
                ? "Scale Mode: DEBUG (Fast / Giant)"
                : "Scale Mode: REALISTIC (Default)";
        }
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
                controlsHintText.text = $"Orbit: Right-Drag | Zoom: Scroll (+Shift Fast) | Select Site: Left-Click | Land: [F]{resumeHint} | Menu: [Esc]";
            }
            else
            {
                string cursorState = cameraController.IsCursorUnlocked ? "UI Mode [Alt/C to Look]" : "Free-Look [Alt/C for UI]";
                controlsHintText.text = $"Move: [WASD] | Sprint: [Shift] | Jump: [Space] | Noclip: [G] | {cursorState} | Menu: [Esc]";
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
    public void OnToggleBeamsClicked()
    {
        if (losSystem != null)
        {
            losSystem.showSiteBeams = !losSystem.showSiteBeams;
            UpdateBeamsButtonLabel();
        }
    }

    private void UpdateBeamsButtonLabel()
    {
        if (toggleBeamsButtonText != null && losSystem != null)
        {
            toggleBeamsButtonText.text = losSystem.showSiteBeams ? "Site LOS Beams: ON" : "Site LOS Beams: OFF";
        }
    }
    private void OnStylizedLightingChanged(float val)
    {
        if (lightingVisualizer != null)
            lightingVisualizer.SetStylizedBlend(val);

        if (stylizedLightingText != null)
        {
            int pct = Mathf.RoundToInt(val * 100f);
            string label = pct == 0 ? "REAL" : (pct == 100 ? "STYLIZED" : $"{pct}%");
            stylizedLightingText.text = $"LOS Map: {label}";
        }
    }
    public void OnAnalyze30DayClicked()
    {
        if (windowAnalyzer == null || analysisReportText == null) return;

        if (losSystem == null || !losSystem.hasSelectedSite)
        {
            analysisReportText.text = "<color=#F87171>Select a landing site first!</color>";
            return;
        }

        MissionWindowReport r = windowAnalyzer.AnalyzeSelectedSite();
        if (!r.isValid) return;

        string gradeColor = r.grade.StartsWith("A") || r.grade.StartsWith("B") ? "#4ADE80"
                          : (r.grade.StartsWith("C") ? "#FACC15" : "#F87171");

        float shadowDays = r.longestShadowHours / 24f;
        float blackoutDays = r.longestBlackoutHours / 24f;

        analysisReportText.text =
            $"<b>30-Day Mission Forecast</b>\n" +
            $"Grade: <color={gradeColor}><b>{r.grade}</b></color>\n" +
            $"Solar Power: <b>{r.sunIlluminationPercent:F1}%</b> | Earth Comms: <b>{r.earthCommsPercent:F1}%</b>\n" +
            $"Max Shadow: <b>{r.longestShadowHours:F0}h</b> ({shadowDays:F1}d) | Max Blackout: <b>{r.longestBlackoutHours:F0}h</b> ({blackoutDays:F1}d)\n" +
            $"<i>{r.summaryAssessment}</i>";
    }
    private void UpdateLaunchSiteLabel()
    {
        if (launchSiteButtonText != null && trajectoryPlanner != null)
            launchSiteButtonText.text = $"Pad: {trajectoryPlanner.GetSelectedLaunchSiteName()}";
    }

    public void OnCycleLaunchSiteClicked()
    {
        if (trajectoryPlanner == null) return;
        trajectoryPlanner.CycleLaunchSite();
        UpdateLaunchSiteLabel();
        RefreshTrajectoryStatusUI();
    }

    public void OnPlanTrajectoryClicked()
    {
        if (trajectoryPlanner == null || celestialSystem == null) return;
        if (losSystem == null || !losSystem.hasSelectedSite)
        {
            if (trajectoryStatusText != null)
                trajectoryStatusText.text = "<color=#F87171>Select a Lunar landing site first!</color>";
            return;
        }

        trajectoryPlanner.EvaluateAndBuildTrajectory(celestialSystem.j2000Days, true);
        RefreshTrajectoryStatusUI();
    }

    public void OnNextOptimalWindowClicked()
    {
        if (trajectoryPlanner == null) return;
        if (losSystem == null || !losSystem.hasSelectedSite)
        {
            if (trajectoryStatusText != null)
                trajectoryStatusText.text = "<color=#F87171>Select a Lunar landing site first!</color>";
            return;
        }

        trajectoryPlanner.StepToNextOptimalWindow(1);
        RefreshTrajectoryStatusUI();
    }

    public void OnPlayTrajectoryAnimClicked()
    {
        if (trajectoryPlanner == null) return;
        trajectoryPlanner.ToggleFlightAnimation();
        if (playTrajectoryAnimText != null)
            playTrajectoryAnimText.text = trajectoryPlanner.isAnimatingFlight ? "Pause Flight Anim" : "Play Flight Anim";
    }

    private void RefreshTrajectoryStatusUI()
    {
        if (trajectoryPlanner == null || trajectoryStatusText == null) return;
        var ev = trajectoryPlanner.CurrentEvaluation;
        if (!ev.isValid) return;

        string statusCol = ev.isGo ? "#4ADE80" : "#F87171";
        string statusTag = ev.isGo ? "GO" : "NO-GO";
        int optCount = trajectoryPlanner.OptimalWindows.Count;
        int optIdx = trajectoryPlanner.currentOptimalWindowIndex >= 0 ? trajectoryPlanner.currentOptimalWindowIndex + 1 : 0;

        trajectoryStatusText.text =
            $"<b>Launch Window: <color={statusCol}>{statusTag}</color></b> (Optimal: {optIdx}/{optCount})\n" +
            $"Launch: {ev.launchUtcString}\n" +
            $"Arrival (+{ev.transitDays:F2}d): {ev.arrivalUtcString}\n" +
            $"Descent Angle: {ev.approachAngleDeg:F0}° | Terrain: {(ev.approachTerrainClear ? "CLEAR" : "HAZARD")}\n" +
            $"<i>{ev.verdictSummary}</i>";
    }
}