// using UnityEngine;
//
// public class ColorChanger : MonoBehaviour
// {
//     private Collider _collider;
//     
//     private Renderer _renderer;
//
//     private Color _defaultColor;
//
//     [SerializeField]
//     private Color _markedColor; 
//
//     private void Awake() 
//     {
//         _collider = GetComponent<Collider>();
//         _renderer = GetComponent<Renderer>();
//
//         _defaultColor = _renderer.material.color;
//     }
//
//     private void OnEnable()
//     {
//         _collider.CollisionEnter += OnCollisionEnter;
//         _collider.CollisionExit += OnCollisionExit;
//     }
//
//     private void OnDisable()
//     {
//         _collider.CollisionEnter -= OnCollisionEnter;
//         _collider.CollisionExit -= OnCollisionExit;
//     }
//
//     private void OnCollisionEnter() 
//         => _renderer.material.color = _markedColor;
//
//     private void OnCollisionExit() 
//         => _renderer.material.color = _defaultColor;
// }
