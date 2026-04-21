using UnityEngine;

public class TurretRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("How smoothly the turret follows the mouse. 10-15 is a good range.")]
    public float rotationSpeed = 12f;

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    // Runs after CameraFollow thanks to Script Execution Order
    void LateUpdate()
    {
        // If remote steering is active, rotate turret based on steering direction
        if (Mathf.Abs(NetworkInputBridge.RemoteSteering) > 0.05f ||
            Mathf.Abs(NetworkInputBridge.RemoteThrottle) > 0.05f)
        {
            RotateTurretRemote();
        }
        else
        {
            RotateTurretTowardsMouse(); // keyboard/mouse fallback
        }
    }
    void RotateTurretRemote()
    {
        // Turret follows tank's forward + steering offset
        Vector3 dir = transform.parent.forward
                    + transform.parent.right * NetworkInputBridge.RemoteSteering;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target,
                Time.deltaTime * rotationSpeed);
        }
    }
    void RotateTurretTowardsMouse()
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 targetPoint = ray.GetPoint(distance);
            Vector3 direction = targetPoint - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * rotationSpeed
                );
            }
        }
    }
}