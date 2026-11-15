using System;
using UnityEngine;

[Serializable]
public struct MissionWindowReport
{
    public bool isValid;
    public float durationDays;
    public float stepHours;
    public float sunIlluminationPercent;
    public float earthCommsPercent;
    public float dualLosPercent;
    public float longestShadowHours;
    public float longestBlackoutHours;
    public float slopeDegrees;
    public string grade;
    public string summaryAssessment;
}

public class MissionWindowAnalyzer : MonoBehaviour
{
    [Header("System References")]
    public CelestialSystem celestialSystem;
    public LunarLOSAndLanding losSystem;
    public DualCameraController cameraController;

    [Header("Analysis Settings")]
    public float analysisDays = 30f;
    public float timeStepHours = 1f;

    public MissionWindowReport LastReport { get; private set; }

    public MissionWindowReport AnalyzeSelectedSite()
    {
        if (celestialSystem == null || losSystem == null || !losSystem.hasSelectedSite)
        {
            LastReport = new MissionWindowReport { isValid = false };
            return LastReport;
        }

        // Save current simulation epoch so we can restore it seamlessly after the fast-forward scan
        double originalJ2000 = celestialSystem.j2000Days;
        bool wasPlaying = celestialSystem.isPlaying;
        celestialSystem.isPlaying = false;

        Vector3 sitePoint = losSystem.selectedPoint;
        Vector3 siteNormal = losSystem.selectedNormal;
        float eyeOffset = cameraController != null ? cameraController.eyeHeight : 0.05f;
        Vector3 origin = sitePoint + siteNormal * eyeOffset;

        int totalSteps = Mathf.Max(1, Mathf.RoundToInt((analysisDays * 24f) / timeStepHours));
        double stepDays = timeStepHours / 24.0;

        int sunClearCount = 0;
        int earthClearCount = 0;
        int dualClearCount = 0;

        int currentShadowStreak = 0;
        int maxShadowStreak = 0;

        int currentBlackoutStreak = 0;
        int maxBlackoutStreak = 0;

        for (int i = 0; i < totalSteps; i++)
        {
            celestialSystem.j2000Days = originalJ2000 + (i * stepDays);
            celestialSystem.UpdateCelestialPositions();

            // Raycast toward Sun
            Vector3 toSun = (losSystem.sunVisual.position - origin).normalized;
            float distToSun = Vector3.Distance(origin, losSystem.sunVisual.position);
            bool sunClear = !Physics.Raycast(origin, toSun, distToSun);

            // Raycast toward Earth
            Vector3 toEarth = (losSystem.earthVisual.position - origin).normalized;
            float distToEarth = Vector3.Distance(origin, losSystem.earthVisual.position);
            bool earthClear = !Physics.Raycast(origin, toEarth, distToEarth);

            if (sunClear)
            {
                sunClearCount++;
                currentShadowStreak = 0;
            }
            else
            {
                currentShadowStreak++;
                if (currentShadowStreak > maxShadowStreak)
                    maxShadowStreak = currentShadowStreak;
            }

            if (earthClear)
            {
                earthClearCount++;
                currentBlackoutStreak = 0;
            }
            else
            {
                currentBlackoutStreak++;
                if (currentBlackoutStreak > maxBlackoutStreak)
                    maxBlackoutStreak = currentBlackoutStreak;
            }

            if (sunClear && earthClear)
                dualClearCount++;
        }

        // Restore exact initial simulation state
        celestialSystem.j2000Days = originalJ2000;
        celestialSystem.UpdateCelestialPositions();
        celestialSystem.isPlaying = wasPlaying;

        float sunPct = (sunClearCount / (float)totalSteps) * 100f;
        float earthPct = (earthClearCount / (float)totalSteps) * 100f;
        float dualPct = (dualClearCount / (float)totalSteps) * 100f;
        float maxShadowHrs = maxShadowStreak * timeStepHours;
        float maxBlackoutHrs = maxBlackoutStreak * timeStepHours;
        float slope = losSystem.surfaceSlopeDegrees;

        ComputeGradeAndSummary(slope, sunPct, earthPct, maxShadowHrs, out string grade, out string summary);

        MissionWindowReport report = new MissionWindowReport
        {
            isValid = true,
            durationDays = analysisDays,
            stepHours = timeStepHours,
            sunIlluminationPercent = sunPct,
            earthCommsPercent = earthPct,
            dualLosPercent = dualPct,
            longestShadowHours = maxShadowHrs,
            longestBlackoutHours = maxBlackoutHrs,
            slopeDegrees = slope,
            grade = grade,
            summaryAssessment = summary
        };

        LastReport = report;
        return report;
    }

    private void ComputeGradeAndSummary(float slope, float sunPct, float earthPct, float maxShadowHrs, out string grade, out string summary)
    {
        // Apollo / Artemis landing safety thresholds:
        // Slope > 15 deg is hazardous for lander tip-over.
        if (slope > 20f)
        {
            grade = "F (Hazardous Slope)";
            summary = "Terrain too steep for safe lander touchdown.";
            return;
        }

        float score = 0f;
        // Slope score (max 30 pts)
        score += Mathf.Clamp(30f - (slope * 1.5f), 0f, 30f);
        // Solar illumination score (max 40 pts; polar peaks reach 80-95%, equatorial ~50%)
        score += Mathf.Clamp((sunPct / 85f) * 40f, 0f, 40f);
        // Earth comms score (max 30 pts)
        score += Mathf.Clamp((earthPct / 95f) * 30f, 0f, 30f);

        if (score >= 82f && slope <= 10f)
        {
            grade = "A (Prime Artemis Site)";
            summary = "Gentle slope, high solar power & continuous Earth comms.";
        }
        else if (score >= 68f && slope <= 15f)
        {
            grade = "B (Mission Viable)";
            summary = "Safe terrain with solid power and communications windows.";
        }
        else if (score >= 50f)
        {
            grade = "C (Battery / Relay Needed)";
            summary = maxShadowHrs > 120f
                ? "Long lunar night requires heavy thermal/battery survival."
                : "Intermittent Earth LOS; orbital comms relay recommended.";
        }
        else if (score >= 35f)
        {
            grade = "D (Marginal)";
            summary = "Severe shadow or far-side comms blackout constraints.";
        }
        else
        {
            grade = "F (Unsuitable)";
            summary = "Extreme shadow/blackout or rough crater wall terrain.";
        }
    }
}