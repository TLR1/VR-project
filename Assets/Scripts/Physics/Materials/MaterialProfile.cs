// Assets/Scripts/Physics/Materials/MaterialProfile.cs
using UnityEngine;

namespace Physics.Materials
{
    [CreateAssetMenu(fileName = "NewMaterialProfile", menuName = "Physics/Material Profile")]
    public class MaterialProfile : ScriptableObject
    {
        [Header("General Properties")]
        [Tooltip("Density (kg/m³) — affects mass")]
        public float Density = 1000f;

        [Tooltip("Elasticity — 0 = no bounce, 1 = perfect bounce")]
        [Range(0f, 1f)]
        public float Elasticity = 0.5f;

        [Tooltip("Stiffness — spring constant k")]
        public float Stiffness = 1000f;

        [Tooltip("Damping coefficient")]
        public float Damping = 50f;

        [Tooltip("Plastic yield threshold (displacement)")]
        public float YieldThreshold = 0.1f;

        [Tooltip("Break (fracture) threshold (displacement)")]
        public float BreakThreshold = 0.3f;

        [Tooltip("Plasticity factor (ratio of permanent stretch)")]
        [Range(0f, 1f)]
        public float Plasticity = 0.05f;
    }
}