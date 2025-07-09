using UnityEngine;
using Physics.Materials;

namespace Physics.Materials
{


    [DisallowMultipleComponent]
    public class MaterialHolder : MonoBehaviour
    {
        [Tooltip("ملف المادة الفيزيائية المرتبط بهذا الجسم")]
        public MaterialProfile Profile;

        // يمكنك لاحقًا إضافة خصائص runtime مثل CurrentDamage أو مرونة لحظية...
    }
}