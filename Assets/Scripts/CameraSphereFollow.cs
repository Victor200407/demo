using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraSphereFollow : MonoBehaviour
{
    public CubeSphereGrid grid;
    public Transform target;
    [Min(0.1f)] public float distance = 3.5f;
    [Range(0f, 20f)] public float smooth = 10f;
    public float fov = 50f;
    [Range(0f, 1f)] public float normalOffsetRatio = 0.6f;
    [Range(-1f, 1f)] public float lateralOffset = 0.15f;
    [Range(0f, 3f)] public float lookAhead = 1.2f;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = fov;
    }

    void Start()
    {
        if (!grid) grid = FindAnyObjectByType<CubeSphereGrid>();
        if (!target)
        {
            var pm = FindAnyObjectByType<PlayerMoverSphere>();
            if (pm) target = pm.transform;
        }
        SnapImmediate();
    }

    void LateUpdate()
    {
        if (!grid || !target) return;
        Vector3 p = target.position;
        Vector3 n = grid.SurfaceNormal(p);
        Vector3 tangentForward = Vector3.ProjectOnPlane(target.forward, n).normalized;
        if (tangentForward.sqrMagnitude < 1e-6f)
        {
            tangentForward = Vector3.ProjectOnPlane(transform.forward, n).normalized;
            if (tangentForward.sqrMagnitude < 1e-6f)
            {
                tangentForward = Vector3.Cross(n, Vector3.right);
                if (tangentForward.sqrMagnitude < 1e-6f)
                    tangentForward = Vector3.Cross(n, Vector3.up);
                if (tangentForward.sqrMagnitude < 1e-6f)
                    tangentForward = Vector3.Cross(n, Vector3.forward);
                tangentForward = tangentForward.normalized;
            }
        }
        Vector3 tangentRight = Vector3.Cross(n, tangentForward).normalized;

        float normalPart = Mathf.Clamp01(normalOffsetRatio);
        Vector3 baseDir = (n * normalPart) - (tangentForward * (1f - normalPart));
        Vector3 offsetDir = baseDir.sqrMagnitude > 1e-8f ? baseDir.normalized : n;
        Vector3 desiredPos = p + offsetDir * distance + tangentRight * (distance * lateralOffset);

        Vector3 lookTarget = p + tangentForward * (distance * lookAhead);
        Quaternion desiredRot = Quaternion.LookRotation((lookTarget - desiredPos).normalized, n);

        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);
    }

    [ContextMenu("Snap Immediate")]
    void SnapImmediate()
    {
        if (!grid || !target) return;
        Vector3 p = target.position;
        Vector3 n = grid.SurfaceNormal(p);
        Vector3 tangentForward = Vector3.ProjectOnPlane(target.forward, n).normalized;
        if (tangentForward.sqrMagnitude < 1e-6f)
        {
            tangentForward = Vector3.ProjectOnPlane(transform.forward, n).normalized;
            if (tangentForward.sqrMagnitude < 1e-6f)
            {
                tangentForward = Vector3.Cross(n, Vector3.right);
                if (tangentForward.sqrMagnitude < 1e-6f)
                    tangentForward = Vector3.Cross(n, Vector3.up);
                if (tangentForward.sqrMagnitude < 1e-6f)
                    tangentForward = Vector3.Cross(n, Vector3.forward);
                tangentForward = tangentForward.normalized;
            }
        }
        Vector3 tangentRight = Vector3.Cross(n, tangentForward).normalized;

        float normalPart = Mathf.Clamp01(normalOffsetRatio);
        Vector3 baseDir = (n * normalPart) - (tangentForward * (1f - normalPart));
        Vector3 offsetDir = baseDir.sqrMagnitude > 1e-8f ? baseDir.normalized : n;
        transform.position = p + offsetDir * distance + tangentRight * (distance * lateralOffset);

        Vector3 lookTarget = p + tangentForward * (distance * lookAhead);
        transform.rotation = Quaternion.LookRotation((lookTarget - transform.position).normalized, n);
    }
}
