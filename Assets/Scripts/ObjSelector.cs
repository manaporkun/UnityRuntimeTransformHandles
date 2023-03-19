using UnityEngine;

public class ObjSelector : MonoBehaviour
{
    private Camera _camera;

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                SelectObject(hit.transform);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                DeselectObject(hit.transform);
            }
        }
    }

    private void DeselectObject(Transform hitInfoTransform)
    {
        throw new System.NotImplementedException();
    }

    private void SelectObject(Transform hitInfoTransform)
    {
        throw new System.NotImplementedException();
    }
}