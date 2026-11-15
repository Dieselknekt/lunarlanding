using UnityEngine;

public class DualCameraController : MonoBehaviour
{
    [Header("Cameras & Target")]
    public Camera orbitalCamera;
    public Camera fpsCamera;
    public Transform moonCenter;

    [Header("Orbital View Settings")]
    public float orbitDistance = 650f;
    public float minOrbitDistance = 65f;
    public float maxOrbitDistance = 1500f;
    public float orbitSpeed = 200f;
    public float zoomSpeed = 250f;
    private float yaw = 35f;
    private float pitch = 25f;

    [Header("Lunar Surface FPS Settings")]
    public float walkSpeed = 5f;
    public float lookSensitivity = 2f;
    private float fpsPitch = 0f;
    private bool isFPS = false;

    void Start()
    {
        SwitchToOrbital();
    }

    void Update()
    {
        // Press Tab to return from surface FPS view to Orbital View
        if (isFPS && Input.GetKeyDown(KeyCode.Tab))
        {
            SwitchToOrbital();
        }

        if (isFPS)
            HandleFPS();
        else
            HandleOrbital();
    }

    void HandleOrbital()
    {
        if (Input.GetMouseButton(1)) // Hold Right Mouse Button to orbit
        {
            yaw += Input.GetAxis("Mouse X") * orbitSpeed * Time.deltaTime;
            pitch -= Input.GetAxis("Mouse Y") * orbitSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -85f, 85f);
        }

        orbitDistance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        orbitDistance = Mathf.Clamp(orbitDistance, minOrbitDistance, maxOrbitDistance);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        orbitalCamera.transform.position = moonCenter.position - (rot * Vector3.forward * orbitDistance);
        orbitalCamera.transform.LookAt(moonCenter.position);
    }

    void HandleFPS()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        fpsCamera.transform.Rotate(Vector3.up, mouseX, Space.Self);
        fpsPitch = Mathf.Clamp(fpsPitch - mouseY, -85f, 85f);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = (fpsCamera.transform.right * h + fpsCamera.transform.forward * v) * walkSpeed * Time.deltaTime;
        fpsCamera.transform.position += move;

        // Keep player anchored 1.7 units above the lunar surface along the radial normal
        Vector3 radialUp = (fpsCamera.transform.position - moonCenter.position).normalized;
        if (Physics.Raycast(moonCenter.position + radialUp * 200f, -radialUp, out RaycastHit hit, 300f))
        {
            fpsCamera.transform.position = hit.point + radialUp * 1.7f;
        }

        Vector3 forwardProjected = Vector3.ProjectOnPlane(fpsCamera.transform.forward, radialUp).normalized;
        Quaternion baseRot = Quaternion.LookRotation(forwardProjected, radialUp);
        fpsCamera.transform.rotation = baseRot * Quaternion.Euler(fpsPitch, 0f, 0f);
    }

    public void SwitchToFPS(Vector3 surfacePoint, Vector3 radialUp)
    {
        isFPS = true;
        orbitalCamera.gameObject.SetActive(false);
        fpsCamera.gameObject.SetActive(true);

        fpsCamera.transform.position = surfacePoint + radialUp * 1.7f;
        Vector3 tangentForward = Vector3.Cross(radialUp, Vector3.right);
        if (tangentForward.sqrMagnitude < 0.01f) tangentForward = Vector3.Cross(radialUp, Vector3.forward);
        fpsCamera.transform.rotation = Quaternion.LookRotation(tangentForward.normalized, radialUp);
        fpsPitch = 0f;

        Cursor.lockState = CursorLockMode.Locked;
    }

    public void SwitchToOrbital()
    {
        isFPS = false;
        fpsCamera.gameObject.SetActive(false);
        orbitalCamera.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
    }
}