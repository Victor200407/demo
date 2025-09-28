using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Kinematyczny ruch po sferze, bez grawitacji.
/// Sterowanie:
///  - A/D: obrót wokół lokalnego 'up' (yaw).
///  - W/S: ruch do przodu/tyłu po wielkim kole (geodezyjnie) => W okrąża planetę.
///  - Space (hold): kopanie (pogłębienie), release: wynurzenie.
public class MoleKinematicController : MonoBehaviour
{
    [Header("Referencje")]
    public Planet planet;
    public Transform cameraPivot;  // opcjonalny pivot kamery
    public Transform cam;          // nie jest wymagane do sterowania, tylko do kamery

    [Header("Ruch")]
    [Tooltip("Prędkość ruchu po łuku (m/s po powierzchni).")]
    public float moveSpeed = 6f;
    [Tooltip("Szybkość obrotu A/D (stopnie/sek).")]
    public float turnSpeedDeg = 120f;
    [Tooltip("Wygładzenie rotacji 'up/forward'.")]
    public float turnLerp = 15f;

    [Header("Kopanie")]
    public float maxDigDepth = 2.0f;
    public float digSpeed = 1.5f;     // m/s w głąb
    public float ascendSpeed = 2.0f;  // m/s ku powierzchni
    public float startDepth = 0f;

    float currentDepth;

    void Start()
    {
        if (!planet)
        {
            Debug.LogError("MoleKinematicController: przypisz Planet.", this);
            enabled = false;
            return;
        }

        currentDepth = Mathf.Clamp(startDepth, 0f, maxDigDepth);
        transform.position = planet.PointOnShell(transform.position, currentDepth);
        // Ustaw poprawne up/forward na starcie
        Vector3 up = planet.UpAt(transform.position);
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(up, Vector3.right).normalized;
        transform.rotation = Quaternion.LookRotation(fwd, up);

        if (cameraPivot) cameraPivot.position = transform.position;
    }

    void Update()
    {
        if (!planet) return;

        Vector3 up = planet.UpAt(transform.position);

        // --- Wejścia (działa dla New Input System i/lub starego) ---
        float yawInput = 0f;   // A/D
        float moveInput = 0f;  // W/S
        bool digHeld = false;

        #if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  yawInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) yawInput += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    moveInput += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  moveInput -= 1f;
            digHeld = kb.spaceKey.isPressed;
        }
        if (gp != null)
        {
            yawInput += gp.leftStick.x.ReadValue();   // lewo/prawo = yaw
            moveInput += gp.leftStick.y.ReadValue();  // przód/tył = ruch
            digHeld |= gp.buttonSouth.isPressed;
        }
        #else
        yawInput = Input.GetAxisRaw("Horizontal"); // A/D
        moveInput = Input.GetAxisRaw("Vertical");  // W/S
        digHeld = Input.GetButton("Jump");         // Space
        #endif

        // --- Obrót A/D wokół lokalnego 'up' (yaw bez przesuwania) ---
        if (Mathf.Abs(yawInput) > 1e-4f)
        {
            float yawDeg = yawInput * turnSpeedDeg * Time.deltaTime;
            Quaternion yawRot = Quaternion.AngleAxis(yawDeg, up);
            // Obróć tylko kierunek patrzenia; pozycję ruszymy dopiero w sekcji ruchu
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            fwd = yawRot * fwd;
            transform.rotation = Quaternion.LookRotation(fwd, up);
        }

        // --- Kopanie / wynurzanie ---
        float targetDepth = currentDepth;
        if (digHeld)
            targetDepth = Mathf.Min(maxDigDepth, currentDepth + digSpeed * Time.deltaTime);
        else
            targetDepth = Mathf.Max(0f, currentDepth - ascendSpeed * Time.deltaTime);
        currentDepth = targetDepth;

        // --- Ruch W/S po wielkim kole (geodezyjnie) ---
        // Definicja: obracamy wektor od środka o kąt = (prędkość łukowa * dt) / promień,
        // oś = up x forward. Ten sam obrót stosujemy do forward → stały kurs po wielkim kole.
        float shellR = Mathf.Max(0.01f, planet.radius - currentDepth);
        Vector3 posFromCenter = (transform.position - planet.Center).normalized * shellR;
        Vector3 forwardT = Vector3.ProjectOnPlane(transform.forward, up).normalized;

        if (Mathf.Abs(moveInput) > 1e-4f)
        {
            float arcLen = moveSpeed * Mathf.Clamp(moveInput, -1f, 1f) * Time.deltaTime;
            float angleRad = arcLen / shellR;
            float angleDeg = angleRad * Mathf.Rad2Deg;

            Vector3 axis = Vector3.Cross(up, forwardT); // prawa ręka: W = do przodu
            if (axis.sqrMagnitude > 1e-8f)
            {
                Quaternion step = Quaternion.AngleAxis(angleDeg, axis.normalized);
                posFromCenter = step * posFromCenter;
                forwardT      = (step * forwardT).normalized; // równoległe przetransportowanie (utrzymuje kurs)
            }
        }

        // Dociśnij do powłoki i ustaw transform
        Vector3 newPos = planet.Center + posFromCenter;
        transform.position = planet.PointOnShell(newPos, currentDepth);

        // Korekta orientacji (pewne wyrównanie numeryczne)
        up = planet.UpAt(transform.position);
        Vector3 finalFwd = Vector3.ProjectOnPlane(forwardT, up).normalized;
        if (finalFwd.sqrMagnitude < 1e-6f) finalFwd = Vector3.Cross(up, Vector3.right).normalized;
        Quaternion targetRot = Quaternion.LookRotation(finalFwd, up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnLerp * Time.deltaTime));

        // Kamera pivot
        if (cameraPivot)
        {
            cameraPivot.position = transform.position;
            Vector3 pivotFwd = Vector3.ProjectOnPlane(cameraPivot.forward, up).normalized;
            if (pivotFwd.sqrMagnitude < 1e-6f) pivotFwd = finalFwd;
            cameraPivot.rotation = Quaternion.LookRotation(pivotFwd, up);
        }
    }
}
