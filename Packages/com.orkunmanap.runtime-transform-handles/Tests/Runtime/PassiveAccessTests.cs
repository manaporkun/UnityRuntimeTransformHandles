using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TransformHandles.Tests
{
    /// <summary>
    /// Passive access and explicit factory lifecycle regressions. Managers include inactive objects;
    /// lazy Instance is never used as an ownership oracle.
    /// </summary>
    public class PassiveAccessTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            ApplicationQuitManager.SetApplicationQuitting(false);
            foreach (var manager in FindManagers())
            {
                Object.DestroyImmediate(manager.gameObject);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ApplicationQuitManager.SetApplicationQuitting(false);

            foreach (var manager in FindManagers())
            {
                Object.DestroyImmediate(manager.gameObject);
            }

            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _spawned.Clear();
            yield return null;
        }

        private static TransformHandleManager[] FindManagers()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<TransformHandleManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<TransformHandleManager>(true);
#endif
        }

        private GameObject NewObject(string name, Vector3 position = default)
        {
            var go = new GameObject(name) { transform = { position = position } };
            _spawned.Add(go);
            return go;
        }

        private Camera NewCamera()
        {
            var go = NewObject("Main Camera", new Vector3(0f, 0f, -10f));
            var camera = go.AddComponent<Camera>();
            camera.tag = "MainCamera";
            return camera;
        }

        private TransformHandleManager NewManager()
        {
            var manager = TransformHandleManager.Instance;
            manager.MainCamera = NewCamera();
            return manager;
        }

        private Handle NewStandaloneHandle()
        {
            var go = NewObject("standalone handle");
            go.SetActive(false);
            var handle = go.AddComponent<Handle>();
            go.SetActive(true);
            return handle;
        }

        [Test]
        public void D1_No_manager_targets_query_does_not_create_manager()
        {
            var handle = NewStandaloneHandle();

            Assert.IsEmpty(handle.Targets);
            Assert.AreEqual(0, FindManagers().Length);
        }

        [Test]
        public void D2_Detached_handle_uses_existing_manager_without_autoattach()
        {
            var manager = NewManager();
            var handle = manager.CreateHandle(NewObject("target").transform);
            _spawned.Add(handle.gameObject);
            manager.RemoveHandle(handle);

            handle.enabled = false;
            handle.enabled = true;

            Assert.IsEmpty(handle.Targets);
            Assert.IsNull(handle.Pivot);
            Assert.IsNotNull(handle.HandleCamera, "an existing manager may supply the camera");
            Assert.AreEqual(1, FindManagers().Length);
            Assert.IsEmpty(manager.GetTargets(handle));
        }

        [Test]
        public void D3_Dead_cached_handle_query_is_empty_and_does_not_create_manager()
        {
            var manager = NewManager();
            var handle = manager.CreateHandle(NewObject("target").transform);
            Object.DestroyImmediate(handle.gameObject);

            Assert.IsEmpty(handle.Targets);
            Assert.AreEqual(1, FindManagers().Length);
        }

        [Test]
        public void D4_Standalone_handle_enable_does_not_create_or_attach_manager()
        {
            var handle = NewStandaloneHandle();
            Assert.IsNull(handle.Pivot);
            Assert.IsNull(handle.HandleCamera);
            Assert.AreEqual(0, FindManagers().Length);

            var manager = TransformHandleManager.Instance;
            Assert.IsNotNull(manager);
            Assert.AreEqual(1, FindManagers().Length);
            Assert.IsNull(handle.Pivot, "explicit manager must not auto-attach the standalone handle");
            Assert.IsEmpty(manager.GetTargets(handle));
        }

        [Test]
        public void D5_Explicit_factory_is_idempotent_and_orders_wake_before_on_enable()
        {
            var first = TransformHandleManager.Instance;
            var second = TransformHandleManager.Instance;
            var handle = first.CreateHandle(NewObject("target").transform);

            Assert.AreSame(first, second);
            Assert.AreEqual(1, FindManagers().Length);
            Assert.AreEqual(1, first.GetTargets(handle).Count);
        }

        [Test]
        public void D6_Disabled_manager_is_used_without_replacement_and_restores_owned_handle()
        {
            var manager = NewManager();
            try
            {
                var handle = manager.CreateHandle(NewObject("target").transform);
                var ghost = handle.Pivot;
                manager.enabled = false;

                Assert.AreEqual(1, handle.Targets.Count);
                handle.enabled = false;
                handle.enabled = true;

                Assert.AreSame(ghost, handle.Pivot);
                Assert.AreEqual(1, FindManagers().Length);
            }
            finally
            {
                manager.enabled = true;
            }
        }

        [Test]
        public void D7_Teardown_callback_passive_targets_does_not_create_manager()
        {
            var manager = NewManager();
            var handle = manager.CreateHandle(NewObject("target").transform);
            var seen = -1;
            handle.OnHandleDestroyedEvent += _ => seen = handle.Targets.Count;

            Object.DestroyImmediate(manager.gameObject);

            Assert.AreEqual(0, seen);
            Assert.AreEqual(0, FindManagers().Length);
        }

        // D8 is documentation-only: explicit Instance may see a dying manager synchronously or create
        // a deferred/post-teardown manager. It is not asserted as prevented by P0-d1.

        [Test]
        public void D9_Explicit_instance_after_teardown_creates_a_fresh_manager()
        {
            var manager = NewManager();
            Object.DestroyImmediate(manager.gameObject);
            Assert.AreEqual(0, FindManagers().Length);

            var fresh = TransformHandleManager.Instance;
            Assert.IsNotNull(fresh);
            Assert.AreEqual(1, FindManagers().Length);
            Assert.IsTrue(fresh.CreateHandle(NewObject("target").transform));
        }

        [Test]
        public void D10_P0c_restore_with_existing_manager_remains_owned()
        {
            var manager = NewManager();
            var handle = manager.CreateHandle(NewObject("target").transform);
            var ghost = handle.Pivot;

            handle.enabled = false;
            handle.enabled = true;

            Assert.AreSame(ghost, handle.Pivot);
            Assert.AreEqual(1, manager.GetTargets(handle).Count);
            Assert.AreEqual(1, FindManagers().Length);
        }

        [Test]
        public void D11_Ghost_initialize_keeps_explicit_factory_semantics()
        {
            var manager = NewManager();
            var ghostGo = NewObject("ghost");
            var ghost = ghostGo.AddComponent<Ghost>();

            ghost.Initialize();

            Assert.AreEqual(1, FindManagers().Length);
            Assert.AreSame(manager, TransformHandleManager.Instance);
        }

        [Test]
        public void D12_Detached_handle_targets_is_empty_with_existing_manager()
        {
            var manager = NewManager();
            var handle = manager.CreateHandle(NewObject("target").transform);
            _spawned.Add(handle.gameObject);
            manager.RemoveHandle(handle);

            Assert.IsEmpty(handle.Targets);
            Assert.AreEqual(1, FindManagers().Length);
        }

        [Test]
        public void D13a_Quit_flag_no_manager_passive_access_and_instance_null()
        {
            ApplicationQuitManager.SetApplicationQuitting(true);
            try
            {
                var handle = NewStandaloneHandle();
                Assert.IsEmpty(handle.Targets);
                Assert.IsNull(handle.HandleCamera);
                Assert.IsNull(TransformHandleManager.Instance);
                Assert.AreEqual(0, FindManagers().Length);
            }
            finally
            {
                ApplicationQuitManager.SetApplicationQuitting(false);
            }

            Assert.IsNotNull(TransformHandleManager.Instance);
            Assert.AreEqual(1, FindManagers().Length);
        }

        [Test]
        public void D13b_Quit_flag_keeps_owned_handle_passive_and_restores_after_quit()
        {
            var manager = NewManager();
            var handle = manager.CreateHandle(NewObject("target").transform);
            var ghost = handle.Pivot;

            ApplicationQuitManager.SetApplicationQuitting(true);
            try
            {
                Assert.IsEmpty(handle.Targets);
                handle.enabled = false;
                handle.enabled = true;

                Assert.IsNull(handle.HandleCamera, "quit guard must not restore the live manager camera");
                Assert.IsNull(handle.Pivot, "quit guard must not restore owned targets");
                Assert.IsNull(TransformHandleManager.Instance);
                Assert.AreEqual(1, FindManagers().Length, "the owning manager may still exist while quitting");
            }
            finally
            {
                ApplicationQuitManager.SetApplicationQuitting(false);
            }

            handle.enabled = false;
            handle.enabled = true;

            Assert.AreSame(ghost, handle.Pivot);
            Assert.AreEqual(1, manager.GetTargets(handle).Count);
            Assert.AreEqual(1, FindManagers().Length);
        }

        [Test]
        public void D14_Detached_handle_clears_stale_camera_after_manager_teardown()
        {
            var manager = NewManager();
            var handle = manager.CreateHandle(NewObject("target").transform);
            _spawned.Add(handle.gameObject);
            manager.RemoveHandle(handle);
            Assert.IsNotNull(handle.HandleCamera, "detach keeps the live manager camera until manager teardown");

            Object.DestroyImmediate(manager.gameObject);
            handle.enabled = false;
            handle.enabled = true;

            Assert.IsNull(handle.HandleCamera, "manager teardown must not leave a stale camera");
            Assert.IsEmpty(handle.Targets);
            Assert.AreEqual(0, FindManagers().Length);

            var fresh = TransformHandleManager.Instance;
            Assert.IsNotNull(fresh);
            Assert.IsNull(handle.Pivot, "fresh manager must not auto-attach an orphan handle");
            Assert.IsEmpty(fresh.GetTargets(handle));
            Assert.AreEqual(1, FindManagers().Length);
        }

        private static T[] FindSceneObjects<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>(true);
