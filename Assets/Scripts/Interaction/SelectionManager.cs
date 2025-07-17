// Assets/Scripts/Interaction/SelectionManager.cs
using System;
using UnityEngine;

/// يدير اختيار مجسّم واحد فى المشهد.
public static class SelectionManager
{
    /// آخر جسم مختار
    public static GameObject Current { get; private set; }

    /// حدث يُستدعى عند التغيير
    public static event Action<GameObject> OnSelect;

    /// جرّب اختيار جسم يُطلَق عليه شعاع من موضع شاشة
    public static void TryPickFromScreenPos(Vector2 screenPos, Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (UnityEngine.Physics.Raycast(ray, out RaycastHit hit))
        {
            GameObject picked = hit.collider.gameObject;

            if (picked != Current)
            {
                Current = picked;
                OnSelect?.Invoke(picked);
            }
        }
    }

    /// اختيار جسم يدويًا
    public static void Register(GameObject go)
    {
        if (go != Current)
        {
            Current = go;
            OnSelect?.Invoke(go);
        }
    }

    /// ألغِ الاختيار (اختياري)
    public static void Clear()
    {
        Current = null;
        OnSelect?.Invoke(null);
    }
}
