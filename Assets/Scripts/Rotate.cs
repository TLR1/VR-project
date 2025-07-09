using UnityEngine;

public class Rotate : MonoBehaviour
{
    [SerializeField]
    private Vector3 _axis;

    [SerializeField]
    private float _speed;

    private void FixedUpdate() 
        => transform.Rotate(_axis.normalized * _speed * Time.fixedDeltaTime);
}
