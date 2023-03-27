using UnityEngine;

public class LightColorChanger : MonoBehaviour
{
    [SerializeField] private float speed;
    
    private Light _light;
    
    // HSV values
    private float _h;
    private float _s;
    private float _v;
    
    private void Awake()
    {
        _light = GetComponent<Light>();
        Color.RGBToHSV(_light.color, out _h, out _s, out _v);
    }
    
    private void Update()
    {
        _h += speed * Time.deltaTime;
        if (_h >= 1)
        {
            _h = 0;
        }
        _light.color = Color.HSVToRGB(_h, _s, _v);
    }
}
