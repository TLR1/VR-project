using UnityEngine;
using System.Collections.Generic;
using Physics.Materials;

namespace Physics.GPU
{
    public enum IntegrationType { Verlet, SemiImplicitEuler }

    public class MassSpringGPU : MonoBehaviour
    {
        [Header("Compute Shader")]
        public ComputeShader massSpringCompute;

        [Header("Data Source")]
        [Tooltip("Use VoxelMassSpringGenerator instead of mesh vertices")]
        public bool useVoxelGenerator = false;

        [Header("Simulation Settings")]
        public float timeStep = 0.02f;
        public float gravity = -9.81f;
        public Vector3 externalForce = Vector3.zero;
        public bool enableAirDrag = false;
        public float airDragFactor = 0.1f;
        public bool enableWind = false;
        public Vector3 windForce = Vector3.zero;

        [Header("Integration")]
        public IntegrationType integration = IntegrationType.Verlet;

        [Header("Performance")]
        [Range(1, 64)]
        public int threadGroupSize = 64;
        public bool useFixedTimestep = true;
        [Tooltip("How often to check for and remove broken springs (frames)")]
        public int springCleanupInterval = 10;

        // Compute Buffers
        private ComputeBuffer _springsBuffer;
        private ComputeBuffer _prevPositionsBuffer;

        // Data structures
        public MassPointGPU[] _massPoints;
        public SpringGPU[] _springs;
        public Vector3[] _prevPositions;

        // Compute Shader kernel indices
        private int _updateForcesKernel;
        private int _updatePositionsKernel;

        // Material properties
        private MaterialHolder _materialHolder;
        private MaterialProfile _materialProfile;

        // Mesh components (only used if not using voxel generator)
        private MeshFilter _meshFilter;
        private Mesh _dynamicMesh;
        private Vector3[] _baseVertices;

        // Voxel generator reference
        private VoxelMassSpringGenerator _voxelGenerator;

        // Simulation state
        private bool _isInitialized = false;
        public int _pointCount { get; private set; }
        public int _springCount { get; private set; }
        private int _frameCount = 0;

        // Public access to buffers for collision system
        public ComputeBuffer _massPointsBuffer { get; private set; }

        // Structs for GPU
        [System.Serializable]
        public struct MassPointGPU
        {
            public Vector3 position;
            public Vector3 velocity;
            public Vector3 force;
            public float mass;
            public int isFixed;

            public static int Size => sizeof(float) * 10 + sizeof(int);
        }

        [System.Serializable]
        public struct SpringGPU
        {
            public int pointAIndex;
            public int pointBIndex;
            public float restLength;
            public float stiffness;
            public float damping;
            public float yieldThreshold;
            public float fractureThreshold;
            public float plasticity;
            public int isBroken;

            public static int Size => sizeof(int) * 3 + sizeof(float) * 6;
        }

        private void Awake()
        {
            _materialHolder = GetComponent<MaterialHolder>();

            if (!useVoxelGenerator)
            {
                _meshFilter = GetComponent<MeshFilter>();
                if (_meshFilter == null)
                {
                    Debug.LogError("MassSpringGPU: MeshFilter component required when not using voxel generator!");
                    return;
                }

                if (_meshFilter.sharedMesh != null)
                {
                    _dynamicMesh = Instantiate(_meshFilter.sharedMesh);
                    _meshFilter.mesh = _dynamicMesh;
                    _baseVertices = _dynamicMesh.vertices;
                }
            }
            else
            {
                _voxelGenerator = GetComponent<VoxelMassSpringGenerator>();
                if (_voxelGenerator == null)
                {
                    Debug.LogError("MassSpringGPU: VoxelMassSpringGenerator component required when using voxel generator!");
                    return;
                }
            }

            if (_materialHolder != null)
            {
                _materialProfile = _materialHolder.Profile;
            }
        }

        private void Start()
        {
            InitializeGPU();
        }

        private void InitializeGPU()
        {
            if (massSpringCompute == null)
            {
                Debug.LogError("MassSpringGPU: No compute shader assigned!");
                return;
            }

            // Get kernel indices
            _updateForcesKernel = massSpringCompute.FindKernel("UpdateForces");
            _updatePositionsKernel = massSpringCompute.FindKernel("UpdatePositions");

            // Generate mass-spring data from mesh
            GenerateMassSpringData();

            // Create compute buffers
            CreateComputeBuffers();

            // Set compute shader parameters
            SetComputeShaderParameters();

            _isInitialized = true;
            Debug.Log($"MassSpringGPU: Initialized with {_pointCount} points and {_springCount} springs");
        }

