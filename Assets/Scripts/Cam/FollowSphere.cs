using UnityEngine;

public class FollowSphere : MonoBehaviour
{
    public Transform sphere;  // Assign this from the inspector
    public Vector3 offset = new Vector3(0, 2, -15); // Optional offset to look from above or behind

    void LateUpdate()
    {
        if (sphere != null)
        {
            transform.position = sphere.position + offset;
            transform.LookAt(sphere); // Optional: make camera face the sphere
        }
    }
}
