using UnityEngine;

namespace TransformHandles.Utils
{
    // NOTE: Singleton<T> must stay public because the public TransformHandleManager derives from it
    // (a public type cannot inherit from an internal base — CS0060). Moving it out of the global
    // namespace is what fixes the CS0436/CS0104 collisions with consumer-defined Singleton<T> types.
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
#if UNITY_2023_1_OR_NEWER
                _instance = FindFirstObjectByType<T>();
#else
                _instance = FindObjectOfType<T>();
#endif

                if (_instance != null) return _instance;

                // Prefer a configured prefab from Resources (named after the type) so
                // serialized references (e.g. handle/ghost prefabs) are wired up. Falls
                // back to a bare GameObject when no such prefab exists.
                var prefab = Resources.Load<T>(typeof(T).Name);
                if (prefab != null)
                {
                    _instance = Instantiate(prefab);
                    _instance.name = typeof(T).Name;
                    return _instance;
                }

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

    internal static class ApplicationQuitManager
    {
        public static bool ApplicationQuitting { get; private set; }

        public static void SetApplicationQuitting(bool quitting)
        {
            ApplicationQuitting = quitting;
        }

        /// <summary>
        /// Resets the quitting flag when a play session starts. With Enter Play Mode Options
        /// (domain reload disabled), static state survives between sessions, so the flag set by
        /// <see cref="Singleton{T}"/>'s OnApplicationQuit on play-mode exit would otherwise stay
        /// true and make every Instance getter return null on the next run.
        /// </summary>
        /// <remarks>
        /// Lives on this non-generic class because [RuntimeInitializeOnLoadMethod] never fires on
        /// generic types. Singleton&lt;T&gt;._instance needs no reset: destroyed instances compare
        /// equal to null, so the Instance getter self-heals on first access.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlaySessionStart()
        {
            ApplicationQuitting = false;
        }
    }
}
