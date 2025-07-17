using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Physics.Materials;

namespace Physics.GPU
{
    [RequireComponent(typeof(MeshFilter), typeof(MassSpringGPU), typeof(MaterialHolder))]
    public class VoxelMassSpringGenerator : MonoBehaviour
    {
        [Header("Voxel Grid (Local)")]
        public Vector3Int dimensions = new Vector3Int(5, 5, 5);
        public float voxelSize = 0.1f;

        [Header("Springs Connectivity")]
        [Tooltip("Number of nearest neighbors per voxel")]
        public int connectionsPerVoxel = 6;

        [Header("Visualization")]
        public GameObject voxelPrefab;
        private List<GameObject> instances = new List<GameObject>();

        [Header("Generation")]
        public bool autoGenerateOnStart = true;

        private MeshFilter meshFilter;
        private MassSpringGPU gpuBody;
        private MaterialHolder matHolder;

        // Data storage for MassSpringGPU access
        private MassSpringGPU.MassPointGPU[] _generatedMassPoints;
        private MassSpringGPU.SpringGPU[] _generatedSprings;
        private Vector3Int _gridSize;
        private Dictionary<Vector3Int, int> _voxelToIndexMap;
        private List<Vector3Int> _voxelPositions;

        void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            gpuBody = GetComponent<MassSpringGPU>();
            matHolder = GetComponent<MaterialHolder>();

            if (autoGenerateOnStart)
                GenerateVoxelMassSpringNetwork();
        }

        [ContextMenu("Generate Voxel Network")]
        public void GenerateVoxelMassSpringNetwork()
        {
            var mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                Debug.LogError("VoxelMassSpringGenerator: no mesh found!");
                return;
            }

            // Clean up existing instances
            instances.ForEach(Destroy);
            instances.Clear();

            // Read material profile
            var prof = matHolder?.Profile;
            if (prof == null)
            {
                Debug.LogError("VoxelMassSpringGenerator: no material profile found!");
                return;
            }

            // Get physics properties from material profile
            float k = prof.Stiffness;
            float d = prof.Damping;
            float yTh = prof.YieldThreshold;
            float fTh = prof.BreakThreshold;
            float pl = prof.Plasticity;

            // Generate voxel positions
            var voxelPositions = new List<Vector3>();

            // 1) Generate surface voxels from mesh vertices
            foreach (var localV in mesh.vertices.Distinct())
            {
                voxelPositions.Add(localV);
            }

