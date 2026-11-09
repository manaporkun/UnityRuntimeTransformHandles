using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TransformHandles.Tests
{
    /// <summary>
    /// P0-a ownership and lifecycle regressions: manager-owned teardown, drag cancellation for every
    /// destruction path, scene ownership retention, teardown-only destroyed-reference safety, and
    /// reentrancy from consumer callbacks.
    /// </summary>
    public class OwnershipLifecycleTests
    {
        private TransformHandleManager _manager;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Scene> _createdScenes = new List<Scene>();
        private Scene _originalScene;

        [SetUp]
        public void SetUp()
        {
            _originalScene = SceneManager.GetActiveScene();

            var cameraGo = NewObject("Main Camera", new Vector3(0f, 0f, -10f));
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";

            _manager = TransformHandleManager.Instance;
            _manager.MainCamera = camera;
            _manager.DestroyAllHandles();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // _manager can be null after a test that intentionally destroyed it. Sweep any
            // accidental resurrection so one failure cannot leak a manager into the next test.
            var manager = _manager != null ? _manager : FindManager();
            if (manager != null)
            {
                manager.DestroyAllHandles();
                Object.DestroyImmediate(manager.gameObject);
            }

            _manager = null;

            if (_originalScene.IsValid() && _originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(_originalScene);
            }

            foreach (var scene in _createdScenes)
            {
                if (!scene.IsValid() || !scene.isLoaded) continue;

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
            }

            _createdScenes.Clear();

            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        private static TransformHandleManager FindManager()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<TransformHandleManager>();
#else
            return Object.FindObjectOfType<TransformHandleManager>();
#endif
        }

        private GameObject NewObject(string name, Vector3 position = default)
        {
            var go = new GameObject(name) { transform = { position = position } };
            _spawned.Add(go);
            return go;
        }

        private static int CountHandles()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<Handle>(FindObjectsSortMode.None).Length;
#else
            return Object.FindObjectsOfType<Handle>().Length;
#endif
        }

        private static int CountGhosts()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<Ghost>(FindObjectsSortMode.None).Length;
#else
            return Object.FindObjectsOfType<Ghost>().Length;
#endif
        }

        private static int CountManagers()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<TransformHandleManager>(FindObjectsSortMode.None).Length;
#else
            return Object.FindObjectsOfType<TransformHandleManager>().Length;
