using System.Collections;
using UnityEngine;

public class InteractablePC : MonoBehaviour
{
    private Transform screenTrans;
    private GameObject osCanvasGo;
    private ComputerOS osScript;
    private Rigidbody rb;
    private bool isKinematicOriginal;
    private PlayerMovement playerController;
    private Transform playerCameraTrans;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private Coroutine transitionCoroutine;
    private float originalNearClip;

    private bool isInteracting = false;
    private bool isLookingAround = false;
    private float lookAroundYaw = 0f;
    private float lookAroundPitch = 0f;
    private Vector3 focusedLocalPos;
    private Quaternion focusedLocalRot;
    private bool isTransitioning = false;
    public bool isPoweredOn = true;

    private void InitializeOSCanvas()
    {
        if (osCanvasGo != null) return;

        osCanvasGo = new GameObject("OS_Canvas");
        osCanvasGo.transform.SetParent(transform); // parent to root monitor to avoid non-uniform scale skewing
        
        // Align position and rotation with the screen face
        osCanvasGo.transform.position = screenTrans.position + screenTrans.up * 0.011f;
        osCanvasGo.transform.rotation = screenTrans.rotation * Quaternion.Euler(90f, 0f, 0f);
        
        // Canvas uses a 640x400 virtual space (half of 1280x800).
        // Each virtual unit is 2x physically larger → all UI text appears 2x bigger and readable.
        // 0.0254 converts FBX inch units to metres. 0.88 fits inside the bezel.
        float canvasScaleX = (screenTrans.lossyScale.x * 0.0254f / 640f) * 0.88f;
        float canvasScaleY = (screenTrans.lossyScale.z * 0.0254f / 400f) * 0.88f;
        osCanvasGo.transform.localScale = new Vector3(canvasScaleX, canvasScaleY, 1f);

        Canvas canvas = osCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        var scaler = osCanvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        osCanvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Add the OS controller script
        osScript = osCanvasGo.AddComponent<ComputerOS>();
        osScript.pcController = this;
    }

