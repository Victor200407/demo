using UnityEngine;

/// Wybrzuszenie planety liczone z PUNKTU NA POWIERZCHNI (nad kretem).
/// Wymaga materiału z właściwościami: _DigCenterWS (Vector3), _BulgeHeight (float), _DigRadius (float), _DigFalloff (float).
[RequireComponent(typeof(Renderer))]
public class PlanetBulgeDriver : MonoBehaviour
{
    [Header("Referencje")]
    public Planet planet;                          // planeta z Center i radius
    public Transform mole;                         // Transform kreta
    public MoleRotateAroundController moleCtrl;    // do Depth/IsForcedExiting
    public Animator moleAnimator;                  // do sprawdzania stanu DigLoop (opcjonalnie)

    [Header("Warunki aktywacji")]
    public bool requireDigLoop = true;             // aktywne tylko w animacji DigLoop?
    public string digLoopStateTag = "DigLoop";
    public bool requireUnderground = true;         // aktywne tylko po zanurzeniu?
    public float depthThreshold = 0.06f;           // od jakiej głębokości „pod ziemią”
    public float depthRamp = 0.25f;                // jak szybko rośnie bulge wraz z głębokością

    [Header("Parametry bulgu")]
    public float maxBulgeHeight = 0.25f;           // wysokość wybrzuszenia (metry)
    public float digRadius = 1.2f;                 // promień działania (metry)
    public float falloff  = 0.6f;                  // miękka krawędź (metry)
    [Tooltip("Dodatkowe uniesienie punktu centrum bulgu, by nie wchodził w powierzchnię (0..parę cm).")]
    public float surfaceOutwardOffset = 0.0f;

    [Header("Wygładzanie")]
    public float appearSpeed = 8f;                 // prędkość narastania
    public float disappearSpeed = 6f;              // prędkość zanikania

    // wewnętrzne
    Renderer _rend;
    MaterialPropertyBlock _mpb;
    float _currentHeight;

    static readonly int ID_BulgeHeight = Shader.PropertyToID("_BulgeHeight");
    static readonly int ID_DigCenterWS = Shader.PropertyToID("_DigCenterWS");
    static readonly int ID_DigRadius   = Shader.PropertyToID("_DigRadius");
    static readonly int ID_DigFalloff  = Shader.PropertyToID("_DigFalloff");

    void Awake()
    {
        _rend = GetComponent<Renderer>();
        _mpb  = new MaterialPropertyBlock();
        if (!planet) planet = GetComponent<Planet>(); // spróbuj automatycznie
    }

    void LateUpdate()
    {
        if (_rend == null || mole == null || planet == null) return;

        // 1) Punkt na POWIERZCHNI bezpośrednio nad kretem
        //    (radialnie od środka planety — niezależnie od tego, czy kret jest pod ziemią)
        Vector3 up = (mole.position - planet.Center);
        up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.up;
        Vector3 surfacePos = planet.Center + up * Mathf.Max(0.001f, planet.radius);
        if (surfaceOutwardOffset != 0f)
            surfacePos += up * surfaceOutwardOffset;

        // 2) Warunki aktywacji
        bool inDigLoop = !requireDigLoop || IsInTaggedState(moleAnimator, 0, digLoopStateTag);

        bool undergroundOK = true;
        float ramp01 = 1f;

        if (requireUnderground && moleCtrl != null)
        {
            float d = moleCtrl.Depth;
            undergroundOK = d > depthThreshold;

            // rampa narastania z głębokością (opcjonalna)
            if (undergroundOK && depthRamp > 0f)
            {
                float t = (d - depthThreshold) / Mathf.Max(1e-5f, depthRamp);
                t = Mathf.Clamp01(t);
                ramp01 = t * t * (3f - 2f * t); // smoothstep
            }
        }

        bool blocked = (moleCtrl != null) && moleCtrl.IsForcedExiting;

        // 3) Docelowa wysokość
        float target = (inDigLoop && undergroundOK && !blocked) ? (maxBulgeHeight * ramp01) : 0f;

        // 4) Wygładzanie
        float speed = (target > _currentHeight) ? appearSpeed : disappearSpeed;
        _currentHeight = Mathf.MoveTowards(_currentHeight, target, speed * Time.deltaTime);

        // 5) Ustaw parametry materiału (per-renderer przez MPB)
        _rend.GetPropertyBlock(_mpb);
        _mpb.SetVector(ID_DigCenterWS, surfacePos);
        _mpb.SetFloat(ID_BulgeHeight, _currentHeight);
        _mpb.SetFloat(ID_DigRadius,   digRadius);
        _mpb.SetFloat(ID_DigFalloff,  Mathf.Max(0.0001f, falloff));
        _rend.SetPropertyBlock(_mpb);
    }

    static bool IsInTaggedState(Animator anim, int layer, string tag)
    {
        if (!anim || string.IsNullOrEmpty(tag)) return false;
        var st = anim.GetCurrentAnimatorStateInfo(layer);
        if (st.IsTag(tag)) return true;
        if (anim.IsInTransition(layer))
        {
            var nt = anim.GetNextAnimatorStateInfo(layer);
            if (nt.IsTag(tag)) return true;
        }
        return false;
    }
}