            // 2) Generate interior voxels
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
                        {
                            voxelPositions.Add(localP);
                        }
                    }

            // 3) Create GPU mass points
            var massPoints = new List<MassSpringGPU.MassPointGPU>();
            var worldPositions = new List<Vector3>();

            foreach (var localPos in voxelPositions)
            {
                Vector3 worldPos = transform.TransformPoint(localPos);
                worldPositions.Add(worldPos);

                var massPoint = new MassSpringGPU.MassPointGPU
                {
                    position = worldPos,
                    velocity = Vector3.zero,
                    force = Vector3.zero,
                    mass = prof.Density * Mathf.Pow(voxelSize, 3),
                    isFixed = 0
                };
                massPoints.Add(massPoint);

                // Create visual voxel if prefab assigned
                if (voxelPrefab)
                {
                    var go = Instantiate(voxelPrefab, worldPos, Quaternion.identity, transform);
                    go.transform.localScale = Vector3.one * voxelSize;
                    instances.Add(go);
                }
            }

            // 4) Build springs via mutual k-NN
            var springs = new List<MassSpringGPU.SpringGPU>();
            var neighbors = new List<List<int>>(massPoints.Count);

            for (int i = 0; i < massPoints.Count; i++)
            {
                var pi = massPoints[i];
                neighbors.Add(
                    massPoints
                        .Select((p, idx) => new { idx, d2 = (p.position - pi.position).sqrMagnitude })
                        .Where(x => x.idx != i)
                        .OrderBy(x => x.d2)
                        .Take(connectionsPerVoxel)
                        .Select(x => x.idx)
                        .ToList()
                );
            }

            // Create springs using material properties
            for (int i = 0; i < massPoints.Count; i++)
            {
                foreach (int j in neighbors[i])
                {
                    if (j > i && neighbors[j].Contains(i))
                    {
                        float restLength = Vector3.Distance(massPoints[i].position, massPoints[j].position);

                        var spring = new MassSpringGPU.SpringGPU
                        {
                            pointAIndex = i,
                            pointBIndex = j,
                            restLength = restLength,
                            stiffness = k,
                            damping = d,
                            yieldThreshold = yTh,
                            fractureThreshold = fTh,
                            plasticity = pl,
                            isBroken = 0
                        };
                        springs.Add(spring);
                    }
                }
            }

            // 5) Store the generated data for MassSpringGPU access
            _generatedMassPoints = massPoints.ToArray();
            _generatedSprings = springs.ToArray();
            _gridSize = dimensions;

            // Create voxel position mapping
            _voxelToIndexMap = new Dictionary<Vector3Int, int>();
            _voxelPositions = new List<Vector3Int>();

            // Map voxel grid positions to indices
            int voxelIndex = 0;
            for (int x = 0; x < dimensions.x; x++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int z = 0; z < dimensions.z; z++)
                    {
                        Vector3 localP = bounds.min + Vector3.Scale(step, new Vector3(x, y, z));
                        if (IsInsideWinding(localP, mesh))
                        {
                            _voxelToIndexMap[new Vector3Int(x, y, z)] = voxelIndex;
                            _voxelPositions.Add(new Vector3Int(x, y, z));
                            voxelIndex++;
                        }
                    }
                }
            }

            Debug.Log($"VoxelMassSpringGenerator: Generated {massPoints.Count} voxels and {springs.Count} springs");
            Debug.Log($"Voxel mapping created with {_voxelToIndexMap.Count} valid voxels");
        }



        void Update()
        {
            // Update voxel visualizations if they exist
            if (instances.Count > 0 && _generatedMassPoints != null)
            {
                int count = Mathf.Min(instances.Count, _generatedMassPoints.Length);
                for (int i = 0; i < count; i++)
                {
                    instances[i].transform.position = _generatedMassPoints[i].position;
                }
            }
        }

        // Winding Number calculation (inside/outside)
        bool IsInsideWinding(Vector3 localP, Mesh mesh)
        {
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            double total = 0.0;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]] - localP;
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

        // Public interface for MassSpringGPU
        public int PointCount => _generatedMassPoints?.Length ?? 0;
        public Vector3Int GridSize => _gridSize;

        public Vector3 GetPointPosition(int index)
        {
            if (_generatedMassPoints != null && index >= 0 && index < _generatedMassPoints.Length)
            {
                return _generatedMassPoints[index].position;
            }
            return Vector3.zero;
        }

        public float GetPointMass(int index)
        {
            if (_generatedMassPoints != null && index >= 0 && index < _generatedMassPoints.Length)
            {
                return _generatedMassPoints[index].mass;
            }
            return 1f;
        }

        public bool IsPointFixed(int index)
        {
            if (_generatedMassPoints != null && index >= 0 && index < _generatedMassPoints.Length)
            {
                return _generatedMassPoints[index].isFixed != 0;
            }
            return false;
        }

        public int GetVoxelIndex(int x, int y, int z)
        {
            if (_voxelToIndexMap != null)
            {
                var key = new Vector3Int(x, y, z);
                return _voxelToIndexMap.TryGetValue(key, out int index) ? index : -1;
            }
            return -1;
        }

        public MassSpringGPU.MassPointGPU[] GetMassPoints()
        {
            return _generatedMassPoints;
        }

        public MassSpringGPU.SpringGPU[] GetSprings()
        {
            return _generatedSprings;
        }

        private void OnDestroy()
        {
            // Clean up voxel instances
            instances.ForEach(Destroy);
            instances.Clear();
        }
    }
}