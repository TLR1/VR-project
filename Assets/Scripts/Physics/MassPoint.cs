using UnityEngine;

/// <summary>
/// يمثل نقطة كتلية تستخدم في تمثيل Mass–Spring.
/// يدعم تجمع القوى وإعادة تعيينها، ويحتوي على بيانات للحركة.
/// </summary>
public class MassPoint
{
    public Vector3 Position;
    public Vector3 PreviousPosition;
    public Vector3 Velocity;
    public Vector3 Force;
    public float Mass = 1f;
    public bool IsFixed = false;
    public MassPoint() { }

    /// <summary>
    /// منشئ لتهيئة الموضع، الكتلة، والحالة الثابتة.
    /// </summary>
    public MassPoint(Vector3 position, float mass = 1f, bool isFixed = false)
    {
        Position = position;
        PreviousPosition = position;
        Velocity = Vector3.zero;
        Force = Vector3.zero;
        Mass = mass;
        IsFixed = isFixed;
    }

    /// <summary>إعادة تعيين مجموع القوى المؤثرة.</summary>
    public void ResetForce() => Force = Vector3.zero;

    /// <summary>تطبيق قوة إضافية على النقطة.</summary>
    public void ApplyForce(Vector3 f) => Force += f;
}
