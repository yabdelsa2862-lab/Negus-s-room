using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 defaultOffset = new Vector3(0f, 3f, -5f);
    public float smoothSpeed = 10f;
    public float mouseSensitivity = 3f;
    public float rotationSmoothSpeed = 10f;

    private float pitch = 20f; // vertical angle
    private float yaw = 0f;    // horizontal angle

    void Start()
    {
        // Set initial yaw based on default camera facing
        yaw = transform.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Check for mouse drag to orbit (left or right mouse button)
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -10f, 60f); // Prevents camera going underground or flipping
        }

        // Calculate rotation and position
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        
        // Offset is in local space of rotation
        Vector3 direction = rotation * Vector3.forward;
        Vector3 targetPosition = target.position - direction * defaultOffset.magnitude + Vector3.up * defaultOffset.y;

        // Smoothly interpolate position
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);

        // Smoothly look at target center (approx pivot height)
        Vector3 lookAtTarget = target.position + Vector3.up * 1f;
        Quaternion targetRotation = Quaternion.LookRotation(lookAtTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
    }
}
