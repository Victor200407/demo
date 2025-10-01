using System.Collections.Generic;
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
    [SerializeField] private float digTurnSpeedDeg = 60f;
    
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

    [Header("Wejścia tuneli (auto-spawn)")]
    [SerializeField] private GameObject entrancePrefab;
    [SerializeField] private float undergroundThreshold = 0.05f;
    [SerializeField] private float entranceMinSpacing = 2.5f;
    [SerializeField] private float entranceSurfaceOffset = 0.02f;
    [SerializeField] private bool entranceDebugLogs = true;

    [SerializeField] private TunnelRecorder recorder;
    
    [Header("Awaryjne wyjście (brak tlenu)")]
    [SerializeField] private float forcedExitDuration = 0.6f;

    float depth;
    bool prevDigHeld; 
    bool gateLocked;

    bool wasUnderground;
    readonly List<Transform> spawnedEntrances = new();
    TunnelEntrance pendingPair;

    int hIsDigging;
    int hDigStart;
    int hDigStop;

    bool isForcedExiting;
    float forcedExitT;
    float forcedExitStartDepth;
    bool suppressDigInput;

    // === Rail control ===
    bool railControlActive;

    const float EPS = 1e-6f;

    public float Depth => depth;
    public bool IsForcedExiting => isForcedExiting;

    void Reset()
    {
        if (!planet) planet = FindFirstObjectByType<Planet>();
    }

    void Start()
    {
        if (!planet) { Debug.LogError("Assign Planet.", this); enabled = false; return; }

        depth = Mathf.Clamp(startDepth, 0f, maxDigDepth);

        Vector3 up = Up();
        transform.position = planet.Center + up * ShellRadius();
        Vector3 fwd = ProjectOnPlaneSafe(transform.forward, up);
        transform.rotation = Quaternion.LookRotation(fwd, up);

        if (cameraPivot) cameraPivot.position = transform.position;

        if (animator)
        {
            hIsDigging = Animator.StringToHash(paramIsDigging);
            hDigStart  = Animator.StringToHash(trigDigStart);
            hDigStop   = Animator.StringToHash(trigDigStop);
        }

        wasUnderground = depth > undergroundThreshold;
        
        if (!recorder) recorder = GetComponent<TunnelRecorder>();
    }

    void Update()
    {
        if (!planet) return;

        // --- Rail control: w tym trybie NIE dotykamy pozycji/rotacji gracza ---
        if (railControlActive)
        {
            if (cameraPivot)
            {
                cameraPivot.position = transform.position;
                Vector3 upRail = (transform.position - planet.Center).sqrMagnitude > EPS
                    ? (transform.position - planet.Center).normalized : Vector3.up;
                Vector3 camFwd = ProjectOnPlaneSafe(cameraPivot.forward, upRail);
                cameraPivot.rotation = Quaternion.LookRotation(camFwd, upRail);
            }
            return;
        }

        // Input
        ReadInput(out float yaw, out float move, out bool digHeld);

        // Animator + gate
        DriveAnimator(digHeld);
        
        bool isUnderground = depth > undergroundThreshold;
        
        if (isUnderground && recorder)
        {
            recorder.TickRecord(transform.position, planet, depth);
        }


        // Forced exit
        if (isForcedExiting)
        {
            forcedExitT += (forcedExitDuration > 1e-6f ? Time.deltaTime / forcedExitDuration : 1f);
            depth = Mathf.Lerp(forcedExitStartDepth, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(forcedExitT)));

            if (forcedExitT >= 1f || depth <= 0.001f)
            {
                depth = 0f;
                isForcedExiting = false;
                gateLocked = false;
                suppressDigInput = false;

                Vector3 upDone = Up();
                transform.position = planet.Center + upDone * ShellRadius();
                Vector3 fwdDone = ProjectOnPlaneSafe(transform.forward, upDone);
                transform.rotation = Quaternion.LookRotation(fwdDone, upDone);
            }
        }

        // Yaw
        Vector3 up = Up();
        if (Mathf.Abs(yaw) > EPS)
        {
            transform.Rotate(up, (digHeld ? digTurnSpeedDeg : turnSpeedDeg) * yaw * Time.deltaTime, Space.World);
        }

        // Ruch po wielkim kole
        if (Mathf.Abs(move) > EPS)
        {
            Vector3 fwdT = ProjectOnPlaneSafe(transform.forward, up);
            float angleDeg = (moveSpeed * Mathf.Clamp(move, -1f, 1f) * Time.deltaTime / ShellRadius()) * Mathf.Rad2Deg;
            Vector3 axis = Vector3.Cross(up, fwdT);
            if (axis.sqrMagnitude > EPS)
            {
                axis.Normalize();
                transform.RotateAround(planet.Center, axis, angleDeg);
                transform.rotation = Quaternion.AngleAxis(angleDeg, axis) * transform.rotation;
            }
        }

        // Kopanie
        bool freezeDepth = (gateLocked || IsInDigStart() || isForcedExiting);
        UpdateDepth(digHeld, freezeDepth);

        // Snap + stabilizacja
        up = Up();
        transform.position = planet.Center + up * ShellRadius();
        Vector3 fwdFinal = ProjectOnPlaneSafe(transform.forward, up);
        Quaternion targetRot = Quaternion.LookRotation(fwdFinal, up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotSmooth * Time.deltaTime));

        // Kamera
        if (cameraPivot)
        {
            cameraPivot.position = transform.position;
            Vector3 camFwd = ProjectOnPlaneSafe(cameraPivot.forward, up);
            cameraPivot.rotation = Quaternion.LookRotation(camFwd, up);
        }

        // Auto-spawn wejść
        bool isUndergroundNow = depth > undergroundThreshold;
        if (!wasUnderground && isUndergroundNow) TrySpawnEntranceHere("ENTRY");
        if (wasUnderground && !isUndergroundNow) TrySpawnEntranceHere("EXIT");
        wasUnderground = isUndergroundNow;
        prevDigHeld = digHeld;
    }

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
        yaw = move = 0f; 
        digHeld = false;

        var kb = Keyboard.current; 
        var gp = Gamepad.current;

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

        if (suppressDigInput || isForcedExiting)
            digHeld = false;
    }

    void DriveAnimator(bool digHeld)
    {
        if (!animator) return;

        bool inStart = IsInDigStart();
        if (digHeld && !prevDigHeld && !inStart)
        {
            animator.ResetTrigger(hDigStop);
            animator.SetTrigger(hDigStart);
            gateLocked = true;
        }
        else if (!digHeld && prevDigHeld)
        {
            animator.ResetTrigger(hDigStart);
            animator.SetTrigger(hDigStop);
        }

        animator.SetBool(hIsDigging, digHeld);
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

    // Public API
    public void BeginForcedExit()
    {
        if (isForcedExiting) return;
        isForcedExiting = true;
        forcedExitT = 0f;
        forcedExitStartDepth = depth;

        suppressDigInput = true;
        gateLocked = true;

        if (animator)
        {
            animator.ResetTrigger(hDigStart);
            animator.SetTrigger(hDigStop);
            animator.SetBool(hIsDigging, false);
        }
    }

    public void BeginRailControl()
    {
        railControlActive = true;
        suppressDigInput = true;
        gateLocked = true;

        if (animator)
        {
            animator.ResetTrigger(hDigStart);
            animator.SetBool(hIsDigging, false);
        }
    }

    public void EndRailControl()
    {
        railControlActive = false;
        suppressDigInput  = false;
        gateLocked        = false;
    }

    // Spawn wejść (bez zmian funkcjonalnych)
    void TrySpawnEntranceHere(string reason)
    {
        if (!entrancePrefab)
        {
            if (entranceDebugLogs) Debug.LogWarning("[Entrance] Brak przypisanego prefab'u wejścia.", this);
            return;
        }

        Vector3 onShell = planet.PointOnShell(transform.position, 0f);
        Vector3 up = (onShell - planet.Center).sqrMagnitude > EPS
            ? (onShell - planet.Center).normalized : Vector3.up;

        Vector3 tangentFwd = ProjectOnPlaneSafe(transform.forward, up);
        if (tangentFwd.sqrMagnitude < EPS)
            tangentFwd = Vector3.Cross(up, Vector3.right).normalized;

        foreach (var e in spawnedEntrances)
        {
            if (!e) continue;
            if (Vector3.Distance(e.position, onShell) < entranceMinSpacing)
            {
                if (entranceDebugLogs) Debug.Log($"[Entrance] Za blisko innego wejścia – pomijam spawn ({reason}).");
                return;
            }
        }

        Vector3 spawnPos = onShell + up * entranceSurfaceOffset;
        Quaternion rot = Quaternion.LookRotation(tangentFwd, up);
        GameObject go = Instantiate(entrancePrefab, spawnPos, rot);

        var te = go.GetComponent<TunnelEntrance>();
        if (te != null)
        {
            te.Planet = planet;
            te.SurfaceNormal = up;

            if (reason == "ENTRY")
            {
                pendingPair = te;
                if (recorder) recorder.BeginRecording(te);
            }
            else
            {
                if (pendingPair != null)
                {
                    te.Linked = pendingPair;
                    pendingPair.Linked = te;
                    
                    if (recorder) recorder.EndRecording(pendingPair, te);
                    
                    pendingPair = null;
                }
            }
        }

        spawnedEntrances.Add(go.transform);

        if (entranceDebugLogs)
            Debug.Log($"[Entrance] Spawn {reason} @ {spawnPos}", go);
    }
}
