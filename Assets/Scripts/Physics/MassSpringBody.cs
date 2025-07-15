using System.Collections.Generic;
using UnityEngine;
using Physics.Materials;
namespace Physics
{
    [RequireComponent(typeof(MeshFilter))]
    public class MassSpringBody : MonoBehaviour
    {
        public enum IntegrationType { Verlet, Euler }

        [Header("Particles & Springs")]
        public List<MassPoint>   Points  = new List<MassPoint>();
        public List<SpringLink>  Springs = new List<SpringLink>();

        [Header("Simulation")]
        public IntegrationType integration = IntegrationType.Verlet;
        public float          TimeStep    = 0.02f;

        [Header("External Forces")]
        public float  Gravity        = -9.81f;
        public bool   enableAirDrag  = false;
        public float  airDragFactor  = 0.1f;
        public bool   enableWind     = false;
        public Vector3 windForce     = Vector3.zero;
        public Vector3 externalForce = Vector3.zero;

        private List<Vector3> _prevPositions;
        private MeshFilter    _mf;
        private Mesh          _dynamicMesh;
        private Vector3[]     _baseVertices;

        // rigid-body state
        private float   _inertia;
        private Vector3 _angularVelocity = Vector3.zero;

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
            _prevPositions = new List<Vector3>(Points.Count);
            foreach (var p in Points)
                _prevPositions.Add(p.Position);

            // حساب عزم القصور الذاتي بدايةً حول COM
            // توزيع الكتلة من البروفايل بالتساوي
            var holder = GetComponent<MaterialHolder>();
            if (holder != null && holder.Profile != null)
            {
                float totalMass = holder.Profile.TotalMass;
                float massPerPoint = (Points.Count > 0) ? totalMass / Points.Count : 1f;
                foreach (var p in Points)
                {
                    if (!p.IsFixed)
                        p.Mass = massPerPoint;
                }
            }

            _inertia = ComputeMomentOfInertia();
        }

        private void FixedUpdate()
        {
            var col = GetComponent<PhysicsCollider>();
            if (col != null && col.IsStatic)
                return;
            // 1) القوى الخارجية
            foreach (var p in Points)
            {
                p.ResetForce();
                if (p.IsFixed) continue;
                p.ApplyForce(Vector3.up * Gravity * p.Mass);
                if (enableAirDrag) p.ApplyForce(-airDragFactor * p.Velocity);
                if (enableWind)    p.ApplyForce(windForce);
                if (externalForce != Vector3.zero) p.ApplyForce(externalForce);
                // if (p.Velocity.y > 0f)
                    // Debug.Log($"[MassPoint] Point {p} is accelerating upward with Vy = {p.Velocity.y}");

            }

            // 2) قوى النوابض
            foreach (var s in Springs)
                s.ApplyForces();

            // 3) التكامل
            if (integration == IntegrationType.Verlet) VerletStep();
            else                                      EulerStep();

            // 4) إزالة النوابض المكسورة
            Springs.RemoveAll(s => s.IsBroken);

            // 5) تطبيق التدوير الصلب من العزم الزاوي
            ApplyRigidRotation();

            // 6) تحديث الميش
            UpdateMeshVertices();
            
        }

        private void VerletStep()
        {
            float dt2 = TimeStep * TimeStep;
            for (int i = 0; i < Points.Count; i++)
            {
                var p = Points[i];
                if (p.IsFixed) continue;
                Vector3 acc  = p.Force / p.Mass;
                Vector3 curr = p.Position;
                Vector3 prev = _prevPositions[i];
                Vector3 next = 2f * curr - prev + acc * dt2;
                p.Velocity      = (next - prev) * (0.5f / TimeStep);
                _prevPositions[i] = curr;
                p.Position      = next;
            }
        }

        private void EulerStep()
        {
            foreach (var p in Points)
            {
                if (p.IsFixed) continue;
                Vector3 acc = p.Force / p.Mass;
                p.Velocity    += acc * TimeStep;
                p.Position    += p.Velocity * TimeStep;
            }
        }

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

