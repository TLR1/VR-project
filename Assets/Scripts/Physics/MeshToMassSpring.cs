using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Physics
{
    [RequireComponent(typeof(MeshFilter), typeof(MassSpringBody))]
    public class MeshToMassSpring : MonoBehaviour
    {
        [Header("Mass-Spring Settings")]
        public float pointMass = 1f;
        public float springStiffness = 100f;
        public float springDamping = 5f;
        public bool autoGenerateOnStart = true;

        private MeshFilter meshFilter;
        private MassSpringBody springBody;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            springBody = GetComponent<MassSpringBody>();

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

            // 1️⃣ MassPoints
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            int vCount = vertices.Length;

            var indexToPoint = new Dictionary<int, MassPoint>(vCount);
            for (int i = 0; i < vCount; i++)
            {
                // تحويل إلى إحداثيات عالمية
                Vector3 worldPos = transform.TransformPoint(vertices[i]);

                var point = new MassPoint
                {
                    Position = worldPos,
                    PreviousPosition = worldPos,
                    Mass = pointMass,
                    IsFixed = false
                };

                springBody.Points.Add(point);
                indexToPoint[i] = point;
            }

            // 2️⃣ SpringLinks من أضلاع المثلثات
            var added = new HashSet<(int, int)>();
            void AddSpring(int a, int b)
            {
                int min = Mathf.Min(a, b), max = Mathf.Max(a, b);
                var key = (min, max);
                if (added.Contains(key)) return;
                added.Add(key);

                var pA = indexToPoint[min];
                var pB = indexToPoint[max];

                var spring = new SpringLink(pA, pB, springStiffness, springDamping);
                springBody.Springs.Add(spring);
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                AddSpring(triangles[i], triangles[i + 1]);
                AddSpring(triangles[i + 1], triangles[i + 2]);
                AddSpring(triangles[i + 2], triangles[i]);
            }

            Debug.Log($"✔️ Generated {springBody.Points.Count} points and {springBody.Springs.Count} springs");
        }
    }
}
