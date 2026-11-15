using UnityEngine;
using UnityEngine.InputSystem;

public class DualCameraController : MonoBehaviour
{
    [Header("Cameras & Target")]
    public Camera orbitalCamera;
    public Camera fpsCamera;
    public Transform moonCenter;
    public Transform siteMarker;
    public Transform playerMarker;

    [Header("Orbital View Settings")]
    public float orbitDistance = 2500f;
    public float minOrbitDistance = 1050f;
    public float maxOrbitDistance = 8000f;
    public float orbitSpeed = 0.15f;
    public float zoomSpeed = 0.25f;
    public float zoomSprintMultiplier = 5f;
    private float yaw = 35f;
    private float pitch = 25f;

    [Header("Camera Transition Settings")]
    public float transitionDuration = 1.5f;
    private bool isTransitioning = false;
    private bool transitioningToFPS = false;
    private float transitionTimer = 0f;

    private Vector3 transStartPos;
    private Quaternion transStartRot;
    private Vector3 transEndPos;
    private Quaternion transEndRot;

    private Vector3 savedOrbitalPos;
    private Quaternion savedOrbitalRot;
    private Vector3 initialMarkerScale = Vector3.one;

    [Header("Saved Astronaut Surface Position")]
    public bool hasSavedSurfacePos = false;
    public Vector3 savedSurfacePoint;
    public Vector3 savedSurfaceUp;

    [Header("Lunar Surface FPS Settings")]
    public float walkSpeed = 12f;
    public float sprintSpeed = 35f;
    public float flySpeed = 60f;
    public float lookSensitivity = 0.04f;
    public float lunarGravity = 9.8f;
    public float jumpForce = 7.5f;

    private float fpsPitch = 0f;
    private bool isFPS = false;
    private bool isNoclip = false;
    private bool isCursorUnlocked = false;
    private float verticalVelocity = 0f;
    private float currentHeightAboveSurface = 1.7f;
    private const float EyeHeight = 1.7f;

    public bool IsFPSActive => isFPS;
    public bool IsCursorUnlocked => isCursorUnlocked;

    void Start()
    {
        if (siteMarker != null)
        {
            initialMarkerScale = siteMarker.localScale;
        }
        if (playerMarker != null)
        {
            playerMarker.gameObject.SetActive(false);
        }
        SetImmediateOrbital();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (isTransitioning)
        {
            HandleTransition();
            return;
        }

        // Press Tab to toggle between Lunar Surface (FPS) and Orbital View (returning to last surface pos)
        if (keyboard.tabKey.wasPressedThisFrame)
        {
            if (isFPS)
            {
                StartTransitionToOrbital();
                return;
            }
            else if (hasSavedSurfacePos)
            {
                ReturnToLastSurfacePosition();
                return;
            }
        }

        // Press Left Alt or C in FPS mode to unlock cursor for UI interaction
        if (isFPS && (keyboard.leftAltKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame))
        {
            SetCursorUnlocked(!isCursorUnlocked);
        }

        // Press G in FPS mode to toggle between Lunar Gravity and Noclip Fly mode
        if (isFPS && keyboard.gKey.wasPressedThisFrame)
        {
            isNoclip = !isNoclip;
            verticalVelocity = 0f;
            Debug.Log(isNoclip ? "[Movement Mode] Noclip Free-Fly Enabled" : "[Movement Mode] Lunar Gravity Enabled");
        }

        if (isFPS)
            HandleFPS(keyboard, mouse);
        else
            HandleOrbital(keyboard, mouse);
    }

    public void SetCursorUnlocked(bool unlocked)
    {
        isCursorUnlocked = unlocked;
        Cursor.lockState = unlocked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = unlocked;
    }

