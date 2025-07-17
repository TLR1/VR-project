using TMPro;
using UnityEngine;

/// <summary>
/// يبني عناصر الخصائص داخل Panel_Properties بناءً على الجسم المختار.
/// يستمع لحدث SelectionManager.OnSelect.
/// </summary>
public class PropertiesPanel : MonoBehaviour
{
    [Header("Prefab & Parent")]
    [SerializeField] private GameObject entryPrefab;   // Prefab لعنصر PropertyEntry
    [SerializeField] private Transform  entriesParent; // حيث تُضاف العناصر (Panel_Properties)

    private void OnEnable()  => SelectionManager.OnSelect += BuildPanel;
    private void OnDisable() => SelectionManager.OnSelect -= BuildPanel;

    /* ------------------------------------------------------ */
    private void BuildPanel(GameObject go)
    {
        // 1) مسح الإدخالات القديمة
        foreach (Transform child in entriesParent)
            Destroy(child.gameObject);

        // 2) عنوان
        AddHeader(go.name);

        // 3) موضع X,Y,Z
        AddVector3("Position", go.transform.position,
            v => go.transform.position = v);

        // 4) مثال: كتلة إن وُجد Rigidbody
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
            AddFloat("Mass", rb.mass, m => rb.mass = m);
    }

    /* ====== دوال مساعدة لبناء الإدخالات ====== */

    void AddHeader(string text)
    {
        var e = Instantiate(entryPrefab, entriesParent);
        Destroy(e.transform.Find("Input").gameObject);      // لا حقل إدخال
        e.transform.Find("Label").GetComponent<TMP_Text>()
            .text = $"<b>{text}</b>";
    }

    void AddFloat(string label, float value, System.Action<float> onChanged)
    {
        var e = Instantiate(entryPrefab, entriesParent);
        e.transform.Find("Label").GetComponent<TMP_Text>().text = label;

        var field = e.transform.Find("Input")
                               .GetComponent<TMP_InputField>();
        field.text = value.ToString("0.###");
        field.onEndEdit.AddListener(str =>
        {
            if (float.TryParse(str, out float v))
                onChanged(v);
        });
    }

    void AddVector3(string label, Vector3 v, System.Action<Vector3> onChanged)
    {
        AddFloat($"{label} X", v.x, x => { v.x = x; onChanged(v); });
        AddFloat($"{label} Y", v.y, y => { v.y = y; onChanged(v); });
        AddFloat($"{label} Z", v.z, z => { v.z = z; onChanged(v); });
    }
}
