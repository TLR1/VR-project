using UnityEngine;

/// <summary>
/// يمثّل نقطة (كتلة) واحدة داخل شبكة الفوكسل.
/// تُخزَّن فيها السرعة والقوة المتراكمة، بالإضافة إلى رقم فهرسي داخل المصفوفة.
/// </summary>
public class VoxelPoint : MonoBehaviour
{
    /// <summary>السرعة الحالية للنقطة (m/s).</summary>
    [HideInInspector] public Vector3 velocity;

    /// <summary>مجموع القوى المؤثّرة في هذه النقطة أثناء خطوة المحاكاة.</summary>
    [HideInInspector] public Vector3 forceAcc;

    /// <summary>
    /// فهرس خطّي للنقطة داخل المصفوفة ثلاثية الأبعاد
    /// (x + y * dimX + z * dimX * dimY).
    /// </summary>
    public int index;
}