        private void GenerateMassSpringData()
        {
            if (useVoxelGenerator && _voxelGenerator == null)
            {
                _voxelGenerator = GetComponent<VoxelMassSpringGenerator>();
                if (_voxelGenerator == null)
                {
                    Debug.LogError("MassSpringGPU: VoxelMassSpringGenerator not found on this GameObject.");
                    return;
                }
            }

            if (!useVoxelGenerator)
            {
                var mesh = _meshFilter.sharedMesh;
                if (mesh == null) return;

                var vertices = mesh.vertices;
                var triangles = mesh.triangles;

                _pointCount = vertices.Length;
                _massPoints = new MassPointGPU[_pointCount];
                _prevPositions = new Vector3[_pointCount];

                // Calculate mass per point
                float particleMass = 1f;
                if (_materialProfile != null)
                {
                    particleMass = _materialProfile.Density * 1f; // Assuming unit volume
                }

                // Initialize mass points
                for (int i = 0; i < _pointCount; i++)
                {
                    Vector3 worldPos = transform.TransformPoint(vertices[i]);
                    _massPoints[i] = new MassPointGPU
                    {
                        position = worldPos,
                        velocity = Vector3.zero,
                        force = Vector3.zero,
                        mass = particleMass,
                        isFixed = 0
                    };
                    _prevPositions[i] = worldPos;
                }

                // Generate springs from mesh edges
                var springSet = new HashSet<(int, int)>();
                var springList = new List<SpringGPU>();

                float stiffness = _materialProfile?.Stiffness ?? 1000f;
                float damping = _materialProfile?.Damping ?? 50f;
                float yieldThreshold = _materialProfile?.YieldThreshold ?? 0.1f;
                float fractureThreshold = _materialProfile?.BreakThreshold ?? 0.3f;
                float plasticity = _materialProfile?.Plasticity ?? 0.05f;

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    AddSpring(triangles[i], triangles[i + 1], springSet, springList, stiffness, damping, yieldThreshold, fractureThreshold, plasticity);
                    AddSpring(triangles[i + 1], triangles[i + 2], springSet, springList, stiffness, damping, yieldThreshold, fractureThreshold, plasticity);
                    AddSpring(triangles[i + 2], triangles[i], springSet, springList, stiffness, damping, yieldThreshold, fractureThreshold, plasticity);
                }

                _springs = springList.ToArray();
                _springCount = _springs.Length;
            }
            else
            {
                _pointCount = _voxelGenerator.PointCount;
                _massPoints = new MassPointGPU[_pointCount];
                _prevPositions = new Vector3[_pointCount];

                // Initialize mass points from voxel generator
                for (int i = 0; i < _pointCount; i++)
                {
                    _massPoints[i] = new MassPointGPU
                    {
                        position = _voxelGenerator.GetPointPosition(i),
                        velocity = Vector3.zero,
                        force = Vector3.zero,
                        mass = _voxelGenerator.GetPointMass(i),
                        isFixed = _voxelGenerator.IsPointFixed(i) ? 1 : 0
                    };
                    _prevPositions[i] = _massPoints[i].position;
                }

                // Generate springs from voxel grid
                var springSet = new HashSet<(int, int)>();
                var springList = new List<SpringGPU>();

                float stiffness = _materialProfile?.Stiffness ?? 1000f;
                float damping = _materialProfile?.Damping ?? 50f;
                float yieldThreshold = _materialProfile?.YieldThreshold ?? 0.1f;
                float fractureThreshold = _materialProfile?.BreakThreshold ?? 0.3f;
                float plasticity = _materialProfile?.Plasticity ?? 0.05f;

                for (int x = 0; x < _voxelGenerator.GridSize.x; x++)
                {
                    for (int y = 0; y < _voxelGenerator.GridSize.y; y++)
                    {
                        for (int z = 0; z < _voxelGenerator.GridSize.z; z++)
                        {
                            int currentIndex = _voxelGenerator.GetVoxelIndex(x, y, z);
                            if (currentIndex == -1) continue; // Skip empty voxels

                            // Check neighbors in the 3x3x3 grid
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    for (int dz = -1; dz <= 1; dz++)
                                    {
                                        int neighborIndex = _voxelGenerator.GetVoxelIndex(x + dx, y + dy, z + dz);
                                        if (neighborIndex != -1 && neighborIndex != currentIndex)
                                        {
                                            AddSpring(currentIndex, neighborIndex, springSet, springList, stiffness, damping, yieldThreshold, fractureThreshold, plasticity);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                _springs = springList.ToArray();
                _springCount = _springs.Length;
            }
        }

        private void AddSpring(int a, int b, HashSet<(int, int)> springSet, List<SpringGPU> springList,
            float stiffness, float damping, float yieldThreshold, float fractureThreshold, float plasticity)
        {
            int min = Mathf.Min(a, b), max = Mathf.Max(a, b);
            var key = (min, max);
            if (springSet.Contains(key)) return;
            springSet.Add(key);

            float restLength = Vector3.Distance(_massPoints[min].position, _massPoints[max].position);

            springList.Add(new SpringGPU
            {
                pointAIndex = min,
                pointBIndex = max,
                restLength = restLength,
                stiffness = stiffness,
                damping = damping,
                yieldThreshold = yieldThreshold,
                fractureThreshold = fractureThreshold,
                plasticity = plasticity,
                isBroken = 0
            });
        }

        private void CreateComputeBuffers()
        {
            // Create buffers
            _massPointsBuffer = new ComputeBuffer(_pointCount, MassPointGPU.Size);
            _springsBuffer = new ComputeBuffer(_springCount, SpringGPU.Size);
            _prevPositionsBuffer = new ComputeBuffer(_pointCount, sizeof(float) * 3);

            // Set buffer data
            _massPointsBuffer.SetData(_massPoints);
            _springsBuffer.SetData(_springs);
            _prevPositionsBuffer.SetData(_prevPositions);
        }

        private void SetComputeShaderParameters()
        {
            // Set buffers
            massSpringCompute.SetBuffer(_updateForcesKernel, "_MassPoints", _massPointsBuffer);
            massSpringCompute.SetBuffer(_updateForcesKernel, "_Springs", _springsBuffer);
            massSpringCompute.SetBuffer(_updatePositionsKernel, "_MassPoints", _massPointsBuffer);
            massSpringCompute.SetBuffer(_updatePositionsKernel, "_PrevPositions", _prevPositionsBuffer);

            // Set constants
            massSpringCompute.SetFloat("_TimeStep", timeStep);
            massSpringCompute.SetFloat("_Gravity", gravity);
            massSpringCompute.SetVector("_ExternalForce", externalForce);
            massSpringCompute.SetFloat("_AirDragFactor", airDragFactor);
            massSpringCompute.SetVector("_WindForce", windForce);
            massSpringCompute.SetInt("_EnableAirDrag", enableAirDrag ? 1 : 0);
            massSpringCompute.SetInt("_EnableWind", enableWind ? 1 : 0);
            massSpringCompute.SetInt("_IntegrationType", (int)integration);
            massSpringCompute.SetInt("_PointCount", _pointCount);
            massSpringCompute.SetInt("_SpringCount", _springCount);
        }

        private void FixedUpdate()
        {
            if (!_isInitialized) return;

            _frameCount++;

            // Update compute shader parameters
            UpdateComputeShaderParameters();

            // Dispatch compute shaders
            DispatchComputeShaders();

            // Remove broken springs (less frequently for performance)
            if (_frameCount % springCleanupInterval == 0)
            {
                RemoveBrokenSprings();
            }

            // Update mesh vertices
            UpdateMeshVertices();
        }

        private void UpdateComputeShaderParameters()
        {
            massSpringCompute.SetFloat("_TimeStep", timeStep);
            massSpringCompute.SetFloat("_Gravity", gravity);
            massSpringCompute.SetVector("_ExternalForce", externalForce);
            massSpringCompute.SetFloat("_AirDragFactor", airDragFactor);
            massSpringCompute.SetVector("_WindForce", windForce);
            massSpringCompute.SetInt("_EnableAirDrag", enableAirDrag ? 1 : 0);
            massSpringCompute.SetInt("_EnableWind", enableWind ? 1 : 0);
        }

        private void DispatchComputeShaders()
        {
            // Calculate thread groups
            int pointGroups = Mathf.CeilToInt((float)_pointCount / threadGroupSize);
            int springGroups = Mathf.CeilToInt((float)_springCount / threadGroupSize);

            // Update forces (springs)
            massSpringCompute.Dispatch(_updateForcesKernel, springGroups, 1, 1);

            // Update positions (mass points)
            massSpringCompute.Dispatch(_updatePositionsKernel, pointGroups, 1, 1);
        }

        private void RemoveBrokenSprings()
        {
            // Read back springs data to check for broken ones
            _springsBuffer.GetData(_springs);

            // Count broken springs
            int brokenCount = 0;
            for (int i = 0; i < _springCount; i++)
            {
                if (_springs[i].isBroken != 0)
                {
                    brokenCount++;
                }
            }

            // If there are broken springs, rebuild the buffer
            if (brokenCount > 0)
            {
                var validSprings = new List<SpringGPU>();
                for (int i = 0; i < _springCount; i++)
                {
                    if (_springs[i].isBroken == 0)
                    {
                        validSprings.Add(_springs[i]);
                    }
                }

                _springs = validSprings.ToArray();
                _springCount = _springs.Length;

                // Recreate the buffer with new size
                if (_springsBuffer != null)
                {
                    _springsBuffer.Release();
                }
                _springsBuffer = new ComputeBuffer(_springCount, SpringGPU.Size);
                _springsBuffer.SetData(_springs);

                // Update compute shader buffer reference
                massSpringCompute.SetBuffer(_updateForcesKernel, "_Springs", _springsBuffer);
                massSpringCompute.SetInt("_SpringCount", _springCount);

                Debug.Log($"Removed {brokenCount} broken springs. Remaining: {_springCount}");
            }
        }

        private void UpdateMeshVertices()
        {
            if (!useVoxelGenerator)
            {
                if (_dynamicMesh == null || _baseVertices == null) return;

                // Read back mass points data
                _massPointsBuffer.GetData(_massPoints);

                // Update mesh vertices
                var vertices = new Vector3[_baseVertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = transform.InverseTransformPoint(_massPoints[i].position);
                }

                _dynamicMesh.vertices = vertices;
                _dynamicMesh.RecalculateNormals();
                _dynamicMesh.RecalculateBounds();
            }
            // When using voxel generator, mesh vertices are not updated
            // The voxel visualization is handled by VoxelMassSpringGenerator
        }

        public void ApplyImpulse(Vector3 impulse, Vector3 contactPoint, float impactRadius = 0.3f)
        {
            if (!_isInitialized) return;

            // Read current data
            _massPointsBuffer.GetData(_massPoints);

            // Apply impulse to nearby points
            for (int i = 0; i < _pointCount; i++)
            {
                float dist = Vector3.Distance(_massPoints[i].position, contactPoint);
                if (dist <= impactRadius)
                {
                    float factor = 1f - (dist / impactRadius);
                    Vector3 dv = (impulse * factor) / _massPoints[i].mass;
                    _massPoints[i].velocity += dv;
                }
            }

            // Write back data
            _massPointsBuffer.SetData(_massPoints);
        }

        public void ApplyLocalDamping(Vector3 center, float radius, float dampingFactor = 0.5f)
        {
            if (!_isInitialized) return;

            // Read current data
            _massPointsBuffer.GetData(_massPoints);

            // Apply damping to nearby points
            for (int i = 0; i < _pointCount; i++)
            {
                float dist = Vector3.Distance(_massPoints[i].position, center);
                if (dist <= radius)
                {
                    _massPoints[i].velocity *= (1f - dampingFactor);
                }
            }

            // Write back data
            _massPointsBuffer.SetData(_massPoints);
        }

        private void OnDestroy()
        {
            // Clean up compute buffers
            if (_massPointsBuffer != null)
            {
                _massPointsBuffer.Release();
                _massPointsBuffer = null;
            }

            if (_springsBuffer != null)
            {
                _springsBuffer.Release();
                _springsBuffer = null;
            }

            if (_prevPositionsBuffer != null)
            {
                _prevPositionsBuffer.Release();
                _prevPositionsBuffer = null;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_isInitialized || _massPoints == null) return;
            
            Gizmos.color = Color.red;
            for (int i = 0; i < _pointCount; i++)
            {
                Gizmos.DrawSphere(_massPoints[i].position, 0.02f);
            }
            
            Gizmos.color = Color.cyan;
            for (int i = 0; i < _springCount; i++)
            {
                if (_springs[i].isBroken == 0)
                {
                    Vector3 posA = _massPoints[_springs[i].pointAIndex].position;
                    Vector3 posB = _massPoints[_springs[i].pointBIndex].position;
                    Gizmos.DrawLine(posA, posB);
                }
            }
        }
#endif
    }
}