using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MoleRotateAroundController))]
public class TunnelTraveller : MonoBehaviour
{
    public Planet planet;
    public float moveSpeed = 8f;                    // m/s po ścieżce
    public AnimationCurve ease = AnimationCurve.Linear(0,0,1,1);
    public bool freezeOxygenDrain = true;           // opcjonalnie: wstrzymaj dren tlenu podczas jazdy

    MoleRotateAroundController ctrl;

    void Awake()
    {
        ctrl = GetComponent<MoleRotateAroundController>();
        if (!planet) planet = FindFirstObjectByType<Planet>();
    }

    public IEnumerator Traverse(TunnelEntrance from, TunnelEntrance to, TunnelPath path, System.Action onDone = null)
    {
        if (path == null || !path.IsValid) yield break;

        // wyłącz manualne kopanie/ruch (jak przy Twoim forced-exit)
        if (ctrl) ctrl.BeginForcedExit(); // wykorzystaj blokadę inputu i „niekopanie”; depth lerp wyłącz, żeby nie wynurzał
        // mały hack: zatrzymaj jego forced-exit lerp – jeśli masz flagę, dodaj API, tu pomiń dla prostoty

        float totalLen = path.TotalLength;
        float dist = 0f;

        // wybierz kierunek: bliżej nam do początku czy do końca?
        // (prościej: jeśli 'from' == wejście bliższe path.points[0], idziemy 0→L, inaczej L→0)
        bool forward = (Vector3.Distance(from.transform.position, path.points[0]) 
                        <= Vector3.Distance(from.transform.position, path.points[^1]));
        float sign = forward ? +1f : -1f;
        dist = forward ? 0f : totalLen;

        while (true)
        {
            // krok po łuku
            float step = moveSpeed * Time.deltaTime;
            dist += sign * step;
            if ((forward && dist >= totalLen) || (!forward && dist <= 0f))
                break;

            path.SampleByDistance(dist, out Vector3 pos, out Vector3 tan);
            Vector3 up = (pos - planet.Center).sqrMagnitude > 1e-6f ? (pos - planet.Center).normalized : Vector3.up;
            Vector3 fwd = Vector3.ProjectOnPlane(tan, up);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(up, Vector3.right);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd.normalized, up));

            yield return null;
        }

        // Snap na wyjściu
        Vector3 endPos = to.transform.position;
        Vector3 upEnd = (endPos - planet.Center).normalized;
        Vector3 fwdEnd = Vector3.ProjectOnPlane(to.transform.forward, upEnd).normalized;
        transform.SetPositionAndRotation(endPos, Quaternion.LookRotation(fwdEnd, upEnd));

        // przywróć kontrolę
        // (jeśli użyłeś BeginForcedExit do blokady inputu, dodaj helper w kontrolerze by wyłączyć tylko blokadę)
        // np.: ctrl.EndExternalControl();
        onDone?.Invoke();
    }
}
