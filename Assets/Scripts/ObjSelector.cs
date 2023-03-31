using System.Collections.Generic;
using TransformHandles;
using UnityEngine;

public class ObjSelector : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Color selectedColor;
    [SerializeField] private Color unselectedColor;
    
    private Camera _camera;
    private CameraMovement _cameraMovement;

    private TransformHandleManager _manager;
    
    private Handle _lastHandle;
    private Dictionary<Transform, Handle> _handleDictionary;

    private void Awake()
    {
        _camera = Camera.main;
        if (_camera != null) _cameraMovement = _camera.GetComponent<CameraMovement>();
        
        _manager = TransformHandleManager.Instance;
        _handleDictionary = new Dictionary<Transform, Handle>();
    }

    private void OnEnable()
    {
        _manager.OnInteractionStartEvent += OnHandleInteractionStart;
        _manager.OnInteractionEvent += OnHandleInteraction;
        _manager.OnInteractionEndEvent += OnHandleInteractionEnd;
    }

    private void OnDisable()
    {
        _manager.OnInteractionStartEvent -= OnHandleInteractionStart;
        _manager.OnInteractionEvent -= OnHandleInteraction;
        _manager.OnInteractionEndEvent -= OnHandleInteractionEnd;
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
        var rendererComponent = hitInfoTransform.gameObject.GetComponent<Renderer>();
        if (rendererComponent == null) rendererComponent = hitInfoTransform.GetComponentInChildren<Renderer>();
        rendererComponent.material.color = unselectedColor;
    }

    private void SelectObject(Transform hitInfoTransform)
    {
        _handleDictionary.Add(hitInfoTransform, _lastHandle);

        hitInfoTransform.tag = "Selected";
        var rendererComponent = hitInfoTransform.gameObject.GetComponent<Renderer>();
        if (rendererComponent == null) rendererComponent =  hitInfoTransform.GetComponentInChildren<Renderer>();
        rendererComponent.material.color = selectedColor;
    }
    
    private void CreateHandle(Transform hitTransform)
    {
        var handle = _manager.CreateHandle(hitTransform);
        _lastHandle = handle;
    }
    
    private void AddTarget(Transform hitTransform)
    {
        _manager.AddTarget(hitTransform, _lastHandle);
    }
    
    private void RemoveTarget(Transform hitTransform)
    {
        var handle = _handleDictionary[hitTransform];
        if (_lastHandle == handle) _lastHandle = null;

        _manager.RemoveTarget(hitTransform, handle);
    }

    private void OnHandleInteractionStart()
    {
        _cameraMovement.enabled = false;
    }

    private static void OnHandleInteraction(Handle handle)
    {
        Debug.Log($"{handle.name} is being interacted with");
    }
    
    private void OnHandleInteractionEnd()
    {
        _cameraMovement.enabled = true;
    }
}