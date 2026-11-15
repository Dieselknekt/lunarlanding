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

    [Header("Visual Marker")]
    public Transform siteMarker;

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

        // 1. Always evaluate LOS for the selected Landing Site if one exists
        if (hasSelectedSite)
        {
            EvaluateLOSAtPoint(selectedPoint, selectedNormal, 1.7f, out hasSunLOS, out hasEarthLOS);

            // Press 'F' to land at the selected site and enter FPS POV
            if (inOrbit && keyboard.fKey.wasPressedThisFrame)
            {
                EnterFPSAtSelectedSite();
            }
        }

        // 2. Always evaluate LOS for the Astronaut (active FPS camera or saved PlayerMarker surface position)
        if (cameraController != null)
        {
            if (cameraController.IsFPSActive && cameraController.fpsCamera != null)
            {
                hasPlayerTelemetry = true;
                Vector3 playerEyePos = cameraController.fpsCamera.transform.position;
                Vector3 radialUp = (playerEyePos - moonCenter.position).normalized;
                EvaluateLOSAtPoint(playerEyePos, radialUp, 0f, out playerHasSunLOS, out playerHasEarthLOS);
            }
            else if (cameraController.hasSavedSurfacePos)
            {
                hasPlayerTelemetry = true;
                EvaluateLOSAtPoint(cameraController.savedSurfacePoint, cameraController.savedSurfaceUp, 1.7f, out playerHasSunLOS, out playerHasEarthLOS);
            }
            else
            {
                hasPlayerTelemetry = false;
            }
        }
    }

    public void SelectLandingSite(Vector3 point, Vector3 normal)
    {
        hasSelectedSite = true;
        selectedPoint = point;
        selectedNormal = normal;

        // Calculate terrain slope relative to the radial normal from the Moon's center
        Vector3 radialUp = (point - moonCenter.position).normalized;
        surfaceSlopeDegrees = Vector3.Angle(radialUp, normal);

        if (siteMarker != null)
        {
            siteMarker.gameObject.SetActive(true);
            siteMarker.position = point;
            siteMarker.up = radialUp;
        }

        EvaluateLOSAtPoint(point, normal, 1.7f, out hasSunLOS, out hasEarthLOS);
        Debug.Log($"[Site Selected] Slope: {surfaceSlopeDegrees:F1} deg | Sun LOS: {hasSunLOS} | Earth LOS: {hasEarthLOS}");
    }

    public void EvaluateLOSAtPoint(Vector3 surfacePoint, Vector3 normal, float heightOffset, out bool sunClear, out bool earthClear)
    {
        Vector3 origin = surfacePoint + normal * heightOffset;

        // 1. Line-of-Sight to the Sun
        Vector3 toSun = (sunVisual.position - origin).normalized;
        float distToSun = Vector3.Distance(origin, sunVisual.position);
        sunClear = !Physics.Raycast(origin, toSun, distToSun);

        // 2. Line-of-Sight to the Earth (Optical + hook for radio wave diffraction)
        Vector3 toEarth = (earthVisual.position - origin).normalized;
        float distToEarth = Vector3.Distance(origin, earthVisual.position);
        bool directEarthLOS = !Physics.Raycast(origin, toEarth, distToEarth);

        earthClear = directEarthLOS || CheckRadioDiffraction(origin, earthVisual.position);
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