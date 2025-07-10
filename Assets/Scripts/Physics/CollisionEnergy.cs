// Assets/Scripts/Physics/CollisionEnergy.cs
using UnityEngine;
using Physics.Materials;

namespace Physics
{
    public static class CollisionEnergy
    {
        public static CollisionEffect EvaluateCollisionEffect(
            MassSpringBody bodyA,
            MassSpringBody bodyB,
            EPAResult result)
        {
            if (bodyA == null || bodyB == null)
                return CollisionEffect.Reflect;

            // حساب متوسط السرعات
            Vector3 vA = AverageVelocity(bodyA);
            Vector3 vB = AverageVelocity(bodyB);

            // سرعة تأثير على طول normal
            Vector3 relV = vA - vB;
            float impactSpeed = Vector3.Dot(relV, result.Normal);
            if (impactSpeed <= 0f)
                return CollisionEffect.Reflect;

            // الكتل
            float mA = TotalMass(bodyA);
            float mB = TotalMass(bodyB);
            float mu = (mA * mB) / (mA + mB);
            float KE = 0.5f * mu * impactSpeed * impactSpeed;

            // قراءة المواد
            var profA = bodyA.GetComponent<MaterialHolder>()?.Profile;
            var profB = bodyB.GetComponent<MaterialHolder>()?.Profile;
            if (profA == null || profB == null)
                return CollisionEffect.Reflect;

            // اختيار القيم الدنيا للعتبات
            float breakTh  = Mathf.Min(profA.BreakThreshold, profB.BreakThreshold);
            float deformTh = Mathf.Min(profA.YieldThreshold, profB.YieldThreshold);
            float avgE      = (profA.Elasticity + profB.Elasticity) * 0.5f;

            // القرار
            if (KE >= breakTh)   return CollisionEffect.Break;
            if (KE >= deformTh)  return CollisionEffect.Deform;

            // أقل من تشوهٍ دائم: نرتد إذا الارتداد كافٍ، وإلا نشوه خفيف
            return avgE > 0.2f ? CollisionEffect.Reflect : CollisionEffect.Deform;
        }

        private static float TotalMass(MassSpringBody body)
        {
            float sum = 0f;
            foreach (var p in body.Points)
                sum += p.Mass;
            return sum;
        }

        private static Vector3 AverageVelocity(MassSpringBody body)
        {
            if (body.Points.Count == 0) return Vector3.zero;
            Vector3 s = Vector3.zero;
            foreach (var p in body.Points)
                s += p.Velocity;
            return s / body.Points.Count;
        }
    }
}
