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
        MeshFilter mf;
        MassSpringBody body;
        MaterialHolder matHolder;
        MaterialProfile profile;

        [Header("Voxel Grid (Local)")]
        public Vector3Int dimensions = new Vector3Int(5, 5, 5);
        public float voxelSize = 0.1f;

        [Header("Springs (Mutual k-NN)")]
        [Tooltip("عدد الجيران الأقرب لكل فوكسل")]
        public int connectionsPerVoxel = 6;
        [Tooltip("المعامل الأساسي للصلابة قبل التعديل بالمادة")]
        public float baseStiffness = 1000f;
        [Tooltip("المعامل الأساسي للتخميد قبل التعديل بالمادة")]
        public float baseDamping = 50f;

        [Header("Visualization (optional)")]
        public GameObject voxelPrefab;
        List<GameObject> instances = new List<GameObject>();

        void Awake()
        {
            mf        = GetComponent<MeshFilter>();
            body      = GetComponent<MassSpringBody>();
            matHolder = GetComponent<MaterialHolder>();
            profile   = matHolder?.Profile;
            Generate();
        }

        void Generate()
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) return;

            // نظّف النقاط والنوابض
            body.Points.Clear();
            body.Springs.Clear();
            instances.ForEach(Destroy);
            instances.Clear();

            // 1) surface voxels بناءً على رؤوس الميش
            foreach (var localV in mesh.vertices.Distinct())
                AddPoint(localV);

            // 2) interior voxels بالتوزيع المنتظم داخل bounds المحلّي
            Bounds lb = mesh.bounds;
            Vector3 min = lb.min;
            Vector3 size = lb.size;
            Vector3 step = new Vector3(
                size.x / (dimensions.x - 1),
                size.y / (dimensions.y - 1),
                size.z / (dimensions.z - 1)
            );

            for (int x = 0; x < dimensions.x; x++)
            for (int y = 0; y < dimensions.y; y++)
            for (int z = 0; z < dimensions.z; z++)
            {
                Vector3 localP = min + Vector3.Scale(step, new Vector3(x, y, z));
                if (IsInsideWinding(localP, mesh))
                    AddPoint(localP);
            }

            // 3) ربط الفوكسل بنوابض Mutual k-NN
            int n = body.Points.Count;
            var nbrs = new List<List<int>>(n);
            for (int i = 0; i < n; i++)
            {
                var pi = body.Points[i];
                nbrs.Add(
                    body.Points
                        .Select((p, idx) => new { idx, d2 = (p.Position - pi.Position).sqrMagnitude })
                        .Where(x => x.idx != i)
                        .OrderBy(x => x.d2)
                        .Take(connectionsPerVoxel)
                        .Select(x => x.idx)
                        .ToList()
                );
            }

            for (int i = 0; i < n; i++)
            foreach (int j in nbrs[i])
                if (j > i && nbrs[j].Contains(i))
                {
                    // أنشئ النابض وادمج خصائص المادة
                    var mpA = body.Points[i];
                    var mpB = body.Points[j];
                    float k = baseStiffness;
                    float d = baseDamping;

                    // 1) عدل الصلابة والتخميد حسب المادة
                    if (profile != null)
                    {
                        k *= profile.Hardness;
                        // نجعل التخميد يزيد مع Brittleness
                        d *= 1f + profile.Brittleness * 5f;
                    }

                    var spring = new SpringLink(mpA, mpB, k, d);

                    // 2) عيّن حدود البلاستيكية والكسر من الطاقة
                    if (profile != null)
                    {
                        // ½·k·x² = E  ⇒  x = sqrt(2E/k)
                        spring.YieldThreshold = Mathf.Sqrt(2f * profile.YieldThreshold / spring.Stiffness);
                        spring.FractureThreshold = Mathf.Sqrt(2f * profile.BreakEnergyThreshold / spring.Stiffness);
                    }

                    body.Springs.Add(spring);
                }
        }

        void AddPoint(Vector3 localP)
        {
            Vector3 worldP = transform.TransformPoint(localP);
            var mp = new MassPoint
            {
                Position         = worldP,
                PreviousPosition = worldP
            };

            // 1) حساب كتلة النقطة بناءً على كثافة المادة وحجم الفوكسل
            float density = profile != null ? profile.Density : 1000f;
            float volume  = voxelSize * voxelSize * voxelSize;
            mp.Mass = density * volume;

            body.Points.Add(mp);

            // 2) إنشاء مجسم بصري إن وُجد prefab
            if (voxelPrefab != null)
            {
                var go = Instantiate(voxelPrefab, worldP, Quaternion.identity, transform);
                go.transform.localScale = Vector3.one * voxelSize;
                instances.Add(go);
            }
        }

        void Update()
        {
            // مزامنة مواقع الـ voxels البصرية مع النقاط المحسوبة
            int c = Mathf.Min(instances.Count, body.Points.Count);
            for (int i = 0; i < c; i++)
                instances[i].transform.position = body.Points[i].Position;
        }

        // -----------------------------
        // دوال Winding Number (كما كانت)
        // -----------------------------
        bool IsInsideWinding(Vector3 localP, Mesh mesh)
        {
            var verts = mesh.vertices;
            var tris  = mesh.triangles;
            double totalAngle = 0.0;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]]     - localP;
                Vector3 b = verts[tris[t + 1]] - localP;
                Vector3 c = verts[tris[t + 2]] - localP;
                totalAngle += SolidAngle(a, b, c);
            }
            return Math.Abs(totalAngle) > Math.PI;
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
