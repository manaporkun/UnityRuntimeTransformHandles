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
            foreach (var s in Object.FindObjectsOfType<TestSingleton>())
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
