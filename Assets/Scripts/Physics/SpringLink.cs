using System;
using UnityEngine;

namespace Physics
{
    public class SpringLink
    {
        public MassPoint PointA => A;
        public MassPoint PointB => B;
        public MassPoint A, B;
        public float RestLength;
        public float Stiffness = 1000f;
        public float Damping   = 50f;

        public float YieldThreshold    = 0f;   // displacement عند بداية البلاستيكية
        public float FractureThreshold = 0f;   // displacement عند الكسر التام
        public float Plasticity        = 0.1f; // نسبة تمدد RestLength عند البلاستيكية
        public bool  IsBroken          = false;

        public event Action OnBroken;

        public SpringLink(MassPoint a, MassPoint b)
        {
            A = a; B = b;
            RestLength = Vector3.Distance(a.Position, b.Position);
        }

        public SpringLink(MassPoint a, MassPoint b, float stiffness, float damping)
            : this(a, b)
        {
            Stiffness = stiffness;
            Damping   = damping;
        }

        public void ApplyForces()
        {
            if (IsBroken) return;

            Vector3 delta = A.Position - B.Position;
            float currLen = delta.magnitude;
            Vector3 dir   = (currLen > Mathf.Epsilon) ? delta.normalized : Vector3.zero;

            // قانون هوك + تخميد نسبِي
            float springForce  = -Stiffness * (currLen - RestLength);
            float dampingForce = -Damping * Vector3.Dot(A.Velocity - B.Velocity, dir);
            Vector3 force      = (springForce + dampingForce) * dir;

            A.ApplyForce(force);
            B.ApplyForce(-force);

            // تقييم البلاستيكية والكسر
            EvaluatePlasticAndFracture(currLen);
        }

        public void EvaluatePlasticAndFracture(float currentLength)
        {
            float strain = Mathf.Abs(currentLength - RestLength);

            // بلاستيكية: إذا تجاوز العتبة
            if (YieldThreshold > 0f && strain > YieldThreshold)
            {
                RestLength += Plasticity * (currentLength - RestLength);
            }

            // كسر تام
            if (FractureThreshold > 0f && strain > FractureThreshold)
            {
                IsBroken = true;
                OnBroken?.Invoke();
            }
        }

        public void Reset()
        {
            IsBroken    = false;
            RestLength  = Vector3.Distance(A.Position, B.Position);
        }
    }
}
