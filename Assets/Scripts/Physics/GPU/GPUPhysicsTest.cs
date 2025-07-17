// using UnityEngine;
// using Physics.GPU;
// using Physics.Materials;
//
// namespace Physics.GPU
// {
//     /// <summary>
//     /// Test script for the GPU Mass-Spring system with interactive controls.
//     /// </summary>
//     public class GPUPhysicsTest : MonoBehaviour
//     {
//         [Header("Test Objects")]
//         public ComputeShader massSpringCompute;
//         public MaterialProfile testMaterial;
//         public GameObject[] testObjects;
//
//         [Header("Interactive Controls")]
//         public KeyCode createCubeKey = KeyCode.C;
//         public KeyCode createSphereKey = KeyCode.S;
//         public KeyCode applyImpulseKey = KeyCode.Space;
//         public KeyCode toggleGravityKey = KeyCode.G;
//         public KeyCode performanceTestKey = KeyCode.P;
//
//         [Header("Impulse Settings")]
//         public float impulseForce = 10f;
//         public float impulseRadius = 0.5f;
//
//         [Header("Performance Monitoring")]
//         public bool showPerformanceInfo = true;
//         public float performanceUpdateInterval = 1f;
//
//         private float _lastPerformanceUpdate;
//         private int _totalPoints;
//         private int _totalSprings;
//         private float _avgFrameTime;
//
//         private void Start()
//         {
//             // Create initial test objects if none exist
//             if (testObjects == null || testObjects.Length == 0)
//             {
//                 CreateTestCube();
//                 CreateTestSphere();
//             }
//
//             UpdatePerformanceStats();
//         }
//
//         private void Update()
//         {
//             HandleInput();
//
//             if (showPerformanceInfo && Time.time - _lastPerformanceUpdate > performanceUpdateInterval)
//             {
//                 UpdatePerformanceStats();
//                 _lastPerformanceUpdate = Time.time;
//             }
//         }
//
//         private void HandleInput()
//         {
//             // Create test objects
//             if (Input.GetKeyDown(createCubeKey))
//             {
//                 CreateTestCube();
//             }
//
//             if (Input.GetKeyDown(createSphereKey))
//             {
//                 CreateTestSphere();
//             }
//
//             // Apply impulse
//             if (Input.GetKeyDown(applyImpulseKey))
//             {
//                 ApplyRandomImpulse();
//             }
//
//             // Toggle gravity
//             if (Input.GetKeyDown(toggleGravityKey))
//             {
//                 ToggleGravity();
//             }
//
//             // Performance test
//             if (Input.GetKeyDown(performanceTestKey))
//             {
//                 RunPerformanceTest();
//             }
//         }
//
//         private void CreateTestCube()
//         {
//             GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
//             cube.name = $"GPU_Cube_{Time.frameCount}";
//             cube.transform.position = Random.insideUnitSphere * 3f + Vector3.up * 5f;
//
//             SetupGPUObject(cube);
//
//             Debug.Log($"Created GPU test cube: {cube.name}");
//         }
//
//         private void CreateTestSphere()
//         {
//             GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//             sphere.name = $"GPU_Sphere_{Time.frameCount}";
//             sphere.transform.position = Random.insideUnitSphere * 3f + Vector3.up * 5f;
//
//             SetupGPUObject(sphere);
//
//             Debug.Log($"Created GPU test sphere: {sphere.name}");
//         }
//
//         private void SetupGPUObject(GameObject obj)
//         {
//             // Remove any existing physics components
//             var existingBody = obj.GetComponent<MassSpringBody>();
//             if (existingBody != null) DestroyImmediate(existingBody);
//
//             var existingCollider = obj.GetComponent<SpringPhysicsCollider>();
//             if (existingCollider != null) DestroyImmediate(existingCollider);
//
//             // Add GPU components
//             var gpuBody = obj.AddComponent<MassSpringGPU>();
//             var gpuCollider = obj.AddComponent<GPUPhysicsCollider>();
//             var materialHolder = obj.AddComponent<MaterialHolder>();
//
//             // Configure
//             gpuBody.massSpringCompute = massSpringCompute;
//             materialHolder.Profile = testMaterial;
//
//             // Randomize physics properties for variety
//             gpuBody.timeStep = 0.02f;
//             gpuBody.gravity = -9.81f;
//             gpuBody.integration = Random.value > 0.5f ?
//                 MassSpringGPU.IntegrationType.Verlet :
//                 MassSpringGPU.IntegrationType.SemiImplicitEuler;
//             gpuBody.threadGroupSize = 64;
//
//             // Randomize material properties
//             if (testMaterial != null)
//             {
//                 var randomProfile = ScriptableObject.CreateInstance<MaterialProfile>();
//                 randomProfile.TotalMass = testMaterial.TotalMass * Random.Range(0.5f, 2f);
//                 randomProfile.Density = testMaterial.Density * Random.Range(0.8f, 1.2f);
//                 randomProfile.Stiffness = testMaterial.Stiffness * Random.Range(0.5f, 1.5f);
//                 randomProfile.Damping = testMaterial.Damping * Random.Range(0.8f, 1.2f);
//                 randomProfile.Elasticity = testMaterial.Elasticity;
//                 randomProfile.YieldThreshold = testMaterial.YieldThreshold;
//                 randomProfile.BreakThreshold = testMaterial.BreakThreshold;
//                 randomProfile.Plasticity = testMaterial.Plasticity;
//
//                 materialHolder.Profile = randomProfile;
//             }
//
//             gpuCollider.impactRadius = 0.0003f;
//         }
//
//         private void ApplyRandomImpulse()
//         {
//             var gpuBodies = FindObjectsOfType<MassSpringGPU>();
//             if (gpuBodies.Length == 0) return;
//
//             var randomBody = gpuBodies[Random.Range(0, gpuBodies.Length)];
//             Vector3 randomDirection = Random.onUnitSphere;
//             Vector3 impulse = randomDirection * impulseForce;
//             Vector3 contactPoint = randomBody.transform.position + Random.insideUnitSphere * 0.5f;
//
//             randomBody.ApplyImpulse(impulse, contactPoint, impulseRadius);
//
//             Debug.Log($"Applied impulse {impulse} to {randomBody.name}");
//         }
//
//         private void ToggleGravity()
//         {
//             var gpuBodies = FindObjectsOfType<MassSpringGPU>();
//             foreach (var body in gpuBodies)
//             {
//                 body.gravity = body.gravity == 0f ? -9.81f : 0f;
//             }
//
//             Debug.Log($"Toggled gravity: {gpuBodies[0].gravity}");
//         }
//
//         private void RunPerformanceTest()
//         {
//             var gpuBodies = FindObjectsOfType<MassSpringGPU>();
//             int totalPoints = 0;
//             int totalSprings = 0;
//
//             foreach (var body in gpuBodies)
//             {
//                 totalPoints += body._pointCount;
//                 totalSprings += body._springCount;
//             }
//
//             Debug.Log("=== GPU Physics Performance Test ===");
//             Debug.Log($"Objects: {gpuBodies.Length}");
//             Debug.Log($"Total Points: {totalPoints}");
//             Debug.Log($"Total Springs: {totalSprings}");
//             Debug.Log($"Thread Groups: {Mathf.CeilToInt((float)totalPoints / 64)}");
//             Debug.Log($"Estimated GPU Time: ~{totalPoints * 0.0001f:F3}ms");
//             Debug.Log("=====================================");
//         }
//
//         private void UpdatePerformanceStats()
//         {
//             var gpuBodies = FindObjectsOfType<MassSpringGPU>();
//             _totalPoints = 0;
//             _totalSprings = 0;
//
//             foreach (var body in gpuBodies)
//             {
//                 _totalPoints += body._pointCount;
//                 _totalSprings += body._springCount;
//             }
//
//             _avgFrameTime = Time.deltaTime * 1000f; // Convert to ms
//         }
//
//         private void OnGUI()
//         {
//             if (!showPerformanceInfo) return;
//
//             GUILayout.BeginArea(new Rect(10, 10, 300, 200));
//             GUILayout.BeginVertical("box");
//
//             GUILayout.Label("GPU Mass-Spring Test", GUI.skin.box);
//             GUILayout.Space(5);
//
//             GUILayout.Label($"Objects: {FindObjectsOfType<MassSpringGPU>().Length}");
//             GUILayout.Label($"Total Points: {_totalPoints}");
//             GUILayout.Label($"Total Springs: {_totalSprings}");
//             GUILayout.Label($"Frame Time: {_avgFrameTime:F2}ms");
//             GUILayout.Label($"FPS: {1f / Time.deltaTime:F1}");
//
//             GUILayout.Space(10);
//             GUILayout.Label("Controls:");
//             GUILayout.Label($"{createCubeKey} - Create Cube");
//             GUILayout.Label($"{createSphereKey} - Create Sphere");
//             GUILayout.Label($"{applyImpulseKey} - Apply Impulse");
//             GUILayout.Label($"{toggleGravityKey} - Toggle Gravity");
//             GUILayout.Label($"{performanceTestKey} - Performance Test");
//
//             GUILayout.EndVertical();
//             GUILayout.EndArea();
//         }
//
//         private void OnDestroy()
//         {
//             // Clean up any created material profiles
//             var materialHolders = FindObjectsOfType<MaterialHolder>();
//             foreach (var holder in materialHolders)
//             {
//                 if (holder.Profile != testMaterial && holder.Profile != null)
//                 {
//                     DestroyImmediate(holder.Profile);
//                 }
//             }
//         }
//     }
// }