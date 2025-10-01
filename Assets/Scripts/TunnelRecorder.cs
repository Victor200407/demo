using System.Collections.Generic;
using UnityEngine;

/// Nagrywa krętą ścieżkę tunelu w WORLD SPACE w trakcie kopania.
/// Przy wynurzeniu buduje TunnelPath i przypina do obu wejść.
[DisallowMultipleComponent]
public class TunnelRecorder : MonoBehaviour
{
    [Header("Referencje")]
    public Planet planet;
    public MoleRotateAroundController controller;

    [Header("Parametry próbkowania")]
    [Tooltip("Minimalny dystans między kolejnymi punktami osi tunelu (m).")]
    public float minPointDist = 0.5f;
    [Tooltip("Minimalna zmiana kierunku (deg), żeby dodać punkt.")]
    public float minAngleDeg = 10f;

    [Header("Pozycja osi tunelu")]
    [Tooltip("Jeśli true – oś tunelu idzie na realnej głębokości kreta; jeśli false – stały offset.")]
    public bool useActualDepth = true;
    [Tooltip("Stały offset w głąb od powierzchni (m), gdy useActualDepth=false.")]
    public float fixedCenterDepth = 0.6f;

    [Header("Debug")]
    public bool verboseLogs = true;

    // stan nagrywania
    bool recording;
    TunnelEntrance entryEntrance; // przy którym zanurkowaliśmy
    readonly List<Vector3> points = new();
    Vector3 lastDir;

    void Reset()
    {
        if (!planet) planet = FindFirstObjectByType<Planet>();
        if (!controller) controller = GetComponent<MoleRotateAroundController>();
    }

    /// Wywołaj po spawnie wejścia ENTRY (zanurzenie)
    public void BeginRecording(TunnelEntrance entry)
    {
        entryEntrance = entry;
        points.Clear();
        recording = true;
        if (verboseLogs) Debug.Log($"[Recorder] Begin at '{entry?.name}'");
        // wymuś pierwszy punkt
        if (planet && controller) AddPointNow(transform.position, controller.Depth);
    }

    /// Wołaj co klatkę, gdy gracz jest pod ziemią
    public void TickRecord(Vector3 molePosWS, Planet p, float currentDepth)
    {
        if (!recording || p == null) return;

        Vector3 center = ComputeCenterline(molePosWS, p, currentDepth);
        if (points.Count == 0)
        {
            points.Add(center);
            lastDir = transform.forward;
            return;
        }

        float dist = Vector3.Distance(points[^1], center);
        if (dist < minPointDist) return;

        float angle = Vector3.Angle(lastDir, transform.forward);
        if (angle < minAngleDeg) return;

        points.Add(center);
        lastDir = transform.forward;
    }

    /// Wywołaj po spawnie wejścia EXIT (już po sparowaniu ENTRY<->EXIT)
    public void EndRecording(TunnelEntrance entry, TunnelEntrance exit)
    {
        if (!recording) return;
        recording = false;

        // dociągnij ostatni punkt
        if (planet && controller) AddPointNow(transform.position, controller.Depth);

        // sanity
        if (points.Count < 2)
        {
            if (verboseLogs) Debug.LogWarning("[Recorder] Za mało punktów – nie tworzę RuntimePath.");
            entryEntrance = null;
            return;
        }
        if (entry == null || exit == null)
        {
            if (verboseLogs) Debug.LogWarning("[Recorder] Brak pary wejść przy EndRecording.");
            entryEntrance = null;
            return;
        }

        // zbuduj ścieżkę
        var path = new TunnelPath { points = new List<Vector3>(points) };
        path.BuildArcLengthTable();

        // przypnij do obu końców
        entry.RuntimePath = path;
        exit.RuntimePath  = path;

        if (verboseLogs) Debug.Log($"[Recorder] Saved RuntimePath (len={path.TotalLength:F2}, pts={path.points.Count}) to '{entry.name}' & '{exit.name}'.");

        entryEntrance = null;
    }

    void AddPointNow(Vector3 molePosWS, float currentDepth)
    {
        Vector3 center = ComputeCenterline(molePosWS, planet, currentDepth);
        if (points.Count == 0 || Vector3.Distance(points[^1], center) > 0.01f)
            points.Add(center);
    }

    // Oś tunelu wyliczana ze SNAPA do powierzchni + zejście w głąb
    Vector3 ComputeCenterline(Vector3 molePosWS, Planet p, float currentDepth)
    {
        Vector3 surface = p.PointOnShell(molePosWS, 0f); // punkt na skorupie
        Vector3 up = (surface - p.Center).sqrMagnitude > 1e-8f ? (surface - p.Center).normalized : Vector3.up;

        float centerDepth = useActualDepth ? Mathf.Clamp(currentDepth, 0f, p.radius * 0.95f)
                                           : Mathf.Clamp(fixedCenterDepth, 0f, p.radius * 0.95f);

        float shellR = Mathf.Max(0.01f, p.radius - centerDepth);
        return p.Center + up * shellR; // punkt osi tunelu w world space
    }
}
