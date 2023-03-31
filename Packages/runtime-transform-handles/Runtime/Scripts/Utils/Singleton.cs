using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (ApplicationQuitManager.ApplicationQuitting)
            {
                return null;
            }

            if (_instance != null) return _instance;
            _instance = FindObjectOfType<T>();

            if (_instance != null) return _instance;
            var obj = new GameObject(typeof(T).Name);
            _instance = obj.AddComponent<T>();

            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = (T)this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        ApplicationQuitManager.SetApplicationQuitting(true);
    }
}

public static class ApplicationQuitManager
{
    public static bool ApplicationQuitting { get; private set; }

    public static void SetApplicationQuitting(bool quitting)
    {
        ApplicationQuitting = quitting;
    }
}