using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ReflectOnCollision : MonoBehaviour
{
    public Vector3 velocity = new Vector3(2, 0, 0); 
    public float speed = 5f;

    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    private void Update()
    {
        transform.position += velocity * speed * Time.deltaTime;
    }

    public void HandleCollision(EPAResult result)
    {
        velocity = Vector3.Reflect(velocity, result.Normal);
        Debug.Log($"Reflected! New velocity: {velocity}");
    }
}