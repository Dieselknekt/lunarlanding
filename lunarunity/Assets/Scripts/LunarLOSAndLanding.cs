using UnityEngine;

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

    [Header("Visual Marker (Optional)")]
    public Transform siteMarker;

    void Update()
    {
        // Left-click on the Moon in Orbital View to select and analyze a landing site
        if (orbitalCamera != null && orbitalCamera.gameObject.activeInHierarchy && Input.GetMouseButtonDown(0))
        {
            Ray ray = orbitalCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                SelectLandingSite(hit.point, hit.normal);
            }
        }

        // Continuously evaluate LOS for the selected site as simulation time progresses
        if (hasSelectedSite)
        {
            EvaluateLOS(selectedPoint, selectedNormal);

            // Press 'F' to land at the selected site and enter FPS POV
            if (orbitalCamera != null && orbitalCamera.gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.F))
            {
                EnterFPSAtSelectedSite();
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

        EvaluateLOS(point, normal);
        Debug.Log($"[Site Selected] Slope: {surfaceSlopeDegrees:F1} deg | Sun LOS: {hasSunLOS} | Earth LOS: {hasEarthLOS} | Press 'F' for FPS View");
    }

    public void EvaluateLOS(Vector3 surfacePoint, Vector3 normal)
    {
        // Offset origin slightly above the surface to prevent self-intersection with the terrain collider
        Vector3 origin = surfacePoint + normal * 0.25f;

        // 1. Line-of-Sight to the Sun
        Vector3 toSun = (sunVisual.position - origin).normalized;
        float distToSun = Vector3.Distance(origin, sunVisual.position);
        hasSunLOS = !Physics.Raycast(origin, toSun, distToSun);

        // 2. Line-of-Sight to the Earth (Optical + hook for radio wave diffraction)
        Vector3 toEarth = (earthVisual.position - origin).normalized;
        float distToEarth = Vector3.Distance(origin, earthVisual.position);
        bool directEarthLOS = !Physics.Raycast(origin, toEarth, distToEarth);

        hasEarthLOS = directEarthLOS || CheckRadioDiffraction(origin, earthVisual.position);
    }

    // Hook for Phase 2: Radio wave propagation / Fresnel knife-edge diffraction around crater rims
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