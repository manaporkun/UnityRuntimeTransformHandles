using System.Collections.Generic;
using TransformHandle;
using UnityEngine;

public class ObjSelector : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    private Camera _camera;
    
    private Dictionary<Transform, Handle> _handleDictionary = new();

    private void Awake()
    {
        _camera = Camera.main;
    } 
    
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, layerMask))
            {
                var hitTransform = hit.transform;
                if (_handleDictionary.Count <= 0)
                {
                    var handle = TransformHandleManager.Instance.CreateHandle(hitTransform);
                    _handleDictionary.Add(hitTransform, handle);
                }
                else
                {
                    var handle = _handleDictionary.Values.GetEnumerator().Current;
                    _handleDictionary.Add(hitTransform, handle);
                    TransformHandleManager.Instance.AddTarget(hitTransform, handle);
                }
                SelectObject(hitTransform);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                var hitTransform = hit.transform;
                var handle = _handleDictionary[hitTransform];
                TransformHandleManager.Instance.RemoveTarget(hitTransform, handle);

                _handleDictionary.Remove(hitTransform);
                
                DeselectObject(hitTransform);
            }
        }

        if (Input.GetMouseButton(2))
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, layerMask))
            {
                var hitTransform = hit.transform;
                var handle = TransformHandleManager.Instance.CreateHandle(hitTransform);
                _handleDictionary.Add(hitTransform, handle);
                
                SelectObject(hitTransform);
            }
        }
    }

    private static void DeselectObject(Component hitInfoTransform)
    {
        hitInfoTransform.tag = "Untagged";
        hitInfoTransform.gameObject.GetComponent<Renderer>().material.color = Color.white;
    }

    private static void SelectObject(Component hitInfoTransform)
    {
        hitInfoTransform.tag = "Selected";
        hitInfoTransform.gameObject.GetComponent<Renderer>().material.color = Color.red;
    }
    
}