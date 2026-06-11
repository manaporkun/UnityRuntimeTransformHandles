using System.Collections;
using NUnit.Framework;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace TransformHandles.Tests
{
    // Concrete subclass of Singleton<T> (TransformHandles.Utils) for lifecycle testing.
    public class TestSingleton : Singleton<TestSingleton> { }

    public class SingletonTests
    {
        [TearDown]
        public void Cleanup()
        {
            // DestroyImmediate -> OnDestroy nulls the static _instance, isolating each test.
            // FindObjectsOfType is deprecated from 2023.1; mirror the runtime guard so the
            // suite compiles warning-free on both the 2021.3 floor and current Unity.
#if UNITY_2023_1_OR_NEWER
            foreach (var s in Object.FindObjectsByType<TestSingleton>(FindObjectsSortMode.None))
#else
            foreach (var s in Object.FindObjectsOfType<TestSingleton>())
#endif
            {
                Object.DestroyImmediate(s.gameObject);
            }
        }

        [Test]
        public void Instance_is_created_lazily_and_is_idempotent()
        {
            var a = TestSingleton.Instance;
            Assert.IsNotNull(a);

            var b = TestSingleton.Instance;
            Assert.AreSame(a, b, "Instance must return the same object on repeated access");
        }

        [UnityTest]
        public IEnumerator Duplicate_component_destroys_itself_and_keeps_original()
        {
            var first = TestSingleton.Instance;

            var duplicateGo = new GameObject("duplicate");
            duplicateGo.AddComponent<TestSingleton>(); // Awake sees an existing instance -> Destroy(self)

            yield return null; // let the deferred Destroy run

            Assert.IsTrue(duplicateGo == null, "a second Singleton instance should self-destruct");
            Assert.AreSame(first, TestSingleton.Instance, "the original instance must survive");
        }
    }
}
