// Assets/Scripts/Camera/FlyCamera.cs
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FlyCamera : MonoBehaviour
{
    [Header("Move / Look")]
    public float lookSpeed = 2f;   // حساسية الماوس
    public float moveSpeed = 10f;  // WASD
    public float boostFactor = 3f;   // عند الضغط على Shift

    float pitch, yaw;

    // --------------------------------------------------
    void OnEnable()     // يُستدعى عند التفعيل
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()    // يُستدعى عند التعطيل
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Start()
    {
        Vector3 a = transform.eulerAngles;
        pitch = a.x;
        yaw = a.y;
    }

    void Update()
    {
        // دوران بالماوس
        pitch -= Input.GetAxis("Mouse Y") * lookSpeed;
        yaw += Input.GetAxis("Mouse X") * lookSpeed;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);

        // حركة WASD
        Vector3 dir = new(
            Input.GetAxisRaw("Horizontal"),
            (Input.GetKey(KeyCode.Space) ? 1 : Input.GetKey(KeyCode.LeftControl) ? -1 : 0),
            Input.GetAxisRaw("Vertical"));

        if (dir.sqrMagnitude > 0)
        {
            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? boostFactor : 1f);
            transform.Translate(dir.normalized * speed * Time.deltaTime, Space.Self);
        }
    }
}
