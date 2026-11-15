using UnityEngine;

public class CelestialSystem : MonoBehaviour
{
    [Header("Celestial Pivots")]
    public Transform sunPivot;
    public Transform earthPivot;

    [Header("Simulation Time (Earth Days)")]
    [Tooltip("Elapsed simulation time in Earth days. One sidereal lunar orbit is ~27.32 days; one synodic lunar day is ~29.53 days.")]
    public float simulationDays = 0f;
    [Tooltip("Simulation speed in Earth days per real-time second.")]
    public float timeSpeed = 1f;
    public bool isPlaying = true;

    [Header("Orbital Parameters")]
    [Tooltip("Lunar axial tilt relative to the ecliptic plane (degrees), responsible for permanent shadows and peaks of eternal light at the poles.")]
    public float lunarTiltDegrees = 6.68f;
    [Tooltip("Apparent oscillation amplitude of Earth in the lunar sky due to lunar libration (degrees).")]
    public float earthLibrationAmplitude = 7f;

    void Update()
    {
        if (isPlaying)
        {
            simulationDays += Time.deltaTime * timeSpeed;
        }
        UpdateCelestialPositions();
    }

    public void UpdateCelestialPositions()
    {
        // Apparent solar orbit around the Moon (1 full cycle per synodic month ~29.53 Earth days)
        float sunAngle = (simulationDays / 29.53f) * 360f;
        if (sunPivot != null)
        {
            sunPivot.rotation = Quaternion.Euler(lunarTiltDegrees, sunAngle, 0f);
        }

        // Apparent Earth position (nearly stationary due to tidal locking, oscillating via libration)
        float librationPhase = (simulationDays / 27.32f) * Mathf.PI * 2f;
        float earthYaw = Mathf.Sin(librationPhase) * earthLibrationAmplitude;
        float earthPitch = Mathf.Cos(librationPhase) * earthLibrationAmplitude;
        if (earthPivot != null)
        {
            earthPivot.rotation = Quaternion.Euler(earthPitch, earthYaw, 0f);
        }
    }
}