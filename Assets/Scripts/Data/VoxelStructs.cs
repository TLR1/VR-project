// Assets/Scripts/Data/VoxelStructs.cs
using UnityEngine;

[System.Serializable]
public struct VoxelGridParams
{
    public float voxelSize;
    public Vector3Int dims;   // (DimX, DimY, DimZ)
}

[System.Serializable]
public struct SpringMaterial
{
    public float yieldThreshold;
    public float fractureThreshold;
    public float stiffness;
    public float damping;
    public float pointMass;
}

[System.Serializable]
public struct CollisionSettings
{
    public float breakRadius;
    public float restitution;
}
