using System;
using System.Linq;
using TransformHandles.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ConsumerJourney
{
    // Optional, read-only UI observations. Staged only after Demos' host UI modules resolve.
    public sealed class SampleProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ObserveSamplePlayer()
        {
            var config = Resources.Load<ConsumerSampleRunConfig>("ConsumerSampleRun");
            if (config == null || !config.scenes.Contains(SceneManager.GetActiveScene().path)) return;
            var owner = new GameObject("Sample Player Observations");
            DontDestroyOnLoad(owner);
            var probe = owner.AddComponent<ConsumerProbe>();
            probe.runId = config.runId;
            probe.source = config.source;
            probe.packageSource = config.packageSource;
            owner.AddComponent<SampleProbe>();
            ConsumerProbe.ReadControls = ReadControls;
            ConsumerProbe.ReadLabelRegions = ReadLabelRegions;
            ConsumerProbe.ReadSampleTargets = ReadTargets;
        }

        private static bool IsVisible(VisualElement element)
        {
            if (element.panel == null || element.worldBound.width <= 0 || element.worldBound.height <= 0) return false;
            for (var current = element; current != null; current = current.parent)
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.visibility == Visibility.Hidden || current.resolvedStyle.opacity <= 0) return false;
            return true;
        }

        private static ControlObservation[] ReadControls()
        {
            return FindObjectsOfType<UIDocument>().Where(document => document.rootVisualElement.panel != null &&
                document.rootVisualElement.resolvedStyle.width > 0 && document.rootVisualElement.resolvedStyle.height > 0)
                .SelectMany(document =>
                {
                    var root = document.rootVisualElement;
                    return root.Query<Button>().ToList().Where(IsVisible).Select((button, index) =>
                    {
                        var text = string.Join(" ", new[] {button.text}.Concat(button.Query<Label>().ToList().Select(label => label.text)).Where(value => !string.IsNullOrEmpty(value)));
                        return ObserveRegion(button, root, text, string.IsNullOrEmpty(button.name) ? "button-" + index : button.name);
                    });
                }).ToArray();
        }

        private static ControlObservation[] ReadLabelRegions()
        {
            return FindObjectsOfType<UIDocument>().Where(document => document.rootVisualElement.panel != null &&
                    document.rootVisualElement.resolvedStyle.width > 0 && document.rootVisualElement.resolvedStyle.height > 0)
                .SelectMany(document => document.rootVisualElement.Query<Label>().ToList().Where(label => IsVisible(label) && !string.IsNullOrEmpty(label.text))
                    .Select(label => ObserveRegion(label, document.rootVisualElement, label.text, label.name))).ToArray();
        }

        private static ControlObservation ObserveRegion(VisualElement element, VisualElement root, string text, string identifier)
        {
            var rectangle = element.worldBound;
            var scaleX = Screen.width / root.resolvedStyle.width;
            var scaleY = Screen.height / root.resolvedStyle.height;
            return new ControlObservation {id = identifier, text = text, x = rectangle.center.x * scaleX, y = rectangle.center.y * scaleY,
                width = rectangle.width * scaleX, height = rectangle.height * scaleY, enabled = element.enabledInHierarchy};
        }

        private static Transform[] ReadTargets()
        {
            var camera = Camera.main;
            if (camera == null) return new Transform[0];
            var handleLayer = LayerMask.NameToLayer("TransformHandle");
            return FindObjectsOfType<Collider>().Where(collider => collider.enabled && collider.gameObject.layer != handleLayer &&
                collider.GetComponent<Renderer>() != null && !collider.name.ToLowerInvariant().Contains("floor") && !collider.name.ToLowerInvariant().Contains("ground"))
                .Select(collider => collider.transform).Distinct().Where(target =>
                {
                    var point = camera.WorldToViewportPoint(target.position);
                    return point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1;
                }).OrderBy(target => target.name, StringComparer.Ordinal).Take(16).ToArray();
        }

        private void Update()
        {
            if (InputWrapper.GetKeyDown(KeyCode.Q)) Application.Quit(0);
        }

        private void OnDestroy()
        {
            ConsumerProbe.ReadControls = null;
            ConsumerProbe.ReadLabelRegions = null;
            ConsumerProbe.ReadSampleTargets = null;
        }
    }
}