#endif
        }

        private HandleBase BeginDrag(Handle handle)
        {
            var axis = handle.GetComponentInChildren<PositionAxis>(true);
            Assert.IsNotNull(axis, "the default handle must expose a PositionAxis");
            Assert.IsTrue(_manager.BeginInteraction(axis, handle.transform.position),
                "the seam must accept an axis owned by this manager");
            Assert.IsTrue(_manager.IsInteracting);
            return axis;
        }

        [Test]
        public void Destroying_manager_destroys_owned_handles_and_ghosts_but_keeps_targets()
        {
            var a = NewObject("a").transform;
            var b = NewObject("b", new Vector3(2f, 1f, 0f)).transform;
            _manager.CreateHandle(a);
            _manager.CreateHandle(b);

            var aPosition = a.position;
            var bPosition = b.position;

            Object.DestroyImmediate(_manager.gameObject);
            _manager = null;

            Assert.AreEqual(0, CountHandles(), "manager teardown must destroy every owned handle");
            Assert.AreEqual(0, CountGhosts(), "manager teardown must destroy every owned ghost");
            Assert.AreEqual(0, CountManagers());
            Assert.AreEqual(aPosition, a.position, "consumer targets must survive teardown unchanged");
            Assert.AreEqual(bPosition, b.position, "consumer targets must survive teardown unchanged");

            _manager = TransformHandleManager.Instance;
            Assert.IsNotNull(_manager.CreateHandle(a), "a surviving target must be re-registrable");
            Assert.IsNotNull(_manager.CreateHandle(b));
        }

        [Test]
        public void Manager_teardown_preserves_target_hierarchy_and_transforms()
        {
            var parent = NewObject("parent", new Vector3(1f, 2f, 3f)).transform;
            var childGo = new GameObject("child");
            _spawned.Add(childGo);
            childGo.transform.SetParent(parent, false);
            childGo.transform.localPosition = new Vector3(0.5f, -1f, 2f);
            childGo.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            childGo.transform.localScale = new Vector3(2f, 3f, 4f);

            _manager.CreateHandle(childGo.transform);

            var worldPosition = childGo.transform.position;
            var worldRotation = childGo.transform.rotation;
            var localPosition = childGo.transform.localPosition;
            var localRotation = childGo.transform.localRotation;
            var localScale = childGo.transform.localScale;

            Object.DestroyImmediate(_manager.gameObject);
            _manager = null;

            Assert.AreSame(parent, childGo.transform.parent, "teardown must not reparent targets");
            Assert.AreEqual(worldPosition, childGo.transform.position);
            Assert.AreEqual(worldRotation, childGo.transform.rotation);
            Assert.AreEqual(localPosition, childGo.transform.localPosition);
            Assert.AreEqual(localRotation, childGo.transform.localRotation);
            Assert.AreEqual(localScale, childGo.transform.localScale);
        }

        [Test]
        public void Destroying_handle_after_manager_does_not_recreate_manager()
        {
            var owned = _manager.CreateHandle(NewObject("owned").transform);
            var detached = _manager.CreateHandle(NewObject("detached").transform);
            _manager.RemoveHandle(detached);

            Object.DestroyImmediate(_manager.gameObject);
            _manager = null;

            Assert.IsFalse(owned, "manager teardown must destroy the handles it still owns");
            Assert.IsTrue(detached, "RemoveHandle detaches without destroying the handle");

            // `owned` is already gone; comparing the swallowed reference must not resurrect a
            // manager. `detached` survives teardown, so its later OnDestroy exercises PR #50's
            // non-creating lookup.
            Object.DestroyImmediate(detached.gameObject);

            Assert.AreEqual(0, CountManagers(), "handle teardown must not instantiate a manager");
        }

        [UnityTest]
        public IEnumerator Teardown_during_drag_cancels_interaction_once()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            Object.DestroyImmediate(_manager.gameObject);
            _manager = null;

            yield return null;

            Assert.AreEqual(1, endCount, "teardown mid-drag must deliver exactly one end event");
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
            Assert.AreEqual(0, CountManagers());
        }

        [Test]
        public void RemoveHandle_during_drag_cancels_and_manager_stays_usable()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            _spawned.Add(handle.gameObject);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            _manager.RemoveHandle(handle);

            Assert.AreEqual(1, endCount);
            Assert.IsFalse(_manager.IsInteracting);
            Assert.AreEqual(0, CountGhosts());
            Assert.IsNotNull(_manager.CreateHandle(NewObject("next").transform),
                "removing one handle must not tear the manager down");
        }

        [Test]
        public void Repeated_and_destroyed_reference_cleanup_is_safe()
        {
            var doomed = _manager.CreateHandle(NewObject("doomed").transform);
            Object.DestroyImmediate(doomed.gameObject);

            Assert.DoesNotThrow(() => _manager.RemoveHandle(doomed),
                "a destroyed handle reference is an idempotent no-op");
            CollectionAssert.IsEmpty(_manager.GetTargets(doomed),
                "querying a destroyed handle returns no targets");

            Assert.DoesNotThrow(() => _manager.DestroyAllHandles());
            Assert.DoesNotThrow(() => _manager.DestroyAllHandles());
            Assert.AreEqual(0, CountGhosts());
            Assert.AreEqual(1, CountManagers(), "package-owned cleanup must not create a manager");

            Assert.Throws<ArgumentNullException>(() => _manager.RemoveHandle(null));
            Assert.Throws<ArgumentNullException>(() => _manager.GetTargets(null));
        }

        [Test]
        public void Callback_created_handle_is_rejected_during_bulk_cleanup()
        {
            var other = NewObject("other").transform;
            var handle = _manager.CreateHandle(NewObject("target").transform);

            var callbackRan = false;
            Handle created = null;
            handle.OnHandleDestroyedEvent += _ =>
            {
                callbackRan = true;
                created = _manager.CreateHandle(other);
                _manager.DestroyAllHandles();
            };

            _manager.DestroyAllHandles();

            Assert.IsTrue(callbackRan);
            Assert.IsNull(created, "creation must be rejected while the bulk cleanup runs");
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
            Assert.IsNotNull(_manager.CreateHandle(other),
                "creation must resume after a non-final DestroyAllHandles");
        }

        [Test]
        public void Creating_a_new_manager_after_teardown_works()
        {
            Object.DestroyImmediate(_manager.gameObject);
            _manager = null;
            Assert.AreEqual(0, CountManagers());

            _manager = TransformHandleManager.Instance;

            Assert.IsNotNull(_manager);
            Assert.IsNotNull(_manager.MainCamera, "the fresh manager must resolve the configured camera");
            var handle = _manager.CreateHandle(NewObject("target").transform);
            Assert.IsNotNull(handle);
            Assert.IsNotNull(handle.Pivot, "the fresh manager must create a ghost pivot");
        }

        [Test]
        public void Active_scene_change_retains_ownership_and_cancels_drag()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            var newScene = SceneManager.CreateScene("P0A_ActiveScene");
            _createdScenes.Add(newScene);
            SceneManager.SetActiveScene(newScene);

            Assert.AreEqual(1, endCount);
            Assert.IsFalse(_manager.IsInteracting);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count,
                "an active-scene change must not clear ownership");
            CollectionAssert.Contains(handle.Targets, target);
        }

        [Test]
        public void Handle_and_ghost_are_created_in_the_active_scene()
        {
            var newScene = SceneManager.CreateScene("P0A_Placement");
            _createdScenes.Add(newScene);
            SceneManager.SetActiveScene(newScene);

            var handle = _manager.CreateHandle(NewObject("target").transform);

            Assert.AreEqual(newScene, handle.gameObject.scene);
            Assert.AreEqual(newScene, handle.Pivot.gameObject.scene);
        }

        [UnityTest]
        public IEnumerator Additive_unload_during_drag_ends_once_without_ghost()
        {
            var newScene = SceneManager.CreateScene("P0A_Unload");
            _createdScenes.Add(newScene);

            // Create the handle in an additive scene, return to the original active scene, then
            // drag. Unloading the non-active owner scene must go through OnDisable/OnDestroy, not
            // through an active-scene change.
            SceneManager.SetActiveScene(newScene);
            var handle = _manager.CreateHandle(NewObject("target").transform);
            SceneManager.SetActiveScene(_originalScene);

            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            var unload = SceneManager.UnloadSceneAsync(newScene);
            while (unload != null && !unload.isDone) yield return null;

            Assert.AreEqual(1, endCount, "scene unload must deliver exactly one end event");
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
            Assert.IsNotNull(_manager.CreateHandle(NewObject("next").transform),
                "the manager must remain usable after a scene unload");
        }

        [Test]
        public void Disabling_handle_during_drag_cancels_interaction_once()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            handle.gameObject.SetActive(false);

            Assert.AreEqual(1, endCount, "SetActive(false) must deliver exactly one end event");
            Assert.IsFalse(_manager.IsInteracting);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count,
                "P0-a keeps the disabled handle owned; restore semantics belong to P0-c");
        }

        [Test]
        public void Unmanaged_handle_start_is_ignored()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var axis = handle.GetComponentInChildren<PositionAxis>(true);
            _manager.RemoveHandle(handle);
            _spawned.Add(handle.gameObject);

            var axisStarts = 0;
            axis.InteractionStart += () => axisStarts++;

            Assert.IsFalse(_manager.BeginInteraction(axis, Vector3.zero));
            Assert.AreEqual(0, axisStarts, "an unmanaged axis must not emit InteractionStart");
            Assert.IsFalse(_manager.IsInteracting);

            var orphanGo = new GameObject("orphan");
            _spawned.Add(orphanGo);
            orphanGo.SetActive(false);
            var orphanAxis = orphanGo.AddComponent<PositionAxis>();

            Assert.IsFalse(_manager.BeginInteraction(orphanAxis, Vector3.zero));
            Assert.IsFalse(_manager.IsInteracting);
        }

        [Test]
        public void Destroying_one_handle_unregisters_and_cleans_ghost()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            var axis = BeginDrag(handle);

            // Destroy the dragged axis first: the manager must fall back to the captured handle and
            // still deliver one end event when the handle is destroyed afterwards.
            Object.DestroyImmediate(axis.gameObject);
            Object.DestroyImmediate(handle.gameObject);

            Assert.AreEqual(1, endCount);
            Assert.IsFalse(_manager.IsInteracting);
            Assert.AreEqual(0, CountGhosts());
            Assert.IsNotNull(_manager.CreateHandle(target),
                "the target must become re-registrable after its handle is destroyed");
        }

        [UnityTest]
        public IEnumerator Deferred_handle_destruction_unregisters_after_frame()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            Object.Destroy(handle.gameObject);
            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.IsFalse(_manager.IsInteracting);
            Assert.AreEqual(0, CountGhosts());
            Assert.IsNotNull(_manager.CreateHandle(target));
        }

        [Test]
        public void Handle_creation_on_inactive_target_works()
        {
            var targetGo = NewObject("inactive");
            targetGo.SetActive(false);

            Handle handle = null;
            Assert.DoesNotThrow(() => handle = _manager.CreateHandle(targetGo.transform));
            Assert.IsNotNull(handle);
            Assert.IsNotNull(handle.Pivot);

            targetGo.SetActive(true);
        }

        [Test]
        public void Quitting_teardown_does_not_recreate_manager()
        {
            var owned = _manager.CreateHandle(NewObject("owned").transform);
            var survivor = _manager.CreateHandle(NewObject("survivor").transform);
            _spawned.Add(survivor.gameObject);
            _manager.RemoveHandle(survivor);

            try
            {
                ApplicationQuitManager.SetApplicationQuitting(true);

                Object.DestroyImmediate(_manager.gameObject);
                _manager = null;
                Object.DestroyImmediate(survivor.gameObject);
            }
            finally
            {
                ApplicationQuitManager.SetApplicationQuitting(false);
            }

            Assert.IsFalse(owned, "owned handles are destroyed during teardown");
            Assert.AreEqual(0, CountManagers());
            Assert.AreEqual(0, CountGhosts());
        }

        [UnityTest]
        public IEnumerator Throwing_callbacks_do_not_leave_unowned_objects()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                throw new InvalidOperationException("end callback");
            };
            handle.OnHandleDestroyedEvent += _ => throw new InvalidOperationException("destroyed callback");
            BeginDrag(handle);

            LogAssert.Expect(LogType.Exception, new Regex(@"^InvalidOperationException: end callback(?:\r?\n|$)"));
            LogAssert.Expect(LogType.Exception, new Regex(@"^InvalidOperationException: destroyed callback(?:\r?\n|$)"));

            Assert.DoesNotThrow(() => _manager.DestroyAllHandles());
            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
            Assert.IsNotNull(_manager.CreateHandle(NewObject("next").transform));

            var second = _manager.CreateHandle(NewObject("second").transform);
            second.OnInteractionEndEvent += _ =>
            {
                endCount++;
                throw new InvalidOperationException("end callback 2");
            };
            second.OnHandleDestroyedEvent += _ => throw new InvalidOperationException("destroyed callback 2");
            BeginDrag(second);

            LogAssert.Expect(LogType.Exception, new Regex(@"^InvalidOperationException: end callback 2(?:\r?\n|$)"));
            LogAssert.Expect(LogType.Exception, new Regex(@"^InvalidOperationException: destroyed callback 2(?:\r?\n|$)"));

            Assert.DoesNotThrow(() => Object.DestroyImmediate(_manager.gameObject));
            _manager = null;
            yield return null;

            Assert.AreEqual(2, endCount);
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
            Assert.AreEqual(0, CountManagers());
        }

        [Test]
        public void Throwing_end_callback_on_direct_handle_destruction_is_contained()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                throw new InvalidOperationException("direct end callback");
            };
            BeginDrag(handle);

            LogAssert.Expect(LogType.Exception, new Regex(@"^InvalidOperationException: direct end callback(?:\r?\n|$)"));

            Assert.DoesNotThrow(() => Object.DestroyImmediate(handle.gameObject));

            Assert.AreEqual(1, endCount);
            Assert.IsFalse(_manager.IsInteracting);
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
            Assert.IsNotNull(_manager.CreateHandle(target), "the target must remain re-registrable");
        }

        private static int CountAll<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
