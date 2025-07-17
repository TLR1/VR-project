using UnityEngine;
using Physics.Materials;

namespace Physics.GPU
{
    [RequireComponent(typeof(MassSpringGPU))]
    [DisallowMultipleComponent]
    public class GPUPhysicsCollider : MonoBehaviour
    {
        [Header("Collision Settings")]
        [Tooltip("Radius around contact point to select impacted mass points.")]
        public float impactRadius = 0.0003f;

        /// <summary>GPU Mass-Spring body component.</summary>
        public MassSpringGPU Body { get; private set; }

        protected virtual void Awake()
        {
            Body = GetComponent<MassSpringGPU>();
        }

        /// <summary>
        /// Find the furthest point in the given direction for collision detection.
        /// </summary>
        public virtual Vector3 FindFurthestPoint(Vector3 direction)
        {
            if (Body == null) return transform.position;

            // For GPU system, we need to read back the data to find the furthest point
            // This is a performance trade-off - in a full GPU collision system,
            // you'd want to do this on the GPU as well
            var massPoints = new MassSpringGPU.MassPointGPU[Body._pointCount];
            Body._massPointsBuffer.GetData(massPoints);

            float maxDot = float.MinValue;
            Vector3 best = transform.position;

            for (int i = 0; i < massPoints.Length; i++)
            {
                float d = Vector3.Dot(massPoints[i].position, direction);
                if (d > maxDot)
                {
                    maxDot = d;
                    best = massPoints[i].position;
                }
            }

            return best;
        }

        /// <summary>
        /// Handle collision with another collider.
        /// </summary>
        public virtual void OnCollision(EPAResult epaResult)
        {
            // Apply impulse to the GPU body
            if (Body != null)
            {
                Vector3 impulse = epaResult.Normal * epaResult.PenetrationDepth * 100f; // Scale factor
                Body.ApplyImpulse(impulse, epaResult.ContactPoint, impactRadius);
            }
        }

        /// <summary>
        /// Register collision result for this frame.
        /// </summary>
        public virtual void RegisterCollisionResult(bool isColliding)
        {
            // Can be used for collision state tracking
        }
    }
}