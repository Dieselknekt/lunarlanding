using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class LunarLOSAndLanding : MonoBehaviour
{
    [Header("References")]
    public Camera orbitalCamera;
    public Transform moonCenter;
    public Transform sunVisual;
    public Transform earthVisual;
    public DualCameraController cameraController;

    [Header("Selected Landing Site Telemetry")]
    public bool hasSelectedSite = false;
    public Vector3 selectedPoint;
    public Vector3 selectedNormal;
    public bool hasSunLOS;
    public bool hasEarthLOS;
    public float surfaceSlopeDegrees;

    [Header("Player / Astronaut Telemetry")]
    public bool hasPlayerTelemetry = false;
    public bool playerHasSunLOS;
    public bool playerHasEarthLOS;

    [Header("Visual Marker & 3D LOS Beams")]
    public float beamThicknessMultiplier = 6f;
    public bool scaleBeamWithOrbitDistance = true;
    public Transform siteMarker;
    public bool showLosBeams = true;
    public float maxVisualBeamLength = 3500f;
    public float normalBeaconLength = 600f;
    private LineRenderer siteNormalBeam;
    private LineRenderer playerNormalBeam;
    private LineRenderer siteSunBeam;
    private LineRenderer siteEarthBeam;
    private LineRenderer playerSunBeam;
    private LineRenderer playerEarthBeam;

    void Start()
    {
        siteSunBeam = CreateBeamRenderer("SiteSunLOSBeam", 1.8f);
        siteEarthBeam = CreateBeamRenderer("SiteEarthLOSBeam", 1.8f);
        playerSunBeam = CreateBeamRenderer("PlayerSunLOSBeam", 1.2f);
        playerEarthBeam = CreateBeamRenderer("PlayerEarthLOSBeam", 1.2f);
        siteNormalBeam = CreateBeamRenderer("SiteNormalBeacon", 1.5f);
        playerNormalBeam = CreateBeamRenderer("PlayerNormalBeacon", 1.5f);
    }

    private LineRenderer CreateBeamRenderer(string beamName, float width)
    {
        GameObject obj = new GameObject(beamName);
        obj.transform.SetParent(transform);
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = width;
        lr.endWidth = width * 0.5f;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // Uses Unity's built-in Sprites/Default shader so vertex colors render cleanly in URP without lighting issues
        Shader unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader != null)
            lr.material = new Material(unlitShader);

        lr.enabled = false;
        return lr;
    }