        public float TotalMass()
        {
            float m = 0f;
            foreach (var p in Points) m += p.Mass;
            return m;
        }
        public void ApplyLocalDamping(Vector3 center, float radius, float dampingFactor = 0.5f)
        {
            foreach (var p in Points)
            {
                if (p.IsFixed) continue;
                float dist = Vector3.Distance(p.Position, center);
                if (dist <= radius)
                    p.Velocity *= (1f - dampingFactor); // تخميد سريع للسرعة
            }
        }

        public void ApplyImpulse(Vector3 impulse, Vector3 contactPoint, float impactRadius = 2600f)
        {
            if (Points.Count == 0) return;

            Vector3 com = ComputeCenterOfMass();
            Vector3 totalTorque = Vector3.zero;

            for (int i = 0; i < Points.Count; i++)
            {
                var p = Points[i];
                if (p.IsFixed) continue;

                float dist = Vector3.Distance(p.Position, contactPoint);
                // Debug.Log($"dsit : {dist}");
                if (dist <= impactRadius)
                {
                    float factor = 1f - (dist / impactRadius);
                    Vector3 dv = (impulse * factor) / p.Mass;
                    p.Velocity += dv;
                    _prevPositions[i] = p.Position - p.Velocity * TimeStep;
                    Vector3 r = p.Position - com;
                    totalTorque += Vector3.Cross(r, dv * p.Mass);
                }
            }

            float inertia = ComputeInertia(com);
            _angularVelocity = (inertia > 1e-5f) ? totalTorque / inertia : Vector3.zero;
            _angularVelocity *= 1000f;
            Debug.Log($"angular : {_angularVelocity}   , interia    :  {inertia}    total tor : {totalTorque}");
        }

        private float ComputeInertia(Vector3 com)
        {
            float inertia = 0f;
            foreach (var p in Points)
            {
                if (p.IsFixed) continue;
                float r2 = (p.Position - com).sqrMagnitude;
                inertia += p.Mass * r2;
            }
            return inertia;
        }
        private Vector3 ComputeCenterOfMass()
        {
            Vector3 sum = Vector3.zero;
            float totalMass = 0f;
            foreach (var p in Points)
            {
                if (p.IsFixed) continue;
                sum += p.Position * p.Mass;
                totalMass += p.Mass;
            }
            return (totalMass > 0f) ? sum / totalMass : transform.position;
        }

        private void ApplyRigidRotation()
        {
            if (_angularVelocity.sqrMagnitude < 1e-6f) return;
            Vector3 com = ComputeCOMPosition();
            float angleRad = _angularVelocity.magnitude * TimeStep;
            float angleDeg = angleRad * Mathf.Rad2Deg;
            Vector3 axis = _angularVelocity.normalized;
            Quaternion dq = Quaternion.AngleAxis(angleDeg, axis);

            for (int i = 0; i < Points.Count; i++)
            {
                var p = Points[i];
                if (p.IsFixed) continue;
                Vector3 r     = p.Position - com;
                p.Position    = com + dq * r;
                Vector3 prevR = _prevPositions[i] - com;
                _prevPositions[i] = com + dq * prevR;
                p.Velocity    = dq * p.Velocity;
            }
        }

        private Vector3 ComputeCOMPosition()
        {
            float total = 0f;
            Vector3 sum = Vector3.zero;
            foreach (var p in Points)
            {
                sum += p.Position * p.Mass;
                total += p.Mass;
            }
            return total > 0f ? sum / total : Vector3.zero;
        }

        private float ComputeMomentOfInertia()
        {
            Vector3 com = ComputeCOMPosition();
            float I = 0f;
            foreach (var p in Points)
            {
                Vector3 r = p.Position - com;
                I += p.Mass * r.sqrMagnitude;
            }
            return I;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Points == null || Springs == null) return;
            Gizmos.color = Color.red;
            foreach (var p in Points)
                Gizmos.DrawSphere(p.Position, 0.02f);
            Gizmos.color = Color.cyan;
            foreach (var s in Springs)
                Gizmos.DrawLine(s.PointA.Position, s.PointB.Position);
        }
#endif
    }
}
