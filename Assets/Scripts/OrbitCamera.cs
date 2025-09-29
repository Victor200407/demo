using UnityEngine;

/// Prosta kamera podążająca za celem (np. CameraPivot gracza).
/// Trzyma stałą odległość i wysokość, patrząc na cel.
public class FixedFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 2f;
    [SerializeField] private float height = 4f;
    [SerializeField] private float smooth = 5f;

    private void LateUpdate()
    {
        if (!target) return;

        Vector3 up = target.up;
        Vector3 back = -target.forward;

        Vector3 desiredPos = target.position + back.normalized * distance + up * height;
        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smooth);

        Vector3 lookDir = (target.position - transform.position);
        if (lookDir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(lookDir.normalized, up);
    }
}