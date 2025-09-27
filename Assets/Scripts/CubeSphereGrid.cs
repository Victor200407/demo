using System;
using UnityEngine;


[ExecuteAlways]
public class CubeSphereGrid : MonoBehaviour
{
    public enum Face { PosX=0, NegX=1, PosY=2, NegY=3, PosZ=4, NegZ=5 }
    public enum Projection { Spherify, Equiangular, Gnomonic }

    [Header("Geometria")]
    [Min(0.1f)] public float radius = 5f;
    [Min(2)]    public int   resolution = 64;
    public Projection projection = Projection.Equiangular;

    [Header("Remap wyglądu siatki")]
    [Range(0f,1f)] public float edgeTighten    = 0.0f; // delikatne prostowanie linii przy szwach
    [Range(0f,1f)] public float cornerSquare   = 0.35f; // „kwadratowanie” naroży (przy wierzchołkach kostki)
    [Range(0.5f,4f)] public float cornerPower  = 1.5f;  // szybkość narastania efektu przy samym rogu
    [Range(0f,1f)] public float mixToSpherify  = 0.0f;  // lekki miks do Spherify (wyrównuje pola)
    [Range(0f,1f)] public float uniformCorners = 1.0f;  // wzmacnia wyrównywanie komórek przy narożach

    public Vector3 Center => transform.position;
    public int N => Mathf.Max(2, resolution);

    public event Action<CubeSphereGrid> OnChanged;

    public void RaiseChanged()
    {
        OnChanged?.Invoke(this);
    }

    [ContextMenu("RefreshPreview")]
    void RefreshPreview()
    {
        RaiseChanged();
    }
    
    // ====== PUBLIC API ======

    /// Środek komórki (face,i,j) w świecie.
    public Vector3 CellToWorldCenter(Face face, int i, int j)
    {
        UVFromIJ(i, j, out float u, out float v);
        Vector3 dir = ProjectDir(face, u, v);
        return Center + dir * radius;
    }

    /// Normalna sfery w punkcie (prosto od środka).
    public Vector3 SurfaceNormal(Vector3 worldPos) => (worldPos - Center).normalized;

    /// Krok o (dx,dy) = (±1,0) lub (0,±1) z automatycznym przejściem między ścianami.
    public void Step(ref Face face, ref int i, ref int j, int dx, int dy)
    {
        float du = 2f / resolution, dv = 2f / resolution;
        UVFromIJ(i, j, out float u, out float v);
        u += dx * du; v += dy * dv;

        if (u >= -1f && u <= 1f && v >= -1f && v <= 1f)
        { IJFromUV(u, v, out i, out j); ClampIJ(ref i, ref j); return; }

        Vector3 p = CubePoint(face, u, v);
        Face newFace = DominantFace(p);
        CubeToFaceUV(newFace, p, out float u2, out float v2);
        IJFromUV(u2, v2, out i, out j);
        face = newFace;
        ClampIJ(ref i, ref j);
    }

    /// Punkt świata na linii siatki (u=const lub v=const). t01 ∈ [0,1].
    public Vector3 EdgeSampleWorld(Face face, bool vertical, int index, float t01)
    {
        float constCoord = -1f + (2f * Mathf.Clamp(index, 0, N)) / N;
        float varCoord   = Mathf.Lerp(-1f, +1f, Mathf.Clamp01(t01));

        float u = vertical ? constCoord : varCoord;
        float v = vertical ? varCoord   : constCoord;

        Vector3 dir = ProjectDir(face, u, v);

        return Center + dir * radius;
    }

    /// Cała polilinia linii siatki (segs+1 punktów).
    public Vector3[] EdgePolylineWorld(Face face, bool vertical, int index, int segs)
    {
        segs = Mathf.Max(8, segs);
        var pts = new Vector3[segs + 1];
        for (int s = 0; s <= segs; s++)
        {
            float t = s / (float)segs;
            pts[s] = EdgeSampleWorld(face, vertical, index, t);
        }
        return pts;
    }

    /// Kierunek promienia dla (face,u,v) wg bieżącej projekcji.
    public Vector3 ProjectDir(Face face, float u, float v)
    {
        Vector2 uv = RemapUV(face, u, v);
        Vector3 dir = projection switch
        {
            Projection.Gnomonic    => GnomonicDir(face, uv.x, uv.y),
            Projection.Equiangular => EquiangularDir(face, uv.x, uv.y),
            _                      => SpherifyDir(face, uv.x, uv.y),
        };

        if (mixToSpherify > 0f)
            dir = Vector3.Slerp(dir, SpherifyDir(face, uv.x, uv.y), mixToSpherify).normalized;

        return dir.normalized;
    }

    // ====== INTERNAL ======

