using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class Panel_Library : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    [MenuItem("Window/UI Toolkit/Panel_Library")]
    public static void ShowExample()
    {
        Panel_Library wnd = GetWindow<Panel_Library>();
        wnd.titleContent = new GUIContent("Panel_Library");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        VisualElement label = new Label("Hello World! From C#");
        root.Add(label);

        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);
    }
}