void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        bool inOrbit = orbitalCamera != null && orbitalCamera.gameObject.activeInHierarchy;

        // Left-click on the Moon in Orbital View to select and analyze a landing site
        if (inOrbit && mouse.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = orbitalCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                SelectLandingSite(hit.point, hit.normal);
            }
        }

        float currentEyeOffset = cameraController != null ? cameraController.eyeHeight : 0.05f;

        float orbitDistanceFactor = 1f;
        if (inOrbit && scaleBeamWithOrbitDistance && orbitalCamera != null)
        {
            float distToMoon = Vector3.Distance(orbitalCamera.transform.position, moonCenter.position);
            orbitDistanceFactor = Mathf.Max(1f, distToMoon / 1800f);
        }

        float baseScale = inOrbit
            ? orbitDistanceFactor
            : (cameraController != null && cameraController.isDebugScale ? 0.15f : 0.02f);
        float beamScale = baseScale * beamThicknessMultiplier;
        float beaconLen = inOrbit ? normalBeaconLength : (cameraController != null && cameraController.isDebugScale ? 25f : 2.5f);

        // 1. Evaluate & visualize LOS + Radial Normal Beacon for the selected Landing Site
        if (hasSelectedSite)
        {
            EvaluateAndDrawLOS(
                selectedPoint, selectedNormal, currentEyeOffset,
                out hasSunLOS, out hasEarthLOS,
                siteSunBeam, siteEarthBeam,
                new Color(1f, 0.85f, 0.2f, 0.9f), // Warm Gold for Sun
                new Color(0.25f, 0.75f, 1f, 0.9f), // Cyan-Blue for Earth
                beamScale
            );

            Vector3 siteRadialUp = (selectedPoint - moonCenter.position).normalized;
            DrawNormalBeacon(siteNormalBeam, selectedPoint, siteRadialUp, beaconLen, beamScale * 0.9f, new Color(1f, 0.35f, 0.45f, 0.85f));

            if (inOrbit && keyboard.fKey.wasPressedThisFrame)
            {
                EnterFPSAtSelectedSite();
            }
        }
        else
        {
            SetBeamEnabled(siteSunBeam, false);
            SetBeamEnabled(siteEarthBeam, false);
            SetBeamEnabled(siteNormalBeam, false);
        }

        // 2. Evaluate & visualize LOS + Radial Normal Beacon for the Astronaut
        if (cameraController != null)
        {
            if (cameraController.IsFPSActive && cameraController.fpsCamera != null)
            {
                hasPlayerTelemetry = true;
                Vector3 playerEyePos = cameraController.fpsCamera.transform.position;
                Vector3 radialUp = (playerEyePos - moonCenter.position).normalized;
                Vector3 chestOrigin = playerEyePos - radialUp * (currentEyeOffset * 0.35f);

                EvaluateAndDrawLOS(
                    chestOrigin, radialUp, 0f,
                    out playerHasSunLOS, out playerHasEarthLOS,
                    playerSunBeam, playerEarthBeam,
                    new Color(0.5f, 1f, 0.4f, 0.8f),
                    new Color(0.3f, 0.95f, 0.85f, 0.8f),
                    beamScale * 0.75f
                );

                // Hide the player's vertical zenith beam while inside FPS POV so it doesn't obstruct overhead view
                SetBeamEnabled(playerNormalBeam, false);
            }
            else if (cameraController.hasSavedSurfacePos)
            {
                hasPlayerTelemetry = true;
                EvaluateAndDrawLOS(
                    cameraController.savedSurfacePoint, cameraController.savedSurfaceUp, currentEyeOffset,
                    out playerHasSunLOS, out playerHasEarthLOS,
                    playerSunBeam, playerEarthBeam,
                    new Color(0.5f, 1f, 0.4f, 0.85f),
                    new Color(0.3f, 0.95f, 0.85f, 0.85f),
                    beamScale * 0.85f
                );

                DrawNormalBeacon(playerNormalBeam, cameraController.savedSurfacePoint, cameraController.savedSurfaceUp, beaconLen, beamScale * 0.9f, new Color(0.35f, 1f, 0.55f, 0.85f));
            }
            else
            {
                hasPlayerTelemetry = false;
                SetBeamEnabled(playerSunBeam, false);
                SetBeamEnabled(playerEarthBeam, false);
                SetBeamEnabled(playerNormalBeam, false);
            }
        }
    }

    private void DrawNormalBeacon(LineRenderer beam, Vector3 surfaceOrigin, Vector3 radialUp, float length, float widthScale, Color beaconColor)
    {
        if (beam == null) return;
        SetBeamEnabled(beam, showLosBeams);
        if (!showLosBeams) return;

        beam.startWidth = 1.4f * widthScale;
        beam.endWidth = 0.2f * widthScale; // Tapers to a sharp zenith pointer in space
        beam.SetPosition(0, surfaceOrigin);
        beam.SetPosition(1, surfaceOrigin + radialUp * length);

        Color tipColor = new Color(beaconColor.r, beaconColor.g, beaconColor.b, 0.15f);
        beam.startColor = beaconColor;
        beam.endColor = tipColor;
    }

    public void SelectLandingSite(Vector3 point, Vector3 normal)
    {
        hasSelectedSite = true;
        selectedPoint = point;
        selectedNormal = normal;

        Vector3 radialUp = (point - moonCenter.position).normalized;
        surfaceSlopeDegrees = Vector3.Angle(radialUp, normal);

        if (siteMarker != null)
        {
            siteMarker.gameObject.SetActive(true);
            siteMarker.position = point;
            siteMarker.up = radialUp;
        }

        float currentEyeOffset = cameraController != null ? cameraController.eyeHeight : 0.05f;
        EvaluateAndDrawLOS(point, normal, currentEyeOffset, out hasSunLOS, out hasEarthLOS, null, null, Color.white, Color.white, 1f);
        Debug.Log($"[Site Selected] Slope: {surfaceSlopeDegrees:F1} deg | Sun LOS: {hasSunLOS} | Earth LOS: {hasEarthLOS}");
    }

    /// <summary>
    /// Selects a landing site directly from selenographic latitude & longitude in degrees (e.g., -89.54f, 0f for Shackleton Rim).
    /// </summary>
    public void SelectSiteByCoordinates(float latitudeDeg, float longitudeDeg, string siteName = "")
    {
        float latRad = latitudeDeg * Mathf.Deg2Rad;
        float lonRad = longitudeDeg * Mathf.Deg2Rad;

        // Inverse of CelestialSystem / NavigatorUI coordinate convention
        Vector3 radialDir = new Vector3(
            Mathf.Cos(latRad) * Mathf.Sin(lonRad),
            Mathf.Sin(latRad),
            Mathf.Cos(latRad) * Mathf.Cos(lonRad)
        ).normalized;

        float rayStartDist = moonCenter.localScale.x * 1.5f + 500f;
        Vector3 rayOrigin = moonCenter.position + radialDir * rayStartDist;

        if (Physics.Raycast(rayOrigin, -radialDir, out RaycastHit hit, rayStartDist * 1.5f))
        {
            SelectLandingSite(hit.point, hit.normal);
            if (cameraController != null)
            {
                cameraController.FocusOrbitalOnDirection(radialDir);
            }
            if (!string.IsNullOrEmpty(siteName))
            {
                Debug.Log($"[Bookmark Selected] {siteName} ({latitudeDeg:F2}°, {longitudeDeg:F2}°)");
            }
        }
    }

    public void EvaluateAndDrawLOS(
        Vector3 surfacePoint, Vector3 normal, float heightOffset,
        out bool sunClear, out bool earthClear,
        LineRenderer sunBeam, LineRenderer earthBeam,
        Color clearSunColor, Color clearEarthColor, float widthScale)
    {
        Vector3 origin = surfacePoint + normal * heightOffset;
        Color blockedColor = new Color(0.95f, 0.2f, 0.2f, 0.95f);

        // 1. Line-of-Sight to the Sun
        Vector3 toSun = (sunVisual.position - origin).normalized;
        float distToSun = Vector3.Distance(origin, sunVisual.position);
        bool hitSunObstruction = Physics.Raycast(origin, toSun, out RaycastHit sunHit, distToSun);
        sunClear = !hitSunObstruction;

        if (sunBeam != null)
        {
            SetBeamEnabled(sunBeam, showLosBeams);
            if (showLosBeams)
            {
                sunBeam.startWidth = 1.6f * widthScale;
                sunBeam.endWidth = 1.2f * widthScale;
                sunBeam.SetPosition(0, origin);
                Vector3 endPos = hitSunObstruction ? sunHit.point : origin + toSun * Mathf.Min(distToSun, maxVisualBeamLength);
                sunBeam.SetPosition(1, endPos);
                Color c = sunClear ? clearSunColor : blockedColor;
                sunBeam.startColor = c;
                sunBeam.endColor = c;
            }
        }

        // 2. Line-of-Sight to the Earth
        Vector3 toEarth = (earthVisual.position - origin).normalized;
        float distToEarth = Vector3.Distance(origin, earthVisual.position);
        bool hitEarthObstruction = Physics.Raycast(origin, toEarth, out RaycastHit earthHit, distToEarth);
        bool directEarthLOS = !hitEarthObstruction;

        earthClear = directEarthLOS || CheckRadioDiffraction(origin, earthVisual.position);

        if (earthBeam != null)
        {
            SetBeamEnabled(earthBeam, showLosBeams);
            if (showLosBeams)
            {
                earthBeam.startWidth = 1.6f * widthScale;
                earthBeam.endWidth = 1.2f * widthScale;
                earthBeam.SetPosition(0, origin);
                Vector3 endPos = hitEarthObstruction ? earthHit.point : origin + toEarth * Mathf.Min(distToEarth, maxVisualBeamLength);
                earthBeam.SetPosition(1, endPos);
                Color c = earthClear ? clearEarthColor : blockedColor;
                earthBeam.startColor = c;
                earthBeam.endColor = c;
            }
        }
    }

    private void SetBeamEnabled(LineRenderer lr, bool enabled)
    {
        if (lr != null && lr.enabled != enabled)
            lr.enabled = enabled;
    }

    private bool CheckRadioDiffraction(Vector3 origin, Vector3 target)
    {
        // TODO: Implement Fresnel zone / knife-edge diffraction loss calculation
        return false;
    }

    public void EnterFPSAtSelectedSite()
    {
        if (hasSelectedSite && cameraController != null)
        {
            Vector3 radialUp = (selectedPoint - moonCenter.position).normalized;
            cameraController.SwitchToFPS(selectedPoint, radialUp);
        }
    }
}