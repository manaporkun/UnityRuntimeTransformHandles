using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Range(0.1f, 9f)][SerializeField] private float sensitivity = 2f;
    [Range(0f, 90f)][SerializeField] private float yRotationLimit = 60f;
    [Range(0f, 90f)][SerializeField] private float xRotationLimit = 60f;

    private Vector2 _rotation = Vector2.zero;
    private const string XAxis = "Mouse X";
    private const string YAxis = "Mouse Y";

    private void Awake()
    {
        _rotation = transform.localRotation.eulerAngles;
    }

    private void Update(){
        _rotation.x += Input.GetAxis(XAxis) * sensitivity;
        _rotation.x = Mathf.Clamp(_rotation.x, -xRotationLimit, xRotationLimit);
            
        _rotation.y += Input.GetAxis(YAxis) * sensitivity;
        _rotation.y = Mathf.Clamp(_rotation.y, -yRotationLimit, yRotationLimit);
            
        var xQuaternion = Quaternion.AngleAxis(_rotation.x, Vector3.up);
        var yQuaternion = Quaternion.AngleAxis(_rotation.y, Vector3.left);

        transform.localRotation = xQuaternion * yQuaternion;
    }
}