#endif
        }

        [Test]
        public void F1_Explicit_factory_during_bulk_cleanup_keeps_guard_and_recovers()
        {
            var manager = NewManager();
            var target = NewObject("old target").transform;
            var nextTarget = NewObject("next target").transform;
            var handle = manager.CreateHandle(target);
            var ghost = handle.Pivot;
            var callbacks = 0;
            TransformHandleManager observed = null;
            Handle created = null;
            handle.OnHandleDestroyedEvent += _ =>
            {
                callbacks++;
                observed = TransformHandleManager.Instance;
                created = observed.CreateHandle(nextTarget);
            };

            LogAssert.Expect(LogType.Warning,
                "TransformHandles: cannot create handles while handles are being destroyed.");
            manager.DestroyAllHandles();

            Assert.AreEqual(1, callbacks);
            Assert.AreSame(manager, observed);
            Assert.IsNull(created);
            Assert.IsTrue(handle == null && ghost == null);
            Assert.IsTrue(target != null && nextTarget != null);
            Assert.AreEqual(1, FindManagers().Length);
            Assert.AreEqual(0, FindSceneObjects<Handle>().Length);
            Assert.AreEqual(0, FindSceneObjects<Ghost>().Length);
            var later = manager.CreateHandle(nextTarget);
            Assert.IsNotNull(later);
            CollectionAssert.AreEquivalent(new[] { nextTarget }, manager.GetTargets(later));
        }

        [Test]
        public void F2_Explicit_factory_in_final_callback_respects_observed_manager_lifetime()
        {
            var manager = NewManager();
            var target = NewObject("old target").transform;
            var nextTarget = NewObject("replacement target").transform;
            var handle = manager.CreateHandle(target);
            var oldGhost = handle.Pivot;
            var callbacks = 0;
            var liveInCallback = false;
            var countInCallback = -1;
            TransformHandleManager observed = null;
            Handle created = null;
            Transform newGhost = null;
            handle.OnHandleDestroyedEvent += _ =>
            {
                callbacks++;
                observed = TransformHandleManager.Instance;
                liveInCallback = observed != null;
                countInCallback = FindManagers().Length;
                if (ReferenceEquals(manager, observed))
                    LogAssert.Expect(LogType.Warning,
                        "TransformHandles: cannot create handles while handles are being destroyed.");
                if (liveInCallback)
                {
                    created = observed.CreateHandle(nextTarget);
                    if (created != null) newGhost = created.Pivot;
                }
            };

            Object.DestroyImmediate(manager.gameObject);

            Assert.AreEqual(1, callbacks);
            Assert.IsTrue(liveInCallback, "explicit access outside quit must return a live manager");
            Assert.IsTrue(manager == null && handle == null && oldGhost == null);
            Assert.IsTrue(target != null && nextTarget != null);
            if (ReferenceEquals(manager, observed))
            {
                Assert.AreEqual(1, countInCallback);
                Assert.IsNull(created);
                Assert.AreEqual(0, FindManagers().Length);
                Assert.AreEqual(0, FindSceneObjects<Handle>().Length);
                Assert.AreEqual(0, FindSceneObjects<Ghost>().Length);
            }
            else
            {
                Assert.That(countInCallback, Is.InRange(1, 2), "the old manager may still be visible");
                Assert.IsTrue(observed != null && created != null && newGhost != null);
                Assert.AreSame(observed, FindManagers()[0]);
                Assert.AreEqual(1, FindManagers().Length);
                Assert.AreSame(newGhost, created.Pivot);
                CollectionAssert.AreEquivalent(new[] { nextTarget }, observed.GetTargets(created));
                Assert.AreEqual(1, FindSceneObjects<Handle>().Length);
                Assert.AreEqual(1, FindSceneObjects<Ghost>().Length);
                Assert.IsNotNull(observed.CreateHandle(NewObject("another target").transform));
            }
        }

        [Test]
        public void F3_Replacement_survives_old_bulk_cleanup_after_nested_manager_destruction()
        {
            var manager = NewManager();
            var target = NewObject("old target").transform;
            var nextTarget = NewObject("replacement target").transform;
            var handle = manager.CreateHandle(target);
            var oldGhost = handle.Pivot;
            var callbacks = 0;
            TransformHandleManager fresh = null;
            Handle created = null;
            Transform newGhost = null;
            handle.OnHandleDestroyedEvent += _ =>
            {
                callbacks++;
                Object.DestroyImmediate(manager.gameObject);
                fresh = TransformHandleManager.Instance;
                created = fresh.CreateHandle(nextTarget);
                newGhost = created.Pivot;
            };

            manager.DestroyAllHandles();

            Assert.AreEqual(1, callbacks);
            Assert.IsTrue(manager == null && handle == null && oldGhost == null);
            Assert.AreNotSame(manager, fresh);
            Assert.IsTrue(fresh != null && created != null && newGhost != null);
            Assert.IsTrue(target != null && nextTarget != null);
            Assert.AreEqual(1, FindManagers().Length);
            Assert.AreSame(fresh, FindManagers()[0]);
            Assert.AreEqual(1, FindSceneObjects<Handle>().Length);
            Assert.AreSame(created, FindSceneObjects<Handle>()[0]);
            Assert.AreEqual(1, FindSceneObjects<Ghost>().Length);
            Assert.AreSame(newGhost, FindSceneObjects<Ghost>()[0].transform);
            Assert.AreSame(newGhost, created.Pivot);
            CollectionAssert.AreEquivalent(new[] { nextTarget }, fresh.GetTargets(created));
            Assert.IsNotNull(fresh.CreateHandle(NewObject("another target").transform));
        }

        [UnityTest]
        public IEnumerator F4_Explicit_factory_after_deferred_destruction_creates_usable_manager()
        {
            var manager = NewManager();
            var target = NewObject("target").transform;
            var handle = manager.CreateHandle(target);
            var ghost = handle.Pivot;
            Object.Destroy(manager.gameObject);
            for (var frame = 0; frame < 5 && manager != null; frame++) yield return null;

            Assert.IsTrue(manager == null && handle == null && ghost == null);
            Assert.IsTrue(target != null);
            Assert.AreEqual(0, FindManagers().Length, "cleanup must not implicitly create a manager");
            Assert.AreEqual(0, FindSceneObjects<Handle>().Length);
            Assert.AreEqual(0, FindSceneObjects<Ghost>().Length);
            var fresh = TransformHandleManager.Instance;
            Assert.AreNotSame(manager, fresh);
            Assert.AreEqual(1, FindManagers().Length);
            var created = fresh.CreateHandle(target);
            Assert.IsNotNull(created);
            CollectionAssert.AreEquivalent(new[] { target }, fresh.GetTargets(created));
        }

        [Test]
        public void F5_Explicit_factory_in_teardown_callback_returns_null_while_quitting()
        {
            var manager = NewManager();
            var target = NewObject("target").transform;
            var handle = manager.CreateHandle(target);
            var ghost = handle.Pivot;
            var callbacks = 0;
            TransformHandleManager observed = manager;
            handle.OnHandleDestroyedEvent += _ =>
            {
                callbacks++;
                observed = TransformHandleManager.Instance;
            };
            try
            {
                ApplicationQuitManager.SetApplicationQuitting(true);
                Object.DestroyImmediate(manager.gameObject);
                Assert.AreEqual(1, callbacks);
                Assert.IsTrue(ReferenceEquals(null, observed));
                Assert.AreEqual(0, FindManagers().Length);
                Assert.IsTrue(manager == null && handle == null && ghost == null);
                Assert.IsTrue(target != null);
            }
            finally
            {
                ApplicationQuitManager.SetApplicationQuitting(false);
            }
        }

        [Test]
        public void F6_Ghost_initialize_after_teardown_creates_manager_without_ownership()
        {
            var manager = NewManager();
            Object.DestroyImmediate(manager.gameObject);
            Assert.AreEqual(0, FindManagers().Length);
            var ghost = NewObject("standalone ghost").AddComponent<Ghost>();

            ghost.Initialize();

            Assert.AreEqual(1, FindManagers().Length);
            var fresh = FindManagers()[0];
            Assert.AreNotSame(manager, fresh);
            ghost.Initialize();
            Assert.AreEqual(1, FindManagers().Length);
            Assert.AreSame(fresh, FindManagers()[0]);
            Assert.AreEqual(0, FindSceneObjects<Handle>().Length);
            Object.DestroyImmediate(fresh.gameObject);
            Assert.AreEqual(0, FindManagers().Length);
            Assert.IsTrue(ghost != null, "Initialize does not transfer standalone ghost ownership");
            Assert.AreEqual(1, FindSceneObjects<Ghost>().Length);
        }
    }
}
