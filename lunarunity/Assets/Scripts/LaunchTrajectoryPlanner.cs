using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct LaunchSitePreset
{
    public string name;
    public float latitudeDeg;
    public float longitudeDeg;

    public LaunchSitePreset(string name, float lat, float lon)
    {
        this.name = name;
        this.latitudeDeg = lat;
        this.longitudeDeg = lon;
    }
}

[Serializable]
public struct LaunchWindowEvaluation
{
    public bool isValid;
    public bool isGo;
    public double launchJ2000;
    public double arrivalJ2000;
    public float transitDays;
    public string launchUtcString;
    public string arrivalUtcString;
    public bool approachTerrainClear;
    public bool arrivalSunClear;
    public bool arrivalEarthClear;
    public float approachAngleDeg;
    public string verdictSummary;
}

public class LaunchTrajectoryPlanner : MonoBehaviour
{
    [Header("System References")]
    public CelestialSystem celestialSystem;
    public LunarLOSAndLanding losSystem;
    public DualCameraController cameraController;
    public Transform moonCenter;
    public Transform earthVisual;
    public Transform sunVisual;

    [Header("Keplerian Transit Settings")]
    public float nominalTransitDays = 3.0f;
    [Tooltip("Auto-calibrated at Start from initial Earth-Moon distance if left at 0.")]
    public float nominalEarthMoonDistance = 0f;
    public int trajectorySegments = 120;
    public float trajectoryLineWidth = 5f;
    public bool showTrajectory = true;

    [Header("Animation Playback")]
    public bool isAnimatingFlight = false;
    [Tooltip("Playback seconds per 3.0 transit days (scales dynamically with actual transit duration).")]
    public float baseAnimationSeconds = 8f;
    private float animationProgress = 0f;

    public List<LaunchSitePreset> launchSites = new List<LaunchSitePreset>
    {
        new LaunchSitePreset("Kennedy Space Center (LC-39B)", 28.608f, -80.604f),
        new LaunchSitePreset("Guiana Space Centre (Kourou)", 5.236f, -52.768f),
        new LaunchSitePreset("Baikonur Cosmodrome", 45.965f, 63.305f),
        new LaunchSitePreset("Vandenberg SFB", 34.742f, -120.572f)
    };

    public int selectedLaunchSiteIndex = 0;
    public LaunchWindowEvaluation CurrentEvaluation { get; private set; }
    public List<LaunchWindowEvaluation> OptimalWindows { get; private set; } = new List<LaunchWindowEvaluation>();
    public int currentOptimalWindowIndex = -1;

    private LineRenderer trajectoryRenderer;
    private GameObject spacecraftMarker;
    private Vector3[] currentTrajectoryPoints;

    void Start()
    {
        if (nominalEarthMoonDistance <= 0f && earthVisual != null && moonCenter != null)
        {
            nominalEarthMoonDistance = Vector3.Distance(earthVisual.position, moonCenter.position);
        }
        CreateVisuals();
    }

    private void CreateVisuals()
    {
        GameObject lineObj = new GameObject("TranslunarTrajectoryLine");
        lineObj.transform.SetParent(transform);
        trajectoryRenderer = lineObj.AddComponent<LineRenderer>();
        trajectoryRenderer.useWorldSpace = true;
        trajectoryRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trajectoryRenderer.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) trajectoryRenderer.material = new Material(shader);
        trajectoryRenderer.enabled = false;

        spacecraftMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spacecraftMarker.name = "SpacecraftTransferMarker";
        spacecraftMarker.transform.SetParent(transform);
        spacecraftMarker.transform.localScale = Vector3.one * 35f;
        Destroy(spacecraftMarker.GetComponent<Collider>());
        Renderer r = spacecraftMarker.GetComponent<Renderer>();
        if (r != null && shader != null)
        {
            r.material = new Material(shader);
            r.material.color = Color.white;
        }
        spacecraftMarker.SetActive(false);
    }

    void Update()
    {
        if (!showTrajectory || losSystem == null || !losSystem.hasSelectedSite)
        {
            if (trajectoryRenderer != null) trajectoryRenderer.enabled = false;
            if (spacecraftMarker != null) spacecraftMarker.SetActive(false);
            return;
        }

        if (!isAnimatingFlight)
        {
            EvaluateAndBuildTrajectory(celestialSystem.j2000Days, false);
        }
        else
        {
            UpdateFlightAnimation();
        }
    }

    public void CycleLaunchSite()
    {
        selectedLaunchSiteIndex = (selectedLaunchSiteIndex + 1) % launchSites.Count;
        EvaluateAndBuildTrajectory(celestialSystem.j2000Days, true);
    }

    public string GetSelectedLaunchSiteName()
    {
        if (launchSites == null || launchSites.Count == 0) return "None";
        return launchSites[Mathf.Clamp(selectedLaunchSiteIndex, 0, launchSites.Count - 1)].name;
    }

    /// <summary>
    /// Computes Keplerian transfer duration (approx. 2.65 - 3.35 days) based on current Earth-Moon distance (perigee vs apogee).
    /// </summary>
    public float ComputeDynamicTransitDays()
    {
        if (earthVisual == null || moonCenter == null) return nominalTransitDays;
        float currentDist = Vector3.Distance(earthVisual.position, moonCenter.position);
        float refDist = nominalEarthMoonDistance > 1f ? nominalEarthMoonDistance : currentDist;
        float ratio = Mathf.Clamp(currentDist / refDist, 0.85f, 1.15f);
        return nominalTransitDays * Mathf.Pow(ratio, 1.5f);
    }

    public LaunchWindowEvaluation EvaluateAndBuildTrajectory(double launchEpochJ2000, bool scanOptimalList)
    {
        if (celestialSystem == null || losSystem == null || !losSystem.hasSelectedSite)
        {
            CurrentEvaluation = new LaunchWindowEvaluation { isValid = false };
            return CurrentEvaluation;
        }

        if (scanOptimalList)
        {
            ScanOptimalWindows(launchEpochJ2000);
        }

        bool wasPlaying = celestialSystem.isPlaying;
        celestialSystem.isPlaying = false;

        // 1. Sample Earth pad position and Earth-Moon distance at Launch Epoch
        celestialSystem.j2000Days = launchEpochJ2000;
        celestialSystem.UpdateCelestialPositions();
        Vector3 earthPosAtLaunch = earthVisual.position;
        Vector3 padWorldPos = GetEarthPadWorldPosition(launchSites[selectedLaunchSiteIndex], launchEpochJ2000);
        Vector3 padUp = (padWorldPos - earthPosAtLaunch).normalized;
        float dynamicTransitDays = ComputeDynamicTransitDays();

        // 2. Sample Moon, Sun & Earth state at Arrival Epoch (+dynamicTransitDays)
        double arrivalEpochJ2000 = launchEpochJ2000 + dynamicTransitDays;
        celestialSystem.j2000Days = arrivalEpochJ2000;
        celestialSystem.UpdateCelestialPositions();

        Vector3 targetSitePos = losSystem.selectedPoint;
        Vector3 moonOrigin = moonCenter.position;
        float moonRadius = Vector3.Distance(targetSitePos, moonOrigin);
        Vector3 siteRadialUp = (targetSitePos - moonOrigin).normalized;

        float eyeOffset = cameraController != null ? cameraController.eyeHeight : 0.05f;
        Vector3 losOrigin = targetSitePos + losSystem.selectedNormal * eyeOffset;

        // Check Sun & Earth LOS at Arrival
        Vector3 toSun = (sunVisual.position - losOrigin).normalized;
        bool arrivalSunClear = !Physics.Raycast(losOrigin, toSun, Vector3.Distance(losOrigin, sunVisual.position));

        Vector3 toEarth = (earthVisual.position - losOrigin).normalized;
        bool arrivalEarthClear = !Physics.Raycast(losOrigin, toEarth, Vector3.Distance(losOrigin, earthVisual.position));

        string arrivalUtc = celestialSystem.GetFormattedUtcString();

        // Restore simulation epoch to Launch Epoch so user sees the launch geometry
        celestialSystem.j2000Days = launchEpochJ2000;
        celestialSystem.UpdateCelestialPositions();
        celestialSystem.isPlaying = wasPlaying;
        string launchUtc = celestialSystem.GetFormattedUtcString();

        // 3. Build 2-Stage Selenocentric Trajectory (Never cuts through the Moon sphere!)
        // Stage A (65% of points): Translunar Transfer from Earth Pad to Lunar Orbit Insertion (LOI) Flank Gate
        // Stage B (35% of points): Spherical Slerp Orbit around the Moon to Descent Gate -> Vertical Touchdown
        Vector3 earthToMoonDir = (moonOrigin - earthPosAtLaunch).normalized;

        // Construct an orthogonal orbit plane containing Earth->Moon and the target landing site
        Vector3 orbitNormal = Vector3.Cross(earthToMoonDir, siteRadialUp);
        if (orbitNormal.sqrMagnitude < 0.001f)
            orbitNormal = Vector3.Cross(earthToMoonDir, Vector3.up);
        orbitNormal.Normalize();

        // Flank tangent direction perpendicular to Earth->Moon in the target's orbital plane
        Vector3 flankDir = Vector3.Cross(orbitNormal, earthToMoonDir).normalized;
        if (Vector3.Dot(flankDir, siteRadialUp) < 0f)
            flankDir = -flankDir;

        float loiAltitudeRadius = moonRadius * 1.32f;   // Safe 32% radius parking orbit altitude
        float descentGateRadius = moonRadius * 1.18f;   // Steep descent gate 18% radius directly above site

        // LOI Gate sits on the lunar flank (90 deg from Earth-Moon axis), guaranteeing clear entry even for far-side targets
        Vector3 loiDir = (flankDir * 0.85f - earthToMoonDir * 0.52f).normalized;
        Vector3 loiGatePos = moonOrigin + loiDir * loiAltitudeRadius;
        Vector3 descentGatePos = moonOrigin + siteRadialUp * descentGateRadius;

        // Tangent vector entering the LOI gate along the lunar orbit
        Vector3 loiOrbitTangent = Vector3.Cross(orbitNormal, loiDir).normalized;
        if (Vector3.Dot(loiOrbitTangent, siteRadialUp - loiDir) < 0f)
            loiOrbitTangent = -loiOrbitTangent;

        float earthRadiusVisual = earthVisual.localScale.x * 0.5f;
        Vector3 p0 = padWorldPos;
        Vector3 p1 = p0 + padUp * (earthRadiusVisual * 2.4f) + earthToMoonDir * (earthRadiusVisual * 3.8f);
        Vector3 p2 = loiGatePos - loiOrbitTangent * (moonRadius * 1.6f);
        Vector3 p3 = loiGatePos;

        int transferCount = Mathf.RoundToInt(trajectorySegments * 0.65f);
        int orbitAndDescentCount = trajectorySegments - transferCount;
        int orbitCount = Mathf.Max(6, Mathf.RoundToInt(orbitAndDescentCount * 0.75f));
        int descentCount = Mathf.Max(3, orbitAndDescentCount - orbitCount);

        currentTrajectoryPoints = new Vector3[trajectorySegments];
        int idx = 0;

        // Stage 1: Translunar Cubic Bezier to LOI Gate
        for (int i = 0; i < transferCount; i++)
        {
            float t = i / (float)transferCount;
            currentTrajectoryPoints[idx++] = EvaluateCubicBezier(p0, p1, p2, p3, t);
        }

        // Stage 2A: Spherical Slerp Arc around Moon center from LOI Gate to Descent Gate above target
        for (int i = 0; i < orbitCount; i++)
        {
            float t = i / (float)orbitCount;
            Vector3 dir = Vector3.Slerp(loiDir, siteRadialUp, Mathf.SmoothStep(0f, 1f, t)).normalized;
            float rad = Mathf.Lerp(loiAltitudeRadius, descentGateRadius, t);
            currentTrajectoryPoints[idx++] = moonOrigin + dir * rad;
        }

        // Stage 2B: Final powered vertical descent from Descent Gate to exact surface landing site
        for (int i = 0; i < descentCount; i++)
        {
            float t = (descentCount == 1) ? 1f : i / (float)(descentCount - 1);
            currentTrajectoryPoints[idx++] = Vector3.Lerp(descentGatePos, targetSitePos, t);
        }

        // 4. Full Lunar Approach Terrain Clearance Check
        bool terrainClear = CheckApproachTerrainClearance(currentTrajectoryPoints, targetSitePos);

        Vector3 finalApproachVec = (currentTrajectoryPoints[trajectorySegments - 2] - targetSitePos).normalized;
        float approachAngleFromHorizon = 90f - Vector3.Angle(siteRadialUp, finalApproachVec);

        // Note: Far-side sites will naturally show NO-GO for direct Earth comms unless a relay satellite is used
        bool isGo = terrainClear && arrivalSunClear && arrivalEarthClear && approachAngleFromHorizon >= 20f;

        string verdict;
        if (isGo)
            verdict = "GO FOR LAUNCH — Clear lunar orbit, sunlit landing & Earth comms.";
        else if (!terrainClear)
            verdict = "NO-GO — Terrain obstruction along lunar approach corridor.";
        else if (!arrivalSunClear && !arrivalEarthClear)
            verdict = "NO-GO — Landing site in lunar night & far-side/blocked comms.";
        else if (!arrivalSunClear)
            verdict = "NO-GO — Landing site in lunar night at arrival.";
        else if (!arrivalEarthClear)
            verdict = "NO-GO — No direct Earth comms at touchdown (far-side/shadowed).";
        else
            verdict = "NO-GO — Approach angle too shallow.";

        if (trajectoryRenderer != null)
        {
            trajectoryRenderer.enabled = showTrajectory;
            trajectoryRenderer.positionCount = trajectorySegments;
            trajectoryRenderer.startWidth = trajectoryLineWidth;
            trajectoryRenderer.endWidth = trajectoryLineWidth * 0.5f;
            trajectoryRenderer.SetPositions(currentTrajectoryPoints);

            Color lineCol = isGo ? new Color(0.2f, 1f, 0.45f, 0.9f) : new Color(1f, 0.3f, 0.25f, 0.9f);
            trajectoryRenderer.startColor = new Color(0.3f, 0.8f, 1f, 0.9f);
            trajectoryRenderer.endColor = lineCol;
        }

        CurrentEvaluation = new LaunchWindowEvaluation
        {
            isValid = true,
            isGo = isGo,
            launchJ2000 = launchEpochJ2000,
            arrivalJ2000 = arrivalEpochJ2000,
            transitDays = dynamicTransitDays,
            launchUtcString = launchUtc,
            arrivalUtcString = arrivalUtc,
            approachTerrainClear = terrainClear,
            arrivalSunClear = arrivalSunClear,
            arrivalEarthClear = arrivalEarthClear,
            approachAngleDeg = Mathf.Clamp(approachAngleFromHorizon, 0f, 90f),
            verdictSummary = verdict
        };

        return CurrentEvaluation;
    }

    private void ScanOptimalWindows(double startJ2000)
    {
        OptimalWindows.Clear();
        currentOptimalWindowIndex = -1;

        double savedJ2000 = celestialSystem.j2000Days;
        bool wasPlaying = celestialSystem.isPlaying;
        celestialSystem.isPlaying = false;

        Vector3 targetSitePos = losSystem.selectedPoint;
        float eyeOffset = cameraController != null ? cameraController.eyeHeight : 0.05f;
        Vector3 losOrigin = targetSitePos + losSystem.selectedNormal * eyeOffset;

        for (int step = 0; step < 140; step++)
        {
            double candidateLaunch = startJ2000 + (step * 0.25);

            // Evaluate dynamic transit duration for each candidate launch epoch
            celestialSystem.j2000Days = candidateLaunch;
            celestialSystem.UpdateCelestialPositions();
            float stepTransitDays = ComputeDynamicTransitDays();

            double candidateArrival = candidateLaunch + stepTransitDays;
            celestialSystem.j2000Days = candidateArrival;
            celestialSystem.UpdateCelestialPositions();

            Vector3 toSun = (sunVisual.position - losOrigin).normalized;
            bool sunOk = !Physics.Raycast(losOrigin, toSun, Vector3.Distance(losOrigin, sunVisual.position));

            Vector3 toEarth = (earthVisual.position - losOrigin).normalized;
            bool earthOk = !Physics.Raycast(losOrigin, toEarth, Vector3.Distance(losOrigin, earthVisual.position));

            if (sunOk && earthOk)
            {
                string arrUtc = celestialSystem.GetFormattedUtcString();
                celestialSystem.j2000Days = candidateLaunch;
                celestialSystem.UpdateCelestialPositions();
                string lchUtc = celestialSystem.GetFormattedUtcString();

                if (OptimalWindows.Count == 0 || (candidateLaunch - OptimalWindows[OptimalWindows.Count - 1].launchJ2000) >= 1.0)
                {
                    OptimalWindows.Add(new LaunchWindowEvaluation
                    {
                        isValid = true,
                        isGo = true,
                        launchJ2000 = candidateLaunch,
                        arrivalJ2000 = candidateArrival,
                        transitDays = stepTransitDays,
                        launchUtcString = lchUtc,
                        arrivalUtcString = arrUtc,
                        approachTerrainClear = true,
                        arrivalSunClear = true,
                        arrivalEarthClear = true,
                        approachAngleDeg = 85f,
                        verdictSummary = "OPTIMAL WINDOW — Sunlit & Earth-connected landing."
                    });
                }
            }
        }

        celestialSystem.j2000Days = savedJ2000;
        celestialSystem.UpdateCelestialPositions();
        celestialSystem.isPlaying = wasPlaying;
    }

    public void StepToNextOptimalWindow(int direction)
    {
        if (OptimalWindows.Count == 0)
        {
            ScanOptimalWindows(celestialSystem.j2000Days);
        }
        if (OptimalWindows.Count == 0) return;

        currentOptimalWindowIndex = (currentOptimalWindowIndex + direction + OptimalWindows.Count) % OptimalWindows.Count;
        double targetLaunch = OptimalWindows[currentOptimalWindowIndex].launchJ2000;

        isAnimatingFlight = false;
        celestialSystem.isPlaying = false;
        celestialSystem.j2000Days = targetLaunch;
        celestialSystem.UpdateCelestialPositions();
        EvaluateAndBuildTrajectory(targetLaunch, false);
    }

    public void ToggleFlightAnimation()
    {
        if (currentTrajectoryPoints == null || currentTrajectoryPoints.Length < 2) return;

        isAnimatingFlight = !isAnimatingFlight;
        if (isAnimatingFlight)
        {
            animationProgress = 0f;
            if (spacecraftMarker != null) spacecraftMarker.SetActive(true);
        }
        else
        {
            if (spacecraftMarker != null) spacecraftMarker.SetActive(false);
            if (CurrentEvaluation.isValid)
            {
                celestialSystem.j2000Days = CurrentEvaluation.launchJ2000;
                celestialSystem.UpdateCelestialPositions();
            }
        }
    }

    private void UpdateFlightAnimation()
    {
        float transitScale = CurrentEvaluation.isValid ? (CurrentEvaluation.transitDays / nominalTransitDays) : 1f;
        float scaledAnimDuration = Mathf.Max(1.5f, baseAnimationSeconds * transitScale);

        animationProgress += Time.deltaTime / scaledAnimDuration;
        if (animationProgress > 1f) animationProgress = 0f;

        if (CurrentEvaluation.isValid && celestialSystem != null)
        {
            celestialSystem.j2000Days = Mathf.Lerp((float)CurrentEvaluation.launchJ2000, (float)CurrentEvaluation.arrivalJ2000, animationProgress);
            celestialSystem.UpdateCelestialPositions();
        }

        float exactIdx = animationProgress * (currentTrajectoryPoints.Length - 1);
        int idx0 = Mathf.FloorToInt(exactIdx);
        int idx1 = Mathf.Min(idx0 + 1, currentTrajectoryPoints.Length - 1);
        float frac = exactIdx - idx0;

        if (spacecraftMarker != null)
        {
            spacecraftMarker.transform.position = Vector3.Lerp(currentTrajectoryPoints[idx0], currentTrajectoryPoints[idx1], frac);
        }
    }

    private Vector3 GetEarthPadWorldPosition(LaunchSitePreset site, double j2000)
    {
        double earthSpinDeg = (j2000 * 360.985647366) % 360.0;
        float latRad = site.latitudeDeg * Mathf.Deg2Rad;
        float lonRad = (site.longitudeDeg + (float)earthSpinDeg) * Mathf.Deg2Rad;

        Vector3 localDir = new Vector3(
            Mathf.Cos(latRad) * Mathf.Sin(lonRad),
            Mathf.Sin(latRad),
            Mathf.Cos(latRad) * Mathf.Cos(lonRad)
        ).normalized;

        float earthRadius = earthVisual.localScale.x * 0.5f;
        return earthVisual.position + localDir * earthRadius;
    }

    private bool CheckApproachTerrainClearance(Vector3[] pts, Vector3 landingTarget)
    {
        int startIdx = Mathf.FloorToInt(pts.Length * 0.6f);
        for (int i = startIdx; i < pts.Length - 1; i++)
        {
            Vector3 segStart = pts[i];
            Vector3 segEnd = pts[i + 1];
            Vector3 dir = segEnd - segStart;
            float dist = dir.magnitude;
            if (dist < 0.001f) continue;

            if (Physics.Raycast(segStart, dir.normalized, out RaycastHit hit, dist))
            {
                if (Vector3.Distance(hit.point, landingTarget) > 5f)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        return (uu * u * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (tt * t * p3);
    }
}