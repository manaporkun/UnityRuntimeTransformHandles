using System.Collections.Generic;
using TransformHandle;
using UnityEngine;

public class ObjSelector : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    
    private Camera _camera;
    private CameraMovement _cameraMovement;

    private TransformHandleManager _transformHandleManager;
    private Handle _lastHandle;
    private Dictionary<Transform, Handle> _handleDictionary;

    private void Awake()
    {
        _camera = Camera.main;
        if (_camera != null) _cameraMovement = _camera.GetComponent<CameraMovement>();
        
        _transformHandleManager = TransformHandleManager.Instance;
        _handleDictionary = new Dictionary<Transform, Handle>();
    }

    private void Start()
    {
        TransformHandleManager.Instance.OnInteractionStartEvent += OnHandleInteractionStart;
        TransformHandleManager.Instance.OnInteractionEvent += OnHandleInteraction;
        TransformHandleManager.Instance.OnInteractionEndEvent += OnHandleInteractionEnd;
    }

    private void Update()
  
    {
        // Add the object to handle if exists, else create a new handle
        if (Input.GetMouseButtonDown(0))
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, layerMask))
            {
                var hitTransform = hit.transform;
                if(_handleDictionary.ContainsKey(hitTransform)) return;
                if (_lastHandle == null) { CreateHandle(hitTransform); }
                else { AddTarget(hitTransform); }
                SelectObject(hitTransform);
            }
        }

        // Remove the object from handle
        if (Input.GetMouseButtonDown(1))
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                var hitTransform = hit.transform;
                if(!_handleDictionary.ContainsKey(hitTransform)) return;
                RemoveTarget(hitTransform);
                DeselectObject(hitTransform);
            }
        }

        // Create new handle for object
        if (Input.GetMouseButton(2))
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 1000f, layerMask)) return;
            if(_handleDictionary.ContainsKey(hit.transform)) return;
            var hitTransform = hit.transform;
            CreateHandle(hitTransform);
            SelectObject(hitTransform);
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
    
    private void CreateHandle(Transform hitTransform)
    {
        var handle = _transformHandleManager.CreateHandle(hitTransform);
        _handleDictionary.Add(hitTransform, handle);
        _lastHandle = handle;
    }
    
    private void AddTarget(Transform hitTransform)
    {
        _transformHandleManager.AddTarget(hitTransform, _lastHandle);
        _handleDictionary.Add(hitTransform, _lastHandle);
    }
    
    private void RemoveTarget(Transform hitTransform)
    {
        var handle = _handleDictionary[hitTransform];
        if (_lastHandle == handle) _lastHandle = null;

        _transformHandleManager.RemoveTarget(hitTransform, handle);
        _handleDictionary.Remove(hitTransform);
    }

    private void OnHandleInteractionStart()
    {
        _cameraMovement.enabled = false;
    }

    private void OnHandleInteraction()
    {
        
    }
    
    private void OnHandleInteractionEnd()
    {
        _cameraMovement.enabled = true;
    }
}