// Assets/Scripts/Physics/MeshToMassSpring.cs
using System.Collections.Generic;
using UnityEngine;
using Physics.Materials;

namespace Physics
{
    [RequireComponent(typeof(MeshFilter), typeof(MassSpringBody))]
    public class MeshToMassSpring : MonoBehaviour
    {
        [Header("Default Particle & Spring Settings (if no MaterialHolder)")]
        public float defaultPointMass      = 1f;
        public float defaultSpringStiffness= 100f;
        public float defaultSpringDamping  = 5f;
        public float defaultYieldThreshold = 0.1f;
        public float defaultBreakThreshold = 0.3f;
        public float defaultPlasticity     = 0.05f;

        [Header("Generation")]
        public bool autoGenerateOnStart = true;

        private MeshFilter meshFilter;
        private MassSpringBody springBody;
        private MaterialHolder matHolder;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            springBody = GetComponent<MassSpringBody>();
            matHolder  = GetComponent<MaterialHolder>();

            if (autoGenerateOnStart)
                GenerateMassSpringFromMesh();
        }

        /// <summary>
        /// يبني MassPoints و SpringLinks من الميش الحالي
        /// ويملأ springBody.Points و springBody.Springs
        /// </summary>
        public void GenerateMassSpringFromMesh()
        {
            var mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                Debug.LogError("MeshToMassSpring: no mesh found!");
                return;
            }

            // تفريغ القوائم
            springBody.Points.Clear();
            springBody.Springs.Clear();

            // 1️⃣ MassPoints ← نستخدم كثافة المادة لحساب الكتلة، وإلا defaultPointMass
            float particleMass = defaultPointMass;
            if (matHolder != null && matHolder.Profile != null)
            {
                // نفترض حجم وحدة 1 for simplicity
                particleMass = matHolder.Profile.Density * 1f;
            }

            var vertices  = mesh.vertices;
            var triangles = mesh.triangles;
            int vCount    = vertices.Length;

            // نخزن الربط من إندكس الـMesh إلى MassPoint
            var indexToPoint = new Dictionary<int, MassPoint>(vCount);
            for (int i = 0; i < vCount; i++)
            {
                Vector3 worldPos = transform.TransformPoint(vertices[i]);
                var p = new MassPoint
                {
                    Position         = worldPos,
                    PreviousPosition = worldPos,
                    Velocity         = Vector3.zero,
                    Force            = Vector3.zero,
                    Mass             = particleMass,
                    IsFixed          = false
                };
                springBody.Points.Add(p);
                indexToPoint[i] = p;
            }

            // 2️⃣ SpringLinks ← من أضلاع المثلثات
            var added = new HashSet<(int, int)>();

            // اقرأ القيم الفيزيائية من ملف المادة أو استخدم الإفتراضية
            float k    = defaultSpringStiffness;
            float d    = defaultSpringDamping;
            float yTh  = defaultYieldThreshold;
            float fTh  = defaultBreakThreshold;
            float pl   = defaultPlasticity;

            if (matHolder != null && matHolder.Profile != null)
            {
                var prof = matHolder.Profile;
                k   = prof.Stiffness;
                d   = prof.Damping;
                yTh = prof.YieldThreshold;
                fTh = prof.BreakThreshold;
                pl  = prof.Plasticity;
            }

            void AddSpring(int a, int b)
            {
                int min = Mathf.Min(a, b), max = Mathf.Max(a, b);
                var key = (min, max);
                if (added.Contains(key)) return;
                added.Add(key);

                var pA = indexToPoint[min];
                var pB = indexToPoint[max];

                var spring = new SpringLink(
                    pA, pB,
                    k, d,
                    yTh, fTh, pl
                );
                springBody.Springs.Add(spring);
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                AddSpring(triangles[i],     triangles[i + 1]);
                AddSpring(triangles[i + 1], triangles[i + 2]);
                AddSpring(triangles[i + 2], triangles[i]);
            }

            Debug.Log($"MeshToMassSpring: Generated {springBody.Points.Count} points and {springBody.Springs.Count} springs");
        }
    }
}
