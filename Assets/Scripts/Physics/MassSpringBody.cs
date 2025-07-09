using System.Collections.Generic;
using UnityEngine;

namespace Physics
{
    [RequireComponent(typeof(MeshFilter))]
    public class MassSpringBody : MonoBehaviour
    {
        public enum IntegrationType { Verlet, Euler }

        [Header("Particles & Springs")]
        public List<MassPoint> Points = new List<MassPoint>();
        public List<SpringLink> Springs = new List<SpringLink>();

        [Header("Simulation")]
        public IntegrationType integration = IntegrationType.Verlet;
        public float TimeStep = 0.02f;

        [Header("External Forces")]
        public float Gravity = -9.81f;
        public bool enableAirDrag = false;
        public float airDragFactor = 0.1f;
        public bool enableWind = false;
        public Vector3 windForce = Vector3.zero;
        public Vector3 externalForce = Vector3.zero;

        private List<Vector3> _prevPositions;
        private MeshFilter _mf;
        private Mesh _dynamicMesh;
        private Vector3[] _baseVertices;

        private void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            if (_mf.sharedMesh != null)
            {
                _dynamicMesh = Instantiate(_mf.sharedMesh);
                _mf.mesh = _dynamicMesh;
                _baseVertices = _dynamicMesh.vertices;
            }
        }

        private void Start()
        {
            _prevPositions = new List<Vector3>(Points.Count);
            foreach (var p in Points)
                _prevPositions.Add(p.Position);
        }

        private void FixedUpdate()
        {
            // 1) تطبيق القوى الخارجية
            foreach (var p in Points)
            {
                p.ResetForce();
                if (p.IsFixed) continue;

                p.ApplyForce(Vector3.up * Gravity * p.Mass);
                if (enableAirDrag) p.ApplyForce(-airDragFactor * p.Velocity);
                if (enableWind) p.ApplyForce(windForce);
                if (externalForce != Vector3.zero) p.ApplyForce(externalForce);
            }

            // 2) تطبيق قوى النوابض
            foreach (var s in Springs)
                s.ApplyForces();

            // 3) التكامل
            switch (integration)
            {
                case IntegrationType.Verlet:
                    VerletStep();
                    break;
                case IntegrationType.Euler:
                    EulerStep();
                    break;
                default:
                    VerletStep();
                    break;
            }

            // 4) إزالة النوابض المنكوبة
            Springs.RemoveAll(s => s.IsBroken);

            // 5) تحديث الميش
            UpdateMeshVertices();
        }

        private void VerletStep()
        {
            float dt2 = TimeStep * TimeStep;
            for (int i = 0; i < Points.Count; i++)
            {
                var p = Points[i];
                if (p.IsFixed) continue;

                Vector3 acc = p.Force / p.Mass;
                Vector3 curr = p.Position;
                Vector3 prev = _prevPositions[i];
                Vector3 next = 2f * curr - prev + acc * dt2;

                p.Velocity = (next - prev) * (0.5f / TimeStep);
                _prevPositions[i] = curr;
                p.Position = next;
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
            }
        }

        private void UpdateMeshVertices()
        {
            if (_dynamicMesh == null || _baseVertices == null) return;
            if (Points.Count != _baseVertices.Length) return;

            Vector3[] verts = new Vector3[_baseVertices.Length];
            for (int i = 0; i < verts.Length; i++)
                verts[i] = transform.InverseTransformPoint(Points[i].Position);

            _dynamicMesh.vertices = verts;
            _dynamicMesh.RecalculateNormals();
            _dynamicMesh.RecalculateBounds();
        }

        public float TotalMass()
        {
            float m = 0f;
            foreach (var p in Points) m += p.Mass;
            return m;
        }

        public void ApplyImpulse(Vector3 impulse, Vector3 contactPoint)
        {
            int count = Points.Count;
            foreach (var p in Points)
                if (!p.IsFixed)
                    p.ApplyForce(impulse / count);
        }

#if UNITY_EDITOR
        /// <summary>
        /// رسم النقاط والنوابض في الـScene View لتشخيص التوزيع.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (Points == null || Springs == null) return;

            // نقاط (اختياري، يعتمد على الvoxelSize من المولد)
            Gizmos.color = Color.yellow;
            foreach (var p in Points)
                Gizmos.DrawSphere(p.Position, 0.02f);

            // خطوط النوابض
            Gizmos.color = Color.cyan;
            foreach (var s in Springs)
                Gizmos.DrawLine(s.PointA.Position, s.PointB.Position);
        }
#endif
    }
}
