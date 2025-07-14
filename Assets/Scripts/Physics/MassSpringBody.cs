using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Physics
{
    [RequireComponent(typeof(MeshFilter))]
    public class MassSpringBody : MonoBehaviour
    {
        public enum IntegrationType { Verlet, Euler }

        [Header("Particles & Springs")]
        public List<MassPoint>  Points  = new List<MassPoint>();
        public List<SpringLink> Springs = new List<SpringLink>();

        [Header("Simulation")]
        public IntegrationType integration = IntegrationType.Verlet;
        public float TimeStep = 0.02f;

        [Header("External Forces")]
        public float  Gravity         = -9.81f;
        public bool   enableAirDrag   = false;
        public float  airDragFactor   = 0.1f;
        public bool   enableWind      = false;
        public Vector3 windForce      = Vector3.zero;
        public Vector3 externalForce  = Vector3.zero;

        // ────── Internal State ──────
        private List<Vector3> _prevPositions;
        private MeshFilter    _mf;
        private Mesh          _dynamicMesh;
        private Vector3[]     _baseVertices;

        private bool      _inertiaDirty = true;
        private Matrix4x4 _invIWorld;

        // ────── Properties for CollisionSolver ──────

        /// <summary>
        /// إجمالي الكتلة.
        /// </summary>
        public float TotalMass() => Points.Sum(p => p.Mass);

        /// <summary>
        /// معكوس الكتلة الكلية (لاحتساب الاندفاع بسرعة).
        /// </summary>
        public float InvMass => 1f / Mathf.Max(TotalMass(), 1e-5f);

        /// <summary>
        /// تقريب معكوس مصفوفة العطالة حول مركز الكتلة.
        /// </summary>
        public Matrix4x4 InvInertiaWorld(Vector3 _)
        {
            if (_inertiaDirty)
            {
                float radius = 0.1f;
                float I = (2f / 5f) * TotalMass() * radius * radius;
                _invIWorld    = Matrix4x4.Scale(new Vector3(1f / I, 1f / I, 1f / I));
                _inertiaDirty = false;
            }
            return _invIWorld;
        }

        /// <summary>
        /// مركز الكتلة بالإحداثيات العالمية.
        /// </summary>
        public Vector3 CenterOfMassWorld()
        {
            if (Points.Count == 0) return transform.position;
            float m = 0f;
            Vector3 sum = Vector3.zero;
            foreach (var p in Points)
            {
                sum += p.Position * p.Mass;
                m   += p.Mass;
            }
            return sum / m;
        }

        /// <summary>
        /// تقريب السرعة عند نقطة معينة بالعالم.
        /// </summary>
        public Vector3 VelocityAtPointWorld(Vector3 rLocal)
        {
            Vector3 worldP = CenterOfMassWorld() + rLocal;
            Vector3 v = Vector3.zero;
            float   w = 0f;
            foreach (var p in Points)
            {
                float d2 = (p.Position - worldP).sqrMagnitude + 1e-6f;
                float wi = 1f / d2;
                v += p.Velocity * wi;
                w += wi;
            }
            return v / w;
        }

        /// <summary>
        /// يوزع اندفاعًا موزعًا على أقرب النقاط بحسب الوزن العكسي للمسافة التربيعية.
        /// </summary>
        public void ApplyImpulseDistributed(Vector3 J, Vector3 contactPoint, int neighbours = 8)
        {
            if (J == Vector3.zero) return;

            var near = Points
                .Where(p => !p.IsFixed)
                .OrderBy(p => (p.Position - contactPoint).sqrMagnitude)
                .Take(neighbours)
                .ToList();
            if (near.Count == 0) return;

            float sumW = 0f;
            var   w    = new float[near.Count];
            for (int i = 0; i < near.Count; i++)
            {
                float d2   = (near[i].Position - contactPoint).sqrMagnitude + 1e-6f;
                w[i]       = 1f / d2;
                sumW      += w[i];
            }

            float fFactor = 1f / Time.fixedDeltaTime;
            for (int i = 0; i < near.Count; i++)
                near[i].ApplyForce(J * (w[i] / sumW) * fFactor);
        }

        /// <summary>
        /// النسخة القديمة لتطبيق الاندفاع على جميع النقاط بالتساوي.
        /// </summary>
        public void ApplyImpulse(Vector3 impulse, Vector3 contactPoint)
            => ApplyImpulseDistributed(impulse, contactPoint, Points.Count);

        // ────── MonoBehaviour Lifecycle ──────

        private void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            if (_mf.sharedMesh != null)
            {
                _dynamicMesh  = Instantiate(_mf.sharedMesh);
                _mf.mesh      = _dynamicMesh;
                _baseVertices = _dynamicMesh.vertices;
            }
        }

        private void Start()
        {
            // حفظ المواقع السابقة للـ Verlet
            _prevPositions = Points.Select(p => p.Position).ToList();
        }

        private void FixedUpdate()
        {
            // 1) إعادة تهيئة القوى الخارجية
            foreach (var p in Points)
            {
                p.ResetForce();
                if (p.IsFixed) continue;

                p.ApplyForce(Vector3.up * Gravity * p.Mass);
                if (enableAirDrag) p.ApplyForce(-airDragFactor * p.Velocity);
                if (enableWind)    p.ApplyForce(windForce);
                if (externalForce != Vector3.zero) p.ApplyForce(externalForce);
            }

            // 2) تطبيق قوى النوابض
            foreach (var s in Springs)
                s.ApplyForces();

            // 3) التكامل الفيزيائي
            switch (integration)
            {
                case IntegrationType.Verlet:
                    VerletStep();
                    break;
                case IntegrationType.Euler:
                    EulerStep();
                    break;
            }

            // 4) إزالة النوابض المنكوبة
            Springs.RemoveAll(s => s.IsBroken);

            // 5) تحديث الميش لإظهار تشوه الشبكة
            UpdateMeshVertices();

            // 6) إعادة بناء العطالة في التحديث القادم
            _inertiaDirty = true;
        }

        // ────── Integration Methods ──────

        private void VerletStep()
        {
            float dt2 = TimeStep * TimeStep;
            for (int i = 0; i < Points.Count; i++)
            {
                var p    = Points[i];
                if (p.IsFixed) continue;

                Vector3 acc  = p.Force / p.Mass;
                Vector3 curr = p.Position;
                Vector3 prev = _prevPositions[i];
                Vector3 next = 2f * curr - prev + acc * dt2;

                p.Velocity           = (next - prev) * (0.5f / TimeStep);
                _prevPositions[i]    = curr;
                p.Position           = next;
                p.ResetForce(); // ينظف القوة بعد التكامل
            }
        }

        private void EulerStep()
        {
            foreach (var p in Points)
            {
                if (p.IsFixed) continue;
                Vector3 acc = p.Force / p.Mass;
                p.Velocity += acc * TimeStep;
                p.Position += p.Velocity * TimeStep;
                p.ResetForce();
            }
        }

        // ────── Mesh Update ──────

        private void UpdateMeshVertices()
        {
            if (_dynamicMesh == null || _baseVertices == null) return;
            if (Points.Count != _baseVertices.Length) return;

            var verts = new Vector3[_baseVertices.Length];
            for (int i = 0; i < verts.Length; i++)
                verts[i] = transform.InverseTransformPoint(Points[i].Position);

            _dynamicMesh.vertices = verts;
            _dynamicMesh.RecalculateNormals();
            _dynamicMesh.RecalculateBounds();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Points == null || Springs == null) return;

            Gizmos.color = Color.green;
            foreach (var p in Points)
                Gizmos.DrawSphere(p.Position, 0.02f);

            Gizmos.color = Color.cyan;
            foreach (var s in Springs)
                Gizmos.DrawLine(s.PointA.Position, s.PointB.Position);
        }
#endif
    }
}
