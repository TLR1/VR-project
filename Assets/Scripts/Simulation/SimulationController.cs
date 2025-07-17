using System.Collections.Generic;
using UnityEngine;

public class SimulationController : MonoBehaviour
{
    public static SimulationController Instance { get; private set; }

    /* ـــــــــــــــــــــ بيانات الشبكة ـــــــــــــــــــــ */
    [Header("Voxel Grid")]
    public float voxelSize = 0.25f;
    public int dimX = 6, dimY = 4, dimZ = 3;

    /* ـــــــــــــــــــــ خصائص المادة ـــــــــــــــــــــ */
    [Header("Material Properties")]
    public float yieldThreshold = 1f;
    public float fractureThreshold = 2f;
    public float stiffness = 100f;
    public float damping = 2f;
    public float pointMass = 1f;

    /* ـــــــــــــــــــــ التصادم ـــــــــــــــــــــ */
    [Header("Collision Settings")]
    public float breakRadius = 0.5f;
    public float restitution = 0.2f;

    /* ـــــــــــــــــــــ التحكم بالزمن ـــــــــــــــــــــ */
    [Header("Simulation Controls")]
    public float timeScale = 1f;

    /* ـــــــــــــــــــــ مراجع الـPrefab ـــــــــــــــــــــ */
    [Header("Prefabs & Parents")]
    [SerializeField] private VoxelPoint pointPrefab;   // اسحب VoxelPrefab هنا
    [SerializeField] private Transform  pointsParent;  // اسحب PointsContainer هنا

    /* ـــــــــــــــــــــ متغيرات داخلية ـــــــــــــــــــــ */
    private readonly List<VoxelPoint> points = new();  // جميع النقاط المولَّدة
    private bool gridBuilt = false;                    // منع إعادة البناء عشوائيًا

    /* --------------------------------------------------------- */

    #region Singleton
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    #endregion

    /* --------------------------------------------------------- */
    #region أزرار التحكم (تُربَط من الـUI)

    public void Play()          // زر Play
    {
        if (!gridBuilt) BuildGrid();
        Time.timeScale = timeScale;
    }

    public void Pause()         // زر Pause
    {
        Time.timeScale = 0f;
    }

    public void ResetSimulation()   // زر Reset
    {
        Time.timeScale = 0f;
        ClearGrid();
        BuildGrid();
    }
    #endregion

    /* --------------------------------------------------------- */
    #region بناء الشبكة

    /// <summary>ينشئ نقاط الفوكسل في مصفوفة منتظمة.</summary>
    private void BuildGrid()
    {
        if (pointPrefab == null || pointsParent == null)
        {
            Debug.LogError("[Sim] Prefab أو Parent غير معيَّنَيْن!");
            return;
        }

        ClearGrid();            // احتياطًا
        float s = voxelSize;
        int idx = 0;

        for (int z = 0; z < dimZ; ++z)
        for (int y = 0; y < dimY; ++y)
        for (int x = 0; x < dimX; ++x)
        {
            Vector3 pos = new Vector3(x * s, y * s, z * s);
            var p = Instantiate(pointPrefab, pos, Quaternion.identity, pointsParent);
            p.index = idx++;
            points.Add(p);
        }

        gridBuilt = true;
        Debug.Log($"[Sim] Generated {points.Count} points");
    }

    /// <summary>يحذف جميع النقاط المتولّدة سابقًا.</summary>
    private void ClearGrid()
    {
        foreach (Transform child in pointsParent)
            Destroy(child.gameObject);

        points.Clear();
        gridBuilt = false;
    }
    #endregion
}
