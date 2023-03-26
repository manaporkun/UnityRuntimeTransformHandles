using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Range(0f, 9f)] [SerializeField] private float sensitivity = 2f;
    [Range(0f, 90f)] [SerializeField] private float yRotationLimit = 60f;
    [Range(0f, 90f)] [SerializeField] private float xRotationLimit = 60f;
    [SerializeField] private float zoomSpeed = 10f;

    private Camera _camera;
    private Vector2 _rotation = Vector2.zero;
    private const string XAxis = "Mouse X";
    private const string YAxis = "Mouse Y";
    private float _fieldOfView;

    private void Awake()
    {
        _rotation = transform.localRotation.eulerAngles;
        _camera = GetComponent<Camera>();
        _fieldOfView = _camera.fieldOfView;
    }

    private void Update()
    {
        UpdateRotation();
        UpdateCameraZoom();
    }

    private void UpdateRotation()
    {
        if(sensitivity == 0f) return;
        _rotation.x += Input.GetAxis(XAxis) * sensitivity;
        _rotation.x = Mathf.Clamp(_rotation.x, -xRotationLimit, xRotationLimit);

        _rotation.y += Input.GetAxis(YAxis) * sensitivity;
        _rotation.y = Mathf.Clamp(_rotation.y, -yRotationLimit, yRotationLimit);

        var xQuaternion = Quaternion.AngleAxis(_rotation.x, Vector3.up);
        var yQuaternion = Quaternion.AngleAxis(_rotation.y, Vector3.left);

        transform.localRotation = xQuaternion * yQuaternion;
    }

    private void UpdateCameraZoom()
    {
        if(zoomSpeed == 0f) return;
        _fieldOfView -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        _camera.fieldOfView = _fieldOfView;
        _camera.fieldOfView = Mathf.Clamp(_fieldOfView, 35f, 100f);
        _fieldOfView = _camera.fieldOfView;
    }
}