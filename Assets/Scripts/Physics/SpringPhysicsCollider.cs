using System.Collections.Generic;
using UnityEngine;
using Physics.Materials;

namespace Physics
{
    [RequireComponent(typeof(MassSpringBody))]
    [DisallowMultipleComponent]
    public class SpringPhysicsCollider : PhysicsCollider
    {
        [Header("Collision Settings")]
        [Tooltip("Radius around contact point to select impacted mass points.")]
        public float ImpactRadius = 0.0003f;

        /// <summary>جسم Mass–Spring المرتبط.</summary>
        public MassSpringBody Body { get; private set; }

        // لتجنّب معالجة نابض واحد أكثر من مرة بالإطار ذاته
        internal HashSet<SpringLink> _deformedThisFrame = new HashSet<SpringLink>();

        protected override void Awake()
        {
            base.Awake();
            Body = GetComponent<MassSpringBody>();
        }

        /// <summary>
        /// لإيجاد أبعد نقطة داخل شبكة voxels.
        /// </summary>
        public override Vector3 FindFurthestPoint(Vector3 direction)
        {
            if (Body == null || Body.Points == null || Body.Points.Count == 0)
                return transform.position;

            float maxDot = float.MinValue;
            Vector3 best = transform.position;
            foreach (var p in Body.Points)
            {
                float d = Vector3.Dot(p.Position, direction);
                if (d > maxDot)
                {
                    maxDot = d;
                    best = p.Position;
                }
            }
            return best;
        }

        /// <summary>
        /// معالجة التصادم: لا نطبّق الاندفاع هنا — التوزيع يتم في CollisionSolver.
        /// نحتفظ فقط بالبلاستيكية والكسر إن لزم.
        /// </summary>
        public override void OnCollision(EPAResult epaResult)
        {
            HandleBreak(epaResult.Normal, epaResult.PenetrationDepth);
        }

        /// <summary>
        /// معالجة البلاستيكية ثم الكسر عند الضرورة.
        /// </summary>
        public void HandleBreak(Vector3 normal, float penetration)
        {
            if (Body == null) return;

            // نقطة التصادم بالعالم
            Vector3 cpWorld = FindFurthestPoint(normal);
            Vector3 cpLocal = Body.transform.InverseTransformPoint(cpWorld);
            float breakRadius = ImpactRadius;

            foreach (var s in Body.Springs)
            {
                if (_deformedThisFrame.Contains(s))
                    continue;

                Vector3 midW = (s.PointA.Position + s.PointB.Position) * 0.5f;
                Vector3 midL = Body.transform.InverseTransformPoint(midW);

                if (Vector3.Distance(midL, cpLocal) <= breakRadius)
                {
                    float currLen = Vector3.Distance(s.PointA.Position, s.PointB.Position);

                    if (s.YieldThreshold > 0f && Mathf.Abs(currLen - s.RestLength) > s.YieldThreshold)
                    {
                        s.EvaluatePlasticAndFracture(currLen);
                        OnCollisionDeform();
                        _deformedThisFrame.Add(s);
                    }

                    if (s.FractureThreshold > 0f && Mathf.Abs(currLen - s.RestLength) > s.FractureThreshold)
                    {
                        s.EvaluatePlasticAndFracture(currLen);
                        OnCollisionBreak();
                        _deformedThisFrame.Add(s);
                    }
                }
            }

            Body.Springs.RemoveAll(s => s.IsBroken);
        }
    }
}
