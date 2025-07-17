using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LibraryItem2 : MonoBehaviour,
                            IBeginDragHandler,
                            IDragHandler,
                            IEndDragHandler
{
    [Header("References")]
    [SerializeField] private GameObject prefab;    // الجسم ثلاثي الأبعاد الذى سنُنشئه
    [SerializeField] private Image      icon;      // أيقونة الزرّ
    [SerializeField] private Image      dragGhost; // كائن DragGhost داخل الـCanvas

    [Header("Ground Plane")]
    [SerializeField] private float groundY = 0f;   // ارتفاع مستوى الأرض (y = 0 افتراضيًا)

    /* --------------------------------------------------------- */
    private void Awake()
    {
        if (dragGhost == null)
            Debug.LogError("[LibraryItem] DragGhost reference missing !");
    }

    /* ----------------- واجهات نظام السحب -------------------- */
    public void OnBeginDrag(PointerEventData e)
    {
        dragGhost.sprite = icon.sprite;
        dragGhost.color  = new Color(1, 1, 1, 0.35f); // شفافية
        dragGhost.gameObject.SetActive(true);
        dragGhost.transform.position = e.position;
    }

    public void OnDrag(PointerEventData e)
    {
        dragGhost.transform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        dragGhost.gameObject.SetActive(false);

        // حوّل إحداثيات المؤشّر لشعاع من الكاميرا
        Ray ray = Camera.main.ScreenPointToRay(e.position);

        // مستوى أفقى مع محور y عند groundY
        // المعادلة: (P - P0) · n = 0  حيث n = up,  P0 = (0, groundY, 0)
        float denom = Vector3.Dot(Vector3.up, ray.direction);
        if (Mathf.Abs(denom) < 1e-5f)
            return;                       // الشعاع موازٍ للمستوى

        float t = (groundY - ray.origin.y) / denom;
        if (t < 0)
            return;                       // التقاطع خلف الكاميرا

        Vector3 hitPoint = ray.origin + t * ray.direction;

        // إنشاء نسخة من الـPrefab عند موضع التقاطع
        var go = Instantiate(prefab, hitPoint, Quaternion.identity);
        SelectionManager.Register(go);

    }
}
