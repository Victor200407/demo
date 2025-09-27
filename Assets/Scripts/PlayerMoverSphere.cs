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
    private Vector2Int lastStepDir = Vector2Int.up;

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

        Vector2Int dir = ReadStep();
        if (dir == Vector2Int.zero)
        {
            nextMoveTime = Mathf.Min(nextMoveTime, Time.time);
            return;
        }

        if (Time.time < nextMoveTime)
            return;

        grid.Step(ref face, ref i, ref j, dir.x, dir.y);
        lastStepDir = dir;
        SnapToCell();

        nextMoveTime = Time.time + moveCooldown;
    }

    Vector2Int ReadStep()
    {
        var kb = Keyboard.current;
        Vector2 input = Vector2.zero;

        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input += Vector2.up;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input += Vector2.down;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input += Vector2.left;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input += Vector2.right;

        if (input.sqrMagnitude < 0.01f)
            return Vector2Int.zero;

        // wybierz dominującą oś (tylko jedna na raz)
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            input = new Vector2(Mathf.Sign(input.x), 0f);
        else if (Mathf.Abs(input.y) > Mathf.Abs(input.x))
            input = new Vector2(0f, Mathf.Sign(input.y));
        else if (lastStepDir != Vector2Int.zero)
            input = lastStepDir;
        else
            input = new Vector2(0f, Mathf.Sign(input.y));

        return new Vector2Int(Mathf.RoundToInt(input.x), Mathf.RoundToInt(input.y));
    }

    void SnapToCell()
    {
        Vector3 p = grid.CellToWorldCenter(face, i, j);
        transform.position = p;

        Vector3 n = grid.SurfaceNormal(p);

        int ii = i, jj = j; var ff = face;
        grid.Step(ref ff, ref ii, ref jj, +1, 0);
        Vector3 pRight = grid.CellToWorldCenter(ff, ii, jj);
        Vector3 tangentI = Vector3.ProjectOnPlane(pRight - p, n).normalized;

        ii = i; jj = j; ff = face;
        grid.Step(ref ff, ref ii, ref jj, 0, +1);
        Vector3 pForward = grid.CellToWorldCenter(ff, ii, jj);
        Vector3 tangentJ = Vector3.ProjectOnPlane(pForward - p, n).normalized;

        if (tangentI.sqrMagnitude < 1e-6f)
            tangentI = Vector3.Cross(tangentJ, n).normalized;
        if (tangentJ.sqrMagnitude < 1e-6f)
            tangentJ = Vector3.Cross(n, tangentI).normalized;

        Vector3 forward = tangentJ;
        Vector3 moveDir = tangentI * lastStepDir.x + tangentJ * lastStepDir.y;
        if (moveDir.sqrMagnitude > 1e-6f)
            forward = moveDir.normalized;

        Vector3 right = Vector3.Cross(n, forward).normalized;
        forward = Vector3.Cross(right, n).normalized;

        transform.rotation = Quaternion.LookRotation(forward, n);
    }
}
