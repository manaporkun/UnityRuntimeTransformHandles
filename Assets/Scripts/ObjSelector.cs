using System.Collections.Generic;
using TransformHandle;
using UnityEngine;

public class ObjSelector : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Color selectedColor;
    [SerializeField] private Color unselectedColor;
    
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
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetMouseButtonDown(0))
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, layerMask))
            {
                var hitTransform = hit.transform;
                if(_handleDictionary.ContainsKey(hitTransform)) return;
                CreateHandle(hitTransform);
                
                var children = hitTransform.GetComponentsInChildren<Transform>();
                foreach (var child in children)
                {
                    SelectObject(child);
                }
            }
        }
        // Add the object to handle if exists, else create a new handle
        else if (Input.GetMouseButtonDown(0))
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f, layerMask))
            {
                var hitTransform = hit.transform;
                if(_handleDictionary.ContainsKey(hitTransform)) return;
                if (_lastHandle == null) { CreateHandle(hitTransform); }
                else { AddTarget(hitTransform); }
                
                var children = hitTransform.GetComponentsInChildren<Transform>();
                foreach (var child in children)
                {
                    SelectObject(child);
                }
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
                var children = hitTransform.GetComponentsInChildren<Transform>();
                foreach (var child in children)
                {
                    DeselectObject(child);
                }
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

    private void DeselectObject(Transform hitInfoTransform)
    {
        _handleDictionary.Remove(hitInfoTransform);

        hitInfoTransform.tag = "Untagged";
        var renderer = hitInfoTransform.gameObject.GetComponent<Renderer>();
        if (renderer == null) renderer = hitInfoTransform.GetComponentInChildren<Renderer>();
        renderer.material.color = unselectedColor;
    }

    private void SelectObject(Transform hitInfoTransform)
    {
        _handleDictionary.Add(hitInfoTransform, _lastHandle);

        hitInfoTransform.tag = "Selected";
        var renderer = hitInfoTransform.gameObject.GetComponent<Renderer>();
        if (renderer == null) renderer =  hitInfoTransform.GetComponentInChildren<Renderer>();
        renderer.material.color = selectedColor;
    }
    
    private void CreateHandle(Transform hitTransform)
    {
        var handle = _transformHandleManager.CreateHandle(hitTransform);
        _lastHandle = handle;
    }
    
    private void AddTarget(Transform hitTransform)
    {
        _transformHandleManager.AddTarget(hitTransform, _lastHandle);
    }
    
    private void RemoveTarget(Transform hitTransform)
    {
        var handle = _handleDictionary[hitTransform];
        if (_lastHandle == handle) _lastHandle = null;

        _transformHandleManager.RemoveTarget(hitTransform, handle);
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