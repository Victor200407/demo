using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraSphereFollow : MonoBehaviour
{
    public CubeSphereGrid grid;
    public Transform target;
    [Min(0.1f)] public float distance = 3.5f;
    [Range(0f, 20f)] public float smooth = 10f;
    public float fov = 50f;

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
        Vector3 desiredPos = p + n * distance;
        Quaternion desiredRot = Quaternion.LookRotation((p - desiredPos).normalized, n);

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
        transform.position = p + n * distance;
        transform.rotation = Quaternion.LookRotation((p - transform.position).normalized, n);
    }
}