    void UVFromIJ(int i, int j, out float u, out float v)
    {
        u = ((i + 0.5f) / resolution) * 2f - 1f; // środki komórek w [-1,1]
        v = ((j + 0.5f) / resolution) * 2f - 1f;
    }

    void IJFromUV(float u, float v, out int i, out int j)
    {
        float fu = (u + 1f) * 0.5f * resolution;
        float fv = (v + 1f) * 0.5f * resolution;
        i = Mathf.FloorToInt(fu);
        j = Mathf.FloorToInt(fv);
    }

    void ClampIJ(ref int i, ref int j)
    { i = Mathf.Clamp(i, 0, resolution - 1); j = Mathf.Clamp(j, 0, resolution - 1); }

    Vector3 CubePoint(Face f, float u, float v) => f switch
    {
        Face.PosX => new Vector3(+1f, v, -u),
        Face.NegX => new Vector3(-1f, v, +u),
        Face.PosY => new Vector3(+u, +1f, v),
        Face.NegY => new Vector3(+u, -1f,-v),
        Face.PosZ => new Vector3(+u, v, +1f),
        _         => new Vector3(-u, v, -1f),
    };

    Face DominantFace(Vector3 p)
    {
        float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y), az = Mathf.Abs(p.z);
        if (ax >= ay && ax >= az) return p.x >= 0 ? Face.PosX : Face.NegX;
        if (ay >= ax && ay >= az) return p.y >= 0 ? Face.PosY : Face.NegY;
        return p.z >= 0 ? Face.PosZ : Face.NegZ;
    }

    void CubeToFaceUV(Face f, Vector3 p, out float u, out float v)
    {
        switch (f)
        {
            case Face.PosX: { float inv = 1f / Mathf.Abs(p.x); u = -p.z * inv; v =  p.y * inv; break; }
            case Face.NegX: { float inv = 1f / Mathf.Abs(p.x); u =  p.z * inv;  v =  p.y * inv; break; }
            case Face.PosY: { float inv = 1f / Mathf.Abs(p.y); u =  p.x * inv;  v =  p.z * inv; break; }
            case Face.NegY: { float inv = 1f / Mathf.Abs(p.y); u =  p.x * inv;  v = -p.z * inv; break; }
            case Face.PosZ: { float inv = 1f / Mathf.Abs(p.z); u =  p.x * inv;  v =  p.y * inv; break; }
            default:         { float inv = 1f / Mathf.Abs(p.z); u = -p.x * inv; v =  p.y * inv; break; }
        }
    }

    // --- Remap UV: wyrównanie przy narożach oraz kosmetyka szwów ---
    Vector2 RemapUV(Face face, float u, float v)
    {
        Vector2 uv = new Vector2(u, v);
        uv = UniformizeUV(face, uv);

        // Maska: 1 w środku płytki, 0 przy samych krawędziach (żeby szew był identyczny na obu ścianach)
        float EdgeMask(float x)
        {
            // |x|=1 -> 0; |x|=0 -> 1. Łagodny spadek przy krawędzi.
            float a = 1f - Mathf.Clamp01((Mathf.Abs(x) - 0.95f) / 0.05f); // 0.95..1.00 map -> 1..0
            return 1f - a; // 0 przy krawędzi, ~1 w środku
        }

        float maskU = EdgeMask(uv.x);
        float maskV = EdgeMask(uv.y);
        float mask  = Mathf.Min(maskU, maskV); // jeśli dotykamy dowolnej krawędzi, maska≈0

        // 1) delikatne prostowanie przy szwach (tylko gdy mask>0)
        if (edgeTighten > 0f && mask > 0f)
        {
            float Remap1(float x)
            {
                float t = (x + 1f) * 0.5f;      // [-1,1] -> [0,1]
                t = t*t*(3f - 2f*t);           // smoothstep
                t = Mathf.Sin(t * Mathf.PI*0.5f);
                return t * 2f - 1f;
            }
            float k = edgeTighten * mask;
            uv.x = Mathf.Lerp(uv.x, Remap1(uv.x), k);
            uv.y = Mathf.Lerp(uv.y, Remap1(uv.y), k);
        }

        // 2) „kwadratowanie” naroży (też wyciszamy przy samych brzegach)
        if (cornerSquare > 0f && mask > 0f)
        {
            float au = Mathf.Abs(uv.x), av = Mathf.Abs(uv.y);
            float f  = Mathf.Pow(au * av, cornerPower);               // rośnie ku narożu
            float gamma = Mathf.Lerp(1f, 0.6f, (cornerSquare * f) * mask);
            uv.x = Mathf.Sign(uv.x) * Mathf.Pow(au, gamma);
            uv.y = Mathf.Sign(uv.y) * Mathf.Pow(av, gamma);
        }

        return uv;
    }

    Vector2 UniformizeUV(Face face, Vector2 uv)
    {
        float strength = Mathf.Clamp01(uniformCorners);
        if (strength <= 0f)
            return new Vector2(Mathf.Clamp(uv.x, -1f, 1f), Mathf.Clamp(uv.y, -1f, 1f));

        float sampleStep = Mathf.Min(0.45f, 1.5f / Mathf.Max(2, resolution));
        for (int iter = 0; iter < 3; iter++)
        {
            float lenU = EstimateSpacing(face, uv, sampleStep, true);
            float lenV = EstimateSpacing(face, uv, sampleStep, false);
            if (lenU <= 1e-6f || lenV <= 1e-6f)
                break;

            float ratio = lenU / lenV;
            if (Mathf.Abs(ratio - 1f) < 1e-3f)
                break;

            float corner = Mathf.Max(Mathf.Abs(uv.x), Mathf.Abs(uv.y));
            float weight = strength * corner * corner; // najmocniej przy narożach
            if (weight <= 0f)
                break;

            float adjust = Mathf.Pow(Mathf.Clamp(ratio, 0.1f, 10f), 0.5f);
            adjust = Mathf.Lerp(1f, adjust, weight);

            uv.x = Mathf.Clamp(uv.x / adjust, -1f, 1f);
            uv.y = Mathf.Clamp(uv.y * adjust, -1f, 1f);
        }

        return uv;
    }

    float EstimateSpacing(Face face, Vector2 uv, float delta, bool horizontal)
    {
        float du = horizontal ? delta : 0f;
        float dv = horizontal ? 0f : delta;

        float u0 = Mathf.Clamp(uv.x - du, -1f, 1f);
        float v0 = Mathf.Clamp(uv.y - dv, -1f, 1f);
        float u1 = Mathf.Clamp(uv.x + du, -1f, 1f);
        float v1 = Mathf.Clamp(uv.y + dv, -1f, 1f);

        Vector3 dir0 = ProjectDirRaw(face, u0, v0);
        Vector3 dir1 = ProjectDirRaw(face, u1, v1);
        float dot = Mathf.Clamp(Vector3.Dot(dir0, dir1), -1f, 1f);
        return Mathf.Acos(dot);
    }

    Vector3 ProjectDirRaw(Face face, float u, float v)
    {
        Vector3 dir = projection switch
        {
            Projection.Gnomonic    => GnomonicDir(face, u, v),
            Projection.Equiangular => EquiangularDir(face, u, v),
            _                      => SpherifyDir(face, u, v),
        };

        if (mixToSpherify > 0f)
            dir = Vector3.Slerp(dir, SpherifyDir(face, u, v), mixToSpherify).normalized;

        return dir.normalized;
    }


    // --- Projekcje ---
    Vector3 GnomonicDir(Face f, float u, float v)
    {
        Vector3 d = f switch
        {
            Face.PosX => new Vector3(+1f, v, -u),
            Face.NegX => new Vector3(-1f, v, +u),
            Face.PosY => new Vector3(+u, +1f, v),
            Face.NegY => new Vector3(+u, -1f,-v),
            Face.PosZ => new Vector3(+u, v, +1f),
            _         => new Vector3(-u, v, -1f),
        };
        return d.normalized;
    }

    Vector3 EquiangularDir(Face f, float u, float v)
    {
        const float K = 0.78539816339f; // pi/4
        float tu = Mathf.Tan(K * u), tv = Mathf.Tan(K * v);
        Vector3 d = f switch
        {
            Face.PosX => new Vector3(+1f, tv, -tu),
            Face.NegX => new Vector3(-1f, tv, +tu),
            Face.PosY => new Vector3(+tu, +1f, tv),
            Face.NegY => new Vector3(+tu, -1f,-tv),
            Face.PosZ => new Vector3(+tu, tv, +1f),
            _         => new Vector3(-tu, tv, -1f),
        };
        return d.normalized;
    }

    Vector3 SpherifyDir(Face f, float u, float v)
    {
        Vector3 c = CubePoint(f, u, v);
        float x=c.x,y=c.y,z=c.z, x2=x*x,y2=y*y,z2=z*z;
        float sx = x * Mathf.Sqrt(1f - (y2/2f) - (z2/2f) + (y2*z2/3f));
        float sy = y * Mathf.Sqrt(1f - (z2/2f) - (x2/2f) + (z2*x2/3f));
        float sz = z * Mathf.Sqrt(1f - (x2/2f) - (y2/2f) + (x2*y2/3f));
        return new Vector3(sx, sy, sz).normalized;
    }
}
