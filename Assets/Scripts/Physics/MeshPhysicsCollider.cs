using UnityEngine;

namespace Physics
{
    [RequireComponent(typeof(MeshFilter))]
    [DisallowMultipleComponent]
    public class MeshPhysicsCollider : PhysicsCollider
    {
        private Mesh _mesh;

        protected override void Awake()
        {
            base.Awake(); 
            var mf = GetComponent<MeshFilter>();
            _mesh = mf ? mf.mesh : null;
        }

        /// <summary>
        /// إرجاع أبعد نقطة في اتجاه معين على الميش.
        /// </summary>
        public override Vector3 FindFurthestPoint(Vector3 direction)
        {
            if (_mesh == null)
                return transform.position;

            // نحوّل الاتجاه للعالم المحلي للمش
            Vector3 localDir = transform.InverseTransformDirection(direction).normalized;

            Vector3 bestVertex = Vector3.zero;
            float maxDot = float.MinValue;

            foreach (var v in _mesh.vertices)
            {
                float d = Vector3.Dot(v, localDir);
                if (d > maxDot)
                {
                    maxDot = d;
                    bestVertex = v;
                }
            }

            // ثم نرجعه إلى الإحداثيات العالمية
            return transform.TransformPoint(bestVertex);
        }

        /// <summary>
        /// نستطيع هنا إعادة تعريف OnCollision لو أردنا تأثيرات صلبة (انعكاس).
        /// مثلاً:
        /// public override void OnCollision(EPAResult e)
        /// {
        ///     OnCollisionReflect(e.Normal);
        /// }
        /// </summary>
    }
}