#else
            return Object.FindObjectsOfType<T>(true).Length;
#endif
        }

        [UnityTest]
        public IEnumerator Destruction_failure_CSharp_immediate_cleans_up() => CheckDestructionFailure(false, false);

        [UnityTest]
        public IEnumerator Destruction_failure_CSharp_deferred_cleans_up() => CheckDestructionFailure(false, true);

        [UnityTest]
        public IEnumerator Destruction_failure_UnityEvent_immediate_cleans_up() => CheckDestructionFailure(true, false);

        [UnityTest]
        public IEnumerator Destruction_failure_UnityEvent_deferred_cleans_up() => CheckDestructionFailure(true, true);

        private IEnumerator CheckDestructionFailure(bool unityEvent, bool deferred)
        {
            var parent = NewObject("parent", new Vector3(3, 2, 1)).transform;
            parent.rotation = Quaternion.Euler(0, 30, 0);
            var target = NewObject("target", new Vector3(4, 5, 6)).transform;
            target.SetParent(parent, true);
            target.localRotation = Quaternion.Euler(10, 20, 30);
            target.localScale = new Vector3(2, 3, 4);
            var localPosition = target.localPosition;
            var localRotation = target.localRotation;
            var worldPosition = target.position;
            var worldRotation = target.rotation;
            var scale = target.localScale;
            var handle = _manager.CreateHandle(target);
            var pivot = handle.Pivot;
            var first = 0;
            var later = 0;
            var unityCalls = 0;
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            handle.OnHandleDestroyedEvent += _ =>
            {
                first++;
                if (!unityEvent) throw new InvalidOperationException("direct destruction CSharp");
            };
            handle.OnHandleDestroyedEvent += _ => later++;
            handle.OnHandleDestroyedUnityEvent.AddListener(_ =>
            {
                unityCalls++;
                throw new InvalidOperationException("direct destruction UnityEvent");
            });
            var laterUnityCalls = 0;
            handle.OnHandleDestroyedUnityEvent.AddListener(_ => laterUnityCalls++);
            BeginDrag(handle);
            LogAssert.Expect(LogType.Exception, new Regex(unityEvent
                ? @"^InvalidOperationException: direct destruction UnityEvent(?:\r?\n|$)"
                : @"^InvalidOperationException: direct destruction CSharp(?:\r?\n|$)"));
            if (deferred)
            {
                Object.Destroy(handle.gameObject);
                for (var frame = 0; frame < 5 && handle != null; frame++) yield return null;
            }
            else Object.DestroyImmediate(handle.gameObject);

            Assert.IsTrue(handle == null, "native destruction must actually complete");
            Assert.AreEqual(1, first);
            Assert.AreEqual(unityEvent ? 1 : 0, later);
            Assert.AreEqual(unityEvent ? 1 : 0, unityCalls);
            Assert.AreEqual(0, laterUnityCalls, "a failing listener must still stop delivery");
            Assert.AreEqual(1, endCount);
            Assert.IsFalse(_manager.IsInteracting);
            Assert.AreEqual(0, CountAll<Handle>());
            Assert.AreEqual(0, CountAll<Ghost>());
            Assert.IsTrue(pivot == null);
            Assert.IsTrue(target != null);
            Assert.AreSame(parent, target.parent);
            Assert.That(Vector3.Distance(localPosition, target.localPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(localRotation, target.localRotation), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(worldPosition, target.position), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(worldRotation, target.rotation), Is.LessThan(0.001f));
            Assert.AreEqual(scale, target.localScale);
            var replacement = _manager.CreateHandle(target);
            Assert.IsNotNull(replacement, "target index must be released before re-registration");
            CollectionAssert.AreEquivalent(new[] { target }, _manager.GetTargets(replacement));
        }

        [Test]
        public void Destruction_failure_after_reentrant_remove_is_idempotent()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);
            var pivot = handle.Pivot;
            var callbacks = 0;
            var ends = 0;
            handle.OnInteractionEndEvent += _ => ends++;
            handle.OnHandleDestroyedEvent += dying =>
            {
                callbacks++;
                _manager.RemoveHandle(dying);
                throw new InvalidOperationException("destruction after remove");
            };
            BeginDrag(handle);
            LogAssert.Expect(LogType.Exception, new Regex(@"^InvalidOperationException: destruction after remove(?:\r?\n|$)"));
            Object.DestroyImmediate(handle.gameObject);
            Assert.AreEqual(1, callbacks);
            Assert.AreEqual(1, ends);
            Assert.IsFalse(_manager.IsInteracting);
            Assert.IsTrue(handle == null && pivot == null && target != null);
            Assert.AreEqual(0, CountAll<Handle>());
            Assert.AreEqual(0, CountAll<Ghost>());
            Assert.IsNotNull(_manager.CreateHandle(target));
        }

        [Test]
        public void Destruction_failure_after_nested_manager_destruction_does_not_resurrect()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);
            var pivot = handle.Pivot;
            var callbacks = 0;
            handle.OnHandleDestroyedEvent += _ =>
            {
                callbacks++;
                Object.DestroyImmediate(_manager.gameObject);
                throw new InvalidOperationException("destruction after manager");
            };
            LogAssert.Expect(LogType.Exception, new Regex(@"^InvalidOperationException: destruction after manager(?:\r?\n|$)"));
            Object.DestroyImmediate(handle.gameObject);
            Assert.AreEqual(1, callbacks);
            Assert.IsTrue(_manager == null && handle == null && pivot == null && target != null);
            Assert.AreEqual(0, CountAll<TransformHandleManager>());
            Assert.AreEqual(0, CountAll<Handle>());
            Assert.AreEqual(0, CountAll<Ghost>());
            _manager = TransformHandleManager.Instance;
            Assert.IsNotNull(_manager.CreateHandle(target));
        }
    }
}
