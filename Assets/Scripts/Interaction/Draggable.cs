using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Draggable : MonoBehaviour
{
    [Tooltip("السرعة (نعومة) انتقال الجسم إلى الموضع الجديد")]
    public float followSpeed = 10f;

    static Plane ground = new Plane(Vector3.up, Vector3.zero); // Y = 0
    static Camera cam;

    bool dragging;
    float yOffset;          // ارتفاع العنصر الأصلى (نبقيه ثابتاً)

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    // ----------------------------------------------------
    void OnMouseDown()        // يلتقطها الـCollider تلقائياً
    {
        if (cam == null) return;

        yOffset = transform.position.y;
        dragging = true;

        // اجعل هذا العنصر فى طبقة IgnoreRaycast حتى لا يعرقل الأشعة أثناء السحب
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    void OnMouseUp()
    {
        dragging = false;
        gameObject.layer = 0;   // Default
    }

    void Update()
    {
        if (!dragging || cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (ground.Raycast(ray, out float hit))
        {
            Vector3 targetPos = ray.GetPoint(hit);
            targetPos.y = yOffset;                    // ثبّت الارتفاع

            // انتقال ناعم
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                followSpeed * Time.deltaTime);
        }
    }
}
