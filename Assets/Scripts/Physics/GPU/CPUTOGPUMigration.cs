using UnityEngine;
using Physics.Materials;

namespace Physics.GPU
{
    /// <summary>
    /// Helper script to migrate from CPU to GPU Mass-Spring system.
    /// </summary>
    public class CPUTOGPUMigration : MonoBehaviour
    {
        [Header("Migration Settings")]
        public ComputeShader massSpringCompute;
        public bool migrateOnStart = false;
        public bool backupOriginalComponents = true;

        [Header("Target Objects")]
        public GameObject[] objectsToMigrate;

        [Header("Migration Results")]
        [SerializeField] private int migratedObjects = 0;
        [SerializeField] private int failedMigrations = 0;

        private void Start()
        {
            if (migrateOnStart)
            {
                MigrateAllObjects();
            }
        }

        [ContextMenu("Migrate All Objects")]
        public void MigrateAllObjects()
        {
            if (massSpringCompute == null)
            {
                Debug.LogError("CPUTOGPUMigration: No compute shader assigned!");
                return;
            }

            migratedObjects = 0;
            failedMigrations = 0;

            // Migrate specified objects
            if (objectsToMigrate != null)
            {
                foreach (var obj in objectsToMigrate)
                {
                    if (obj != null)
                    {
                        MigrateObject(obj);
                    }
                }
            }

            // Also find and migrate any objects with MassSpringBody in the scene
            var cpuBodies = FindObjectsOfType<MassSpringBody>();
            foreach (var cpuBody in cpuBodies)
            {
                if (!System.Array.Exists(objectsToMigrate, obj => obj == cpuBody.gameObject))
                {
                    MigrateObject(cpuBody.gameObject);
                }
            }

            Debug.Log($"Migration complete: {migratedObjects} successful, {failedMigrations} failed");
        }

        [ContextMenu("Migrate Selected Object")]
        public void MigrateSelectedObject()
        {
            if (massSpringCompute == null)
            {
                Debug.LogError("CPUTOGPUMigration: No compute shader assigned!");
                return;
            }

            var selected = UnityEditor.Selection.activeGameObject;
            if (selected != null)
            {
                MigrateObject(selected);
            }
            else
            {
                Debug.LogWarning("CPUTOGPUMigration: No object selected!");
            }
        }

        public void MigrateObject(GameObject obj)
        {
            if (obj == null) return;

            try
            {
                var cpuBody = obj.GetComponent<MassSpringBody>();
                if (cpuBody == null)
                {
                    Debug.LogWarning($"CPUTOGPUMigration: {obj.name} has no MassSpringBody component");
                    return;
                }

                // Backup original components if requested
                if (backupOriginalComponents)
                {
                    BackupComponents(obj);
                }

                // Store original settings
                var originalTimeStep = cpuBody.TimeStep;
                var originalGravity = cpuBody.Gravity;
                var originalExternalForce = cpuBody.externalForce;
                var originalEnableAirDrag = cpuBody.enableAirDrag;
                var originalAirDragFactor = cpuBody.airDragFactor;
                var originalEnableWind = cpuBody.enableWind;
                var originalWindForce = cpuBody.windForce;
                var originalIntegration = cpuBody.integration;

                // Get material profile
                var materialHolder = obj.GetComponent<MaterialHolder>();
                var materialProfile = materialHolder?.Profile;

                // Remove CPU components
                var cpuCollider = obj.GetComponent<SpringPhysicsCollider>();
                if (cpuCollider != null)
                {
                    DestroyImmediate(cpuCollider);
                }

                DestroyImmediate(cpuBody);

                // Add GPU components
                var gpuBody = obj.AddComponent<MassSpringGPU>();
                var gpuCollider = obj.AddComponent<GPUPhysicsCollider>();

                // Ensure material holder exists
                if (materialHolder == null)
                {
                    materialHolder = obj.AddComponent<MaterialHolder>();
                }

                // Configure GPU components
                gpuBody.massSpringCompute = massSpringCompute;
                materialHolder.Profile = materialProfile;

                // Transfer settings
                gpuBody.timeStep = originalTimeStep;
                gpuBody.gravity = originalGravity;
                gpuBody.externalForce = originalExternalForce;
                gpuBody.enableAirDrag = originalEnableAirDrag;
                gpuBody.airDragFactor = originalAirDragFactor;
                gpuBody.enableWind = originalEnableWind;
                gpuBody.windForce = originalWindForce;
                gpuBody.integration = originalIntegration == MassSpringBody.IntegrationType.Verlet ?
    IntegrationType.Verlet :
    IntegrationType.SemiImplicitEuler;
                gpuBody.threadGroupSize = 64;

                // Configure collision
                gpuCollider.impactRadius = 0.0003f;

                migratedObjects++;
                Debug.Log($"Successfully migrated {obj.name} to GPU");
            }
            catch (System.Exception e)
            {
                failedMigrations++;
                Debug.LogError($"Failed to migrate {obj.name}: {e.Message}");
            }
        }

