using UnityEngine;

public class Actions : MonoBehaviour
{
    public void Toggle(GameObject go) 
        => go.SetActive(!go.activeSelf);

    public void Quit() => Application.Quit();
}
