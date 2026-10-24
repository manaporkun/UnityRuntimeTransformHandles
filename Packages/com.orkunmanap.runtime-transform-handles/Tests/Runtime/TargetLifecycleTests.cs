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
    /// P0-b target-loss and ownership consistency regressions. Frame-dependent cases use the real
    /// manager Update loop and yield; no reflection or package seams beyond the existing BeginInteraction.
    /// </summary>
    public class TargetLifecycleTests
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
            var manager = _manager != null ? _manager : FindManager();
            if (manager != null)
            {
                manager.DestroyAllHandles();
                Object.DestroyImmediate(manager.gameObject);
            }

            _manager = null;

            if (_originalScene.IsValid() && _originalScene.isLoaded)
                SceneManager.SetActiveScene(_originalScene);

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

        private HandleBase BeginDrag(Handle handle)
        {
            var axis = handle.GetComponentInChildren<PositionAxis>(true);
            Assert.IsNotNull(axis, "the default handle must expose a PositionAxis");
            Assert.IsTrue(_manager.BeginInteraction(axis, handle.transform.position));
            Assert.IsTrue(_manager.IsInteracting);
            return axis;
        }

        [UnityTest]
        public IEnumerator T01_Idle_partial_loss_prunes_and_keeps_survivor()
        {
            var a = NewObject("a");
            var b = NewObject("b", Vector3.right);
            var handle = _manager.CreateHandleFromList(new List<Transform> { a.transform, b.transform });
            var position = b.transform.position;

            Object.DestroyImmediate(a);
            var targets = _manager.GetTargets(handle);

            Assert.AreEqual(1, targets.Count);
            CollectionAssert.Contains(targets, b.transform);
            Assert.IsTrue(handle);
            Assert.AreEqual(position, b.transform.position);

            yield return null;

            Assert.IsTrue(handle);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
            Assert.AreEqual(position, b.transform.position);
        }

        [UnityTest]
        public IEnumerator T02_Idle_last_loss_destroys_on_next_update()
        {
            var target = NewObject("target");
            var handle = _manager.CreateHandle(target.transform);
            var destroyed = 0;
            handle.OnHandleDestroyedEvent += _ => destroyed++;

            Object.DestroyImmediate(target);

            Assert.IsEmpty(_manager.GetTargets(handle), "query prunes but does not destroy");
            Assert.IsTrue(handle, "handle remains alive until Update");
            Assert.AreEqual(0, destroyed);

            yield return null;

            Assert.IsFalse(handle);
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
            Assert.AreEqual(1, destroyed);
            Assert.IsNotNull(_manager.CreateHandle(NewObject("fresh").transform));
        }

        [UnityTest]
        public IEnumerator T03_Drag_partial_loss_cancels_once_on_next_update()
        {
            var a = NewObject("a");
            var b = NewObject("b", Vector3.right);
            var handle = _manager.CreateHandleFromList(new List<Transform> { a.transform, b.transform });
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            Object.DestroyImmediate(a);
            _manager.GetTargets(handle);

            Assert.AreEqual(0, endCount, "query must not cancel");
            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.IsFalse(_manager.IsInteracting);
            Assert.IsTrue(handle);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
            CollectionAssert.Contains(_manager.GetTargets(handle), b.transform);
        }

        [UnityTest]
        public IEnumerator T04_Drag_last_loss_ends_then_destroys_once()
        {
            var target = NewObject("target");
            var handle = _manager.CreateHandle(target.transform);
            var endCount = 0;
            var destroyed = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            handle.OnHandleDestroyedEvent += _ => destroyed++;
            BeginDrag(handle);

            Object.DestroyImmediate(target);
            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.AreEqual(1, destroyed);
            Assert.IsFalse(handle);
            Assert.AreEqual(0, CountGhosts());
        }

        [UnityTest]
        public IEnumerator T05_All_targets_lost_in_one_frame_do_not_duplicate_callbacks()
        {
            var a = NewObject("a");
            var b = NewObject("b");
            var handle = _manager.CreateHandleFromList(new List<Transform> { a.transform, b.transform });
            var destroyed = 0;
            handle.OnHandleDestroyedEvent += _ => destroyed++;

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            yield return null;
            yield return null;

            Assert.AreEqual(1, destroyed);
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
        }

        [UnityTest]
        public IEnumerator T06_Deferred_target_destroy_is_pruned_without_missing_reference()
        {
            var target = NewObject("target");
            var handle = _manager.CreateHandle(target.transform);

            Object.Destroy(target);
            yield return null;
            yield return null;

            Assert.DoesNotThrow(() => _manager.GetTargets(handle));
            Assert.IsFalse(handle);
            Assert.AreEqual(0, CountGhosts());
        }

        [Test]
        public void T07_Parent_replaces_one_child_and_child_is_re_registrable()
        {
            var parent = NewObject("parent").transform;
            var child = NewObject("child").transform;
            child.SetParent(parent);
            var handle = _manager.CreateHandle(child);
            var parentPosition = parent.position;

            Assert.IsTrue(_manager.AddTarget(parent, handle));
            CollectionAssert.AreEquivalent(new[] { parent }, _manager.GetTargets(handle));
            Assert.AreEqual(parentPosition, parent.position);
            Assert.IsNotNull(_manager.CreateHandle(child), "replaced child must be free");
        }

        [Test]
        public void T08_Parent_replaces_all_children_without_double_delta()
        {
            var parent = NewObject("parent").transform;
            var child1 = NewObject("child1").transform;
            var child2 = NewObject("child2").transform;
            child1.SetParent(parent);
            child2.SetParent(parent);
            var handle = _manager.CreateHandleFromList(new List<Transform> { child1, child2 });
            var before = child1.position;

            Assert.IsTrue(_manager.AddTarget(parent, handle));
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
            CollectionAssert.Contains(_manager.GetTargets(handle), parent);

            _manager.UpdateGroupPosition(handle.Pivot.GetComponent<Ghost>(), Vector3.right);
            Assert.That(Vector3.Distance(child1.position, before + Vector3.right), Is.LessThan(1e-4f));
        }

        [Test]
        public void T09_List_normalizes_parent_child_and_rejects_all_invalid()
        {
            var parent = NewObject("parent").transform;
            var child = NewObject("child").transform;
            child.SetParent(parent);

            var forward = _manager.CreateHandleFromList(new List<Transform> { parent, child });
            Assert.IsNotNull(forward);
            CollectionAssert.AreEquivalent(new[] { parent }, _manager.GetTargets(forward));
            _manager.DestroyAllHandles();

            var reverse = _manager.CreateHandleFromList(new List<Transform> { child, parent });
            Assert.IsNotNull(reverse);
            CollectionAssert.AreEquivalent(new[] { parent }, _manager.GetTargets(reverse));
            _manager.DestroyAllHandles();

            Assert.IsNull(_manager.CreateHandleFromList(new List<Transform> { null }));
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
        }

        [Test]
        public void T10_Wrong_and_unmanaged_handles_throw_before_mutation()
        {
            var a = NewObject("a").transform;
            var b = NewObject("b").transform;
            var h1 = _manager.CreateHandle(a);
            var h2 = _manager.CreateHandle(b);

            Assert.Throws<InvalidOperationException>(() => _manager.RemoveTarget(a, h2));
            CollectionAssert.Contains(_manager.GetTargets(h1), a);
            Assert.IsNull(_manager.CreateHandle(a), "ownership must remain unchanged");

            var orphanGo = NewObject("orphan");
            orphanGo.SetActive(false);
            var orphan = orphanGo.AddComponent<Handle>();
            Assert.Throws<InvalidOperationException>(() => _manager.RemoveTarget(b, orphan));
            CollectionAssert.Contains(_manager.GetTargets(h2), b);
        }

        [Test]
        public void T11_Direct_RemoveTarget_after_destroy_cleans_partial_and_last()
        {
            var a = NewObject("a").transform;
            var b = NewObject("b").transform;
            var partial = _manager.CreateHandleFromList(new List<Transform> { a, b });
            Object.DestroyImmediate(a.gameObject);

            Assert.DoesNotThrow(() => _manager.RemoveTarget(a, partial));
            CollectionAssert.AreEquivalent(new[] { b }, _manager.GetTargets(partial));

            // Clean the surviving partial handle before the global-count last-target scenario.
            _manager.DestroyAllHandles();

            var lastTarget = NewObject("last").transform;
            var last = _manager.CreateHandle(lastTarget);
            Object.DestroyImmediate(lastTarget.gameObject);
            Assert.DoesNotThrow(() => _manager.RemoveTarget(lastTarget, last));
            Assert.IsFalse(last);
            Assert.AreEqual(0, CountGhosts());

            var queryTarget = NewObject("queryTarget");
            var queryTransform = queryTarget.transform;
            var queryHandle = _manager.CreateHandle(queryTransform);
            Object.DestroyImmediate(queryTarget);
            Assert.IsEmpty(_manager.GetTargets(queryHandle), "query-first prunes and queues pending");
            Assert.IsTrue(queryHandle);
            Assert.DoesNotThrow(() => _manager.RemoveTarget(queryTransform, queryHandle));
            Assert.IsFalse(queryHandle, "query-pruned last target must finalize on RemoveTarget");
            Assert.AreEqual(0, CountGhosts());
        }

        [UnityTest]
        public IEnumerator T12_End_callback_GetTargets_sees_pruned_membership()
        {
            var a = NewObject("a");
            var b = NewObject("b");
            var handle = _manager.CreateHandleFromList(new List<Transform> { a.transform, b.transform });
            var endCount = 0;
            var callbackCount = -1;
            Handle callbackPending = null;
            var callbackPendingDestroyed = 0;
            handle.OnInteractionEndEvent += h =>
            {
                endCount++;
                callbackCount = h.Targets.Count;

                // This handle is created after the reconcile snapshot. Its query-pruned pending
                // loss must survive the current pass and finalize on the next Update.
                var pendingTarget = NewObject("callbackPending");
                callbackPending = _manager.CreateHandle(pendingTarget.transform);
                callbackPending.OnHandleDestroyedEvent += _ => callbackPendingDestroyed++;
                Object.DestroyImmediate(pendingTarget);
                _manager.GetTargets(callbackPending);
            };
            BeginDrag(handle);
            Object.DestroyImmediate(a);

            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.AreEqual(1, callbackCount);
            Assert.IsTrue(handle);
            Assert.IsTrue(callbackPending, "new pending loss waits for a later Update");
            Assert.AreEqual(0, callbackPendingDestroyed);

            yield return null;
            Assert.IsFalse(callbackPending);
            Assert.AreEqual(1, callbackPendingDestroyed);
        }

        [UnityTest]
        public IEnumerator T13_End_callback_RemoveHandle_detaches_without_duplicate_destroy()
        {
            var a = NewObject("a");
            var b = NewObject("b");
            var handle = _manager.CreateHandleFromList(new List<Transform> { a.transform, b.transform });
            _spawned.Add(handle.gameObject);
            var endCount = 0;
            var destroyed = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                _manager.RemoveHandle(handle);
            };
            handle.OnHandleDestroyedEvent += _ => destroyed++;
            BeginDrag(handle);
            Object.DestroyImmediate(a);

            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.AreEqual(0, destroyed);
            Assert.IsTrue(handle, "callback-detached handle stays alive");
            Assert.AreEqual(0, CountGhosts());
        }

        [UnityTest]
        public IEnumerator T14_End_callback_DestroyAllHandles_aborts_safely()
        {
            var a = NewObject("a");
            var b = NewObject("b");
            var handle = _manager.CreateHandleFromList(new List<Transform> { a.transform, b.transform });
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                _manager.DestroyAllHandles();
            };
            BeginDrag(handle);
            Object.DestroyImmediate(a);

            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
        }

        [UnityTest]
        public IEnumerator T15_End_callback_AddTarget_prevents_wrong_empty_cleanup()
        {
            var a = NewObject("a");
            var c = NewObject("c");
            var handle = _manager.CreateHandle(a.transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                _manager.AddTarget(c.transform, handle);
            };
            BeginDrag(handle);
            Object.DestroyImmediate(a);

            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.IsTrue(handle);
            CollectionAssert.AreEquivalent(new[] { c.transform }, _manager.GetTargets(handle));
        }

        [UnityTest]
        public IEnumerator T16_Owner_unrelated_drag_is_not_cancelled()
        {
            var a = NewObject("a");
            var b = NewObject("b");
            var handleA = _manager.CreateHandle(a.transform);
            var handleB = _manager.CreateHandle(b.transform);
            var endB = 0;
            handleB.OnInteractionEndEvent += _ => endB++;
            BeginDrag(handleB);

            Object.DestroyImmediate(a);
            yield return null;

            Assert.AreEqual(0, endB);
            Assert.IsTrue(_manager.IsInteracting);
            Assert.IsTrue(handleB);
            Assert.IsFalse(handleA);
        }

        [UnityTest]
        public IEnumerator T17_BeginInteraction_rejects_pending_loss_after_query_prune()
        {
            var a = NewObject("a");
            var b = NewObject("b");
            var handle = _manager.CreateHandleFromList(new List<Transform> { a.transform, b.transform });
            var axis = handle.GetComponentInChildren<PositionAxis>(true);
            Object.DestroyImmediate(a);
            _manager.GetTargets(handle);

            Assert.IsFalse(_manager.BeginInteraction(axis, handle.transform.position));
            Assert.IsFalse(_manager.IsInteracting);

            yield return null;
            Assert.IsTrue(handle);
            CollectionAssert.AreEquivalent(new[] { b.transform }, _manager.GetTargets(handle));

            var c = NewObject("c");
            var d = NewObject("d");
            var startHandle = _manager.CreateHandleFromList(new List<Transform> { c.transform, d.transform });
            startHandle.OnInteractionStartEvent += _ => _manager.RemoveTarget(c.transform, startHandle);
            var startAxis = startHandle.GetComponentInChildren<PositionAxis>(true);

            Assert.IsFalse(_manager.BeginInteraction(startAxis, startHandle.transform.position),
                "a start callback that cancels the drag must make BeginInteraction return false");
            Assert.IsFalse(_manager.IsInteracting);
            Assert.IsTrue(startHandle);
            CollectionAssert.AreEquivalent(new[] { d.transform }, _manager.GetTargets(startHandle));
        }

        [Test]
        public void T18_Group_updates_skip_destroyed_members_and_average_live_count()
        {
            var handleGo = NewObject("handle");
            handleGo.SetActive(false);
            var handle = handleGo.AddComponent<Handle>();
            var ghostGo = NewObject("ghost");
            var ghost = ghostGo.AddComponent<Ghost>();
            var group = new TransformGroup(ghost, handle);
            var live = NewObject("live", Vector3.zero).transform;
            var dead = NewObject("dead", Vector3.right).transform;
            group.AddTransform(live);
            group.AddTransform(dead);
            Object.DestroyImmediate(dead.gameObject);

            Assert.DoesNotThrow(() => group.UpdatePositions(Vector3.right));
            Assert.That(Vector3.Distance(live.position, Vector3.right), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(group.GetAveragePosRotScale().Position, live.position), Is.LessThan(1e-4f));
        }

        [UnityTest]
        public IEnumerator T19_Target_only_additive_unload_prunes_while_handle_survives()
        {
            var scene = SceneManager.CreateScene("P0B_TargetUnload");
            _createdScenes.Add(scene);
            SceneManager.SetActiveScene(scene);
            var targetInScene = NewObject("targetInScene").transform;
            SceneManager.SetActiveScene(_originalScene);
            var survivor = NewObject("survivor", Vector3.right).transform;
            var handle = _manager.CreateHandleFromList(new List<Transform> { targetInScene, survivor });
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone) yield return null;
            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.IsTrue(handle);
            CollectionAssert.AreEquivalent(new[] { survivor }, _manager.GetTargets(handle));
        }

        [UnityTest]
        public IEnumerator T20_Throwing_end_and_destroyed_callbacks_are_contained()
        {
            var target = NewObject("target");
            var handle = _manager.CreateHandle(target.transform);
            var endCount = 0;
            var destroyed = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                throw new InvalidOperationException("end callback");
            };
            handle.OnHandleDestroyedEvent += _ =>
            {
                destroyed++;
                throw new InvalidOperationException("destroyed callback");
            };
            BeginDrag(handle);

            LogAssert.Expect(LogType.Exception, new Regex(@"^InvalidOperationException: end callback(?:\r?\n|$)"));
            LogAssert.Expect(LogType.Exception, new Regex(@"^InvalidOperationException: destroyed callback(?:\r?\n|$)"));

            Object.DestroyImmediate(target);
            yield return null;
            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.AreEqual(1, destroyed);
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
        }

        [Test]
        public void T21_Explicit_null_and_fake_null_contracts_are_distinct()
        {
            var target = NewObject("target");
            var survivor = NewObject("survivor");
            var handle = _manager.CreateHandleFromList(new List<Transform> { target.transform, survivor.transform });

            Assert.Throws<ArgumentNullException>(() => _manager.CreateHandle(null));
            Assert.Throws<ArgumentNullException>(() => _manager.GetTargets(null));
            Assert.Throws<ArgumentNullException>(() => _manager.RemoveHandle(null));
            Assert.DoesNotThrow(() => _manager.RemoveTarget(null, handle));
            Assert.IsFalse(_manager.AddTarget(null, handle));

            var group = new TransformGroup(null, null);
            Assert.Throws<ArgumentNullException>(() => group.RemoveTransform(null));

            var destroyedTarget = target.transform;
            Object.DestroyImmediate(target);
            Assert.IsFalse(_manager.AddTarget(destroyedTarget, handle));
            Assert.DoesNotThrow(() => _manager.RemoveTarget(destroyedTarget, handle));
            Assert.IsNull(_manager.CreateHandleFromList(new List<Transform> { null }));

            Assert.Throws<ArgumentNullException>(() => _manager.CreateHandle(destroyedTarget));
        }

        [Test]
        public void T22_Query_first_last_RemoveTarget_end_callback_AddTarget_rescues_handle()
        {
            var a = NewObject("a");
            var c = NewObject("c");
            var d = NewObject("d");
            var target = a.transform;
            var handle = _manager.CreateHandle(target);
            var endCount = 0;
            var destroyed = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                _manager.AddTarget(c.transform, handle);
            };
            handle.OnHandleDestroyedEvent += _ => destroyed++;
            BeginDrag(handle);

            Object.DestroyImmediate(a);
            _manager.GetTargets(handle);
            _manager.RemoveTarget(target, handle);

            Assert.IsTrue(handle);
            Assert.AreEqual(1, endCount);
            Assert.AreEqual(0, destroyed);
            Assert.IsNotNull(handle.Pivot);
            CollectionAssert.AreEquivalent(new[] { c.transform }, _manager.GetTargets(handle));
            Assert.IsTrue(_manager.AddTarget(d.transform, handle));
            CollectionAssert.AreEquivalent(new[] { c.transform, d.transform }, _manager.GetTargets(handle));
        }

        [Test]
        public void T23_Query_first_last_RemoveTarget_end_callback_RemoveHandle_detaches_safely()
        {
            var a = NewObject("a");
            var target = a.transform;
            var handle = _manager.CreateHandle(target);
            _spawned.Add(handle.gameObject);
            var endCount = 0;
            var destroyed = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                _manager.RemoveHandle(handle);
            };
            handle.OnHandleDestroyedEvent += _ => destroyed++;
            BeginDrag(handle);

            Object.DestroyImmediate(a);
            _manager.GetTargets(handle);
            Assert.DoesNotThrow(() => _manager.RemoveTarget(target, handle));

            Assert.AreEqual(1, endCount);
            Assert.AreEqual(0, destroyed);
            Assert.IsTrue(handle, "callback-detached handle stays alive");
            Assert.AreEqual(0, CountGhosts());
            Assert.IsEmpty(_manager.GetTargets(handle));
        }

        [Test]
        public void T24_Query_first_last_RemoveTarget_end_callback_DestroyAllHandles_stops_safely()
        {
            var a = NewObject("a");
            var target = a.transform;
            var handle = _manager.CreateHandle(target);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                _manager.DestroyAllHandles();
            };
            BeginDrag(handle);

            Object.DestroyImmediate(a);
            _manager.GetTargets(handle);
            Assert.DoesNotThrow(() => _manager.RemoveTarget(target, handle));

            Assert.AreEqual(1, endCount);
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
        }
    }
}
