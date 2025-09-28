using UnityEngine;

/// Kamera podążająca za graczem bez sterowania myszką.
/// Ustawiona w stałej odległości i zawsze patrzy na cel.
public class FixedFollowCamera : MonoBehaviour
{
    public Transform target;   // np. CameraPivot gracza
    public float distance = 2f;
    public float height = 4f;
    public float smooth = 5f;

    void LateUpdate()
    {
        if (!target) return;

        // kierunek "za graczem"
        Vector3 up = target.up;
        Vector3 back = -target.forward;

        Vector3 desiredPos = target.position 
                             + back.normalized * distance 
                             + up * height;

        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smooth);
        transform.rotation = Quaternion.LookRotation((target.position - transform.position).normalized, up);
    }
}