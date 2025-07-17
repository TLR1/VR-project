using UnityEngine;
using UnityEngine.UIElements;
using Physics.Materials;  // تأكد أنك تستخدم الـ namespace الصحيح
using System.Collections.Generic;

public class MaterialInspector : MonoBehaviour
{
    private DropdownField materialDropdown;
    private FloatField dampingField, yieldField, breakField, plasticityField;
    private FloatField densityField, elasticityField, stiffnessField;
    private Button applyButton;

    private MaterialProfile selectedProfile;
    private List<MaterialProfile> allProfiles;

    void Awake()
    {
        var uiDoc = GetComponent<UIDocument>();
        var root = uiDoc.rootVisualElement;

        // 🔗 ربط الحقول بالأسماء (تأكد أن الاسم متطابق تمامًا مع UXML)
        materialDropdown = root.Q<DropdownField>("materialDropdown");
        dampingField = root.Q<FloatField>("dampingField");
        yieldField = root.Q<FloatField>("yieldField");
        breakField = root.Q<FloatField>("breakField");
        plasticityField = root.Q<FloatField>("plasticityField");
        densityField = root.Q<FloatField>("densityField");
        elasticityField = root.Q<FloatField>("elasticityField");
        stiffnessField = root.Q<FloatField>("stiffnessField");
        applyButton = root.Q<Button>("applyButton");

        // ✅ التحقق من التوصيل
        if (materialDropdown == null || applyButton == null || dampingField == null)
        {
            Debug.LogError("❌ UI elements missing. Check UXML names!");
            return;
        }

        // 📥 تحميل كل ملفات MaterialProfile من Resources
        allProfiles = new List<MaterialProfile>(Resources.LoadAll<MaterialProfile>("physics/Materials"));
        if (allProfiles.Count == 0)
        {
            Debug.LogWarning("⚠️ No MaterialProfile assets found in Resources/physics/Materials.");
            return;
        }

        // 📝 تعبئة القائمة بالأسماء
        materialDropdown.choices = new List<string>();
        foreach (var profile in allProfiles)
            materialDropdown.choices.Add(profile.name);

        // 🔁 عند اختيار مادة
        materialDropdown.RegisterValueChangedCallback(evt =>
        {
            selectedProfile = allProfiles.Find(p => p.name == evt.newValue);
            if (selectedProfile != null)
                LoadProfile(selectedProfile);
        });

        // ⬆️ عند الضغط على زر "Apply"
        applyButton.clicked += () =>
        {
            if (selectedProfile == null) return;

            selectedProfile.Damping = dampingField.value;
            selectedProfile.YieldThreshold = yieldField.value;
            selectedProfile.BreakThreshold = breakField.value;
            selectedProfile.Plasticity = plasticityField.value;
            selectedProfile.Density = densityField.value;
            selectedProfile.Elasticity = elasticityField.value;
            selectedProfile.Stiffness = stiffnessField.value;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(selectedProfile); // لحفظ التعديل في الـ Editor
#endif
        };

        // 💡 اختيار أول عنصر تلقائيًا
        selectedProfile = allProfiles[0];
        materialDropdown.value = selectedProfile.name;
        LoadProfile(selectedProfile);
    }

    private void LoadProfile(MaterialProfile profile)
    {
        dampingField.value = profile.Damping;
        yieldField.value = profile.YieldThreshold;
        breakField.value = profile.BreakThreshold;
        plasticityField.value = profile.Plasticity;
        densityField.value = profile.Density;
        elasticityField.value = profile.Elasticity;
        stiffnessField.value = profile.Stiffness;
    }
}
