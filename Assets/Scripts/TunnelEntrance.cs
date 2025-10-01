using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class TunnelEntrance : MonoBehaviour
{
    [field: SerializeField] public string EntranceId { get; private set; }

    [Header("Powierzchnia")]
    [HideInInspector] public Planet Planet;
    [HideInInspector] public Vector3 SurfaceNormal;

    [Header("UI nad wejściem")]
    public float uiHeight = 0.6f;
    public float uiNormalOffset = 0.05f;

    [Header("Parowanie tuneli")]
    public TunnelEntrance Linked;
    [HideInInspector] public TunnelPath RuntimePath;

    [Header("Wyjście")]
    public float exitPush = 0.35f;
    public float reenterBlockTime = 0.25f;

    [Header("Przeprowadzanie")]
    public bool traverseAlongCurve = true;   // włączone: używamy RuntimePath / Bezier fallback
    public float bezierDepth = 1.2f;
    public float traverseSpeed = 7f;
    public AnimationCurve traverseEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Debug")]
    public bool verboseLogs = true;
    public bool drawPathGizmo = true;

    void Awake()
    {
        if (string.IsNullOrEmpty(EntranceId))
            EntranceId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    public Vector3 IconWorldPos()
    {
        Vector3 up = (SurfaceNormal.sqrMagnitude > 1e-6f) ? SurfaceNormal : transform.up;
        return transform.position + up * (uiHeight + uiNormalOffset);
    }

    public void Enter(GameObject player)
    {
        if (Linked == null)
        {
            if (verboseLogs) Debug.LogWarning($"[TunnelEntrance] {name}: brak Linked – nic nie robię.");
            return;
        }

        var block = player.GetComponent<TunnelReenterBlocker>();
        if (block != null && block.IsBlockedFrom(this)) return;

        if (block == null) block = player.AddComponent<TunnelReenterBlocker>();
        block.Arm(Linked, reenterBlockTime);

        if (traverseAlongCurve && Planet != null)
            StartCoroutine(Traverse(player));
        else
            TeleportFallback(player);
    }

    void TeleportFallback(GameObject player)
    {
        Vector3 upOut = (Linked.SurfaceNormal.sqrMagnitude > 1e-6f) ? Linked.SurfaceNormal : Linked.transform.up;
        Vector3 outPos = Linked.transform.position + upOut * exitPush;

        Vector3 fwdIn = player.transform.forward;
        Vector3 fwdOut = Vector3.ProjectOnPlane(fwdIn, upOut).normalized;
        if (fwdOut.sqrMagnitude < 1e-6f) fwdOut = Vector3.Cross(upOut, Vector3.right).normalized;

        player.transform.SetPositionAndRotation(outPos, Quaternion.LookRotation(fwdOut, upOut));

        if (verboseLogs) Debug.Log($"[TunnelEntrance] {name}: TELEPORT fallback do '{Linked.name}'.");
    }

    IEnumerator Traverse(GameObject player)
    {
        var ctrl = player.GetComponent<MoleRotateAroundController>();
        if (ctrl) ctrl.BeginRailControl();

        // *** 1) Użyj RuntimePath (z tego wejścia) lub z Linked ***
        TunnelPath path = RuntimePath;
        if ((path == null || !path.IsValid) && Linked != null)
            path = Linked.RuntimePath;

        if (path != null && path.IsValid)
        {
            path.EnsureBuilt();

            // wybór kierunku najbliższego temu wejściu
            Vector3 start = path.points[0];
            Vector3 end   = path.points[^1];
            bool forward  = (Vector3.Distance(transform.position, start) <= Vector3.Distance(transform.position, end));

            float dist    = forward ? 0f : path.TotalLength;
            float target  = forward ? path.TotalLength : 0f;
            float sign    = forward ? +1f : -1f;

            if (verboseLogs) Debug.Log($"[TunnelEntrance] {name}: TRAVERSE po RuntimePath (len={path.TotalLength:F2}, forward={forward}).");

            while ((forward && dist < target) || (!forward && dist > target))
            {
                float step = traverseSpeed * Time.deltaTime;
                dist += sign * step;

                path.SampleByDistance(dist, out Vector3 pos, out Vector3 tan);

                Vector3 up = (pos - Planet.Center).sqrMagnitude > 1e-6f
                    ? (pos - Planet.Center).normalized : Vector3.up;

                Vector3 fwd = Vector3.ProjectOnPlane(tan, up);
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(up, Vector3.right);

                player.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd.normalized, up));
                yield return null;
            }
        }
        else
        {
            // *** 2) Fallback: Bezier ***
            if (verboseLogs) Debug.LogWarning($"[TunnelEntrance] {name}: Brak poprawnego RuntimePath – FALLBACK Bézier.");

            Vector3 Apos = transform.position;
            Vector3 nA   = (SurfaceNormal.sqrMagnitude > 1e-6f) ? SurfaceNormal : transform.up;

            Vector3 Bpos = Linked.transform.position + ((Linked.SurfaceNormal.sqrMagnitude > 1e-6f) ? Linked.SurfaceNormal : Linked.transform.up) * exitPush;
            Vector3 nB   = (Linked.SurfaceNormal.sqrMagnitude > 1e-6f) ? Linked.SurfaceNormal : Linked.transform.up;

            float d = Mathf.Max(0f, bezierDepth);
            Vector3 P0 = Apos;
            Vector3 P1 = Apos - nA * d;
            Vector3 P3 = Bpos;
            Vector3 P2 = (Bpos - nB * d);

            float length = ApproxCurveLength(P0, P1, P2, P3, 24);
            float duration = Mathf.Max(0.05f, length / Mathf.Max(0.01f, traverseSpeed));

            float t = 0f;
            while (t < 1f)
            {
                float u = traverseEase != null ? traverseEase.Evaluate(t) : t;
                Vector3 pos = Cubic(P0, P1, P2, P3, u);
                Vector3 vel = CubicDeriv(P0, P1, P2, P3, u);

                Vector3 up = (pos - Planet.Center).sqrMagnitude > 1e-6f ? (pos - Planet.Center).normalized : Vector3.up;
                Vector3 fwd = Vector3.ProjectOnPlane(vel, up);
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(up, Vector3.right);

                player.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd.normalized, up));

                t += Time.deltaTime / duration;
                yield return null;
            }
        }

        // Snap na wyjściu
        {
            Vector3 pos = Linked.transform.position + ((Linked.SurfaceNormal.sqrMagnitude > 1e-6f) ? Linked.SurfaceNormal : Linked.transform.up) * exitPush;
            Vector3 up  = (pos - Planet.Center).sqrMagnitude > 1e-6f ? (pos - Planet.Center).normalized : Vector3.up;
            Vector3 fwd = Vector3.ProjectOnPlane(Linked.transform.forward, up).normalized;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(up, Vector3.right);

            player.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));
        }

        if (ctrl) ctrl.EndRailControl();
    }

    // --- Bézier helpers (fallback) ---
    static Vector3 Cubic(Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3, float t)
    {
        float it = 1f - t;
        return it*it*it*P0 + 3f*it*it*t*P1 + 3f*it*t*t*P2 + t*t*t*P3;
    }

    static Vector3 CubicDeriv(Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3, float t)
    {
        float it = 1f - t;
        return 3f*it*it*(P1-P0) + 6f*it*t*(P2-P1) + 3f*t*t*(P3-P2);
    }

    static float ApproxCurveLength(Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3, int samples)
    {
        Vector3 prev = P0; float acc = 0f;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 cur = Cubic(P0, P1, P2, P3, t);
            acc += Vector3.Distance(prev, cur);
            prev = cur;
        }
        return acc;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawPathGizmo) return;

        TunnelPath p = RuntimePath ?? Linked?.RuntimePath;
        if (p == null || !p.IsValid) return;

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        for (int i = 0; i < p.points.Count - 1; i++)
        {
            Gizmos.DrawLine(p.points[i], p.points[i + 1]);
        }

        Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.9f);
        Gizmos.DrawSphere(p.points[0], 0.06f);
        Gizmos.DrawSphere(p.points[^1], 0.06f);
    }
#endif
}

/// Prosty „bezpiecznik” przed pętlą wejść/wyjść.
public class TunnelReenterBlocker : MonoBehaviour
{
    TunnelEntrance lastExit;
    float unblockAt;

    public void Arm(TunnelEntrance exitEntrance, float time)
    {
        lastExit = exitEntrance;
        unblockAt = Time.time + time;
    }

    public bool IsBlockedFrom(TunnelEntrance entrance)
    {
        if (Time.time >= unblockAt) return false;
        return entrance == lastExit;
    }
}
