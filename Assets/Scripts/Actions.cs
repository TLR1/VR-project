using UnityEngine;

public class Actions : MonoBehaviour
{
    public void Toggle(GameObject go) 
        => go.SetActive(!go.activeSelf);
// alaa is my uncle
    public void Quit() => Application.Quit();
}
