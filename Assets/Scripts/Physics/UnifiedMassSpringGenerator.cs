// Assets/Scripts/Physics/UnifiedMassSpringGenerator.cs
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

        [Header("Voxel Grid (Local)")]
        public Vector3Int dimensions   = new Vector3Int(5, 5, 5);
        public float voxelSize         = 0.1f;

        [Header("Springs Connectivity")]
        [Tooltip("Number of nearest neighbors per voxel")]
        public int connectionsPerVoxel = 6;

        [Header("Visualization")]
        public GameObject voxelPrefab;
        private List<GameObject> instances = new List<GameObject>();

        void Awake()
        {
            mf         = GetComponent<MeshFilter>();
            body       = GetComponent<MassSpringBody>();
            matHolder  = GetComponent<MaterialHolder>();

            GenerateMassSpringNetwork();
        }

        void GenerateMassSpringNetwork()
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) return;

            // نظف القوائم
            body.Points.Clear();
            body.Springs.Clear();
            instances.ForEach(Destroy);
            instances.Clear();

            // اقرأ ملف المادة
            var prof = matHolder.Profile;
            float k    = prof.Stiffness;
            float d    = prof.Damping;
            float yTh  = prof.YieldThreshold;
            float fTh  = prof.BreakThreshold;
            float pl   = prof.Plasticity;

            // 1) Generate surface points from mesh vertices
            foreach (var localV in mesh.vertices.Distinct())
                AddPoint(localV, prof, true);  // سطحية

            // 2) Generate interior voxels (non-surface)
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
                        Vector3 localP = bounds.min + Vector3.Scale(step, new Vector3(x, y, z));
                        if (IsInsideWinding(localP, mesh))
                            AddPoint(localP, prof, false); // نقاط داخلية فقط
                    }

            // 3) Build springs via mutual k-NN
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

            for (int i = 0; i < n; i++)
            {
                foreach (int j in neighbors[i])
                {
                    if (j > i && neighbors[j].Contains(i))
                    {
                        var spring = new SpringLink(
                            body.Points[i],
                            body.Points[j],
                            k, d, yTh, fTh, pl
                        );
                        body.Springs.Add(spring);
                    }
                }
            }
        }

        void AddPoint(Vector3 localP, MaterialProfile prof, bool isSurface = true)
        {
            Vector3 worldP = transform.TransformPoint(localP);
            var mp = new MassPoint
            {
                Position         = worldP,
                PreviousPosition = worldP,
                Mass             = prof.Density * Mathf.Pow(voxelSize, 3),
                Velocity         = Vector3.zero,
                Force            = Vector3.zero,
                IsSurface        = isSurface
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
            int c = Mathf.Min(instances.Count, body.Points.Count);
            for (int i = 0; i < c; i++)
                instances[i].transform.position = body.Points[i].Position;
        }

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
            return 2.0 * System.Math.Atan2(num, den);
        }
    }
}
