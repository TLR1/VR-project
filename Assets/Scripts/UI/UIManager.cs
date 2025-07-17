using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    // الحقول الأساسية
    public TMP_InputField inputDimX;
    public TMP_InputField inputDimY;
    public TMP_InputField inputDimZ;

    public TMP_InputField inputYieldThreshold;
    public TMP_InputField inputFractureThreshold;
    public TMP_InputField inputStiffness;
    public TMP_InputField inputDamping;

    public TMP_InputField inputPointMass;
    public TMP_InputField inputBreakRadius;
    public TMP_InputField inputRestitution;
    public TMP_InputField inputTimeScale;

    public Button playButton;
    public Button pauseButton;
    public Button resetButton;

    public SimulationController simulationController;

    void Start()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        pauseButton.onClick.AddListener(OnPauseClicked);
        resetButton.onClick.AddListener(OnResetClicked);
    }

    public void ApplySettings()
    {
        int dimX = int.Parse(inputDimX.text);
        int dimY = int.Parse(inputDimY.text);
        int dimZ = int.Parse(inputDimZ.text);

        float yieldThreshold = float.Parse(inputYieldThreshold.text);
        float fractureThreshold = float.Parse(inputFractureThreshold.text);
        float stiffness = float.Parse(inputStiffness.text);
        float damping = float.Parse(inputDamping.text);

        float pointMass = float.Parse(inputPointMass.text);
        float breakRadius = float.Parse(inputBreakRadius.text);
        float restitution = float.Parse(inputRestitution.text);
        float timeScale = float.Parse(inputTimeScale.text);

        simulationController.dimX = dimX;
        simulationController.dimY = dimY;
        simulationController.dimZ = dimZ;

        simulationController.yieldThreshold = yieldThreshold;
        simulationController.fractureThreshold = fractureThreshold;
        simulationController.stiffness = stiffness;
        simulationController.damping = damping;

        simulationController.pointMass = pointMass;
        simulationController.breakRadius = breakRadius;
        simulationController.restitution = restitution;
        simulationController.timeScale = timeScale;
    }

    void OnPlayClicked() => simulationController.Play();
    void OnPauseClicked() => simulationController.Pause();
    void OnResetClicked() => simulationController.ResetSimulation();
}
