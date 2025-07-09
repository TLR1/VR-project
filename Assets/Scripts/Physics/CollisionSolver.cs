using System.Collections.Generic;
using UnityEngine;

namespace Physics
{
    public class CollisionSolver : MonoBehaviour
    {
        public List<PhysicsCollider> Colliders = new List<PhysicsCollider>();

        private void FixedUpdate()
        {
            foreach (var col in Colliders)
            {
                // احسب GJK → EPA واحصل على النتيجة
                EPAResult result = ComputeEPA(col);

                // أخطر الكوليدر بالتصادم
                col.OnCollision(result);

                // إذا كان الكوليدر من نوع SpringPhysicsCollider، استعمل Body وHandleBreak
                if (col is SpringPhysicsCollider spc)
                {
                    // استخدم الخاصية العامة Body
                    var body = spc.Body;
                    if (body != null)
                    {
                        // مرر النقطة والعمق إلى الأسلوب العام
                        spc.HandleBreak(result.Normal, result.PenetrationDepth);
                    }
                }
            }
        }

        private EPAResult ComputeEPA(PhysicsCollider col)
        {
            // تنفيذ خوارزمية GJK + EPA هنا...
            return new EPAResult
            {
                Normal = Vector3.up,
                PenetrationDepth = 0.1f
            };
        }
    }
}
