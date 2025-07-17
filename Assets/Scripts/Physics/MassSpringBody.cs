using System.Collections.Generic;
using System.Linq;
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
        private bool _isSleeping = false;
        private float _sleepCooldown = 0f;
        private Queue<float> energyHistory = new Queue<float>();
        private Queue<float> recentEnergy = new Queue<float>();
        private const int EnergyWindow = 3; // عدد الإطارات للمراقبة
        private const float EnergyThreshold = 800f;

        private bool IsTrulyAtRest()
        {
            float kinetic = 0f;
            foreach (var p in Points)
            {
                if (p.IsFixed) continue;
                kinetic += 0.5f * p.Mass * p.Velocity.sqrMagnitude;
            }

            recentEnergy.Enqueue(kinetic);
            if (recentEnergy.Count > EnergyWindow)
                recentEnergy.Dequeue();

            // إذا كانت كل القراءات الأخيرة تقريبًا صغيرة جدًا، نوقف الحركة
            Debug.Log($" sum : {(recentEnergy.Sum() / recentEnergy.Count)}");

            return (recentEnergy.Sum() / recentEnergy.Count) < EnergyThreshold;
            // return recentEnergy.All(e => e < EnergyThreshold);
        }


        // rigid-body state
        private float   _inertia;
        private Vector3 _angularVelocity = Vector3.zero;
        public Material lineMaterial; // Assign an Unlit/Color material in Inspector

        private Physics.MassSpringBody body;
       private static Material CreateGLMaterial()
        {
            string shaderCode = @"
Shader ""Hidden/Internal-Colored""
{
    SubShader
    {
        Tags { ""RenderType""=""Opaque"" }
        Pass
        {
            ZWrite Off Cull Off Fog { Mode Off }
            Blend SrcAlpha OneMinusSrcAlpha

            BindChannels {
                Bind ""vertex"", vertex
                Bind ""color"", color
            }
        }
    }
}";

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                Debug.LogError("Shader 'Hidden/Internal-Colored' not found!");
                return null;
            }

            return new Material(shader);
        }

        private void Awake()
        {
            body = GetComponent<Physics.MassSpringBody>();

            _mf = GetComponent<MeshFilter>();
            if (_mf.sharedMesh != null)
            {
                _dynamicMesh  = Instantiate(_mf.sharedMesh);
                _mf.mesh      = _dynamicMesh;
                _baseVertices = _dynamicMesh.vertices;
            }
        }

        private void OnRenderObject()
        {
            if (lineMaterial == null || body == null || body.Springs == null || body.Points == null)
                return;

            lineMaterial.SetPass(0);
            GL.PushMatrix();

            // --- Draw Springs (Cyan Lines) ---
            GL.Begin(GL.LINES);
            GL.Color(Color.cyan);
            foreach (var s in body.Springs)
            {
                GL.Vertex(s.PointA.Position);
                GL.Vertex(s.PointB.Position);
            }
            GL.End();

            // --- Draw Points (Red Crosses) ---
            GL.Begin(GL.LINES);
            GL.Color(Color.red);
            float size = 0.02f;
            foreach (var p in body.Points)
            {
                Vector3 pos = p.Position;

                GL.Vertex(pos + Vector3.left * size);
                GL.Vertex(pos + Vector3.right * size);

                GL.Vertex(pos + Vector3.up * size);
                GL.Vertex(pos + Vector3.down * size);

                GL.Vertex(pos + Vector3.forward * size);
                GL.Vertex(pos + Vector3.back * size);
            }
            GL.End();

            GL.PopMatrix();
        }

        private void Start()
        {
            if (lineMaterial == null)
            {
                lineMaterial = CreateGLMaterial();
            }
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
        private float ComputeTotalEnergy()
        {
            float kinetic = 0f;
            foreach (var p in Points)
                if (!p.IsFixed)
                    kinetic += 0.5f * p.Mass * p.Velocity.sqrMagnitude;

            float springEnergy = 0f;
            foreach (var s in Springs)
            {
                if (s.IsBroken) continue;
                float stretch = (s.PointA.Position - s.PointB.Position).magnitude - s.RestLength;
                springEnergy += 0.5f * s.Stiffness * stretch * stretch;
            }

            return kinetic + springEnergy;
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
            // 7) منطق النوم
            if (IsTrulyAtRest())
            {
                foreach (var p in Points)
                    p.Velocity = Vector3.zero;
                _angularVelocity = Vector3.zero;
            }



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
        public bool IsAlmostAtRest(float velocityThreshold = 0.01f, float forceThreshold = 0.01f)
        {
            foreach (var p in Points)
            {
                if (!p.IsFixed)
                {
                    Debug.Log($"[tttttt] velo : {p.Velocity.magnitude}    ,  force : {p.Force.magnitude}");
                    if (p.Velocity.magnitude > velocityThreshold || p.Force.magnitude > forceThreshold)
                        return false;
                }
            }
            return true;
        }
        public void WakeUp()
        {
            if (!_isSleeping) return;

            foreach (var p in Points)
                p.IsFixed = false;

            _isSleeping = false;
            _sleepCooldown = 0f;
            Debug.Log($"[WakeUp] Body {name} was reactivated.");
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
