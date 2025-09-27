using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerMoverSphere : MonoBehaviour
{
    public CubeSphereGrid grid;
    public CubeSphereGrid.Face face = CubeSphereGrid.Face.PosZ;
    public int i = 0, j = 0;
    public float moveCooldown = 0.08f;

    private float nextMoveTime;

    void Start()
    {
        if (!grid) grid = FindAnyObjectByType<CubeSphereGrid>();
        i = Mathf.Clamp(i, 0, grid.resolution - 1);
        j = Mathf.Clamp(j, 0, grid.resolution - 1);
        SnapToCell();
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (Time.time < nextMoveTime) return;

        Vector2Int dir = ReadStep();
        if (dir == Vector2Int.zero) return;

        grid.Step(ref face, ref i, ref j, dir.x, dir.y);
        SnapToCell();

        nextMoveTime = Time.time + moveCooldown;
    }

    Vector2Int ReadStep()
    {
        if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame) return new Vector2Int(0, +1);
        if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame) return new Vector2Int(0, -1);
        if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame) return new Vector2Int(-1, 0);
        if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame) return new Vector2Int(+1, 0);
        return Vector2Int.zero;
    }

    void SnapToCell()
    {
        Vector3 p = grid.CellToWorldCenter(face, i, j);
        transform.position = p;

        Vector3 n = grid.SurfaceNormal(p);
        transform.up = n;

        // Wyznacz lokalny "wschód" (kierunek +i po stycznej), a forward ustaw w kierunku +j
        int ii = i, jj = j; var ff = face;
        grid.Step(ref ff, ref ii, ref jj, +1, 0);
        Vector3 pRight = grid.CellToWorldCenter(ff, ii, jj);
        Vector3 rightTangent = Vector3.ProjectOnPlane((pRight - p).normalized, n).normalized;

        if (rightTangent.sqrMagnitude > 0.0001f)
            transform.forward = Vector3.Cross(n, rightTangent); // "do góry" siatki (oś j)
    }
}
