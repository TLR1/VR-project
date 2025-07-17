// Assets/Scripts/Interaction/ClickPicker.cs
using UnityEngine;

/// يوضع على الكاميرا ـ يلتقط المجسّم تحت مؤشر الفأرة عند الضغط.
public class ClickPicker : MonoBehaviour
{
    [Tooltip("الكاميرا المُستخدمة للالتقاط (اتركه فارغًا لاستخدام Camera.main)")]
    public Camera targetCamera;

    void Awake() => targetCamera ??= Camera.main;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SelectionManager.TryPickFromScreenPos(Input.mousePosition, targetCamera);
        }
    }
}
