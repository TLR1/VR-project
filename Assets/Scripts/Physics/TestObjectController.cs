using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TestObjectController : MonoBehaviour
{
    public Vector3 initialVelocity = new Vector3(2, 0, 0);
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        rb.linearVelocity = initialVelocity;
    }

    public void ApplyReflection(Vector3 normal)
    {
        rb.linearVelocity = Vector3.Reflect(rb.linearVelocity, normal);
        Debug.Log($"Reflected! New velocity: {rb.linearVelocity}");
    }

    public void ApplyDeformation()
    {
        GetComponent<Renderer>().material.color = Color.yellow;
        Debug.Log("Deformed!");
    }

    public void ApplyBreak()
    {
        Debug.Log("Breaking object!");
        Destroy(gameObject);
    }
}