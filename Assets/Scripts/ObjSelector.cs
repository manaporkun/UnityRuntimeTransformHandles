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
    
    private HandleGroup _lastHandleGroup;
    private Dictionary<Transform, HandleGroup> _handleDictionary;

    private void Awake()
    {
        _camera = Camera.main;
        if (_camera != null) _cameraMovement = _camera.GetComponent<CameraMovement>();
        
        _manager = TransformHandleManager.Instance;
        _handleDictionary = new Dictionary<Transform, HandleGroup>();
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
                if (_lastHandleGroup == null) { CreateHandle(hitTransform); }
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
        _handleDictionary.Add(hitInfoTransform, _lastHandleGroup);

        hitInfoTransform.tag = "Selected";
        var rendererComponent = hitInfoTransform.gameObject.GetComponent<Renderer>();
        if (rendererComponent == null) rendererComponent =  hitInfoTransform.GetComponentInChildren<Renderer>();
        rendererComponent.material.color = selectedColor;
    }
    
    private void CreateHandle(Transform hitTransform)
    {
        var handle = _manager.CreateHandle(hitTransform);
        _lastHandleGroup = handle;
        
        handle.OnInteractionStartEvent += OnHandleInteractionStart;
        handle.OnInteractionEvent += OnHandleInteraction;
        handle.OnInteractionEndEvent += OnHandleInteractionEnd;
        handle.OnHandleDestroyedEvent += OnHandleDestroyed;
    }

    private void AddTarget(Transform hitTransform)
    {
        _manager.AddTarget(hitTransform, _lastHandleGroup);
    }
    
    private void RemoveTarget(Transform hitTransform)
    {
        var handle = _handleDictionary[hitTransform];
        if (_lastHandleGroup == handle) _lastHandleGroup = null;

        _manager.RemoveTarget(hitTransform, handle);
    }

    private void OnHandleInteractionStart(HandleGroup handleGroup)
    {
        _cameraMovement.enabled = false;
    }

    private static void OnHandleInteraction(HandleGroup handleGroup)
    {
        Debug.Log($"{handleGroup.name} is being interacted with");
    }
    
    private void OnHandleInteractionEnd(HandleGroup handleGroup)
    {
        _cameraMovement.enabled = true;
    }
    
    private void OnHandleDestroyed(HandleGroup handleGroup)
    {
        handleGroup.OnInteractionStartEvent -= OnHandleInteractionStart;
        handleGroup.OnInteractionEvent -= OnHandleInteraction;
        handleGroup.OnInteractionEndEvent -= OnHandleInteractionEnd;
        handleGroup.OnHandleDestroyedEvent -= OnHandleDestroyed;
    }
}