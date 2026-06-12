using UnityEngine;

[RequireComponent(typeof(HingeJoint))]
public class CabinetDoor : MonoBehaviour
{
    [Tooltip("The parent cabinet GameObject this door belongs to.")]
    public GameObject cabinet;

    [Tooltip("Is the cabinet door currently locked?")]
    public bool isLocked = false;

    private HingeJoint hinge;
    private JointLimits originalLimits;
    private Rigidbody rb;

    void Start()
    {
        hinge = GetComponent<HingeJoint>();
        rb = GetComponent<Rigidbody>();

        if (hinge != null)
        {
            originalLimits = hinge.limits;
        }

        // Dynamically ignore collisions between this door and the cabinet frame/shelves at runtime
        if (cabinet != null)
        {
            Collider[] cabColliders = cabinet.GetComponentsInChildren<Collider>(true);
            Collider[] doorColliders = GetComponentsInChildren<Collider>(true);
            foreach (var cc in cabColliders)
            {
                foreach (var dc in doorColliders)
                {
                    if (cc != null && dc != null)
                    {
                        Physics.IgnoreCollision(cc, dc, true);
                    }
                }
            }
        }

        // Apply initial lock state if set in editor
        if (isLocked)
        {
            SetLocked(true);
        }
    }

    /// <summary>
    /// Locks or unlocks the cabinet door.
    /// When locked, the door is closed and its hinge limits are set to 0.
    /// When unlocked, original limits are restored.
    /// </summary>
    public void SetLocked(bool locked)
    {
        isLocked = locked;

        if (hinge == null) hinge = GetComponent<HingeJoint>();
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (hinge == null) return;

        if (isLocked)
        {
            // Close the door immediately
            transform.localRotation = Quaternion.identity;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Lock the joint limits to 0
            JointLimits limits = hinge.limits;
            limits.min = 0f;
            limits.max = 0f;
            hinge.limits = limits;
        }
        else
        {
            // Restore original limits
            hinge.limits = originalLimits;
        }
    }
}
