using UnityEngine;

[ExecuteAlways]
public class Planet : MonoBehaviour
{
    [Header("Parametry planety")]
    public float radius = 5f;

    public Vector3 Center => transform.position;

    public Vector3 UpAt(Vector3 worldPos)
    {
        Vector3 up = worldPos - Center; // wektor od center do worldPos
        return up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
    }

    public Vector3 PointOnShell(Vector3 approxWorldPos, float depth)
    {
        float shellR = Mathf.Max(0.01f, radius - Mathf.Max(0f, depth));
        Vector3 dir = UpAt(approxWorldPos);
        return Center + dir * shellR;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
        Gizmos.DrawSphere(Center, radius);
    }
#endif
}