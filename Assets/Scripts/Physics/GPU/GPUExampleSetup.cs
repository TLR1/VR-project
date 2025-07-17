using UnityEngine;
using Physics.Materials;

namespace Physics.GPU
{
    /// <summary>
    /// Example setup script showing how to use the GPU Mass-Spring system.
    /// This replaces the CPU-based MassSpringBody with the GPU version.
    /// </summary>
    public class GPUExampleSetup : MonoBehaviour
    {
        [Header("Setup")]
        public ComputeShader massSpringCompute;
        public MaterialProfile materialProfile;

        [Header("Object Settings")]
        public GameObject targetObject;
        public bool setupOnStart = true;

        private void Start()
        {
            if (setupOnStart)
            {
                SetupGPUObject();
            }
        }

        [ContextMenu("Setup GPU Object")]
        public void SetupGPUObject()
        {
            if (targetObject == null)
            {
                Debug.LogError("GPUExampleSetup: No target object assigned!");
                return;
            }

            // Remove CPU components if they exist
            var cpuBody = targetObject.GetComponent<MassSpringBody>();
            if (cpuBody != null)
            {
                DestroyImmediate(cpuBody);
            }

            var cpuCollider = targetObject.GetComponent<SpringPhysicsCollider>();
            if (cpuCollider != null)
            {
                DestroyImmediate(cpuCollider);
            }

            // Add GPU components
            var gpuBody = targetObject.GetComponent<MassSpringGPU>();
            if (gpuBody == null)
            {
                gpuBody = targetObject.AddComponent<MassSpringGPU>();
            }

            var gpuCollider = targetObject.GetComponent<GPUPhysicsCollider>();
            if (gpuCollider == null)
            {
                gpuCollider = targetObject.AddComponent<GPUPhysicsCollider>();
            }

            // Add material holder if needed
            var materialHolder = targetObject.GetComponent<MaterialHolder>();
            if (materialHolder == null)
            {
                materialHolder = targetObject.AddComponent<MaterialHolder>();
            }

            // Configure components
            gpuBody.massSpringCompute = massSpringCompute;
            materialHolder.Profile = materialProfile;

            // Configure simulation settings
            gpuBody.timeStep = 0.02f;
            gpuBody.gravity = -9.81f;
            gpuBody.integration = MassSpringGPU.IntegrationType.Verlet;
            gpuBody.threadGroupSize = 64;

            // Configure collision settings
            gpuCollider.impactRadius = 0.0003f;

            Debug.Log($"GPUExampleSetup: Successfully configured {targetObject.name} for GPU simulation");
        }

        [ContextMenu("Create Test Cube")]
        public void CreateTestCube()
        {
            // Create a simple cube for testing
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "GPU_TestCube";
            cube.transform.position = Vector3.up * 2f;

            // Assign as target and setup
            targetObject = cube;
            SetupGPUObject();
        }

        [ContextMenu("Create Test Sphere")]
        public void CreateTestSphere()
        {
            // Create a sphere for testing
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "GPU_TestSphere";
            sphere.transform.position = Vector3.up * 3f;

            // Assign as target and setup
            targetObject = sphere;
            SetupGPUObject();
        }

        [ContextMenu("Performance Test")]
        public void PerformanceTest()
        {
            if (targetObject == null) return;

            var gpuBody = targetObject.GetComponent<MassSpringGPU>();
            if (gpuBody == null) return;

            Debug.Log($"Performance Test Results:");
            Debug.Log($"- Points: {gpuBody._pointCount}");
            Debug.Log($"- Springs: {gpuBody._springCount}");
            Debug.Log($"- Thread Groups: {Mathf.CeilToInt((float)gpuBody._pointCount / gpuBody.threadGroupSize)}");
            Debug.Log($"- Integration: {gpuBody.integration}");
        }
    }
}