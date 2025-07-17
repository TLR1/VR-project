#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using Physics.Materials;

public class PrefabGenerator
{
    private const string modelsPath = "Assets/VR-Models/";
    private const string savePath = "Assets/VR-Models/GeneratedPrefabs/";
    private const string defaultMaterialPath = "physics/Materials/DefaultMaterial"; // Without .asset

    [MenuItem("Tools/Generate Prefabs with MaterialHolder")]
    public static void GeneratePrefabs()
    {
        if (!Directory.Exists(savePath))
            Directory.CreateDirectory(savePath);

        var guids = AssetDatabase.FindAssets("t:Model", new[] { modelsPath });

        var defaultMat = Resources.Load<MaterialProfile>(defaultMaterialPath);
        if (defaultMat == null)
        {
            Debug.LogError("❌ DefaultMaterial.asset not found at Resources/physics/Materials/");
            return;
        }

        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
                continue;

            GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
                continue;

            if (instance.GetComponent<MaterialHolder>() == null)
            {
                var holder = instance.AddComponent<MaterialHolder>();
                holder.Profile = defaultMat;
            }

            string prefabName = Path.GetFileNameWithoutExtension(assetPath);
            string newPath = savePath + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, newPath);
            Object.DestroyImmediate(instance);
        }

        Debug.Log("✅ Prefabs generated in: " + savePath);
        AssetDatabase.Refresh();
    }
}
#endif