    private void SetRaycasterEnabled(bool enabled)
    {
        if (osCanvasGo != null)
        {
            var raycaster = osCanvasGo.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = enabled;
            }
        }
    }

    public void SetPowerState(bool on)
    {
        isPoweredOn = on;
        if (osScript != null)
        {
            osScript.UpdatePowerOverlay();
        }
        if (!isPoweredOn && isInteracting)
        {
            StopInteraction();
        }
    }

    void Start()
    {
        // Find screen face child mesh (FBX child named 'screen', 'Screen', 'screen (1)', 'Screen (1)')
        screenTrans = transform.Find("screen");
        if (screenTrans == null) screenTrans = transform.Find("Screen");
        if (screenTrans == null) screenTrans = transform.Find("screen (1)");
        if (screenTrans == null) screenTrans = transform.Find("Screen (1)");
        // If no screen face found, use the first child that has a MeshFilter (the visual part)
        if (screenTrans == null)
        {
            foreach (Transform child in transform)
            {
                if (child.GetComponent<MeshFilter>() != null)
                {
                    screenTrans = child;
                    break;
                }
            }
        }
        // Final fallback
        if (screenTrans == null) screenTrans = transform;

        rb = GetComponent<Rigidbody>();

        // Initialize OS Canvas immediately so the screens show the OS constantly
        InitializeOSCanvas();
        SetRaycasterEnabled(false);
    }

    public void StartInteraction(PlayerMovement player)
    {
        if (isInteracting || !isPoweredOn) return;
        isInteracting = true;

        playerController = player;
        playerCameraTrans = player.playerCamera;

        // Freeze Rigidbody physics so monitor doesn't fall/slide during typing
        if (rb != null)
        {
            isKinematicOriginal = rb.isKinematic;
            rb.isKinematic = true;
        }

        // Cache camera original position/rotation in player space
        originalCameraLocalPos = playerCameraTrans.localPosition;
        originalCameraLocalRot = playerCameraTrans.localRotation;

        // Ensure Canvas is initialized and active
        InitializeOSCanvas();
        if (osCanvasGo != null)
        {
            osCanvasGo.SetActive(true);
            Canvas canvas = osCanvasGo.GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }
        }
        SetRaycasterEnabled(true);

        // Set player interacting flag
        playerController.isInteractingWithPC = true;

        // Cache and adjust camera near clip plane to prevent close clipping of the screen
        originalNearClip = Camera.main.nearClipPlane;
        Camera.main.nearClipPlane = 0.01f;

        // Calculate target local position and rotation relative to the root monitor (transform) to avoid non-uniform scaling distortion
        float fovRad = Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float screenHeight = screenTrans.lossyScale.z * 0.0254f; // FBX inch units → meters-equivalent
        float distance = (screenHeight * 0.5f) / Mathf.Tan(fovRad);
        distance *= 0.82f; // Close enough to read text clearly with bezel still visible

        Vector3 targetWorldPos = screenTrans.position + screenTrans.up * distance;
        Quaternion targetWorldRot = screenTrans.rotation * Quaternion.Euler(90f, 0f, 0f);

        Vector3 targetLocalPos = transform.InverseTransformPoint(targetWorldPos);
        Quaternion targetLocalRot = Quaternion.Inverse(transform.rotation) * targetWorldRot;

        focusedLocalPos = targetLocalPos;
        focusedLocalRot = targetLocalRot;

        // Parent camera to the root monitor (transform) to avoid non-uniform scale skewing
        playerCameraTrans.SetParent(transform);

        // Smoothly transition camera to look at screen
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionCamera(
            targetLocalPos, 
            targetLocalRot, 
            0.5f,
            true // unlock cursor at end
        ));
    }

    public void StopInteraction()
    {
        if (!isInteracting) return;
        isInteracting = false;
        isLookingAround = false;

        // Disable raycasting when interaction stops to avoid distance conflicts
        SetRaycasterEnabled(false);

        // Restore camera near clip plane
        Camera.main.nearClipPlane = originalNearClip;

        // Parent camera back to player root
        playerCameraTrans.SetParent(playerController.transform);

        // Smoothly transition camera back to player's head center
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionCamera(
            originalCameraLocalPos,
            originalCameraLocalRot,
            0.5f,
            false // hide/lock cursor at end
        ));
    }

    private IEnumerator TransitionCamera(Vector3 targetLocalPos, Quaternion targetLocalRot, float duration, bool enterInteract)
    {
        isTransitioning = true;
        float elapsed = 0f;
        Vector3 startLocalPos = playerCameraTrans.localPosition;
        Quaternion startLocalRot = playerCameraTrans.localRotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t); // smooth interpolation

            playerCameraTrans.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, t);
            playerCameraTrans.localRotation = Quaternion.Slerp(startLocalRot, targetLocalRot, t);
            yield return null;
        }

        playerCameraTrans.localPosition = targetLocalPos;
        playerCameraTrans.localRotation = targetLocalRot;

        if (enterInteract)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            isLookingAround = false;
        }
        else
        {
            // Restore Rigidbody physics
            if (rb != null)
            {
                rb.isKinematic = isKinematicOriginal;
            }

            // Lock and hide cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Re-enable player movement
            playerController.isInteractingWithPC = false;
        }
        isTransitioning = false;
    }

    private void StartLookAround()
    {
        isLookingAround = true;
        lookAroundYaw = 0f;
        lookAroundPitch = 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator TransitionToFocus()
    {
        isTransitioning = true;
        isLookingAround = false;

        float elapsed = 0f;
        float duration = 0.3f;
        Vector3 startLocalPos = playerCameraTrans.localPosition;
        Quaternion startLocalRot = playerCameraTrans.localRotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            playerCameraTrans.localPosition = Vector3.Lerp(startLocalPos, focusedLocalPos, t);
            playerCameraTrans.localRotation = Quaternion.Slerp(startLocalRot, focusedLocalRot, t);
            yield return null;
        }

        playerCameraTrans.localPosition = focusedLocalPos;
        playerCameraTrans.localRotation = focusedLocalRot;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isTransitioning = false;
    }

    void Update()
    {
        if (!isInteracting || isTransitioning) return;

        // Space exits PC interaction entirely
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StopInteraction();
            return;
        }

        // Escape toggles seat head look-around
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isLookingAround)
            {
                if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
                transitionCoroutine = StartCoroutine(TransitionToFocus());
            }
            else
            {
                StartLookAround();
            }
            return;
        }

        // Seated mouse look rotation update
        if (isLookingAround)
        {
            float mouseX = Input.GetAxis("Mouse X") * playerController.mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * playerController.mouseSensitivity;

            lookAroundYaw += mouseX;
            lookAroundPitch -= mouseY;

            lookAroundPitch = Mathf.Clamp(lookAroundPitch, -60f, 60f);
            lookAroundYaw = Mathf.Clamp(lookAroundYaw, -110f, 110f);

            playerCameraTrans.localRotation = Quaternion.AngleAxis(lookAroundYaw, Vector3.up) * focusedLocalRot * Quaternion.AngleAxis(lookAroundPitch, Vector3.right);
        }
    }
}
