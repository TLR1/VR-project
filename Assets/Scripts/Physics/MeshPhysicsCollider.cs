using UnityEngine;

namespace Physics
{


// لا نحتاج هنا الـ OnEnable/OnDisable لأن الأبّ يَسِيرُ على التسجيل/الإلغاء تلقائيّاً
    [RequireComponent(typeof(MeshFilter))]
    public class MeshPhysicsCollider : PhysicsCollider
    {
        private Mesh _mesh;

        // إذا كان في الأبّ:
        //   protected virtual void Awake() { … }
        // يمكننا هنا override
        protected override void Awake()
        {
            base.Awake(); // يسجّل نفسه بالـ Solver
            var mf = GetComponent<MeshFilter>();
            _mesh = mf ? mf.mesh : null;
        }

        public override Vector3 FindFurthestPoint(Vector3 direction)
        {
            if (_mesh == null)
                return transform.position;

            // نحوّل الاتجاه للعالم المحلي أولاً
            Vector3 localDir = transform.InverseTransformDirection(direction);
            Vector3 best = Vector3.zero;
            float max = float.MinValue;

            foreach (var v in _mesh.vertices)
            {
                float d = Vector3.Dot(v, localDir);
                if (d > max)
                {
                    max = d;
                    best = v;
                }
            }

            // ثم نرجعه لنظام العالم
            return transform.TransformPoint(best);
        }
    }
}