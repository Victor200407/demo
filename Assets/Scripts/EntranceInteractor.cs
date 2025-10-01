using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class EntranceInteractor : MonoBehaviour
{
    [Header("Detekcja wejść")]
    public float detectRadius = 2.6f;
    public LayerMask entranceMask = ~0; // opcjonalny filtr warstw

    [Header("Twój stożek 3D")]
    [Tooltip("Prefab stożka z meshem (musi mieć ConeIndicator w root'cie lub dziecku).")]
    public GameObject conePrefab;
    public Material coneOverrideMaterial;
    [Range(0f,1f)] public float coneAlpha = 1f;
    public float coneUniformScale = 1f;
    public float coneTipOffset = 0.06f;
    public float coneExtraOffset = 0.0f;
    public bool coneTipAlongPlusY = true;

    [Header("Sterowanie")]
    public KeyCode interactKey = KeyCode.E;

    Transform _currentEntrance;
    ConeIndicator _cone;

    void Update()
    {
        var nearest = FindNearestEntrance();
        HandleCone(nearest);

        bool pressed = false;
        #if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null) pressed |= kb.eKey.wasPressedThisFrame;
        #endif
        if (Input.GetKeyDown(interactKey)) pressed = true;

        if (pressed && nearest != null)
            nearest.Enter(gameObject);
    }

    TunnelEntrance FindNearestEntrance()
    {
        TunnelEntrance best = null;
        float bestDist = float.MaxValue;
        Vector3 pos = transform.position;

        // Overlap (preferowane; wymaga triggerów/koliderów na wejściach)
        var cols = Physics.OverlapSphere(pos, detectRadius, entranceMask, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            var te = c.GetComponentInParent<TunnelEntrance>() ?? c.GetComponent<TunnelEntrance>();
            if (!te) continue;
            float d = Vector3.Distance(pos, te.transform.position);
            if (d < bestDist) { best = te; bestDist = d; }
        }

        // Fallback po tagu (jeśli brak koliderów)
        if (best == null)
        {
            var gos = GameObject.FindGameObjectsWithTag("TunnelEntrance");
            foreach (var go in gos)
            {
                var te = go.GetComponent<TunnelEntrance>();
                if (!te) continue;
                float d = Vector3.Distance(pos, te.transform.position);
                if (d < detectRadius && d < bestDist) { best = te; bestDist = d; }
            }
        }

        return best;
    }

    void HandleCone(TunnelEntrance target)
    {
        if (target == null)
        {
            if (_cone) Destroy(_cone.gameObject);
            _cone = null;
            _currentEntrance = null;
            return;
        }

        if (_currentEntrance != target.transform || _cone == null)
        {
            if (_cone) Destroy(_cone.gameObject);

            GameObject go = conePrefab != null ? Instantiate(conePrefab) : new GameObject("ConeIndicator (Missing Prefab)");
            _cone = go.GetComponentInChildren<ConeIndicator>() ?? go.AddComponent<ConeIndicator>();

            // appearance
            _cone.overrideMaterial     = coneOverrideMaterial != null ? coneOverrideMaterial : _cone.overrideMaterial;
            _cone.alpha                = coneAlpha;
            _cone.uniformScale         = (coneUniformScale <= 0f) ? 1f : coneUniformScale;
            _cone.tipOffsetAlongNormal = coneTipOffset;
            _cone.extraHeightOffset    = coneExtraOffset;
            _cone.tipAlongPlusY        = coneTipAlongPlusY;
            _cone.ApplyAppearance();

            _currentEntrance = target.transform;
        }

        _cone.SetPoseForEntrance(target);
    }

    void OnDisable()
    {
        if (_cone) Destroy(_cone.gameObject);
        _cone = null;
        _currentEntrance = null;
    }
}
