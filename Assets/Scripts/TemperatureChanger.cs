using UnityEngine;

public class TemperatureChanger : MonoBehaviour
{
    [SerializeField] private float speed;
    
    private Light _light;

    private void Awake()
    {
        _light = GetComponent<Light>();
    }

    private void Update()
    {
        Color.RGBToHSV(_light.color, out var h, out var s, out var v);
        h += speed * Time.deltaTime;
        if (h >= 360)
        {
            h = 0;
        }
        _light.color = Color.HSVToRGB(h, s, v);
    }
}
