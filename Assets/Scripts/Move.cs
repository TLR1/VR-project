using UnityEngine;

public class Move : MonoBehaviour
{
    [SerializeField]
    private Vector3 _target;

    [SerializeField]
    private float _speed;

    private void FixedUpdate()
    {
        var step = _speed * Time.fixedDeltaTime;
        transform.position = 
            Vector3.MoveTowards(transform.position, _target, step);

        if (Vector3.Distance(transform.position, _target) < 0.001f)
            _target *= -1.0f;
    }
}
