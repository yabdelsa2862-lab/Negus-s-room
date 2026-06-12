using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class DoorController : MonoBehaviour
{
    [Tooltip("If checked, the door opens outward. Otherwise, it opens inward.")]
    [SerializeField] private bool openOutward = false;

    public bool OpenOutward
    {
        get => openOutward;
        set
        {
            openOutward = value;
            UpdateLimits();
        }
    }

    private HingeJoint hinge;

    void Start()
    {
        hinge = GetComponent<HingeJoint>();
        UpdateLimits();
    }

    void OnValidate()
    {
        // OnValidate runs when values are changed in the Inspector
        if (hinge == null) hinge = GetComponent<HingeJoint>();
        if (hinge != null) UpdateLimits();
    }

    public void UpdateLimits()
    {
        if (hinge == null) return;

        JointLimits limits = hinge.limits;
        if (openOutward)
        {
            // Swing outward
            limits.min = 0f;
            limits.max = 130f;
        }
        else
        {
            // Swing inward
            limits.min = -130f;
            limits.max = 0f;
        }
        limits.bounciness = 0.2f;
        hinge.useLimits = true;
        hinge.limits = limits;
    }
}
