using UnityEngine;
using System;

/// <summary>
/// يمثل نابضاً بين نقطتين كتليتين، يحسب قوة هوك والتخميد،
/// ويدعم بلاستيكية وكسر عند تجاوز العتبات.
/// </summary>
public class SpringLink
{
    // النقاط المرتبطة
    public MassPoint A, B;
    public MassPoint PointA => A;  // للتوافق مع SpringPhysicsCollider
    public MassPoint PointB => B;  // للتوافق مع SpringPhysicsCollider

    public float RestLength;
    public float Stiffness = 1000f;
    public float Damping = 50f;

    // عتبات البلاستيكية (Yield) والكسر (Fracture)
    public float YieldThreshold = 0f;
    public float FractureThreshold = 0f;
    public bool IsBroken = false;
    public float Plasticity = 0.1f;

    /// <summary>
    /// حدث يُطلق عند كسر النابض.
    /// </summary>
    public event Action OnBroken;

    /// <summary>
    /// طاقة التشوه الحالية (½·k·x²).
    /// </summary>
    public float CurrentStrainEnergy
    {
        get
        {
            float x = Vector3.Distance(A.Position, B.Position) - RestLength;
            return 0.5f * Stiffness * x * x;
        }
    }

    /// <summary>
    /// إنشاء نابض مع ضبط طول الراحة من المواضع الابتدائية.
    /// </summary>
    public SpringLink(MassPoint a, MassPoint b)
    {
        A = a;
        B = b;
        RestLength = Vector3.Distance(a.Position, b.Position);
    }

    /// <summary>
    /// إنشاء نابض مع تحديد الصلابة والتخميد.
    /// </summary>
    public SpringLink(MassPoint a, MassPoint b, float stiffness, float damping)
        : this(a, b)
    {
        Stiffness = stiffness;
        Damping = damping;
    }

    /// <summary>
    /// يحسب ويطبق قوى النوابض، ويعالج البلاستيكية والكسر.
    /// </summary>
    public void ApplyForces()
    {
        if (IsBroken) return;

        Vector3 delta = A.Position - B.Position;
        float currLen = delta.magnitude;
        Vector3 dir = (currLen > Mathf.Epsilon) ? delta.normalized : Vector3.zero;

        // قانون هوك + تخميد
        float springForce = -Stiffness * (currLen - RestLength);
        float dampingForce = -Damping * Vector3.Dot(A.Velocity - B.Velocity, dir);
        Vector3 force = (springForce + dampingForce) * dir;

        A.ApplyForce(force);
        B.ApplyForce(-force);

        // البلاستيكية والكسر عبر الاستدعاء الموحد
        EvaluatePlasticAndFracture(currLen);
    }

    /// <summary>
    /// يقيم البلاستيكية ويحدث الكسر إن لزم.
    /// (مهم لSpringPhysicsCollider)
    /// </summary>
    public void EvaluatePlasticAndFracture(float currentLength)
    {
        float strain = Mathf.Abs(currentLength - RestLength);
        if (YieldThreshold > 0f && strain > YieldThreshold)
            RestLength += Plasticity * (currentLength - RestLength);

        if (FractureThreshold > 0f && strain > FractureThreshold)
        {
            IsBroken = true;
            OnBroken?.Invoke();
        }
    }

    /// <summary>
    /// إعادة تهيئة النابض إلى حالته الابتدائية (قبل التكسر).
    /// </summary>
    public void Reset()
    {
        IsBroken = false;
        RestLength = Vector3.Distance(A.Position, B.Position);
    }
}
