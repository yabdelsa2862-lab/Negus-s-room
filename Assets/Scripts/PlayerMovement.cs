using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float jumpHeight = 0.8f;
    public float gravity = 15f;

    [Header("First Person Look")]
    public Transform playerCamera;
    public float mouseSensitivity = 2f;
    public float lookLimitUpper = 80f;
    public float lookLimitLower = -80f;

    [Header("Camera Bobbing")]
    public float bobSpeed = 12f;
    public float bobAmount = 0.03f;
    private float defaultCameraY;
    private float bobTimer = 0f;

    [Header("First Person Arm Visuals")]
    public Transform firstPersonLeftArm;
    public Transform firstPersonRightArm;
    public float armSwingSpeed = 5f;
    public float armSwaySpeed = 6f;
    public float armSwayAmount = 0.015f;
    private Vector3 defaultLeftArmPos;
    private Vector3 defaultRightArmPos;
    private bool isSwingingArm = false;
    private float swingTimer = 0f;

    [Header("Grabbing & Physics")]
    public float grabDistance = 3.2f;
    public float holdDistance = 1.1f;
    public float carrySpeed = 15f;
    public float throwForce = 12f;
    public float maxCarryMass = 10f; // Carry items <= 10kg completely (computers, screens)
    public float maxLiftMass = 40f;  // Tilt/lift heavy items up to 40kg (desks)
    public float objectRotationSpeed = 4f;
    public float carryRotationSpeed = 15f;
    
    private Rigidbody carriedObject = null;
    private float originalDrag;
    private float originalAngularDrag;
    private Quaternion relativeCarriedRotation;
    private Quaternion targetCarriedRotation;
    private bool isRotatingObject = false;
    private bool isHeavyGrab = false;
    private Vector3 localGrabPoint;
    private Texture2D whiteTexture;
    private Texture2D greenTexture;
    private bool canGrabTarget = false;
    
    private CabinetDoor lookedAtDoor = null;
    private Texture2D hudBgTexture;

    [Header("Animation References (Procedural limbs for shadow)")]
    public Transform leftLeg;
    public Transform rightLeg;
    public Transform leftArm;
    public Transform rightArm;
    public float walkAnimSpeed = 12f;
    public float maxSwingAngle = 30f;
    public float returnToIdleSpeed = 5f;

    private Rigidbody rb;
    private CapsuleCollider col;
    private float walkTime;
    private bool isGrounded;
    private float rotationX = 0f;
    private bool shouldJump = false;

    private float horizontalInput;
    private float verticalInput;

    [HideInInspector]
    public bool isInteractingWithPC = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        // Configure Rigidbody constraints
        rb.useGravity = true;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.5f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (playerCamera != null)
        {
            defaultCameraY = playerCamera.localPosition.y;
        }

        if (firstPersonLeftArm != null)
        {
            defaultLeftArmPos = firstPersonLeftArm.localPosition;
            firstPersonLeftArm.gameObject.SetActive(false); // start hidden
        }
        if (firstPersonRightArm != null)
        {
            defaultRightArmPos = firstPersonRightArm.localPosition;
            firstPersonRightArm.gameObject.SetActive(false); // start hidden
        }

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Create 1x1 solid color textures for the dynamic crosshair
        whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();

        greenTexture = new Texture2D(1, 1);
        greenTexture.SetPixel(0, 0, Color.green);
        greenTexture.Apply();

        hudBgTexture = new Texture2D(1, 1);
        hudBgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
        hudBgTexture.Apply();
    }

    void Update()
    {
        if (isInteractingWithPC)
        {
            horizontalInput = 0f;
            verticalInput = 0f;
            shouldJump = false;
            return;
        }

        // Toggle cursor lock with Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        // 1. Mouse Look / Object Rotation
        if (Cursor.lockState == CursorLockMode.Locked && playerCamera != null)
        {
            isRotatingObject = carriedObject != null && !isHeavyGrab && Input.GetMouseButton(1);

            if (isRotatingObject)
            {
                // Rotate carried object relative to camera view axes
                float rotateX = Input.GetAxis("Mouse X") * objectRotationSpeed;
                float rotateY = Input.GetAxis("Mouse Y") * objectRotationSpeed;

                Vector3 camUp = playerCamera.up;
                Vector3 camRight = playerCamera.right;

                targetCarriedRotation = Quaternion.AngleAxis(-rotateX, camUp) * Quaternion.AngleAxis(rotateY, camRight) * targetCarriedRotation;
            }
            else
            {
                // Normal mouse look
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

                transform.Rotate(Vector3.up * mouseX);

                rotationX -= mouseY;
                rotationX = Mathf.Clamp(rotationX, lookLimitLower, lookLimitUpper);
                playerCamera.localRotation = Quaternion.Euler(rotationX, 0f, 0f);

                // Maintain relative rotation as player turns/looks
                if (carriedObject != null)
                {
                    // If we just released Right Click, cache the new relative orientation
                    if (Input.GetMouseButtonUp(1))
                    {
                        relativeCarriedRotation = Quaternion.Inverse(playerCamera.rotation) * targetCarriedRotation;
                    }
                    targetCarriedRotation = playerCamera.rotation * relativeCarriedRotation;
                }
            }
        }

        // Ground check
        float rayLength = (col.height * 0.5f) + 0.1f;
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, rayLength);

        // Jump Input
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            shouldJump = true;
        }

        // 2. Grabbing Inputs (E key to grab/drop, Left-click to punch/throw)
        HandleGrabbingInput();

        // 3. Camera Bobbing
        bool isMoving = (Mathf.Abs(horizontalInput) > 0.1f || Mathf.Abs(verticalInput) > 0.1f) && isGrounded;
        HandleCameraBob(isMoving);

        // 4. Procedural Arm Sway / Bobbing
        HandleArmMovement(isMoving);

        // 5. Limb animations (for shadows)
        AnimateLimbs(isMoving);

        // 6. Update Crosshair State (for dynamic color indicator)
        UpdateCrosshairState();

        // 7. Update lookedAtDoor and handle cabinet locking input
        UpdateCabinetLocking();
    }

    void FixedUpdate()
    {
        if (isInteractingWithPC)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        rb.AddForce(Vector3.down * (gravity - Physics.gravity.magnitude), ForceMode.Acceleration);

        // Calculate velocity
        Vector3 moveInput = (transform.forward * verticalInput + transform.right * horizontalInput);
        if (moveInput.magnitude > 1f)
        {
            moveInput.Normalize();
        }

        Vector3 targetVelocity = moveInput * moveSpeed;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 velocityChange = targetVelocity - new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        velocityChange.x = Mathf.Clamp(velocityChange.x, -10f, 10f);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -10f, 10f);

        rb.AddForce(new Vector3(velocityChange.x, 0f, velocityChange.z), ForceMode.VelocityChange);

        if (shouldJump)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * Mathf.Sqrt(jumpHeight * 2f * gravity), ForceMode.VelocityChange);
            shouldJump = false;
        }

        // 6. Physics Grabbing Object Hold Update (FixedUpdate is best for Rigidbody velocity manipulation!)
        UpdateCarriedObjectPhysics();
    }

    private void HandleGrabbingInput()
    {
        // Left Mouse Button Down to Grab (or Punch if no object)
        if (Input.GetMouseButtonDown(0))
        {
            if (carriedObject == null)
            {
                // Try to grab. If it fails, trigger an arm swing punch animation.
                if (!TryGrabObject())
                {
                    if (!isSwingingArm)
                    {
                        isSwingingArm = true;
                        swingTimer = 0f;
                    }
                }
            }
        }

        // Left Mouse Button Up to Drop
        if (Input.GetMouseButtonUp(0))
        {
            if (carriedObject != null)
            {
                DropObject();
            }
        }

        // Throw with F, E, or Middle Click (MouseButton 2) (only if carrying)
        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(2))
        {
            if (carriedObject != null)
            {
                ThrowObject();
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                // Interact with PC (only if not carrying)
                Ray ray = new Ray(playerCamera.position, playerCamera.forward);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, grabDistance))
                {
                    InteractablePC pc = hit.collider.GetComponentInParent<InteractablePC>();
                    if (pc != null)
                    {
                        pc.StartInteraction(this);
                    }
                }
            }
        }
    }

    private bool TryGrabObject()
    {
        if (playerCamera == null) return false;

        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, grabDistance))
        {
            Rigidbody targetRb = hit.collider.attachedRigidbody;
            if (targetRb != null && !targetRb.isKinematic)
            {
                if (targetRb.mass <= maxLiftMass)
                {
                    carriedObject = targetRb;
                    isHeavyGrab = (targetRb.mass > maxCarryMass);
                    localGrabPoint = carriedObject.transform.InverseTransformPoint(hit.point);

                    originalDrag = carriedObject.linearDamping;
                    originalAngularDrag = carriedObject.angularDamping;

                    if (isHeavyGrab)
                    {
                        // Heavy object physics settings (keep gravity active so it tilts/drags realistically)
                        carriedObject.useGravity = true;
                    }
                    else
                    {
                        // Light object physics settings (disable gravity, high drag to float smoothly)
                        carriedObject.useGravity = false;
                        carriedObject.linearDamping = 10f;
                        carriedObject.angularDamping = 10f;

                        // Initialize target orientations (only for light objects since heavy objects tilt/pivot on ground)
                        relativeCarriedRotation = Quaternion.Inverse(playerCamera.rotation) * carriedObject.rotation;
                        targetCarriedRotation = carriedObject.rotation;
                    }

                    // Temporarily ignore collisions between player and carried object to prevent physics glitching
                    IgnorePlayerCollision(col, carriedObject.gameObject, true);

                    // Trigger a brief arm swing to show grab action
                    isSwingingArm = true;
                    swingTimer = 0f;

                    Debug.Log("SceneSetup: Grabbed object " + targetRb.name + " (Heavy: " + isHeavyGrab + ")");
                    return true;
                }
                else
                {
                    Debug.Log("SceneSetup: Object too heavy to lift! Mass: " + targetRb.mass);
                }
            }
        }
        return false;
    }

    private void DropObject()
    {
        if (carriedObject == null) return;

        // Restore collisions with player
        IgnorePlayerCollision(col, carriedObject.gameObject, false);

        if (!isHeavyGrab)
        {
            carriedObject.useGravity = true;
            carriedObject.linearDamping = originalDrag;
            carriedObject.angularDamping = originalAngularDrag;
        }

        carriedObject = null;
        isHeavyGrab = false;
    }

    private void ThrowObject()
    {
        if (carriedObject == null) return;

        if (isHeavyGrab)
        {
            // Cannot throw heavy objects like desks! Just drop it.
            DropObject();
            return;
        }

        // Restore collisions with player
        IgnorePlayerCollision(col, carriedObject.gameObject, false);

        // Reset settings
        carriedObject.useGravity = true;
        carriedObject.linearDamping = originalDrag;
        carriedObject.angularDamping = originalAngularDrag;

        // Apply impulse force forward
        carriedObject.AddForce(playerCamera.forward * throwForce, ForceMode.Impulse);

        carriedObject = null;
        isHeavyGrab = false;

        // Trigger arm swing throw animation
        isSwingingArm = true;
        swingTimer = 0f;
    }

    private void IgnorePlayerCollision(Collider playerCol, GameObject obj, bool ignore)
    {
        if (playerCol == null || obj == null) return;
        Collider[] objColliders = obj.GetComponentsInChildren<Collider>();
        foreach (var c in objColliders)
        {
            if (c != playerCol)
            {
                Physics.IgnoreCollision(playerCol, c, ignore);
            }
        }
    }

    private void UpdateCarriedObjectPhysics()
    {
        if (carriedObject == null || playerCamera == null) return;

        if (isHeavyGrab)
        {
            // Apply physics force at the specific grabbed corner/position to tilt/pivot the object
            Vector3 targetHoldPos = playerCamera.position + playerCamera.forward * holdDistance;
            Vector3 worldGrabPoint = carriedObject.transform.TransformPoint(localGrabPoint);
            Vector3 pullDir = targetHoldPos - worldGrabPoint;

            // Apply a limited force so we can lift corners/edges but not carry the whole heavy table
            float forceLimit = 220f; 
            Vector3 pullForce = pullDir * 250f;
            if (pullForce.magnitude > forceLimit)
            {
                pullForce = pullForce.normalized * forceLimit;
            }
            carriedObject.AddForceAtPosition(pullForce, worldGrabPoint, ForceMode.Force);
        }
        else
        {
            // Target position in front of the camera
            Vector3 holdPos = playerCamera.position + playerCamera.forward * holdDistance;
            
            // Calculate vector distance to target hold point
            Vector3 pullDir = holdPos - carriedObject.position;

            // Set velocity proportional to pull distance (mass-independent velocity manipulation)
            carriedObject.linearVelocity = pullDir * carrySpeed;
            
            // Physics-based rotation alignment using angularVelocity
            Quaternion rotationDiff = targetCarriedRotation * Quaternion.Inverse(carriedObject.rotation);
            rotationDiff.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            if (Mathf.Abs(angle) > 0.1f)
            {
                // Apply proportional angular velocity to rotate towards the target rotation smoothly
                carriedObject.angularVelocity = axis * (angle * Mathf.Deg2Rad * carryRotationSpeed);
            }
            else
            {
                carriedObject.angularVelocity = Vector3.zero;
            }
        }
    }

    private void HandleArmMovement(bool isMoving)
    {
        if (firstPersonLeftArm == null || firstPersonRightArm == null) return;

        bool shouldBeVisible = (carriedObject != null || isSwingingArm);
        if (firstPersonLeftArm.gameObject.activeSelf != shouldBeVisible)
        {
            firstPersonLeftArm.gameObject.SetActive(shouldBeVisible);
        }
        if (firstPersonRightArm.gameObject.activeSelf != shouldBeVisible)
        {
            firstPersonRightArm.gameObject.SetActive(shouldBeVisible);
        }

        if (!shouldBeVisible) return;

        if (isSwingingArm)
        {
            swingTimer += Time.deltaTime * armSwingSpeed;
            // Swing arms down and up on X axis
            float angleX = Mathf.Sin(swingTimer * Mathf.PI) * -35f;
            float angleY = Mathf.Sin(swingTimer * Mathf.PI) * -15f;
            
            // Swing both arms forward and inward symmetrically
            firstPersonRightArm.localRotation = Quaternion.Euler(angleX, -angleY, 0f);
            firstPersonLeftArm.localRotation = Quaternion.Euler(angleX, angleY, 0f);

            if (swingTimer >= 1.0f)
            {
                isSwingingArm = false;
                firstPersonRightArm.localRotation = Quaternion.identity;
                firstPersonLeftArm.localRotation = Quaternion.identity;
                firstPersonRightArm.localPosition = defaultRightArmPos;
                firstPersonLeftArm.localPosition = defaultLeftArmPos;
            }
        }
        else
        {
            // If carrying an object, raise arms to "holding" pose, else normal idle/walk position
            Vector3 targetRightArmPos = defaultRightArmPos;
            Vector3 targetLeftArmPos = defaultLeftArmPos;
            Quaternion targetRightArmRot = Quaternion.identity;
            Quaternion targetLeftArmRot = Quaternion.identity;

            if (carriedObject != null)
            {
                // Raise hands and rotate them inward slightly as if carrying (scaled for longer arms)
                targetRightArmPos += new Vector3(-0.08f, 0.12f, 0.08f);
                targetLeftArmPos += new Vector3(0.08f, 0.12f, 0.08f);
                targetRightArmRot = Quaternion.Euler(-25f, -15f, 0f);
                targetLeftArmRot = Quaternion.Euler(-25f, 15f, 0f);
            }

            if (isMoving)
            {
                // Sway arms out of phase for a natural stride bobbing effect
                float swayX = Mathf.Cos(bobTimer * 0.5f) * armSwayAmount;
                float swayY = Mathf.Sin(bobTimer) * armSwayAmount;

                firstPersonRightArm.localPosition = targetRightArmPos + new Vector3(swayX, swayY, 0f);
                firstPersonLeftArm.localPosition = targetLeftArmPos + new Vector3(-swayX, swayY, 0f);
            }
            else
            {
                firstPersonRightArm.localPosition = Vector3.Lerp(firstPersonRightArm.localPosition, targetRightArmPos, Time.deltaTime * 5f);
                firstPersonLeftArm.localPosition = Vector3.Lerp(firstPersonLeftArm.localPosition, targetLeftArmPos, Time.deltaTime * 5f);
            }

            firstPersonRightArm.localRotation = Quaternion.Slerp(firstPersonRightArm.localRotation, targetRightArmRot, Time.deltaTime * 5f);
            firstPersonLeftArm.localRotation = Quaternion.Slerp(firstPersonLeftArm.localRotation, targetLeftArmRot, Time.deltaTime * 5f);
        }
    }

    private void HandleCameraBob(bool isMoving)
    {
        if (playerCamera == null) return;

        if (isMoving)
        {
            bobTimer += Time.deltaTime * bobSpeed;
            float newY = defaultCameraY + Mathf.Sin(bobTimer) * bobAmount;
            playerCamera.localPosition = new Vector3(playerCamera.localPosition.x, newY, playerCamera.localPosition.z);
        }
        else
        {
            bobTimer = 0f;
            Vector3 targetPos = new Vector3(playerCamera.localPosition.x, defaultCameraY, playerCamera.localPosition.z);
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, targetPos, Time.deltaTime * 8f);
        }
    }

    private void AnimateLimbs(bool isMoving)
    {
        if (leftLeg == null || rightLeg == null || leftArm == null || rightArm == null)
            return;

        if (isMoving)
        {
            walkTime += Time.deltaTime * walkAnimSpeed;
            float swing = Mathf.Sin(walkTime) * maxSwingAngle;

            leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            leftArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            rightArm.localRotation = Quaternion.Euler(swing, 0f, 0f);
        }
        else
        {
            leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, Time.deltaTime * returnToIdleSpeed);
            rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, Time.deltaTime * returnToIdleSpeed);
            leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.identity, Time.deltaTime * returnToIdleSpeed);
            rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.identity, Time.deltaTime * returnToIdleSpeed);
            walkTime = 0f;
        }
    }

    private void UpdateCrosshairState()
    {
        canGrabTarget = false;
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, grabDistance))
        {
            Rigidbody targetRb = hit.collider.attachedRigidbody;
            if (targetRb != null && !targetRb.isKinematic)
            {
                if (targetRb.mass <= maxLiftMass)
                {
                    canGrabTarget = true;
                }
            }
        }
    }

    private void UpdateCabinetLocking()
    {
        lookedAtDoor = null;
        if (playerCamera == null || isInteractingWithPC) return;

        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, grabDistance))
        {
            lookedAtDoor = hit.collider.GetComponentInParent<CabinetDoor>();
            if (lookedAtDoor != null)
            {
                if (Input.GetKeyDown(KeyCode.L))
                {
                    bool newLockState = !lookedAtDoor.isLocked;
                    CabinetDoor[] allDoors = FindObjectsByType<CabinetDoor>(FindObjectsSortMode.None);
                    foreach (var d in allDoors)
                    {
                        if (d != null && d.cabinet == lookedAtDoor.cabinet)
                        {
                            Rigidbody doorRb = d.GetComponent<Rigidbody>();
                            if (doorRb != null && carriedObject == doorRb)
                            {
                                DropObject();
                            }
                            d.SetLocked(newLockState);
                        }
                    }
                }
            }
        }
    }

    void OnGUI()
    {
        if (isInteractingWithPC) return;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Texture2D activeTex = (canGrabTarget && carriedObject == null) ? greenTexture : whiteTexture;
            if (activeTex != null)
            {
                // Draw a simple 12x2 and 2x12 crosshair in the center of the screen
                // Horizontal line
                GUI.DrawTexture(new Rect((Screen.width - 12) / 2f, (Screen.height - 2) / 2f, 12, 2), activeTex);
                // Vertical line
                GUI.DrawTexture(new Rect((Screen.width - 2) / 2f, (Screen.height - 12) / 2f, 2, 12), activeTex);
            }

            // Draw lock/unlock HUD prompt below crosshair
            if (lookedAtDoor != null && hudBgTexture != null)
            {
                string promptText = lookedAtDoor.isLocked ? "Press [L] to Unlock Cabinet" : "Press [L] to Lock Cabinet";

                GUIStyle style = new GUIStyle();
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 14;
                style.normal.textColor = Color.white;
                style.fontStyle = FontStyle.Bold;

                Vector2 size = style.CalcSize(new GUIContent(promptText));
                float boxWidth = size.x + 30f;
                float boxHeight = size.y + 15f;
                float boxX = (Screen.width - boxWidth) / 2f;
                float boxY = (Screen.height / 2f) + 45f; // 45 pixels below the crosshair center

                GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, boxHeight), hudBgTexture);
                GUI.Label(new Rect(boxX, boxY, boxWidth, boxHeight), promptText, style);
            }
        }
    }
}
