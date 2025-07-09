using System.Collections.Generic;
using UnityEngine;
using Physics.Materials;

namespace Physics
{
    [RequireComponent(typeof(MassSpringBody))]
    [DisallowMultipleComponent]
    public class SpringPhysicsCollider : PhysicsCollider
    {
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

            // نبحث عن الـ MassPoint بأكبر قيمة dot
            float maxDot = float.MinValue;
            Vector3 best = transform.position;

            foreach (var p in Body.Points)
            {
                Vector3 worldPos = p.Position;
                float d = Vector3.Dot(worldPos, direction);
                if (d > maxDot)
                {
                    maxDot = d;
                    best = worldPos;
                }
            }

            return best;
        }

        public override void OnCollision(EPAResult epaResult)
        {
            // هنا نستدعي HandleBreak (Break + Plastic) حسب الضغط
            HandleBreak(epaResult.Normal, epaResult.PenetrationDepth);
        }

        /// <summary>
        /// معالجة البلاستيكيّة ثم الكسر عند الضرورة.
        /// </summary>
        public void HandleBreak(Vector3 normal, float penetration)
        {
            if (Body == null) return;

            // نقطة التصادم بالعالم
            Vector3 cpWorld = FindFurthestPoint(normal);
            // نحتاج للإحداثيات المحلية للفحص
            Vector3 cpLocal = Body.transform.InverseTransformPoint(cpWorld);

            // نصف قطر التأثير (يمكن جعله متغيراً إذا شئنا)
            float breakRadius = 0.3f;

            foreach (var s in Body.Springs)
            {
                // نتجنّب معالجة النابض ذاته أكثر من مرة
                if (_deformedThisFrame.Contains(s))
                    continue;

                // منتصف النابض
                Vector3 midW = (s.PointA.Position + s.PointB.Position) * 0.5f;
                Vector3 midL = Body.transform.InverseTransformPoint(midW);

                // إن كان ضمن نصف القطر
                if (Vector3.Distance(midL, cpLocal) <= breakRadius)
                {
                    // احفظ الطول قبل التقييم
                    float currLen = Vector3.Distance(s.PointA.Position, s.PointB.Position);

                    // تقييم البلاستيكية
                    if (s.YieldThreshold > 0f && Mathf.Abs(currLen - s.RestLength) > s.YieldThreshold)
                    {
                        s.EvaluatePlasticAndFracture(currLen);
                        // أشعِر deformation event
                        OnCollisionDeform();
                        _deformedThisFrame.Add(s);
                    }

                    // تقييم الكسر
                    if (s.FractureThreshold > 0f && Mathf.Abs(currLen - s.RestLength) > s.FractureThreshold)
                    {
                        s.EvaluatePlasticAndFracture(currLen);
                        // أشعِر break event
                        OnCollisionBreak();
                        _deformedThisFrame.Add(s);
                    }
                }
            }

            // أزل النوابض المنكوبة
            Body.Springs.RemoveAll(s => s.IsBroken);
        }
    }
}
