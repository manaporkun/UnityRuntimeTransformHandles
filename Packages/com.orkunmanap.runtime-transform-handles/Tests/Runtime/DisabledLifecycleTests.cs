using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TransformHandles.Tests
{
    /// <summary>
    /// P0-c disabled/re-enabled handle/manager and camera-lifetime regressions (C1-C21).
    /// Uses the real manager Update loop and public ownership oracles; no reflection.
    /// </summary>
    public class DisabledLifecycleTests
    {
        private TransformHandleManager _manager;
        private Camera _camera;
        private GameObject _cameraGo;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _cameraGo = NewObject("Main Camera", new Vector3(0f, 0f, -10f));
            _camera = _cameraGo.AddComponent<Camera>();
            _camera.tag = "MainCamera";

            _manager = TransformHandleManager.Instance;
            _manager.MainCamera = _camera;
            _manager.DestroyAllHandles();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_manager != null)
            {
                _manager.enabled = true;
                if (!_manager.gameObject.activeSelf) _manager.gameObject.SetActive(true);
                _manager.DestroyAllHandles();
                Object.DestroyImmediate(_manager.gameObject);
                _manager = null;
            }

            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _spawned.Clear();
            yield return null;
        }

        private GameObject NewObject(string name, Vector3 position = default)
        {
            var go = new GameObject(name) { transform = { position = position } };
            _spawned.Add(go);
            return go;
        }

        private Camera NewCamera(string name, bool mainTag = false)
        {
            var go = NewObject(name, new Vector3(0f, 0f, -10f));
            var camera = go.AddComponent<Camera>();
            if (mainTag) camera.tag = "MainCamera";
            return camera;
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
            Assert.IsNotNull(axis);
            Assert.IsTrue(_manager.BeginInteraction(axis, handle.transform.position));
            return axis;
        }

        private static void AssertRestored(Handle handle)
        {
            Assert.IsTrue(handle);
            Assert.IsNotNull(handle.Pivot, "restore must set the ghost pivot");
            Assert.IsTrue(handle.GetComponentInChildren<PositionAxis>(true).gameObject.activeSelf,
                "restore must reactivate axes");
        }

        [TestCase(0, 0)]
        [TestCase(0, 1)]
        [TestCase(0, 2)]
        [TestCase(1, 0)]
        [TestCase(1, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 0)]
        [TestCase(2, 1)]
        [TestCase(2, 2)]
        public void UniformSetterDoesNotReviveHandleDetachedByEndCallback(int setter, int action)
        {
            var handle = _manager.CreateHandleFromList(new List<Transform>
                { NewObject("a", Vector3.left).transform, NewObject("b", Vector3.right).transform });
            _spawned.Add(handle.gameObject); // RemoveHandle deliberately leaves the detached object alive.
            handle.UniformGroupScale = true;
            handle.Type = HandleType.Scale;
            _manager.ChangeHandleSpace(handle, Space.Self);
            var center = handle.GetComponentInChildren<ScaleGlobal>();
            Assert.IsTrue(_manager.BeginInteraction(center, handle.Pivot.position));
            var ends = 0;
            handle.OnInteractionEndEvent += h =>
            {
                ends++;
                if (action == 0) _manager.RemoveHandle(h);
                if (action == 1) h.Disable();
                if (action == 2) Object.DestroyImmediate(h.gameObject);
            };
            if (setter == 0) handle.Axes = HandleAxes.X;
            if (setter == 1) handle.Type = HandleType.Rotation;
            if (setter == 2) handle.UniformGroupScale = false;
            Assert.AreEqual(1, ends);
            Assert.IsFalse(_manager.IsInteracting);
            if (action == 2) Assert.IsTrue(handle == null);
            else
            {
                Assert.IsNull(handle.Pivot);
                Assert.IsNull(handle.GetComponentInChildren<HandleBase>());
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void UniformGroupChangesCancelDragAndRefreshControls(int change)
        {
            var a = NewObject("a", Vector3.left).transform;
            var b = NewObject("b", Vector3.right).transform;
            var handle = _manager.CreateHandleFromList(new List<Transform> { a, b });
            _spawned.Add(handle.gameObject); // RemoveHandle deliberately leaves the detached object alive.
            handle.UniformGroupScale = true;
            handle.Type = HandleType.Scale;
            _manager.ChangeHandleSpace(handle, Space.Self);
            var center = handle.GetComponentInChildren<ScaleGlobal>();
            Assert.IsNotNull(center);
            var ends = 0;
            handle.OnInteractionEndEvent += _ => ends++;
            Assert.IsTrue(_manager.BeginInteraction(center, handle.Pivot.position));
            var ghost = handle.Pivot.GetComponent<Ghost>();
            handle.Pivot.localScale = Vector3.one * 2;
            _manager.UpdateGroupScaleUpdate(ghost, Vector3.one);
            Assert.AreEqual(Vector3.one * 2, a.localScale);
            Assert.AreEqual(Vector3.left * 2, a.position);
            if (change == 0) handle.Axes = HandleAxes.X;
            if (change == 1) handle.Type = HandleType.Position;
            if (change == 2) handle.UniformGroupScale = false;
            if (change == 3) _manager.AddTarget(NewObject("c").transform, handle);
            if (change == 4) _manager.RemoveTarget(b, handle);
            Assert.AreEqual(1, ends);
            Assert.IsFalse(_manager.IsInteracting);
            if (change == 0) Assert.IsNull(handle.GetComponentInChildren<ScaleGlobal>());
            if (change == 3) Assert.IsNull(handle.GetComponentInChildren<ScaleAxis>());
            if (change == 4) Assert.IsNotNull(handle.GetComponentInChildren<ScaleAxis>());
            if (change == 3)
            {
                center = handle.GetComponentInChildren<ScaleGlobal>();
                Assert.IsTrue(_manager.BeginInteraction(center, handle.Pivot.position));
                handle.Pivot.localScale = Vector3.one * 1.5f;
                _manager.UpdateGroupScaleUpdate(ghost, Vector3.one * .5f);
                Assert.AreEqual(Vector3.one * 3, a.localScale);
                _manager.CancelInteraction(handle);
            }
        }

        [Test]
        public void C1_Handle_component_disable_is_reversible()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var ghost = handle.Pivot;

            handle.enabled = false;
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
            Assert.IsNull(handle.Pivot);

            handle.enabled = true;
            AssertRestored(handle);
            Assert.AreSame(ghost, handle.Pivot);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
        }

        [Test]
        public void C2_Handle_GameObject_disable_is_reversible()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var ghost = handle.Pivot;

            handle.gameObject.SetActive(false);
            Assert.IsNull(handle.Pivot);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);

            handle.gameObject.SetActive(true);
            AssertRestored(handle);
            Assert.AreSame(ghost, handle.Pivot);
        }

        [Test]
        public void C3_Handle_disable_during_drag_cancels_once_and_restores()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            handle.enabled = false;
            Assert.AreEqual(1, endCount);
            Assert.IsFalse(_manager.IsInteracting);
            Assert.AreEqual(1, CountGhosts()); // exactly this handle's one ghost remains

            handle.enabled = true;
            AssertRestored(handle);
            Assert.IsTrue(_manager.BeginInteraction(handle.GetComponentInChildren<PositionAxis>(true),
                handle.transform.position));
            handle.enabled = false;
        }

        [Test]
        public void C4_Public_Disable_is_local_suspension_not_detach()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var pivot = handle.Pivot;
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            handle.Disable();
            Assert.AreEqual(1, endCount);
            Assert.IsNull(handle.Pivot);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count, "ownership is retained");
            Assert.AreEqual(1, CountGhosts(), "the owned ghost is retained");

            handle.Enable(pivot);
            AssertRestored(handle);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
        }

        [Test]
        public void C5_Manager_component_disable_retains_ownership_and_cancels_once()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var ghost = handle.Pivot;
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            _manager.enabled = false;
            Assert.AreEqual(1, endCount);
            Assert.IsFalse(_manager.enabled);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
            Assert.AreEqual(1, CountGhosts());

            _manager.enabled = true;
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
            Assert.AreSame(ghost, handle.Pivot);
            Assert.AreEqual(1, CountHandles());
        }

        [Test]
        public void C6_Manager_GameObject_disable_retains_ownership_and_cancels_once()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            _manager.gameObject.SetActive(false);
            Assert.AreEqual(1, endCount);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);

            _manager.gameObject.SetActive(true);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
            Assert.AreEqual(1, CountHandles());
        }

        [UnityTest]
        public IEnumerator C7_Target_loss_while_handle_disabled_follows_P0b()
        {
            var a = NewObject("a").transform;
            var b = NewObject("b").transform;
            var handle = _manager.CreateHandleFromList(new List<Transform> { a, b });
            handle.enabled = false;

            Object.DestroyImmediate(a.gameObject);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
            yield return null;
            Assert.IsTrue(handle, "survivor keeps the disabled handle owned");

            handle.enabled = true;
            AssertRestored(handle);
            CollectionAssert.AreEquivalent(new[] { b }, _manager.GetTargets(handle));

            var lastTarget = NewObject("last").transform;
            var last = _manager.CreateHandle(lastTarget);
            last.enabled = false;
            Object.DestroyImmediate(lastTarget.gameObject);
            yield return null;
            yield return null;
            Assert.IsFalse(last, "active manager cleans an empty disabled handle");
        }

        [UnityTest]
        public IEnumerator C8_Target_loss_while_manager_disabled_is_deferred()
        {
            var target = NewObject("target").transform;
            var handle = _manager.CreateHandle(target);
            _manager.enabled = false;
            Object.DestroyImmediate(target.gameObject);
            yield return null;
            Assert.IsTrue(handle, "disabled manager does not run cleanup");

            _manager.enabled = true;
            yield return null;
            yield return null;
            Assert.IsFalse(handle);
            Assert.AreEqual(0, CountGhosts());
        }

        [UnityTest]
        public IEnumerator C9_Create_without_camera_is_safe_and_recovers()
        {
            _cameraGo.tag = "Untagged";
            _manager.MainCamera = null;

            var target = NewObject("target").transform;
            Handle handle = null;
            Assert.DoesNotThrow(() => handle = _manager.CreateHandle(target));
            Assert.IsNotNull(handle);
            Assert.IsNull(handle.HandleCamera);
            Assert.DoesNotThrow(() => handle.Type = HandleType.All);

            _manager.MainCamera = _camera;
            yield return null;
            Assert.AreSame(_camera, handle.HandleCamera);
            Assert.IsTrue(_manager.BeginInteraction(handle.GetComponentInChildren<PositionAxis>(true),
                handle.transform.position));
            handle.enabled = false;
        }

        [UnityTest]
        public IEnumerator C10_Camera_destroyed_during_drag_cancels_and_falls_back()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            var fallback = NewCamera("Camera.main", true);
            Object.DestroyImmediate(_cameraGo);
            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.IsFalse(_manager.IsInteracting);
            Assert.IsTrue(handle);
            Assert.AreSame(fallback, handle.HandleCamera);
        }

        [Test]
        public void C11_Camera_replacement_cancels_once_same_reference_does_not()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ => endCount++;
            BeginDrag(handle);

            var replacement = NewCamera("Replacement");
            _manager.MainCamera = replacement;
            Assert.AreEqual(1, endCount);
            Assert.AreSame(replacement, handle.HandleCamera);

            _manager.MainCamera = replacement;
            Assert.AreEqual(1, endCount);
        }

        [Test]
        public void C12_Explicitly_assigned_disabled_camera_is_valid()
        {
            var disabledGo = NewObject("Disabled Camera");
            var disabled = disabledGo.AddComponent<Camera>();
            disabled.enabled = false;
            disabledGo.SetActive(false);
            _manager.MainCamera = disabled;

            var handle = _manager.CreateHandle(NewObject("target").transform);
            Assert.AreSame(disabled, handle.HandleCamera);
            Assert.IsTrue(_manager.BeginInteraction(handle.GetComponentInChildren<PositionAxis>(true),
                handle.transform.position));
            handle.enabled = false;
        }

        [UnityTest]
        public IEnumerator C13_End_callback_reenable_during_OnDisable_keeps_restored_state()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                if (!handle.enabled) handle.enabled = true;
            };
            BeginDrag(handle);

            handle.enabled = false;
            Assert.AreEqual(1, endCount);
            Assert.IsTrue(handle.enabled);
            AssertRestored(handle);

            var goHandle = _manager.CreateHandle(NewObject("target2").transform);
            var goGhost = goHandle.Pivot;
            var goEndCount = 0;
            var reopenRequested = false;
            goHandle.OnInteractionEndEvent += _ =>
            {
                goEndCount++;
                reopenRequested = true; // caller/test defers SetActive(true) to the next frame
            };
            BeginDrag(goHandle);
            goHandle.gameObject.SetActive(false);

            // The callback only requests reopen; Unity forbids SetActive(true) while OnDisable is
            // still activating/deactivating. The disabled state must settle before caller reopen.
            Assert.AreEqual(1, goEndCount);
            Assert.IsTrue(reopenRequested);
            Assert.IsFalse(goHandle.gameObject.activeSelf);
            Assert.IsNull(goHandle.Pivot);
            yield return null;
            Assert.IsFalse(goHandle.gameObject.activeSelf);

            goHandle.gameObject.SetActive(true);
            yield return null;

            Assert.AreEqual(1, goEndCount);
            Assert.IsTrue(goHandle.gameObject.activeSelf);
            Assert.AreSame(goGhost, goHandle.Pivot);
            AssertRestored(goHandle);
            Assert.IsTrue(_manager.BeginInteraction(goHandle.GetComponentInChildren<PositionAxis>(true),
                goHandle.transform.position), "next drag must work after deferred reopen");
        }

        [Test]
        public void C28_Parent_inactive_handle_is_safe_and_no_input()
        {
            var parent = NewObject("parent");
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var ghost = handle.Pivot;
            handle.gameObject.transform.SetParent(parent.transform);
            var axis = handle.GetComponentInChildren<PositionAxis>(true);

            parent.SetActive(false);
            Assert.IsFalse(handle.isActiveAndEnabled);
            Assert.IsFalse(_manager.BeginInteraction(axis, handle.transform.position));
            Assert.IsFalse(_manager.IsInteracting);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);

            parent.SetActive(true);
            Assert.IsTrue(handle.isActiveAndEnabled);
            Assert.AreSame(ghost, handle.Pivot);
            AssertRestored(handle);
        }

        [Test]
        public void C14_Manager_reenable_keeps_same_handle_group_and_ghost()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var ghost = handle.Pivot;
            _manager.enabled = false;
            _manager.enabled = true;

            Assert.AreEqual(1, CountHandles());
            Assert.AreEqual(1, CountGhosts());
            Assert.AreSame(ghost, handle.Pivot);
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
        }

        [Test]
        public void C15_Create_while_manager_disabled_is_owned_dormant()
        {
            _manager.enabled = false;
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var axis = handle.GetComponentInChildren<PositionAxis>(true);

            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
            Assert.IsFalse(_manager.BeginInteraction(axis, handle.transform.position));
            Assert.IsFalse(_manager.IsInteracting);

            _manager.enabled = true;
            Assert.IsTrue(_manager.BeginInteraction(axis, handle.transform.position));
            handle.enabled = false;
        }

        [Test]
        public void C16_Throwing_end_callback_on_disable_is_contained()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                throw new InvalidOperationException("disable end");
            };
            BeginDrag(handle);
            LogAssert.Expect(LogType.Exception, new Regex(@"^InvalidOperationException: disable end(?:\r?\n|$)"));

            Assert.DoesNotThrow(() => handle.enabled = false);
            Assert.AreEqual(1, endCount);
            Assert.IsFalse(handle.enabled);
            handle.enabled = true;
            AssertRestored(handle);
        }

        [Test]
        public void C17_Disable_callback_removing_another_handle_is_safe()
        {
            var handleA = _manager.CreateHandle(NewObject("a").transform);
            var handleB = _manager.CreateHandle(NewObject("b").transform);
            _spawned.Add(handleB.gameObject);
            var endA = 0;
            handleA.OnInteractionEndEvent += _ =>
            {
                endA++;
                _manager.RemoveHandle(handleB);
            };
            BeginDrag(handleA);

            Assert.DoesNotThrow(() => handleA.enabled = false);
            Assert.AreEqual(1, endA);
            Assert.IsTrue(handleB, "removed handle is detached but its GameObject stays alive");
            Assert.IsNull(handleB.Pivot);
            Assert.AreEqual(0, _manager.GetTargets(handleB).Count);
            Assert.AreEqual(1, CountGhosts(), "only handle A's ghost remains");
        }

        [Test]
        public void C18_Live_main_tag_change_does_not_replace_assigned_camera()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var liveMain = NewCamera("Live Main", true);
            Assert.AreSame(_camera, _manager.MainCamera);
            Assert.AreSame(_camera, handle.HandleCamera);

            _manager.MainCamera = liveMain;
            Assert.AreSame(liveMain, handle.HandleCamera);
        }

        [Test]
        public void C19_Camera_setter_revalidates_callback_detach_and_last_write_wins()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            _spawned.Add(handle.gameObject);
            var replacement = NewCamera("Replacement");
            var last = NewCamera("Last");
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                _manager.RemoveHandle(handle);
                _manager.MainCamera = last;
            };
            BeginDrag(handle);

            Assert.DoesNotThrow(() => _manager.MainCamera = replacement);
            Assert.AreEqual(1, endCount);
            Assert.IsTrue(handle, "detached handle stays alive");
            Assert.AreEqual(0, _manager.GetTargets(handle).Count);
            Assert.AreSame(last, _manager.MainCamera);
        }

        [Test]
        public void C20_BeginInteraction_rejects_inactive_or_camera_unusable_inputs()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var axis = handle.GetComponentInChildren<PositionAxis>(true);

            _manager.enabled = false;
            Assert.IsFalse(_manager.BeginInteraction(axis, handle.transform.position));
            _manager.enabled = true;

            handle.enabled = false;
            Assert.IsFalse(_manager.BeginInteraction(axis, handle.transform.position));
            handle.enabled = true;

            axis.gameObject.SetActive(false);
            Assert.IsFalse(_manager.BeginInteraction(axis, handle.transform.position));
            axis.gameObject.SetActive(true);

            _cameraGo.tag = "Untagged";
            _manager.MainCamera = null;
            Assert.IsFalse(_manager.BeginInteraction(axis, handle.transform.position));
            Assert.IsFalse(_manager.IsInteracting);
            _manager.MainCamera = _camera;
        }

        [Test]
        public void C21_Reenable_from_end_callback_during_RemoveHandle_cannot_resurrect()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            _spawned.Add(handle.gameObject);
            var endCount = 0;
            var destroyed = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                handle.enabled = false;
                handle.enabled = true;
            };
            handle.OnHandleDestroyedEvent += _ => destroyed++;
            BeginDrag(handle);

            Assert.DoesNotThrow(() => _manager.RemoveHandle(handle));
            Assert.AreEqual(1, endCount);
            Assert.AreEqual(0, destroyed);
            Assert.IsTrue(handle);
            Assert.IsNull(handle.Pivot);
            Assert.AreEqual(0, _manager.GetTargets(handle).Count);
        }

        [UnityTest]
        public IEnumerator C22_Idle_camera_destroyed_lateupdate_is_safe()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            handle.Type = HandleType.All;

            Object.DestroyImmediate(_cameraGo);
            yield return null;
            yield return null;

            Assert.IsTrue(handle);
            Assert.IsTrue(handle.HandleCamera == null, "destroyed camera must satisfy Unity null");
            Assert.AreEqual(1, _manager.GetTargets(handle).Count);
        }

        [UnityTest]
        public IEnumerator C23_Fallback_callback_assignment_wins()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            var last = NewCamera("Last");
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                _manager.MainCamera = last;
            };
            BeginDrag(handle);
            NewCamera("Fallback", true);
            Object.DestroyImmediate(_cameraGo);

            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.AreSame(last, _manager.MainCamera);
            Assert.AreSame(last, handle.HandleCamera);
        }

        [UnityTest]
        public IEnumerator C24_Fallback_callback_null_wins()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                _manager.MainCamera = null;
            };
            BeginDrag(handle);
            NewCamera("Fallback", true);
            Object.DestroyImmediate(_cameraGo);

            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.IsNull(_manager.MainCamera);
            Assert.IsNull(handle.HandleCamera);
            Assert.IsTrue(handle);
        }

        [UnityTest]
        public IEnumerator C25_Fallback_callback_manager_destroyed_stops_safely()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                Object.DestroyImmediate(_manager.gameObject);
            };
            BeginDrag(handle);
            NewCamera("Fallback", true);
            Object.DestroyImmediate(_cameraGo);

            yield return null;

            Assert.AreEqual(1, endCount);
            Assert.IsFalse(handle);
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
            _manager = null;
        }

        [Test]
        public void C26_Public_Disable_callback_selfdestroy_is_safe()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            handle.OnInteractionEndEvent += _ => Object.DestroyImmediate(handle.gameObject);
            BeginDrag(handle);

            Assert.DoesNotThrow(() => handle.Disable());
            Assert.IsFalse(handle);
            Assert.AreEqual(0, CountHandles());
            Assert.AreEqual(0, CountGhosts());
        }

        [Test]
        public void C27_Camera_setter_callback_handle_destruction_is_safe()
        {
            var handle = _manager.CreateHandle(NewObject("target").transform);
            var endCount = 0;
            handle.OnInteractionEndEvent += _ =>
            {
                endCount++;
                Object.DestroyImmediate(handle.gameObject);
            };
            BeginDrag(handle);
            var replacement = NewCamera("Replacement");

            Assert.DoesNotThrow(() => _manager.MainCamera = replacement);
            Assert.AreEqual(1, endCount);
            Assert.IsFalse(handle);
            Assert.AreEqual(0, CountGhosts());
            Assert.AreSame(replacement, _manager.MainCamera);
            Assert.IsNotNull(_manager.CreateHandle(NewObject("fresh").transform));
        }
    }
}
