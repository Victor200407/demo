using UnityEngine;

[DisallowMultipleComponent]
public class SimpleTunnelTeleporter : MonoBehaviour
{
    [Tooltip("Głębokość wejścia w głąb planety (metry).")]
    public float enterDepth = 1.2f;

    public void TeleportInside(GameObject player)
    {
        var te = GetComponent<TunnelEntrance>();
        if (te == null || te.Planet == null) return;

        Vector3 pos = transform.position;
        Vector3 up  = (te.SurfaceNormal.sqrMagnitude > 1e-6f) ? te.SurfaceNormal : transform.up;

        // Przesuń wzdłuż -up o enterDepth
        Vector3 inner = pos - up * enterDepth;

        // Snap rotacji: patrz wzdłuż stycznej (zachowaj forward wejścia po stycznej)
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(up, Vector3.right).normalized;

        player.transform.position = inner;
        player.transform.rotation = Quaternion.LookRotation(fwd, up);

        Debug.Log("[Teleporter] Gracz wszedł do tunelu.");
    }
}