using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MoleRotateAroundController : MonoBehaviour
{
    [Header("Referencje")]
    [SerializeField] private Planet planet;
    [SerializeField] private Transform cameraPivot;

    [Header("Ruch")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float turnSpeedDeg = 360f;
    [SerializeField] private float rotSmooth = 15f;

    [Header("Kopanie")]
    [SerializeField] private float maxDigDepth = 2f;
    [SerializeField] private float digSpeed = 4f;
    [SerializeField] private float ascendSpeed = 4f;
    [SerializeField] private float startDepth = 0f;

    [Header("Animator (gate DigStart)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string paramIsDigging = "IsDigging";
    [SerializeField] private string trigDigStart = "DigStart";
    [SerializeField] private string trigDigStop = "DigStop";
    [SerializeField] private string digStartStateTag = "DigStart";
    [SerializeField] private float digStartExitNormalizedTime = 0.98f;
    [SerializeField] private bool delayDepthUntilAfterDigStart = true;

    // ── stan
    float depth;
    bool prevDigHeld; 
    bool gateLocked;

    // ── cache hashy
    int hIsDigging;
    int hDigStart;
    int hDigStop;

    // epsilon do porównań bliskich zeru
    const float EPS = 1e-6f;

    void Reset()
    {
        if (!planet) planet = FindFirstObjectByType<Planet>();
    }

    void Start()
    {
        if (!planet) { Debug.LogError("Assign Planet.", this); enabled = false; return; }

        depth = Mathf.Clamp(startDepth, 0f, maxDigDepth);

        // startowa pozycja i rotacja
        Vector3 up = Up();
        transform.position = planet.Center + up * ShellRadius();
        Vector3 fwd = ProjectOnPlaneSafe(transform.forward, up);
        transform.rotation = Quaternion.LookRotation(fwd, up);

        if (cameraPivot)
        {
            cameraPivot.position = transform.position;
        }

        if (animator)
        {
            hIsDigging = Animator.StringToHash(paramIsDigging);
            hDigStart  = Animator.StringToHash(trigDigStart);
            hDigStop   = Animator.StringToHash(trigDigStop);
        }
    }

    void Update()
    {
        if (!planet) return;

        // 1) Wejście
        ReadInput(out float yaw, out float move, out bool digHeld);

        // 2) Animator + gate
        DriveAnimator(digHeld);

        // 3) Yaw w miejscu (A/D)
        Vector3 up = Up();
        if (Mathf.Abs(yaw) > EPS)
            transform.Rotate(up, yaw * turnSpeedDeg * Time.deltaTime, Space.World);

        // 4) Ruch po wielkim kole (W/S)
        if (Mathf.Abs(move) > EPS)
        {
            Vector3 fwdT = ProjectOnPlaneSafe(transform.forward, up);
            float angleDeg = (moveSpeed * Mathf.Clamp(move, -1f, 1f) * Time.deltaTime / ShellRadius()) * Mathf.Rad2Deg;
            Vector3 axis = Vector3.Cross(up, fwdT);
            if (axis.sqrMagnitude > EPS)
            {
                axis.Normalize();
                transform.RotateAround(planet.Center, axis, angleDeg);
                transform.rotation = Quaternion.AngleAxis(angleDeg, axis) * transform.rotation; // transport kierunku
            }
        }

        // 5) Kopanie (z blokadą do końca DigStart)
        bool freezeDepth = delayDepthUntilAfterDigStart && (gateLocked || IsInDigStart());
        UpdateDepth(digHeld, freezeDepth);

        // 6) Snap do powłoki + stabilizacja rotacji
        up = Up();
        transform.position = planet.Center + up * ShellRadius();
        Vector3 fwdFinal = ProjectOnPlaneSafe(transform.forward, up);
        Quaternion targetRot = Quaternion.LookRotation(fwdFinal, up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotSmooth * Time.deltaTime));

        // 7) Kamera
        if (cameraPivot)
        {
            cameraPivot.position = transform.position;
            Vector3 camFwd = ProjectOnPlaneSafe(cameraPivot.forward, up);
            cameraPivot.rotation = Quaternion.LookRotation(camFwd, up);
        }
    }

    // ───────────────────── helpers ─────────────────────
    Vector3 Up() => planet.UpAt(transform.position);
    float  ShellRadius() => Mathf.Max(0.01f, planet.radius - depth);

    static Vector3 ProjectOnPlaneSafe(Vector3 v, Vector3 n)
    {
        Vector3 p = Vector3.ProjectOnPlane(v, n);
        if (p.sqrMagnitude < EPS)
        {
            p = Vector3.Cross(n, Vector3.right);
            if (p.sqrMagnitude < EPS) p = Vector3.Cross(n, Vector3.forward);
        }
        return p.normalized;
    }

    void ReadInput(out float yaw, out float move, out bool digHeld)
    {
        yaw = move = 0f; digHeld = false;
        #if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; var gp = Gamepad.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  yaw -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) yaw += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    move += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  move -= 1f;
            digHeld = kb.spaceKey.isPressed;
        }
        if (gp != null)
        {
            yaw  += gp.leftStick.x.ReadValue();
            move += gp.leftStick.y.ReadValue();
            digHeld |= gp.buttonSouth.isPressed;
        }
        #else
        yaw = Input.GetAxisRaw("Horizontal");
        move = Input.GetAxisRaw("Vertical");
        digHeld = Input.GetButton("Jump");
        #endif
    }

    void DriveAnimator(bool digHeld)
    {
        if (!animator) return;

        // guard: nie strzelaj DigStart gdy już wchodzisz do DigStart
        bool inStart = IsInDigStart();
        if (digHeld && !prevDigHeld && !inStart)
        {
            animator.ResetTrigger(hDigStop);
            animator.SetTrigger(hDigStart);
            gateLocked = true; // natychmiast blokuj zmianę depth
        }
        else if (!digHeld && prevDigHeld)
        {
            animator.ResetTrigger(hDigStart);
            animator.SetTrigger(hDigStop);
        }

        animator.SetBool(hIsDigging, digHeld);
        prevDigHeld = digHeld;

        // zwolnij gate po wyjściu z DigStart
        if (gateLocked && !IsInDigStart()) gateLocked = false;
    }

    bool IsInDigStart()
    {
        if (!animator) return false;
        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsTag(digStartStateTag) && st.normalizedTime < digStartExitNormalizedTime) return true;
        if (animator.IsInTransition(0))
        {
            var nt = animator.GetNextAnimatorStateInfo(0);
            if (nt.IsTag(digStartStateTag)) return true;
        }
        return false;
    }

    void UpdateDepth(bool digHeld, bool freeze)
    {
        if (freeze) return;
        float v = digHeld ? +digSpeed : -ascendSpeed;
        depth = Mathf.Clamp(depth + v * Time.deltaTime, 0f, maxDigDepth);
    }
}
