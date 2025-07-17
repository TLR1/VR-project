// Assets/Scripts/UI/UIVoxelGridController.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIVoxelGridController : MonoBehaviour
{
    [Header("Voxel Grid Inputs")]
    [SerializeField] private TMP_InputField voxelSizeInput;
    [SerializeField] private TMP_InputField dimXInput;
    [SerializeField] private TMP_InputField dimYInput;
    [SerializeField] private TMP_InputField dimZInput;

    [Header("Material Inputs")]
    [SerializeField] private TMP_InputField yieldInput;
    [SerializeField] private TMP_InputField fractureInput;
    [SerializeField] private TMP_InputField stiffnessInput;
    [SerializeField] private TMP_InputField dampingInput;
    [SerializeField] private TMP_InputField massInput;

    [Header("Collision Inputs")]
    [SerializeField] private TMP_InputField breakRadiusInput;
    [SerializeField] private TMP_InputField restitutionInput;

    [Header("Buttons")]
    [SerializeField] private Button applyButton;   // يحفظ الإعدادات فقط
    [SerializeField] private Button playButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resetButton;

    // أحداث يلتقطها SimulationController
    public System.Action<VoxelGridParams> OnParamsChanged;
    public System.Action<SpringMaterial>  OnMaterialChanged;
    public System.Action<CollisionSettings> OnCollisionChanged;

    public System.Action OnPlayPressed;
    public System.Action OnPausePressed;
    public System.Action OnResetPressed;

    private void Awake()
    {
        applyButton.onClick.AddListener(ReadAndDispatch);
        playButton.onClick.AddListener(() => OnPlayPressed?.Invoke());
        pauseButton.onClick.AddListener(() => OnPausePressed?.Invoke());
        resetButton.onClick.AddListener(() => OnResetPressed?.Invoke());
    }

    private void ReadAndDispatch()
    {
        var grid = new VoxelGridParams
        {
            voxelSize = float.Parse(voxelSizeInput.text),
            dims      = new Vector3Int(
                            int.Parse(dimXInput.text),
                            int.Parse(dimYInput.text),
                            int.Parse(dimZInput.text))
        };

        var mat = new SpringMaterial
        {
            yieldThreshold    = float.Parse(yieldInput.text),
            fractureThreshold = float.Parse(fractureInput.text),
            stiffness         = float.Parse(stiffnessInput.text),
            damping           = float.Parse(dampingInput.text),
            pointMass         = float.Parse(massInput.text)
        };

        var col = new CollisionSettings
        {
            breakRadius = float.Parse(breakRadiusInput.text),
            restitution = float.Parse(restitutionInput.text)
        };

        Debug.Log($"[UI] Grid {grid.dims}  VoxSize={grid.voxelSize}");
        Debug.Log($"[UI] Material k={mat.stiffness} damping={mat.damping}");
        Debug.Log($"[UI] Collision radius={col.breakRadius}");

        OnParamsChanged?.Invoke(grid);
        OnMaterialChanged?.Invoke(mat);
        OnCollisionChanged?.Invoke(col);
    }
}
