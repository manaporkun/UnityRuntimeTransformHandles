using System.Collections.Generic;
using TransformHandles;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConsumerJourney
{
    // A consumer-owned version of README's public CreateHandle example.
    // Only real pointer/keyboard input reaches these application commands.
    public sealed class ConsumerExample : MonoBehaviour
    {
        public Transform[] targets;
        public TransformHandleSettings settings;
        public Handle CurrentHandle { get; private set; }
        public int Starts { get; private set; }
        public int Changes { get; private set; }
        public int Ends { get; private set; }
        public int Destroyed { get; private set; }
        public int Selections { get; private set; }
        public bool Grouped { get; private set; }

        private TransformHandleManager _manager;

        private void Start()
        {
            _manager = TransformHandleManager.Instance;
            _manager.MainCamera = Camera.main;
            _manager.Settings = settings;
        }

        private void Update()
        {
            if (InputWrapper.GetKeyDown(KeyCode.Q)) Application.Quit(0);
            if (InputWrapper.GetMouseButtonDown(0))
            {
                var ray = Camera.main.ScreenPointToRay(InputWrapper.PointerPosition);
                // Handle colliders take precedence; clicking a handle is never a reselection.
                var layer = LayerMask.NameToLayer("TransformHandle");
                if (!Physics.Raycast(ray, 1000, 1 << layer) && Physics.Raycast(ray, out var hit, 1000, ~(1 << layer)))
                {
                    foreach (var target in targets)
                        if (hit.transform == target) { Select(target, false); break; }
                }
            }
            if (InputWrapper.GetKeyDown(KeyCode.G)) Select(targets[0], true);
            if (InputWrapper.GetKeyDown(KeyCode.D) && CurrentHandle != null)
                CurrentHandle.enabled = !CurrentHandle.enabled;
            if (InputWrapper.GetKeyDown(KeyCode.N))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex == 0 ? 1 : 0);
        }

        private void Select(Transform target, bool group)
        {
            _manager.DestroyAllHandles();
            CurrentHandle = group ? _manager.CreateHandleFromList(new List<Transform>(targets)) : _manager.CreateHandle(target);
            Grouped = group;
            Selections++;
            CurrentHandle.PositionSnap = Vector3.one * .5f;
            CurrentHandle.RotationSnap = 15;
            CurrentHandle.ScaleSnap = Vector3.one * .1f;
            CurrentHandle.SnappingType = SnappingType.Relative;
            CurrentHandle.OnInteractionStartEvent += _ => Starts++;
            CurrentHandle.OnInteractionEvent += _ => Changes++;
            CurrentHandle.OnInteractionEndEvent += _ => Ends++;
            CurrentHandle.OnHandleDestroyedEvent += _ => Destroyed++;
        }
    }
}
