// Assets/Scripts/Interaction/ObjectManipulator.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectManipulator : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotateSpeed = 90f;
    public float scaleSpeed = 0.7f;

    Transform target;
    readonly Plane ground = new(Vector3.up, 0f);

    void Update()
    {
        // التحقق من وجود هدف
        if (target == null)
        {
            if (SelectionManager.Current != null)
                target = SelectionManager.Current.transform;
            else
                return;
        }

        var mouse = Mouse.current;

        // تحريك
        if (mouse.leftButton.IsPressed())
        {
            Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            if (ground.Raycast(ray, out float hit))
            {
                Vector3 pos = ray.GetPoint(hit);
                pos.y = target.position.y;
                target.position = Vector3.Lerp(target.position, pos, moveSpeed * Time.deltaTime);
            }
        }

        // تدوير
        if (mouse.rightButton.IsPressed() && Keyboard.current.shiftKey.isPressed)
        {
            float delta = mouse.delta.ReadValue().x;
            target.Rotate(Vector3.up, delta * rotateSpeed * Time.deltaTime, Space.World);
        }

        // مقياس
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.1f)
        {
            float factor = 1 + Mathf.Sign(scroll) * scaleSpeed * Time.deltaTime;
            target.localScale *= factor;
        }
    }
}
