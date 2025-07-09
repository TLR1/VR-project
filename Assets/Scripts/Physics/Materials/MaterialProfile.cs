using UnityEngine;

namespace Physics.Materials
{
    [CreateAssetMenu(fileName = "NewMaterialProfile", menuName = "Physics/Material Profile")]
    public class MaterialProfile : ScriptableObject
    {
        [Header("🔍 الخصائص العامة")]
        [Tooltip("الكثافة (كغم / م³) — تؤثر على الكتلة والطاقة المنتقلة")]
        public float Density = 1000f;

        [Tooltip("المرونة — 0 = لا ارتداد، 1 = ارتداد مثالي")]
        [Range(0f, 1f)]
        public float Elasticity = 0.5f;

        [Tooltip("الصلابة — مقاومة الانبعاج الموضعي")]
        public float Hardness = 50f;

        [Tooltip("الهشاشة — 0 = مرن تمامًا، 1 = هش جدًا")]
        [Range(0f, 1f)]
        public float Brittleness = 0.2f;

        [Header("🧪 حدود الإجهاد")]
        [Tooltip("الطاقة اللازمة لحدوث تشوه دائم")]
        public float YieldThreshold = 200f;

        [Tooltip("الطاقة اللازمة لحدوث كسر تام")]
        public float BreakEnergyThreshold = 500f;
    }
}