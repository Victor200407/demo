using UnityEngine;

/// Kontroler dla Twojego stożka 3D (prefab z meshem).
/// Zakładamy, że oś modelu to +Y (standard). Jeśli czubek jest w -Y, odznacz tipAlongPlusY.
[DisallowMultipleComponent]
public class ConeIndicator : MonoBehaviour
{
    [Header("Orientacja modelu")]
    [Tooltip("Zostaw zaznaczone, jeśli czubek stożka jest wzdłuż +Y. Jeśli czubek jest w -Y, odznacz.")]
    public bool tipAlongPlusY = true;

    [Header("Wygląd (opcjonalnie)")]
    public Material overrideMaterial;
    [Range(0f,1f)] public float alpha = 1f;
    public float uniformScale = 1f;

    [Header("Offsety względem wejścia")]
    public float tipOffsetAlongNormal = 0.06f; // nad powierzchnię
    public float extraHeightOffset = 0f;

    MeshRenderer[] _renderers;

    void Awake()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>(true);
        ApplyAppearance();
    }

    public void ApplyAppearance()
    {
        if (_renderers == null) _renderers = GetComponentsInChildren<MeshRenderer>(true);

        if (overrideMaterial != null)
        {
            foreach (var r in _renderers) r.sharedMaterial = overrideMaterial;
        }
        if (_renderers != null)
        {
            foreach (var r in _renderers)
            {
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                {
                    var c = r.sharedMaterial.color;
                    c.a = alpha;
                    r.sharedMaterial.color = c;
                }
            }
        }

        transform.localScale = Vector3.one * Mathf.Max(0.0001f, uniformScale);
    }

    public void SetPoseForEntrance(TunnelEntrance entrance)
    {
        if (!entrance) return;

        // Pewna normalna NA ZEWNĄTRZ względem środka planety
        Vector3 radial = entrance.Planet ? (entrance.transform.position - entrance.Planet.Center) : entrance.SurfaceNormal;
        Vector3 outward = radial.sqrMagnitude > 1e-8f
            ? radial.normalized
            : (entrance.SurfaceNormal.sqrMagnitude > 1e-8f ? entrance.SurfaceNormal.normalized : entrance.transform.up);

        // Pozycja czubka nad wejściem
        Vector3 tipPos = entrance.transform.position + outward * (tipOffsetAlongNormal + extraHeightOffset);

        // Wektory osi modelu i celu
        Vector3 modelAxis = tipAlongPlusY ? Vector3.up : -Vector3.up;
        Vector3 targetDir = tipAlongPlusY ? -outward : outward; // czubek wskazuje wejście

        Quaternion rot = Quaternion.FromToRotation(modelAxis, targetDir);
        transform.SetPositionAndRotation(tipPos, rot);
    }
}
