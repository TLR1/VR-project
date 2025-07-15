using UnityEngine;

/// <summary>
/// يمثل نقطة كتلية تستخدم في تمثيل Mass–Spring.
/// يدعم جمع القوى، التثبيت، والتحكم في السرعة.
/// </summary>
public class MassPoint
{
    public Vector3 Position;
    public Vector3 PreviousPosition;
    public Vector3 Velocity;
    public Vector3 Force;

    public float Mass = 1f;
    public bool IsFixed = false;

    /// <summary>هل تم تفعيل التخميد المحلي على هذه النقطة؟</summary>
    public bool LocallyDamped = false;

    public MassPoint() { }

    public MassPoint(Vector3 position, float mass = 1f, bool isFixed = false)
    {
        Position = position;
        PreviousPosition = position;
        Velocity = Vector3.zero;
        Force = Vector3.zero;
        Mass = mass;
        IsFixed = isFixed;
        LocallyDamped = false;
    }

    /// <summary>إعادة تعيين القوى المجمعة لهذه النقطة.</summary>
    public void ResetForce()
    {
        Force = Vector3.zero;
        LocallyDamped = false;
    }

    /// <summary>تطبيق قوة على النقطة.</summary>
    public void ApplyForce(Vector3 f) => Force += f;
}