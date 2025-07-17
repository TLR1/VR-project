// Assets/Scripts/Physics/UnifiedMassSpringGenerator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Physics.Materials;

namespace Physics
{
    [RequireComponent(typeof(MeshFilter), typeof(MassSpringBody), typeof(MaterialHolder))]
    public class UnifiedMassSpringGenerator : MonoBehaviour
    {
        MeshFilter        mf;
        MassSpringBody    body;
        MaterialHolder    matHolder;

        [Header("Voxel Grid (Local)")]
        public Vector3Int dimensions        = new Vector3Int(5, 5, 5);
        public float      voxelSize          = 0.1f;

        [Header("Springs Connectivity")]
        [Tooltip("Number of nearest neighbors per voxel")]
        public int connectionsPerVoxel       = 6;

        [Header("Visualization")]
        public GameObject voxelPrefab;
        private List<GameObject> instances  = new List<GameObject>();

        void Awake()
        {
            mf        = GetComponent<MeshFilter>();
            body      = GetComponent<MassSpringBody>();
            matHolder = GetComponent<MaterialHolder>();

            // ===== تنطبق الإعدادات بناءً على المادة =====
            ApplyMaterialDrivenSettings(matHolder.Profile);

            GenerateMassSpringNetwork();
        }

        /// <summary>
        /// يضبط الأبعاد وحجم الفوكسل وعدد الاتصالات بناءً على خصائص المادة.
        /// </summary>
        private void ApplyMaterialDrivenSettings(MaterialProfile prof)
        {
            // مثال: اختيار دقة الشبكة حسب صلابة المادة
            if (prof.Stiffness >= 800f)
            {
                // مواد صلبة جدًا (فولاذ، حجر)
                dimensions          = new Vector3Int(4, 4, 4);
                voxelSize           = 0.25f;
                connectionsPerVoxel = 6;
            }
            else if (prof.Stiffness >= 300f)
            {
                // مواد متوسطة الصلابة (زجاج، خشب)
                dimensions          = new Vector3Int(6, 6, 6);
                voxelSize           = 0.15f;
                connectionsPerVoxel = 12;
            }
            else if (prof.Stiffness >= 100f)
            {
                // مواد لَدنة (طين، مطاط)
                dimensions          = new Vector3Int(8, 8, 8);
                voxelSize           = 0.1f;
                connectionsPerVoxel = 18;
            }
            else
            {
                // مواد ناعمة جدًا (رغوة)
                dimensions          = new Vector3Int(10, 10, 10);
                voxelSize           = 0.08f;
                connectionsPerVoxel = 26;
            }

            Debug.Log($"[UnifiedGenerator] Using material '{prof.name}': " +
                      $"Dimensions={dimensions}, VoxelSize={voxelSize:F2}, Conns={connectionsPerVoxel}");
        }

        void GenerateMassSpringNetwork()
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) return;

            // نظف القوائم الحالية
            body.Points.Clear();
            body.Springs.Clear();
            instances.ForEach(Destroy);
            instances.Clear();

            var prof = matHolder.Profile;
            float k   = prof.Stiffness;
            float d   = prof.Damping;
            float yTh = prof.YieldThreshold;
            float fTh = prof.BreakThreshold;
            float pl  = prof.Plasticity;

            // 1) نقاط السطح
            foreach (var localV in mesh.vertices.Distinct())
                AddPoint(localV, prof);

            // 2) نقاط الداخل ضمن الأبعاد المحددة
            var bounds = mesh.bounds;
            Vector3 step = new Vector3(
                bounds.size.x / (dimensions.x - 1),
                bounds.size.y / (dimensions.y - 1),
                bounds.size.z / (dimensions.z - 1)
            );
            for (int x = 0; x < dimensions.x; x++)
            for (int y = 0; y < dimensions.y; y++)
            for (int z = 0; z < dimensions.z; z++)
            {
                Vector3 localP = bounds.min + Vector3.Scale(new Vector3(x, y, z), step);
                if (IsInsideWinding(localP, mesh))
                    AddPoint(localP, prof);
            }

            // 3) بناء النوابض عبر أقرب k-NN
            int n = body.Points.Count;
            var neighbors = new List<List<int>>(n);
            for (int i = 0; i < n; i++)
            {
                var pi = body.Points[i];
                neighbors.Add(
                    body.Points
                        .Select((p, idx) => new { idx, d2 = (p.Position - pi.Position).sqrMagnitude })
                        .Where(x => x.idx != i)
                        .OrderBy(x => x.d2)
                        .Take(connectionsPerVoxel)
                        .Select(x => x.idx)
                        .ToList()
                );
            }

            // أنشئ SpringLink بكل قطعه
            for (int i = 0; i < n; i++)
            {
                foreach (int j in neighbors[i])
                {
                    if (j > i && neighbors[j].Contains(i))
                    {
                        var spring = new SpringLink(
                            body.Points[i],
                            body.Points[j],
                            k,    // stiffness
                            d,    // damping
                            yTh,  // yieldThreshold
                            fTh,  // fractureThreshold
                            pl    // plasticity
                        );
                        body.Springs.Add(spring);
                    }
                }
            }
        }

        private void AddPoint(Vector3 localP, MaterialProfile prof)
        {
            Vector3 worldP = transform.TransformPoint(localP);
            var mp = new MassPoint
            {
                Position         = worldP,
                PreviousPosition = worldP,
                Mass             = prof.Density * Mathf.Pow(voxelSize, 3),
                Velocity         = Vector3.zero,
                Force            = Vector3.zero
            };
            body.Points.Add(mp);

            if (voxelPrefab)
            {
                var go = Instantiate(voxelPrefab, worldP, Quaternion.identity, transform);
                go.transform.localScale = Vector3.one * voxelSize;
                instances.Add(go);
            }
        }

        void Update()
        {
            // حدّث مواقع Prefabs تبعًا لنقاط الجسم
            int count = Mathf.Min(instances.Count, body.Points.Count);
            for (int i = 0; i < count; i++)
                instances[i].transform.position = body.Points[i].Position;
        }

        // ————————————————————— طرق Winding Number ————————————————————— 

        bool IsInsideWinding(Vector3 localP, Mesh mesh)
        {
            var verts = mesh.vertices;
            var tris  = mesh.triangles;
            double total = 0.0;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t    ]] - localP;
                Vector3 b = verts[tris[t + 1]] - localP;
                Vector3 c = verts[tris[t + 2]] - localP;
                total += SolidAngle(a, b, c);
            }
            return Mathf.Abs((float)total) > Mathf.PI;
        }

        double SolidAngle(Vector3 a, Vector3 b, Vector3 c)
        {
            double la = a.magnitude, lb = b.magnitude, lc = c.magnitude;
            double num = Vector3.Dot(a, Vector3.Cross(b, c));
            double den = la * lb * lc
                       + Vector3.Dot(a, b) * lc
                       + Vector3.Dot(b, c) * la
                       + Vector3.Dot(c, a) * lb;
            return 2.0 * Math.Atan2(num, den);
        }
    }
}
