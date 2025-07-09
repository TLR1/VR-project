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
            Debug.Log("BodyA : " + bodyA + "    -    bodyB : " + bodyB);
            if (bodyA == null || bodyB == null)
                return CollisionEffect.Reflect;

            // حساب متوسط سرعة كل جسم
            Vector3 velocityA = AverageVelocity(bodyA);
            Vector3 velocityB = AverageVelocity(bodyB);

            Vector3 relativeVelocity = velocityA - velocityB;
            float impactSpeed = Vector3.Dot(relativeVelocity, result.Normal);
            Debug.Log("impact speed = " + impactSpeed);
            if (impactSpeed <= 0)
                return CollisionEffect.Reflect;

            // حساب الكتلة التقريبية لكل جسم
            float massA = TotalMass(bodyA);
            float massB = TotalMass(bodyB);

            float reducedMass = (massA * massB) / (massA + massB);
            float kineticEnergy = 0.5f * reducedMass * impactSpeed * impactSpeed;

            // استخراج ملف المادة
            var profileA = bodyA.GetComponent<MaterialHolder>()?.Profile;
            var profileB = bodyB.GetComponent<MaterialHolder>()?.Profile;
            Debug.Log($"A Material: {profileA?.name} | Elasticity: {profileA?.Elasticity}, Brittleness: {profileA?.Brittleness}");
            Debug.Log($"B Material: {profileB?.name} | Elasticity: {profileB?.Elasticity}, Brittleness: {profileB?.Brittleness}");
            if (profileA == null || profileB == null)
            {
                Debug.LogWarning("Missing MaterialProfile on one of the MassSpringBodies.");
                return CollisionEffect.Reflect;
            }

            float breakThreshold = Mathf.Min(profileA.BreakEnergyThreshold, profileB.BreakEnergyThreshold);
            float deformThreshold = Mathf.Min(profileA.YieldThreshold, profileB.YieldThreshold);
            float avgElasticity = (profileA.Elasticity + profileB.Elasticity) / 2f;

            // اتخاذ القرار
            if (kineticEnergy >= breakThreshold)
                return CollisionEffect.Break;

            if (kineticEnergy >= deformThreshold)
                return CollisionEffect.Deform;

            return avgElasticity > 0.2f ? CollisionEffect.Reflect : CollisionEffect.Deform;
        }

        private static float TotalMass(MassSpringBody body)
        {
            float total = 0f;
            foreach (var p in body.Points)
                total += p.Mass;
            return total;
        }

        private static Vector3 AverageVelocity(MassSpringBody body)
        {
            if (body.Points.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            foreach (var p in body.Points)
                sum += p.Velocity;
            return sum / body.Points.Count;
        }
    }
}