// Assets/Scripts/Interaction/CursorToggle.cs
using UnityEngine;
using UnityEngine.InputSystem;


public class CursorToggle : MonoBehaviour
{
    [SerializeField] Key toggleKey = Key.Tab;

    FlyCamera flyCam;

    void Awake() => flyCam = FindObjectOfType<FlyCamera>();

    void Start()                         // ابدأ في "وضع UI"
    {
        if (flyCam != null) flyCam.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (Keyboard.current[toggleKey].wasPressedThisFrame)
            ToggleMode();
    }

    void ToggleMode()
    {
        if (flyCam == null) return;

        bool toGameMode = !flyCam.enabled;   // سنقلب الحالة الحالية
        flyCam.enabled = toGameMode;         // سيمسك أو يحرر المؤشر فى OnEnable/OnDisable
    }
}
