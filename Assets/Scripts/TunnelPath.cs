using System.Collections.Generic;
using UnityEngine;

/// Kręta ścieżka tunelu (world-space) z próbkowaniem po długości.
[System.Serializable]
public class TunnelPath
{
    public List<Vector3> points = new();   // punkty osi tunelu w world-space

    const int SegSamples = 16;             // próbki per segment do wyliczenia długości
    readonly List<float> cumulativeLen = new();
    readonly List<int>   cumIdxStart   = new();
    bool arcBuilt;

    public bool IsValid => points != null && points.Count >= 2;
    public float TotalLength => (arcBuilt && cumulativeLen.Count > 0) ? cumulativeLen[^1] : 0f;

    public static Vector3 Catmull(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                      + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    public static Vector3 CatmullDeriv(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        return 0.5f * ((-p0 + p2) + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t
                      + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
    }

    /// Zbuduj tablicę długości łuku – wywołaj po każdej zmianie points.
    public void BuildArcLengthTable()
    {
        arcBuilt = false;
        cumulativeLen.Clear();
        cumIdxStart.Clear();

        if (!IsValid) return;

        cumulativeLen.Add(0f);
        cumIdxStart.Add(0);

        float acc = 0f;
        int n = points.Count;
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(0, i - 1)];
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p3 = points[Mathf.Min(n - 1, i + 2)];

            Vector3 prev = p1;
            for (int s = 1; s <= SegSamples; s++)
            {
                float t = s / (float)SegSamples;
                Vector3 cur = Catmull(p0, p1, p2, p3, t);
                acc += Vector3.Distance(prev, cur);
                cumulativeLen.Add(acc);
                cumIdxStart.Add(i);
                prev = cur;
            }
        }

        arcBuilt = true;
    }

    /// Upewnij się, że tablica długości jest gotowa.
    public void EnsureBuilt()
    {
        if (!arcBuilt) BuildArcLengthTable();
    }

    /// Próbka pozycji/tangensa wg odległości (0..TotalLength).
    public void SampleByDistance(float dist, out Vector3 pos, out Vector3 tangent)
    {
        EnsureBuilt();
        pos = points[0];
        tangent = (points[1] - points[0]).normalized;

        if (!IsValid || cumulativeLen.Count < 2) return;

        dist = Mathf.Clamp(dist, 0f, TotalLength);

        int lo = 0, hi = cumulativeLen.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (cumulativeLen[mid] < dist) lo = mid; else hi = mid;
        }

        float d0 = cumulativeLen[lo];
        float d1 = cumulativeLen[hi];
        float u  = (d1 > d0) ? (dist - d0) / (d1 - d0) : 0f;

        int i = cumIdxStart[lo];
        int n = points.Count;

        Vector3 p0 = points[Mathf.Max(0, i - 1)];
        Vector3 p1 = points[i];
        Vector3 p2 = points[i + 1];
        Vector3 p3 = points[Mathf.Min(n - 1, i + 2)];

        pos     = Catmull(p0, p1, p2, p3, u);
        tangent = CatmullDeriv(p0, p1, p2, p3, u).normalized;
    }
}
