using System;
using UnityEngine;

namespace Physics
{
    /// <summary>
    /// المكوِّن الأساسي لأيّ Collider يستخدم GJK+EPA.
    /// يتحكّم بتسجيل التصادم وإطلاق أحداث الدخول/الخروج،
    /// ويوفّر دوال افتراضية للاستجابة: Reflect، Deform، Break.
    /// </summary>
    public abstract class PhysicsCollider : MonoBehaviour
    {
        private CollisionSolver _collisionSolver;

        /// <summary>
        /// هل كان هناك تصادم في الإطار الماضي؟
        /// </summary>
        public bool Colliding { get; private set; }

        private bool _currentState;

        /// <summary>
        /// يُطلق عند دخول التصادم (Collision Enter).
        /// </summary>
        public event Action CollisionEnter;

        /// <summary>
        /// يُطلق عند خروج التصادم (Collision Exit).
        /// </summary>
        public event Action CollisionExit;

        /// <summary>
        /// يربط هذا المكوّن بالـ CollisionSolver الموجود في المشهد.
        /// </summary>
        protected virtual void Awake()
        {
            // استخدام FindFirstObjectByType بدلاً من FindObjectOfType
            _collisionSolver = FindFirstObjectByType<CollisionSolver>();
            Colliding = false;
            _currentState = false;
        }

        /// <summary>
        /// يسجّل نفسه في قائمة الـ Colliders لدى الـ Solver.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (_collisionSolver != null)
                _collisionSolver.Colliders.Add(this);
        }

        /// <summary>
        /// يزيل نفسه من قائمة الـ Colliders لدى الـ Solver عند التعطيل.
        /// </summary>
        protected virtual void OnDisable()
        {
            if (_collisionSolver != null)
                _collisionSolver.Colliders.Remove(this);
        }

        /// <summary>
        /// يجب أن يُنفّذه كلّ Collider فرعيّ
        /// لإرجاع أبعد نقطة في الاتجاه المُعطى.
        /// </summary>
        public abstract Vector3 FindFurthestPoint(Vector3 direction);

        /// <summary>
        /// يستدعى كلّ إطار من قبل Solver ليجمّع نتائج التصادم.
        /// </summary>
        public void RegisterCollisionResult(bool hit)
        {
            if (!_currentState && hit)
                _currentState = true;
        }

        /// <summary>
        /// يقارن الحالة السابقة بالحالية،
        /// ويطلق أحداث الدخول أو الخروج إن تغيّرت.
        /// </summary>
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

            // أعد تعيين الحالة لبدء الإطار التالي
            _currentState = false;
        }

        // -----------------------------------------------------------------------------------------------------------------
        //  هذه الدوال الافتراضية يُمكن إعادة تعريفها في الأصناف الفرعيّة
        //  لتطبيق انعكاس، تشوّه، أو كسر عند التصادم.
        //  الـ CollisionSolver هو من يستدعي هذه الدوال مباشرةً بناءً على CollisionEffect.
        // -----------------------------------------------------------------------------------------------------------------

        public virtual void OnCollisionReflect(Vector3 normal)
        {
            Debug.Log($"{name}: Reflect");
        }

        public virtual void OnCollisionDeform()
        {
            Debug.Log($"{name}: Deform");
        }

        public virtual void OnCollisionBreak()
        {
            Debug.Log($"{name}: Break");
        }

        public virtual void OnCollision(EPAResult epaResult)
        {
            // يمكن تركها فارغة أو تنفيذ الاستجابة المناسبة
        }
    }
}
