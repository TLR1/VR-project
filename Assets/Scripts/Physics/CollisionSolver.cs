using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Physics.Materials;

namespace Physics
{
    public class CollisionSolver : MonoBehaviour
    {
        private const float jMax = 10f;
        public List<PhysicsCollider> Colliders { get; private set; } = new List<PhysicsCollider>();

        private void FixedUpdate()
        {
            SolveCollisions();
        }

        private void SolveCollisions()
        {
            int n = Colliders.Count;
            if (n < 2) return;

            // مسح قائمة التشويه للإطار الحالي
            foreach (var spc in Colliders.OfType<SpringPhysicsCollider>())
                spc._deformedThisFrame.Clear();

            // فحص كل زوج
            for (int i = 0; i < n - 1; i++)
            {
                for (int k = i + 1; k < n; k++)
                {
                    var A = Colliders[i];
                    var B = Colliders[k];

                    // 1) GJK
                    var gjk = RunGJK(A, B);
                    A.RegisterCollisionResult(gjk.Collision);
                    B.RegisterCollisionResult(gjk.Collision);
                    if (!gjk.Collision) 
                        continue;

                    // 2) EPA
                    var epa = EPA.Expand(
                        gjk.FinalSimplex,
                        dir => A.FindFurthestPoint(dir) - B.FindFurthestPoint(-dir)
                    );
                    if (!epa.Success) 
                        continue;

                    // 3) جلب MassSpringBody إن وُجد
                    var bodyA = A.GetComponent<MassSpringBody>();
                    var bodyB = B.GetComponent<MassSpringBody>();
                    if (bodyA == null || bodyB == null)
                        continue;

                    // 4) معامل المرونة
                    float eA = A.GetComponent<MaterialHolder>()?.Profile.Elasticity ?? 0f;
                    float eB = B.GetComponent<MaterialHolder>()?.Profile.Elasticity ?? 0f;
                    float e  = 0.5f * (eA + eB);

                    // 5) حساب الدفع (impulse)
                    Vector3 vCOM_A = ComputeCOMVelocity(bodyA);
                    Vector3 vCOM_B = ComputeCOMVelocity(bodyB);
                    float vRel = Vector3.Dot(vCOM_A - vCOM_B, epa.Normal);

                    if (vRel > 0f)
                    {
                        float mA = bodyA.TotalMass();
                        float mB = bodyB.TotalMass();
                        float mu = (mA * mB) / (mA + mB);
                        float rawJ = -(1f + e) * mu * vRel;
                        float j    = Mathf.Clamp(rawJ, -jMax, jMax);
                        Vector3 impulse = j * epa.Normal;

                        bodyA.ApplyImpulse(  impulse, epa.ContactPoint );
                        bodyB.ApplyImpulse( -impulse, epa.ContactPoint );
                    }

                    // 6) فصل الأجسام
                    Vector3 sepA = -epa.Normal * (epa.PenetrationDepth * 0.5f);
                    Vector3 sepB =  epa.Normal * (epa.PenetrationDepth * 0.5f);
                    ResolveSeparation(A, sepA);
                    ResolveSeparation(B, sepB);

                    // 7) إخطار الكولايدرز
                    A.OnCollision(epa);
                    B.OnCollision(epa);
                }
            }

            // 8) إطلاق أحداث الدخول/الخروج
            foreach (var c in Colliders)
                c.UpdateState();
        }

        private GJKResult RunGJK(PhysicsCollider a, PhysicsCollider b)
        {
            Vector3 dir     = Vector3.forward;
            Vector3 support = a.FindFurthestPoint(dir) - b.FindFurthestPoint(-dir);

            var simplex = new Simplex();
            simplex.Push(support);
            dir = -support;

            for (int iter = 0; iter < 50; iter++)
            {
                support = a.FindFurthestPoint(dir) - b.FindFurthestPoint(-dir);
                if (Vector3.Dot(support, dir) <= 0f)
                    return new GJKResult { Collision = false };

                simplex.Push(support);
                if (simplex.NextSimplex(ref dir))
                {
                    bool hit = (simplex.Count == 4);
                    return new GJKResult { Collision = hit, FinalSimplex = hit ? simplex : null };
                }
            }

            Debug.LogWarning("GJK max iterations reached without resolution.");
            return new GJKResult { Collision = false };
        }

        private void ResolveSeparation(PhysicsCollider col, Vector3 sep)
        {
            // إذا كان SpringPhysicsCollider: نفصل كل نقاط الجسم
            if (col is SpringPhysicsCollider spc && spc.Body != null)
            {
                foreach (var p in spc.Body.Points)
                    if (!p.IsFixed)
                        p.Position += sep;
            }
            else
            {
                // لأي Collider آخر نفصله بتحريك الترانسفورم
                col.transform.position += sep;
            }
        }

        private Vector3 ComputeCOMVelocity(MassSpringBody body)
        {
            float totalM = 0f;
            Vector3 sumV = Vector3.zero;
            foreach (var p in body.Points)
            {
                sumV   += p.Velocity * p.Mass;
                totalM += p.Mass;
            }
            return (totalM > 0f) ? sumV / totalM : Vector3.zero;
        }
    }
}
