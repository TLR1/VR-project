// Assets/Scripts/Physics/SpringLink.cs
using System;
using UnityEngine;

namespace Physics
{
    public class SpringLink
    {
        public MassPoint PointA { get; }
        public MassPoint PointB { get; }

        public float RestLength { get; private set; }
        public float Stiffness  { get; }
        public float Damping    { get; }

        public float YieldThreshold    { get; }
        public float FractureThreshold { get; }
        public float Plasticity        { get; }

        public bool IsBroken { get; private set; } = false;
        public event Action OnBroken;

        public SpringLink(
            MassPoint pointA,
            MassPoint pointB,
            float stiffness,
            float damping,
            float yieldThreshold,
            float fractureThreshold,
            float plasticity
        )
        {
            PointA = pointA;
            PointB = pointB;

            RestLength        = Vector3.Distance(pointA.Position, pointB.Position);
            Stiffness         = stiffness;
            Damping           = damping;
            YieldThreshold    = yieldThreshold;
            FractureThreshold = fractureThreshold;
            Plasticity        = plasticity;
        }

        public void ApplyForces()
        {
            if (IsBroken) return;

            Vector3 delta = PointA.Position - PointB.Position;
            float currLen = delta.magnitude;
            Vector3 dir   = (currLen > Mathf.Epsilon) ? delta.normalized : Vector3.zero;

            float springForce  = -Stiffness * (currLen - RestLength);
            float dampingForce = -Damping * Vector3.Dot(PointA.Velocity - PointB.Velocity, dir);
            Vector3 totalForce = (springForce + dampingForce) * dir;

            // تطبيق القوى
            PointA.ApplyForce(totalForce);
            PointB.ApplyForce(-totalForce);

            // تقليل السرعة بعد التمدد الشديد
            float stretch = Mathf.Abs(currLen - RestLength);
            if (stretch > RestLength * 0.1f) // تجاوز التشوه المسموح
            {
                if (!PointA.IsFixed) PointA.Velocity *= 0.9f;
                if (!PointB.IsFixed) PointB.Velocity *= 0.9f;
            }
        }


        /// <summary>
        /// يجب مناداة هذا التابع فقط عند التصادم لتحليل الانكسار والتشوه.
        /// </summary>
        public void EvaluatePlasticAndFracture(float currLen)
        {
            if (IsBroken) return;

            float stretch = Mathf.Abs(currLen - RestLength);

            // البلاستيكية
            if (YieldThreshold > 0f && stretch > YieldThreshold)
                RestLength += Plasticity * (currLen - RestLength);

            // الانكسار
            if (FractureThreshold > 0f && stretch > FractureThreshold)
            {
                IsBroken = true;
                OnBroken?.Invoke();
            }
        }

        public void Reset()
        {
            IsBroken   = false;
            RestLength = Vector3.Distance(PointA.Position, PointB.Position);
        }
    }
}
