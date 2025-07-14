using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Physics.Materials;

namespace Physics
{
    /// <summary>
    /// يدير عملية كشف الاصطدام (GJK + EPA)، فكّ التداخل، 
    /// وتطبيق اندفاع موزّع على Mass–Spring bodies.
    /// أضف هذا المكوّن إلى GameObject واحد في المشهد.
    /// </summary>
    public class CollisionSolver : MonoBehaviour
    {
        private const float jMax                 = 10f;   // حد أمان للاندفاع
        private const float restitutionThreshold = 0.2f;  // لا ارتداد إن كانت السرعة النسبية أقل
        private const float frictionScale        = 0.5f;  // يضرب بمعامل الاحتكاك في MaterialProfile

        public List<PhysicsCollider> Colliders { get; } = new();

        private void FixedUpdate() => SolveCollisions();

        private void SolveCollisions()
        {
            int n = Colliders.Count;
            if (n < 2) return;

            // مسح قائمة التشوه لأي SpringPhysicsCollider
            foreach (var spc in Colliders.OfType<SpringPhysicsCollider>())
                spc._deformedThisFrame.Clear();

            // فحص كل أزواج الكولايدرز
            for (int i = 0; i < n - 1; i++)
            {
                for (int k = i + 1; k < n; k++)
                {
                    var A = Colliders[i];
                    var B = Colliders[k];

                    // 1) GJK
                    var gjk = RunGJK(A, B);
                    if (!gjk.Collision) continue;

                    // 2) EPA
                    var epa = EPA.Expand(
                        gjk.FinalSimplex,
                        dir => A.FindFurthestPoint(dir) - B.FindFurthestPoint(-dir)
                    );
                    if (!epa.Success) continue;

                    // 3) اجمع الـ bodies
                    var bodyA = A.GetComponent<MassSpringBody>();
                    var bodyB = B.GetComponent<MassSpringBody>();
                    if (bodyA == null || bodyB == null) continue;

                    // 4) تصحيح الموضع (Position Correction)
                    SplitSeparation(A, B, epa.Normal, epa.PenetrationDepth * 0.8f);

                    // 5) حساب اندفاع النقطة
                    Vector3 rA = epa.ContactPoint - bodyA.CenterOfMassWorld();
                    Vector3 rB = epa.ContactPoint - bodyB.CenterOfMassWorld();

                    Vector3 vA   = bodyA.VelocityAtPointWorld(rA);
                    Vector3 vB   = bodyB.VelocityAtPointWorld(rB);
                    Vector3 vRel = vB - vA;
                    float   vRelN = Vector3.Dot(vRel, epa.Normal);

                    if (vRelN >= -restitutionThreshold) continue;

                    float e = 0.5f * (
                        (A.GetComponent<MaterialHolder>()?.Profile.Elasticity ?? 0f) +
                        (B.GetComponent<MaterialHolder>()?.Profile.Elasticity ?? 0f)
                    );

                    float invMassN =
                        bodyA.InvMass + bodyB.InvMass +
                        Vector3.Dot(
                            epa.Normal,
                            Vector3.Cross(bodyA.InvInertiaWorld(rA) * Vector3.Cross(rA, epa.Normal), rA) +
                            Vector3.Cross(bodyB.InvInertiaWorld(rB) * Vector3.Cross(rB, epa.Normal), rB)
                        );

                    float jn = Mathf.Clamp(
                        -(1f + e) * vRelN / invMassN,
                        -jMax, jMax
                    );

                    Vector3 impulse = jn * epa.Normal;

                    // 6) إضافة احتكاك كولوم إذا وُجد
                    Vector3 tangent = vRel - vRelN * epa.Normal;
                    if (tangent.sqrMagnitude > 1e-8f)
                    {
                        tangent.Normalize();
                        float vRelT = Vector3.Dot(vRel, tangent);

                        float invMassT =
                            bodyA.InvMass + bodyB.InvMass +
                            Vector3.Dot(
                                tangent,
                                Vector3.Cross(bodyA.InvInertiaWorld(rA) * Vector3.Cross(rA, tangent), rA) +
                                Vector3.Cross(bodyB.InvInertiaWorld(rB) * Vector3.Cross(rB, tangent), rB)
                            );

                        float jt = -vRelT / invMassT;

                        float muA = A.GetComponent<MaterialHolder>()?.Profile.Friction ?? 0.5f;
                        float muB = B.GetComponent<MaterialHolder>()?.Profile.Friction ?? 0.5f;
                        float mu  = frictionScale * (muA + muB);

                        jt = Mathf.Clamp(jt, -mu * jn, mu * jn);
                        impulse += jt * tangent;
                    }

                    // 7) تطبيق الاندفاع موزعاً
                    bodyA.ApplyImpulseDistributed( impulse,  epa.ContactPoint );
                    bodyB.ApplyImpulseDistributed(-impulse, epa.ContactPoint );

                    // 8) إشعار الكولايدرز
                    A.OnCollision(epa);
                    B.OnCollision(epa);
                }
            }

            // تحديث الحالة (تشوه/كسر)
            foreach (var c in Colliders)
                c.UpdateState();
        }

        #region GJK & EPA Helpers
        private GJKResult RunGJK(PhysicsCollider a, PhysicsCollider b)
        {
            Vector3 dir     = Vector3.forward;
            Vector3 support = a.FindFurthestPoint(dir) - b.FindFurthestPoint(-dir);

            var simplex = new Simplex();
            simplex.Push(support);
            dir = -support;

            for (int i = 0; i < 50; i++)
            {
                support = a.FindFurthestPoint(dir) - b.FindFurthestPoint(-dir);
                if (Vector3.Dot(support, dir) <= 0f)
                    return new GJKResult { Collision = false };

                simplex.Push(support);
                if (simplex.NextSimplex(ref dir))
                    return new GJKResult { Collision = true, FinalSimplex = simplex };
            }

            Debug.LogWarning("GJK reached max iterations.");
            return new GJKResult { Collision = false };
        }
        #endregion

        #region Position Correction
        private void SplitSeparation(PhysicsCollider A, PhysicsCollider B, Vector3 n, float depth)
        {
            Vector3 sepA = -n * (depth * 0.5f);
            Vector3 sepB =  n * (depth * 0.5f);
            ResolveSeparation(A, sepA);
            ResolveSeparation(B, sepB);
        }

        private static void ResolveSeparation(PhysicsCollider col, Vector3 offset)
        {
            if (col is SpringPhysicsCollider spc && spc.Body != null)
            {
                foreach (var p in spc.Body.Points)
                    if (!p.IsFixed) p.Position += offset;
            }
            else
            {
                col.transform.position += offset;
            }
        }
        #endregion
    }
}
