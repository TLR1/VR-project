using UnityEngine;
using UnityEngine.UIElements;

public class ToolbarUI : MonoBehaviour
{
    private Button playButton;
    private Button resetButton;
    private Button exitButton;

    public GameObject libraryUI;
    public GameObject propertiesUI;

    void Awake()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        playButton = root.Q<Button>("play-button");
        resetButton = root.Q<Button>("reset-button");
        exitButton = root.Q<Button>("exit-button");

        playButton?.RegisterCallback<ClickEvent>(e =>
        {
            gameObject.SetActive(false);
            if (libraryUI != null) libraryUI.SetActive(true);
            if (propertiesUI != null) propertiesUI.SetActive(true);
        });

        resetButton?.RegisterCallback<ClickEvent>(e =>
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        });

        exitButton?.RegisterCallback<ClickEvent>(e =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }
}
