using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Physics.Materials;

namespace Physics
{
    public class CollisionSolver : MonoBehaviour
    {
        private const float jMax = 10f;
        private const float defaultImpactRadius = 0.3f;

        public List<PhysicsCollider> Colliders { get; private set; }

        private void Awake()
        {
            Colliders = FindObjectsOfType<PhysicsCollider>(true).ToList();
            Debug.Log($"[CollisionSolver] Registered {Colliders.Count} PhysicsColliders.");
        }

        private void FixedUpdate()
        {
            SolveCollisions();
        }

        private void SolveCollisions()
        {
            int n = Colliders.Count;
            if (n < 2) return;

            // Clear per‐frame deformation
            foreach (var spc in Colliders.OfType<SpringPhysicsCollider>())
                spc._deformedThisFrame.Clear();

            // 1. Detect and handle collisions
            for (int i = 0; i < n - 1; i++)
            {
                for (int k = i + 1; k < n; k++)
                {
                    var A = Colliders[i];
                    var B = Colliders[k];
                    var bodyA = A.GetComponent<MassSpringBody>();
                    var bodyB = B.GetComponent<MassSpringBody>();

                    // skip self‐collision
                    if (bodyA != null && bodyA == bodyB)
                        continue;

                    // narrow‐phase GJK
                    var gjk = RunGJK(A, B);
                    A.RegisterCollisionResult(gjk.Collision);
                    B.RegisterCollisionResult(gjk.Collision);
                    if (!gjk.Collision) continue;

                    // wide‐phase EPA
                    var epa = EPA.Expand(
                        gjk.FinalSimplex,
                        d => A.FindFurthestPoint(d) - B.FindFurthestPoint(-d)
                    );
                    if (!epa.Success) continue;

                    bool firstContact = !A.Colliding && !B.Colliding;

                    // restitution & relative velocity
                    float eA = A.GetComponent<MaterialHolder>()?.Profile.Elasticity ?? 0.0f;
                    float eB = B.GetComponent<MaterialHolder>()?.Profile.Elasticity ?? 0.0f;
                    float e = 0.5f * (eA + eB);

                    Vector3 vCOM_A = bodyA != null ? ComputeCOMVelocity(bodyA) : Vector3.zero;
                    Vector3 vCOM_B = bodyB != null ? ComputeCOMVelocity(bodyB) : Vector3.zero;
                    float vRel = Vector3.Dot(vCOM_A - vCOM_B, epa.Normal);

                    Debug.Log($"vRel : {vRel}");

                    if (firstContact && vRel > 0f && bodyA != null && bodyB != null)
                    {
                        float mA = bodyA.TotalMass();
                        float mB = bodyB.TotalMass();
                        float mu = (mA * mB) / (mA + mB);
                        float rawJ = -(1f + e) * mu * vRel;
                        float j = Mathf.Clamp(rawJ, -jMax, jMax);
                        Vector3 impulse = j * epa.Normal;

                        float rA = (A is SpringPhysicsCollider spA) ? spA.ImpactRadius : defaultImpactRadius;
                        float rB = (B is SpringPhysicsCollider spB) ? spB.ImpactRadius : defaultImpactRadius;

                        bodyA.ApplyImpulse(impulse, epa.ContactPoint);
                        bodyA.ApplyLocalDamping(epa.ContactPoint, rA, 0.9f);

                        bodyB.ApplyImpulse(-impulse, epa.ContactPoint);
                        bodyB.ApplyLocalDamping(epa.ContactPoint, rB, 0.9f);

                        Debug.Log($"[Physics] vRel={vRel}, e={e}, j={j}, rawJ={rawJ}, mu={mu}");
                    }

                    // --- Penetration resolution ---

                    float pd = epa.PenetrationDepth;
                    Vector3 fullSep = epa.Normal * pd;

                    float invA = (bodyA != null ? 1f / bodyA.TotalMass() : 0f);
                    float invB = (bodyB != null ? 1f / bodyB.TotalMass() : 0f);
                    float invSum = invA + invB;
                    if (invSum <= 0f) invSum = 1f;

                    float ratioA = invA / invSum;
                    float ratioB = invB / invSum;

                    float sepFactor = Mathf.Clamp(pd / 0.05f, 0.02f, 0.2f);
                    Vector3 sepA = -fullSep * ratioA * sepFactor;
                    Vector3 sepB =  fullSep * ratioB * sepFactor;

                    if (A is SpringPhysicsCollider sp1)
                        sp1.Body.Points.ForEach(p => { if (!p.IsFixed) p.Position += sepA; });
                    else if (!A.IsStatic)
                        A.transform.position += sepA;

                    if (B is SpringPhysicsCollider sp2)
                        sp2.Body.Points.ForEach(p => { if (!p.IsFixed) p.Position += sepB; });
                    else if (!B.IsStatic)
                        B.transform.position += sepB;

                    // ✅ تخميد إضافي بعد التصحيح
                    if (bodyA != null) bodyA.ApplyLocalDamping(epa.ContactPoint, 1.5f, 0.9f);
                    if (bodyB != null) bodyB.ApplyLocalDamping(epa.ContactPoint, 1.5f, 0.9f);

                    // notify break/deform
                    A.OnCollision(epa);
                    var inv = epa; inv.Normal = -epa.Normal;
                    B.OnCollision(inv);
                }
            }

            foreach (var c in Colliders)
                c.UpdateState();
        }


        private GJKResult RunGJK(PhysicsCollider a, PhysicsCollider b)
        {
            Vector3 dir     = Vector3.forward;
            Vector3 support = a.FindFurthestPoint(dir) - b.FindFurthestPoint(-dir);
            var simplex     = new Simplex();
            simplex.Push(support);
            dir = -support;

            const int maxIter = 50;
            for (int i = 0; i < maxIter; i++)
            {
                support = a.FindFurthestPoint(dir) - b.FindFurthestPoint(-dir);
                if (Vector3.Dot(support, dir) <= 0f)
                    return new GJKResult { Collision = false };

                simplex.Push(support);
                if (simplex.NextSimplex(ref dir))
                {
                    bool hit = simplex.Count == 4;
                    return new GJKResult { Collision = hit, FinalSimplex = hit ? simplex : null };
                }
            }

            Debug.LogWarning("[CollisionSolver] GJK max iterations reached.");
            return new GJKResult { Collision = false };
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
            Debug.Log($"[Body] COM velocity after collision: {sumV / totalM}");
            return totalM > 0f ? sumV / totalM : Vector3.zero;
        }
    }
}