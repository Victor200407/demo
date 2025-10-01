using UnityEngine;

[DisallowMultipleComponent]
public class BillboardIcon : MonoBehaviour
{
    [Tooltip("Sprite Twojej ikonki wejścia.")]
    public Sprite icon;
    [Tooltip("Skala ikonki.")]
    public float scale = 1f;

    Camera _cam;
    SpriteRenderer _sr;

    void Awake()
    {
        _cam = Camera.main;
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr == null)
        {
            var go = new GameObject("Icon");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 5000; // nad wszystkim
        }
        _sr.sprite = icon;
        transform.localScale = Vector3.one * scale;
    }

    public void SetSprite(Sprite s)
    {
        icon = s;
        if (_sr) _sr.sprite = s;
    }

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // Billboard
        Vector3 toCam = (_cam.transform.position - transform.position);
        if (toCam.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
    }
}