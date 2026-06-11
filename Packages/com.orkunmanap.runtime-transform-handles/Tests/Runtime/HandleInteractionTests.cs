using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TransformHandles.Tests
{
    /// <summary>
    /// Covers the interaction surface consumers build on: the Handle event contract
    /// (C# events + Inspector UnityEvents, ordering, destroy notification), the
    /// HandleBase interaction lifecycle, group updates through the ghost pivot, and
    /// ghost cleanup. Drives the public entry points directly — the same calls
    /// TransformHandleManager makes from its input loop.
    /// </summary>
    public class HandleInteractionTests
    {
        private TransformHandleManager _manager;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            var cameraGo = NewObject("Main Camera", new Vector3(0f, 0f, -10f));
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";

            _manager = TransformHandleManager.Instance;
            _manager.MainCamera = camera;
            _manager.DestroyAllHandles();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                _manager.DestroyAllHandles();
                Object.DestroyImmediate(_manager.gameObject);
                _manager = null;
            }

            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        private GameObject NewObject(string name, Vector3 position = default)
        {
            var go = new GameObject(name) { transform = { position = position } };
            _spawned.Add(go);
            return go;
        }

        private static int CountGhosts()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<Ghost>(FindObjectsSortMode.None).Length;
#else
            return Object.FindObjectsOfType<Ghost>().Length;
#endif
        }

        [Test]
        public void Interaction_events_fire_in_order()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var calls = new List<string>();
            handle.OnInteractionStartEvent += _ => calls.Add("start");
            handle.OnInteractionEvent += _ => calls.Add("stay");
            handle.OnInteractionEndEvent += _ => calls.Add("end");

            handle.InteractionStart();
            handle.InteractionStay();
            handle.InteractionStay();
            handle.InteractionEnd();

            CollectionAssert.AreEqual(new[] { "start", "stay", "stay", "end" }, calls);
        }

        [Test]
        public void Inspector_unity_events_fire_alongside_csharp_events()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var unityEventCalls = 0;
            Handle eventArg = null;
            handle.OnInteractionStartUnityEvent.AddListener(h => { unityEventCalls++; eventArg = h; });

            handle.InteractionStart();

            Assert.AreEqual(1, unityEventCalls);
            Assert.AreSame(handle, eventArg, "the UnityEvent must pass the firing handle");
        }

        [Test]
        public void HandleDestroyed_event_fires_when_last_target_is_removed()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);
            var destroyed = false;
            handle.OnHandleDestroyedEvent += _ => destroyed = true;

            _manager.RemoveTarget(target, handle);

            Assert.IsTrue(destroyed);
            Assert.IsFalse(handle);
        }

        [Test]
        public void HandleBase_interaction_lifecycle_fires_events_and_resets_delta()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var axis = handle.GetComponentInChildren<PositionAxis>(true);
            Assert.IsNotNull(axis, "default Position handle must expose at least one PositionAxis");

            var started = false;
            var updated = false;
            var ended = false;
            axis.InteractionStart += () => started = true;
            axis.InteractionUpdate += _ => updated = true;
            axis.InteractionEnd += () => ended = true;

            axis.StartInteraction(Vector3.zero);
            axis.Delta = 0.5f;
            axis.Interact(Vector3.zero);
            axis.EndInteraction();

            Assert.IsTrue(started);
            Assert.IsTrue(updated);
            Assert.IsTrue(ended);
            Assert.AreEqual(0f, axis.Delta, "EndInteraction must reset the interaction delta");
        }

        [Test]
        public void UpdateGroupPosition_moves_every_target_by_the_change()
        {
            var a = NewObject("a").transform;
            var b = NewObject("b", new Vector3(2f, 0f, 0f)).transform;
            var handle = _manager.CreateHandleFromList(new List<Transform> { a, b });
            var ghost = handle.Pivot.GetComponent<Ghost>();
            Assert.IsNotNull(ghost, "handle.Pivot must be the group's ghost pivot");

            var change = new Vector3(1f, 2f, 3f);
            _manager.UpdateGroupPosition(ghost, change);

            Assert.AreEqual(change, a.position);
            Assert.AreEqual(new Vector3(2f, 0f, 0f) + change, b.position);
        }

        [Test]
        public void RemoveHandle_terminates_the_ghost()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            Assert.AreEqual(1, CountGhosts());

            _manager.RemoveHandle(handle);

            Assert.AreEqual(0, CountGhosts(), "removing the handle must destroy its ghost pivot");
        }

        [Test]
        public void DestroyAllHandles_leaves_no_ghosts_behind()
        {
            _manager.CreateHandle(NewObject("a").transform);
            _manager.CreateHandle(NewObject("b", new Vector3(4f, 0f, 0f)).transform);
            Assert.AreEqual(2, CountGhosts());

            _manager.DestroyAllHandles();

            Assert.AreEqual(0, CountGhosts());
        }
    }
}
