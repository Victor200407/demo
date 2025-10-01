using UnityEngine;

/// Ustawia parametry gradientu w materiale stożka na podstawie bounds mesha.
/// Działa z Shader Graph mającym properties: _TipY, _Height, _Softness, _BaseColor (opcjonalnie).
[RequireComponent(typeof(Renderer))]
public class ConeGradientAutoSetup : MonoBehaviour
{
    [Header("Konfiguracja czubka")]
    [Tooltip("Zaznacz, jeśli czubek modelu jest po stronie MIN Y (najniższy Y w bounds). " +
             "Odznacz, jeśli czubek jest po stronie MAX Y.")]
    public bool tipIsAtMinY = true;

    [Header("Miękkość (to samo co 'Softness' w materiale)")]
    [Range(0f, 0.6f)]
    public float softness = 0.2f;

    [Header("Który materiał?")]
    [Tooltip("Indeks materiału w Rendererze (0 = pierwszy).")]
    public int materialIndex = 0;

    static readonly int ID_TipY      = Shader.PropertyToID("_TipY");
    static readonly int ID_Height    = Shader.PropertyToID("_Height");
    static readonly int ID_Softness  = Shader.PropertyToID("_Softness");

    Renderer _renderer;
    MeshFilter _mf;
    MaterialPropertyBlock _mpb;

    void Reset()
    {
        _renderer = GetComponent<Renderer>();
        _mf = GetComponent<MeshFilter>();
    }

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Apply();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        _renderer = GetComponent<Renderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Apply();
    }
#endif

    void Apply()
    {
        if (_renderer == null) return;

        // znajdź bounds mesha (z dziecka jeśli potrzeba)
        if (_mf == null) _mf = GetComponent<MeshFilter>();
        Mesh mesh = _mf ? _mf.sharedMesh : null;
        if (mesh == null)
        {
            // spróbuj w dzieciach
            var childMF = GetComponentInChildren<MeshFilter>();
            mesh = childMF ? childMF.sharedMesh : null;
            if (mesh == null) return;
        }

        Bounds b = mesh.bounds; // w przestrzeni OBIEKTU/MESHA
        float height = b.size.y;
        float tipY   = tipIsAtMinY ? b.min.y : b.max.y;

        // wpisz do MPB, żeby nie psuć sharedMaterial
        _renderer.GetPropertyBlock(_mpb, materialIndex);
        _mpb.SetFloat(ID_TipY, tipY);
        _mpb.SetFloat(ID_Height, Mathf.Max(1e-5f, height));
        _mpb.SetFloat(ID_Softness, Mathf.Clamp01(softness));
        _renderer.SetPropertyBlock(_mpb, materialIndex);
    }
}