        private void BackupComponents(GameObject obj)
        {
            // Create a backup object
            var backup = new GameObject($"{obj.name}_CPU_Backup");
            backup.transform.SetParent(obj.transform);
            backup.transform.localPosition = Vector3.zero;
            backup.transform.localRotation = Quaternion.identity;
            backup.transform.localScale = Vector3.one;

            // Copy components
            var cpuBody = obj.GetComponent<MassSpringBody>();
            if (cpuBody != null)
            {
                var backupBody = backup.AddComponent<MassSpringBody>();
                // Note: We can't easily copy the lists, but we can store the settings
                backupBody.TimeStep = cpuBody.TimeStep;
                backupBody.Gravity = cpuBody.Gravity;
                backupBody.externalForce = cpuBody.externalForce;
                backupBody.enableAirDrag = cpuBody.enableAirDrag;
                backupBody.airDragFactor = cpuBody.airDragFactor;
                backupBody.enableWind = cpuBody.enableWind;
                backupBody.windForce = cpuBody.windForce;
                backupBody.integration = cpuBody.integration;
            }

            var cpuCollider = obj.GetComponent<SpringPhysicsCollider>();
            if (cpuCollider != null)
            {
                var backupCollider = backup.AddComponent<SpringPhysicsCollider>();
                backupCollider.ImpactRadius = cpuCollider.ImpactRadius;
            }

            var materialHolder = obj.GetComponent<MaterialHolder>();
            if (materialHolder != null)
            {
                var backupHolder = backup.AddComponent<MaterialHolder>();
                backupHolder.Profile = materialHolder.Profile;
            }

            backup.SetActive(false); // Hide the backup
        }

        [ContextMenu("Find All CPU Objects")]
        public void FindAllCPUObjects()
        {
            var cpuBodies = FindObjectsOfType<MassSpringBody>();
            Debug.Log($"Found {cpuBodies.Length} CPU Mass-Spring objects:");

            foreach (var body in cpuBodies)
            {
                Debug.Log($"- {body.name} ({body.Points.Count} points, {body.Springs.Count} springs)");
            }
        }

        [ContextMenu("Find All GPU Objects")]
        public void FindAllGPUObjects()
        {
            var gpuBodies = FindObjectsOfType<MassSpringGPU>();
            Debug.Log($"Found {gpuBodies.Length} GPU Mass-Spring objects:");

            foreach (var body in gpuBodies)
            {
                Debug.Log($"- {body.name} ({body._pointCount} points, {body._springCount} springs)");
            }
        }

        [ContextMenu("Performance Comparison")]
        public void PerformanceComparison()
        {
            var cpuBodies = FindObjectsOfType<MassSpringBody>();
            var gpuBodies = FindObjectsOfType<MassSpringGPU>();

            int cpuPoints = 0;
            int cpuSprings = 0;
            foreach (var body in cpuBodies)
            {
                cpuPoints += body.Points.Count;
                cpuSprings += body.Springs.Count;
            }

            int gpuPoints = 0;
            int gpuSprings = 0;
            foreach (var body in gpuBodies)
            {
                gpuPoints += body._pointCount;
                gpuSprings += body._springCount;
            }

            Debug.Log("=== Performance Comparison ===");
            Debug.Log($"CPU Objects: {cpuBodies.Length}");
            Debug.Log($"CPU Points: {cpuPoints}");
            Debug.Log($"CPU Springs: {cpuSprings}");
            Debug.Log($"GPU Objects: {gpuBodies.Length}");
            Debug.Log($"GPU Points: {gpuPoints}");
            Debug.Log($"GPU Springs: {gpuSprings}");
            Debug.Log($"Estimated CPU Time: ~{cpuPoints * 0.0025f:F3}ms");
            Debug.Log($"Estimated GPU Time: ~{gpuPoints * 0.0001f:F3}ms");
            Debug.Log($"Speedup: ~{cpuPoints * 0.0025f / (gpuPoints * 0.0001f):F1}x");
            Debug.Log("==============================");
        }
    }
}