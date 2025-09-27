using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CubeSphereGridLinesMeshRenderer : MonoBehaviour
{
    [Header("Refs")]
    public CubeSphereGrid grid;
    public Material material;               // URP/Unlit najlepiej (kolor linii)

    [Header("Quality")]
    [Min(1)] public int step = 1;           // 1 = każda linia siatki
    [Min(8)] public int segsPerEdge = 96;   // gładkość łuków

    [Header("Appearance")]
    [Min(0.0001f)] public float worldWidth = 0.01f; // szerokość „paska” w jednostkach świata
    public bool drawCanonicalOnlyEdges = true;      // rysuj krawędzie tylko na PosX/PosY/PosZ (bez dubli)

    MeshFilter mf;
    MeshRenderer mr;
    Mesh mesh;

    CubeSphereGrid subscribed;

    void OnEnable()
    {
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        if (material && mr.sharedMaterial != material) mr.sharedMaterial = material;

        if (!grid) grid = FindAnyObjectByType<CubeSphereGrid>();
        Resubscribe();

        RebuildMesh(); // jednorazowo przy włączeniu
    }

    void OnDisable()
    {
        Unsubscribe();
        ClearMesh();
    }

    void OnValidate()
    {
        if (!grid) grid = FindAnyObjectByType<CubeSphereGrid>();
        Resubscribe();
        // bez auto – czekamy na OnChanged z CubeSphereGrid (z context menu)
    }

    void Resubscribe()
    {
        if (subscribed == grid) return;
        Unsubscribe();
        if (grid)
        {
            grid.OnChanged += OnGridChanged;
            subscribed = grid;
        }
    }

    void Unsubscribe()
    {
        if (subscribed)
        {
            subscribed.OnChanged -= OnGridChanged;
            subscribed = null;
        }
    }

    void OnGridChanged(CubeSphereGrid g)
    {
        if (g == grid) RebuildMesh();
    }

    [ContextMenu("Rebuild Grid Lines (mesh)")]
    public void RebuildMesh()
    {
        if (!grid)
        {
            ClearMesh();
            return;
        }

        int N = grid.N;
        int stepClamped = Mathf.Clamp(step, 1, N);
        int segs = Mathf.Max(8, segsPerEdge);

        // Bufory do budowy
        var verts = new List<Vector3>(N * N * segs * 2);
        var tris  = new List<int>(N * N * segs * 6);

        // które ściany rysować pełne krawędzie (żeby uniknąć dubli na szwach)
        bool CanonicalFace(CubeSphereGrid.Face f)
            => (f == CubeSphereGrid.Face.PosX || f == CubeSphereGrid.Face.PosY || f == CubeSphereGrid.Face.PosZ);

        for (int f = 0; f < 6; f++)
        {
            var face = (CubeSphereGrid.Face)f;
            bool canon = CanonicalFace(face);

            // piony (u = const)
            int iStart = drawCanonicalOnlyEdges ? (canon ? 0 : 1) : 0;
            int iEnd   = drawCanonicalOnlyEdges ? (canon ? N : N - 1) : N;
            for (int i = iStart; i <= iEnd; i += stepClamped)
            {
                var pts = grid.EdgePolylineWorld(face, vertical: true, index: i, segs: segs);
                AppendStrip(pts, verts, tris, grid.Center, worldWidth);
            }

            // poziomy (v = const)
            int jStart = drawCanonicalOnlyEdges ? (canon ? 0 : 1) : 0;
            int jEnd   = drawCanonicalOnlyEdges ? (canon ? N : N - 1) : N;
            for (int j = jStart; j <= jEnd; j += stepClamped)
            {
                var pts = grid.EdgePolylineWorld(face, vertical: false, index: j, segs: segs);
                AppendStrip(pts, verts, tris, grid.Center, worldWidth);
            }
        }

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "CubeSphereGridLinesMesh";
            mesh.indexFormat = (verts.Count > 65000)
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
        }
        else
        {
            mesh.Clear();
            mesh.indexFormat = (verts.Count > 65000)
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0, true);

        // Normale nie są wymagane dla Unlit, ale ustawimy przyzwoite, by inne shadery też działały
        var norms = new Vector3[verts.Count];
        for (int k = 0; k < verts.Count; k++)
        {
            norms[k] = (verts[k] - grid.Center).normalized;
        }
        mesh.SetNormals(norms);

        // Bounds — sfera o promieniu+odstęp
        float pad = worldWidth * 2f;
        mesh.bounds = new Bounds(grid.Center, Vector3.one * (grid.radius * 2f + pad * 2f));

        mf.sharedMesh = mesh;
        if (material && mr.sharedMaterial != material) mr.sharedMaterial = material;
    }

    void ClearMesh()
    {
#if UNITY_EDITOR
        if (mesh != null && !Application.isPlaying)
            DestroyImmediate(mesh);
        else if (mesh != null)
            Destroy(mesh);
#else
        if (mesh != null) Destroy(mesh);
#endif
        mesh = null;
        if (mf) mf.sharedMesh = null;
    }

    // Buduje „pasek” (quad strip) wzdłuż polilinii punktów na sferze.
    // Szerokość w jednostkach świata. Pasek leży po stycznej: side = normalize(cross(tangent, normal)).
    static void AppendStrip(Vector3[] pts, List<Vector3> verts, List<int> tris, Vector3 center, float width)
    {
        if (pts == null || pts.Length < 2) return;

        int baseIndex = verts.Count;
        float half = width * 0.5f;

        // precompute „side” dla każdego punktu
        Vector3[] sides = new Vector3[pts.Length];

        for (int i = 0; i < pts.Length; i++)
        {
            Vector3 p = pts[i];
            Vector3 n = (p - center).normalized;

            Vector3 tangent;
            if (i == 0)
                tangent = (pts[i + 1] - p).normalized;
            else if (i == pts.Length - 1)
                tangent = (p - pts[i - 1]).normalized;
            else
            {
                var t1 = (p - pts[i - 1]).normalized;
                var t2 = (pts[i + 1] - p).normalized;
                tangent = (t1 + t2).normalized;
                if (tangent.sqrMagnitude < 1e-6f) tangent = (pts[i + 1] - pts[i - 1]).normalized;
            }

            Vector3 side = Vector3.Cross(tangent, n).normalized;
            if (side.sqrMagnitude < 1e-6f)
            {
                // fallback: prostopadły do n, byle stabilny
                side = Vector3.Cross(n, Vector3.right).normalized;
                if (side.sqrMagnitude < 1e-6f)
                    side = Vector3.Cross(n, Vector3.up).normalized;
            }
            sides[i] = side;
        }

        // wierzchołki L/R
        for (int i = 0; i < pts.Length; i++)
        {
            Vector3 side = sides[i] * half;
            verts.Add(pts[i] - side); // left
            verts.Add(pts[i] + side); // right
        }

        // trójkąty (po dwa na segment)
        for (int i = 0; i < pts.Length - 1; i++)
        {
            int i0 = baseIndex + i * 2;
            int i1 = i0 + 1;
            int i2 = i0 + 2;
            int i3 = i0 + 3;

            tris.Add(i0); tris.Add(i2); tris.Add(i1);
            tris.Add(i1); tris.Add(i2); tris.Add(i3);
        }
    }
}
