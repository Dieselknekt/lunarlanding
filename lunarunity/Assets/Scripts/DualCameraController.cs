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
    public float minOrbitDistance = 1015f;
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

    [Header("Scale Profile (Realistic Compromise Default)")]
    public bool isDebugScale = false;
    public float eyeHeight = 0.05f;
    public float walkSpeed = 0.4f;
    public float sprintSpeed = 2.5f;
    public float flySpeed = 15f;
    public float lunarGravity = 0.45f;
    public float jumpForce = 0.35f;

    [Header("FPS Controls")]
    public float lookSensitivity = 0.04f;

    private float fpsPitch = 0f;
    private bool isFPS = false;
    private bool isNoclip = false;
    private bool isCursorUnlocked = false;
    private bool isPaused = false;
    private float verticalVelocity = 0f;
    private float currentHeightAboveSurface = 0.05f;

    public bool IsFPSActive => isFPS;
    public bool IsCursorUnlocked => isCursorUnlocked;
    public bool IsPaused => isPaused;

    void Start()
    {
        if (siteMarker != null)
            initialMarkerScale = siteMarker.localScale;

        if (playerMarker != null)
            playerMarker.gameObject.SetActive(false);

        ApplyScaleProfile(isDebugScale);
        SetImmediateOrbital();
    }

    public void ApplyScaleProfile(bool useDebug)
    {
        isDebugScale = useDebug;

        if (useDebug)
        {
            // Debug / Fast-Travel Scale (original large values)
            eyeHeight = 1.7f;
            walkSpeed = 12f;
            sprintSpeed = 35f;
            flySpeed = 60f;
            lunarGravity = 9.8f;
            jumpForce = 7.5f;
            if (fpsCamera != null) fpsCamera.nearClipPlane = 0.3f;
        }
        else
        {
            // Realistic Compromise Scale (Default - vast horizon & towering craters)
            eyeHeight = 0.05f;
            walkSpeed = 0.4f;
            sprintSpeed = 2.5f;
            flySpeed = 15f;
            lunarGravity = 0.45f;
            jumpForce = 0.35f;
            if (fpsCamera != null) fpsCamera.nearClipPlane = 0.01f;
        }

        // If currently standing on the surface, smoothly adjust eye height immediately
        if (isFPS && !isNoclip)
        {
            currentHeightAboveSurface = eyeHeight;
            verticalVelocity = 0f;
        }
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        if (isPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            if (isFPS)
                SetCursorUnlocked(isCursorUnlocked);
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    void Update()
    {
        if (isPaused) return;

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

            // Proportional grounded tolerance so both 0.05 and 1.7 eye heights jump reliably
            float groundedTolerance = Mathf.Max(0.002f, eyeHeight * 0.05f);
            bool isGrounded = currentHeightAboveSurface <= eyeHeight + groundedTolerance;

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

            if (currentHeightAboveSurface <= eyeHeight)
            {
                currentHeightAboveSurface = eyeHeight;
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

        if (playerMarker != null)
        {
            playerMarker.gameObject.SetActive(false);
        }

        transStartPos = savedOrbitalPos;
        transStartRot = savedOrbitalRot;
        transEndPos = surfacePoint + radialUp * eyeHeight;

        Vector3 tangentForward = Vector3.Cross(radialUp, Vector3.right);
        if (tangentForward.sqrMagnitude < 0.01f) tangentForward = Vector3.Cross(radialUp, Vector3.forward);
        transEndRot = Quaternion.LookRotation(tangentForward.normalized, radialUp);

        fpsPitch = 0f;
        currentHeightAboveSurface = eyeHeight;
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

        // Shrink SiteMarker proportionally to the active scale profile (0.01x in realistic scale, 0.1x in debug scale)
        float targetShrinkFactor = isDebugScale ? 0.1f : 0.01f;
        if (siteMarker != null)
        {
            Vector3 fromScale = transitioningToFPS ? initialMarkerScale : initialMarkerScale * targetShrinkFactor;
            Vector3 toScale = transitioningToFPS ? initialMarkerScale * targetShrinkFactor : initialMarkerScale;
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
    public void FocusOrbitalOnDirection(Vector3 radialDir)
    {
        if (isFPS) return;
        radialDir.Normalize();
        pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(radialDir.y, -1f, 1f)) * Mathf.Rad2Deg, -85f, 85f);
        yaw = Mathf.Atan2(radialDir.x, radialDir.z) * Mathf.Rad2Deg;
    }
}