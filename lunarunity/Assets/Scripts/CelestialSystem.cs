using System;
using UnityEngine;

public class CelestialSystem : MonoBehaviour
{
    [Header("Celestial Pivots")]
    public Transform sunPivot;
    public Transform earthPivot;

    [Header("Start Date & Time (UTC)")]
    public int startYear = 2026;
    public int startMonth = 11;
    public int startDay = 15;
    public int startHour = 12;
    public int startMinute = 0;

    [Header("Simulation Time")]
    [Tooltip("Days elapsed since J2000.0 (2000-01-01 12:00:00 UTC). Driven automatically from Start Date.")]
    public double j2000Days;
    [Tooltip("Simulation speed in Earth days per real-time second (1.0 = 1 day/sec, 0.00001157 = real-time).")]
    public float timeSpeed = 0.05f;
    public bool isPlaying = true;

    private static readonly DateTime J2000Epoch = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    void Awake()
    {
        ResetToStartDate();
    }

    void Update()
    {
        if (isPlaying)
        {
            j2000Days += (double)Time.deltaTime * timeSpeed;
        }
        UpdateCelestialPositions();
    }

    public void ResetToStartDate()
    {
        try
        {
            DateTime startUtc = new DateTime(
                Mathf.Clamp(startYear, 1900, 2200),
                Mathf.Clamp(startMonth, 1, 12),
                Mathf.Clamp(startDay, 1, DateTime.DaysInMonth(Mathf.Clamp(startYear, 1900, 2200), Mathf.Clamp(startMonth, 1, 12))),
                Mathf.Clamp(startHour, 0, 23),
                Mathf.Clamp(startMinute, 0, 59),
                0,
                DateTimeKind.Utc
            );
            SetDateTimeUtc(startUtc);
        }
        catch
        {
            SetDateTimeUtc(DateTime.UtcNow);
        }
    }

    public void SetDateTimeUtc(DateTime utcTime)
    {
        j2000Days = (utcTime - J2000Epoch).TotalDays;
        UpdateCelestialPositions();
    }

    public DateTime GetCurrentDateTimeUtc()
    {
        return J2000Epoch.AddDays(j2000Days);
    }

    public string GetFormattedUtcString()
    {
        return GetCurrentDateTimeUtc().ToString("yyyy-MM-dd HH:mm 'UTC'");
    }

    public void UpdateCelestialPositions()
    {
        double d = j2000Days;

        // Fundamental astronomical arguments (in degrees) since J2000.0
        // Sun's mean longitude (L_sun) and mean anomaly (M_sun)
        double L_sun = NormalizeDeg(280.460 + 0.9856474 * d);
        double M_sun = NormalizeDeg(357.528 + 0.9856003 * d) * Mathf.Deg2Rad;
        double lambdaSun = L_sun + 1.915 * Math.Sin(M_sun) + 0.020 * Math.Sin(2.0 * M_sun);

        // Moon's mean longitude (L_moon), mean anomaly (M_moon), and ascending node (Omega)
        double L_moon = NormalizeDeg(218.316 + 13.176396 * d);
        double M_moon = NormalizeDeg(134.963 + 13.064993 * d) * Mathf.Deg2Rad;
        double Omega = NormalizeDeg(125.045 - 0.052954 * d) * Mathf.Deg2Rad;
        double F = NormalizeDeg(93.272 + 13.229350 * d) * Mathf.Deg2Rad;

        // True ecliptic longitude & latitude of the Moon
        double lambdaMoon = L_moon + 6.289 * Math.Sin(M_moon);
        double betaMoon = 5.128 * Math.Sin(F); // Inclination of lunar orbit ~5.145 deg

        // Selenographic Sun direction (sub-solar longitude & latitude on the Moon)
        // Sub-solar longitude is driven by the synodic elongation between Sun and Moon
        double subSolarLon = NormalizeSignedDeg(lambdaSun - lambdaMoon);
        // Sub-solar latitude oscillates by +/- 1.54 deg (lunar axial tilt to ecliptic)
        double subSolarLat = 1.5424 * Math.Sin((lambdaSun * Mathf.Deg2Rad) - Omega);

        if (sunPivot != null)
        {
            // Note: SunVisual is placed at local +Z (0, 0, +dist), so we orient +Z toward the sub-solar point
            sunPivot.rotation = Quaternion.Euler(-(float)subSolarLat, (float)subSolarLon, 0f);
        }

        // Selenographic Earth direction (optical libration in longitude and latitude)
        // Earth sits near (0 lat, 0 lon) in selenographic coords, oscillating due to libration
        double librationLon = -6.289 * Math.Sin(M_moon);
        double librationLat = -betaMoon - 1.5424 * Math.Sin(F);

        if (earthPivot != null)
        {
            earthPivot.rotation = Quaternion.Euler(-(float)librationLat, (float)librationLon, 0f);
        }
    }

    private double NormalizeDeg(double deg)
    {
        deg %= 360.0;
        if (deg < 0) deg += 360.0;
        return deg;
    }

    private double NormalizeSignedDeg(double deg)
    {
        deg = NormalizeDeg(deg);
        if (deg > 180.0) deg -= 360.0;
        return deg;
    }
}