    void HandleOrbital(Keyboard keyboard, Mouse mouse)
    {
        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * orbitSpeed * Time.deltaTime;
            pitch -= delta.y * orbitSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -85f, 85f);
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float currentZoomSpeed = keyboard.leftShiftKey.isPressed ? zoomSpeed * zoomSprintMultiplier : zoomSpeed;
            orbitDistance -= scroll * currentZoomSpeed;
            orbitDistance = Mathf.Clamp(orbitDistance, minOrbitDistance, maxOrbitDistance);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        orbitalCamera.transform.position = moonCenter.position - (rot * Vector3.forward * orbitDistance);
        orbitalCamera.transform.LookAt(moonCenter.position);
    }

    void HandleFPS(Keyboard keyboard, Mouse mouse)
    {
        if (!isCursorUnlocked)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue() * lookSensitivity;
            fpsCamera.transform.Rotate(Vector3.up, mouseDelta.x, Space.Self);
            fpsPitch = Mathf.Clamp(fpsPitch - mouseDelta.y, -85f, 85f);
        }

        float h = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float v = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);

        Vector3 radialUp = (fpsCamera.transform.position - moonCenter.position).normalized;

        if (isNoclip)
        {
            float upInput = (keyboard.spaceKey.isPressed ? 1f : 0f) - (keyboard.leftShiftKey.isPressed ? 1f : 0f);
            Vector3 flyMove = (fpsCamera.transform.right * h + fpsCamera.transform.forward * v + radialUp * upInput) * flySpeed * Time.deltaTime;
            fpsCamera.transform.position += flyMove;
        }
        else
        {
            float currentSpeed = keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
            Vector3 moveDir = Vector3.ProjectOnPlane(fpsCamera.transform.right * h + fpsCamera.transform.forward * v, radialUp).normalized;
            fpsCamera.transform.position += moveDir * currentSpeed * Time.deltaTime;

            radialUp = (fpsCamera.transform.position - moonCenter.position).normalized;
            float surfaceRadius = GetSurfaceRadiusAtDirection(radialUp);

            bool isGrounded = currentHeightAboveSurface <= EyeHeight + 0.05f;
            if (isGrounded && keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = jumpForce;
                isGrounded = false;
            }

            if (!isGrounded || verticalVelocity > 0f)
            {
                verticalVelocity -= lunarGravity * Time.deltaTime;
                currentHeightAboveSurface += verticalVelocity * Time.deltaTime;
            }

            if (currentHeightAboveSurface <= EyeHeight)
            {
                currentHeightAboveSurface = EyeHeight;
                verticalVelocity = 0f;
            }

            fpsCamera.transform.position = moonCenter.position + radialUp * (surfaceRadius + currentHeightAboveSurface);
        }

        Vector3 finalUp = (fpsCamera.transform.position - moonCenter.position).normalized;
        Vector3 forwardProjected = Vector3.ProjectOnPlane(fpsCamera.transform.forward, finalUp).normalized;
        if (forwardProjected.sqrMagnitude < 0.001f)
            forwardProjected = Vector3.Cross(finalUp, Vector3.right).normalized;

        Quaternion baseRot = Quaternion.LookRotation(forwardProjected, finalUp);
        fpsCamera.transform.rotation = baseRot * Quaternion.Euler(fpsPitch, 0f, 0f);
    }

    private float GetSurfaceRadiusAtDirection(Vector3 radialUp)
    {
        float maxCheckDist = moonCenter.localScale.x * 2f + 500f;
        if (Physics.Raycast(moonCenter.position + radialUp * maxCheckDist, -radialUp, out RaycastHit hit, maxCheckDist * 1.5f))
        {
            return Vector3.Distance(moonCenter.position, hit.point);
        }
        return moonCenter.localScale.x * 0.5f;
    }

    public void SwitchToFPS(Vector3 surfacePoint, Vector3 radialUp)
    {
        if (isTransitioning) return;

        savedOrbitalPos = orbitalCamera.transform.position;
        savedOrbitalRot = orbitalCamera.transform.rotation;

        if (siteMarker != null && !isFPS)
        {
            initialMarkerScale = siteMarker.localScale;
        }

        // Hide green PlayerMarker while astronaut is active on the surface
        if (playerMarker != null)
        {
            playerMarker.gameObject.SetActive(false);
        }

        transStartPos = savedOrbitalPos;
        transStartRot = savedOrbitalRot;
        transEndPos = surfacePoint + radialUp * EyeHeight;

        Vector3 tangentForward = Vector3.Cross(radialUp, Vector3.right);
        if (tangentForward.sqrMagnitude < 0.01f) tangentForward = Vector3.Cross(radialUp, Vector3.forward);
        transEndRot = Quaternion.LookRotation(tangentForward.normalized, radialUp);

        fpsPitch = 0f;
        currentHeightAboveSurface = EyeHeight;
        verticalVelocity = 0f;
        isNoclip = false;
        isCursorUnlocked = false;

        transitioningToFPS = true;
        isTransitioning = true;
        transitionTimer = 0f;
    }

    public void ReturnToLastSurfacePosition()
    {
        if (hasSavedSurfacePos && !isFPS)
        {
            SwitchToFPS(savedSurfacePoint, savedSurfaceUp);
        }
    }

    private void StartTransitionToOrbital()
    {
        if (isTransitioning) return;

        // Save astronaut's exact surface coordinates and place the green PlayerMarker there
        Vector3 radialUp = (fpsCamera.transform.position - moonCenter.position).normalized;
        float surfaceRadius = GetSurfaceRadiusAtDirection(radialUp);
        savedSurfaceUp = radialUp;
        savedSurfacePoint = moonCenter.position + radialUp * surfaceRadius;
        hasSavedSurfacePos = true;

        if (playerMarker != null)
        {
            playerMarker.gameObject.SetActive(true);
            playerMarker.position = savedSurfacePoint;
            playerMarker.up = savedSurfaceUp;
        }

        transStartPos = fpsCamera.transform.position;
        transStartRot = fpsCamera.transform.rotation;
        transEndPos = savedOrbitalPos;
        transEndRot = savedOrbitalRot;

        fpsCamera.gameObject.SetActive(false);
        orbitalCamera.gameObject.SetActive(true);
        orbitalCamera.transform.position = transStartPos;
        orbitalCamera.transform.rotation = transStartRot;

        isFPS = false;
        isCursorUnlocked = false;
        transitioningToFPS = false;
        isTransitioning = true;
        transitionTimer = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HandleTransition()
    {
        transitionTimer += Time.deltaTime;
        float t = Mathf.Clamp01(transitionTimer / transitionDuration);
        float smoothT = t * t * (3f - 2f * t);

        orbitalCamera.transform.position = Vector3.Lerp(transStartPos, transEndPos, smoothT);
        orbitalCamera.transform.rotation = Quaternion.Slerp(transStartRot, transEndRot, smoothT);

        if (siteMarker != null)
        {
            Vector3 fromScale = transitioningToFPS ? initialMarkerScale : initialMarkerScale * 0.1f;
            Vector3 toScale = transitioningToFPS ? initialMarkerScale * 0.1f : initialMarkerScale;
            siteMarker.localScale = Vector3.Lerp(fromScale, toScale, smoothT);
        }

        if (t >= 1f)
        {
            isTransitioning = false;
            if (transitioningToFPS)
            {
                isFPS = true;
                orbitalCamera.gameObject.SetActive(false);
                fpsCamera.gameObject.SetActive(true);
                fpsCamera.transform.position = transEndPos;
                fpsCamera.transform.rotation = transEndRot;
                SetCursorUnlocked(false);
            }
        }
    }

    public void SwitchToOrbital()
    {
        if (isFPS) StartTransitionToOrbital();
    }

    private void SetImmediateOrbital()
    {
        isFPS = false;
        isTransitioning = false;
        isCursorUnlocked = false;
        fpsCamera.gameObject.SetActive(false);
        orbitalCamera.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}