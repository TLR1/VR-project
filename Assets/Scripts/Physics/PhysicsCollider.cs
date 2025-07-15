using System;
using UnityEngine;

namespace Physics
{
    public abstract class PhysicsCollider : MonoBehaviour
    {
        [Tooltip("إذا كان هذا الكوليندر يمثل أرضًا أو جدارًا ثابتًا فلا يتحرك.")]
        public bool IsStatic = false;

        private CollisionSolver _collisionSolver;

        public bool Colliding { get; private set; }
        private bool _currentState;

        public event Action CollisionEnter;
        public event Action CollisionExit;

        protected virtual void Awake()
        {
            _collisionSolver = FindFirstObjectByType<CollisionSolver>();
            Colliding = false;
            _currentState = false;
        }

        protected virtual void OnEnable()
        {
            if (_collisionSolver == null)
                _collisionSolver = FindFirstObjectByType<CollisionSolver>();

            if (_collisionSolver != null && _collisionSolver.Colliders != null && !_collisionSolver.Colliders.Contains(this))
                _collisionSolver.Colliders.Add(this);
        }

        protected virtual void OnDisable()
        {
            if (_collisionSolver == null)
                _collisionSolver = FindFirstObjectByType<CollisionSolver>();

            if (_collisionSolver != null && _collisionSolver.Colliders != null)
                _collisionSolver.Colliders.Remove(this);
        }

        public abstract Vector3 FindFurthestPoint(Vector3 direction);

        public void RegisterCollisionResult(bool hit)
        {
            if (!_currentState && hit)
                _currentState = true;
        }

        public void UpdateState()
        {
            if (Colliding != _currentState)
            {
                if (_currentState)
                    CollisionEnter?.Invoke();
                else
                    CollisionExit?.Invoke();

                Colliding = _currentState;
            }

            _currentState = false;
        }

        public virtual void OnCollisionReflect(Vector3 normal) { }
        public virtual void OnCollisionDeform()               { }
        public virtual void OnCollisionBreak()                { }
        public virtual void OnCollision(EPAResult epaResult)  { }
    }
}
