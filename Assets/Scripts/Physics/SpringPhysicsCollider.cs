using UnityEngine;
using Physics.Materials;

namespace Physics
{
    [RequireComponent(typeof(MassSpringBody))]
    [DisallowMultipleComponent]
    public class SpringPhysicsCollider : PhysicsCollider
    {
        /// <summary>
        /// خاصية عامة للوصول إلى جسم النوّابض من CollisionSolver.
        /// </summary>
        public MassSpringBody Body { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Body = GetComponent<MassSpringBody>();
        }

        public override Vector3 FindFurthestPoint(Vector3 direction)
        {
            // تنفيذ خاص لإرجاع أبعد نقطة في هذا الكوليدر
            // مثال بسيط: إرجاع مركز الجسم
            return transform.position;
        }

        public override void OnCollision(EPAResult epaResult)
        {
            // هنا يمكنك تمييز نوع الاستجابة حسب بيانات epaResult
            // سنفترض دائماً تطبيق كسر
            HandleBreak(epaResult.Normal, epaResult.PenetrationDepth);
        }

        /// <summary>
        /// يعالج كسر النوابض القريبة من نقطة التصادم.
        /// أصبح هذا الأسلوب عاماً حتى يتم استدعاؤه من CollisionSolver.
        /// </summary>
        public void HandleBreak(Vector3 normal, float penetration)
        {
            if (Body == null) return;

            // احسب نقطة التصادم في محلي الجسم
            Vector3 cpWorld = FindFurthestPoint(normal);
            Vector3 cpLocal = Body.transform.InverseTransformPoint(cpWorld);
            float breakRadius = 0.3f;

            // افحص كل نابض وقم بتقييم الكسر
            foreach (var s in Body.Springs)
            {
                Vector3 midW = (s.PointA.Position + s.PointB.Position) * 0.5f;
                Vector3 midL = Body.transform.InverseTransformPoint(midW);
                if (Vector3.Distance(midL, cpLocal) <= breakRadius)
                {
                    float currLen = Vector3.Distance(s.PointA.Position, s.PointB.Position);
                    s.EvaluatePlasticAndFracture(currLen);
                }
            }

            // أزل النوابض المنكوبة
            Body.Springs.RemoveAll(s => s.IsBroken);
        }
    }
}
