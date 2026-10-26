using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TransformHandles;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ConsumerJourney
{
    [Serializable]
    public sealed class PlayerRequest
    {
        public string stage, nonce;
    }

    [Serializable]
    public sealed class PlayerObservation
    {
        public int schema = 1;
        public string runId, source, packageSource, session, stage, nonce, unity, scene, pipeline, platform;
        public string lifecycleStage, buildId;
        public int frame, width, height, input, starts, changes, ends, destroyed, selections, members, liveHandles, liveGhosts, missingScripts, visibleRenderers;
        public bool focused, grouped, handleEnabled, shaderSupported, cameraPresent;
        public string type, space, rotationShortcut;
        public Vector3[] positions, rotations, scales;
        public Vector3 pivot, handleRotation;
        public Vector2[] targetPixels;
        public Vector2 handleX, handleY, handleRing, pivotPixel;
        public Color highlightColor, cameraBackground;
        public Color[] targetColors;
        public float handleSize;
        public ControlObservation[] controls;
        public ControlObservation[] labelRegions;
        public string[] labels;
        public VisualObservation[] visuals;
        public string[] errors;
    }

    [Serializable]
    public sealed class ControlObservation
    {
        public string id, text;
        public float x, y, width, height;
        public bool enabled;
    }

    [Serializable]
    public sealed class PlayerStatus
    {
        public string runId, session, scene;
        public int frame;
        public string[] controls;
    }

    [Serializable]
    public sealed class VisualObservation
    {
        public string kind;
        public Color color;
        public int left, top, right, bottom;
    }

    // Observes only. Requests can capture state and pixels, never select, transform,
    // switch a mode, invoke a workspace command or manufacture an interaction event.
    public sealed class ConsumerProbe : MonoBehaviour
    {
        public string runId, source, packageSource;
        public static Func<ControlObservation[]> ReadControls;
        public static Func<ControlObservation[]> ReadLabelRegions;
        public static Func<Transform[]> ReadSampleTargets;
        private static ConsumerProbe _instance;
        private string _evidence, _session, _lastNonce;
        private bool _capturing;
        private bool _ready;
        private ConsumerBuildIdentity _build;
        private float _nextStatus;
        private readonly List<string> _errors = new List<string>();
        private readonly HashSet<Handle> _observed = new HashSet<Handle>();
        private int _starts, _changes, _ends, _destroyed;

        private void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            var stamp = Resources.Load<TextAsset>("ConsumerJourneyBuild");
            if (stamp == null) throw new InvalidOperationException("Native build identity is missing");
            _build = JsonUtility.FromJson<ConsumerBuildIdentity>(stamp.text);
            _evidence = Argument("-consumerEvidence");
            _session = Argument("-consumerSession");
            if (string.IsNullOrEmpty(_evidence) || string.IsNullOrEmpty(_session)) { enabled = false; return; }
            if (!Directory.Exists(_evidence)) throw new InvalidOperationException("Driver must create evidence directory first");
            Application.logMessageReceived += RecordLog;
        }

        private void RecordLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert || message.Contains("ERROR: Shader")) && _errors.Count < 128)
                _errors.Add(message + "\n" + stack);
        }

        private IEnumerator Start()
        {
            if (!enabled) yield break;
            for (var i = 0; i < 30; i++) yield return null;
            while (!SplashScreen.isFinished) yield return null;
            yield return Capture(new PlayerRequest {stage = "ready", nonce = _session});
            _ready = true;
        }

        private void Update()
        {
            foreach (var handle in FindObjectsOfType<Handle>())
            {
                if (!_observed.Add(handle)) continue;
                handle.OnInteractionStartEvent += _ => _starts++;
                handle.OnInteractionEvent += _ => _changes++;
                handle.OnInteractionEndEvent += _ => _ends++;
                handle.OnHandleDestroyedEvent += _ => _destroyed++;
            }
            if (_ready && Time.unscaledTime >= _nextStatus)
            {
                _nextStatus = Time.unscaledTime + .2f;
                var status = new PlayerStatus {runId = runId, session = _session, scene = SceneManager.GetActiveScene().name,
                    frame = Time.frameCount, controls = (ReadControls?.Invoke() ?? new ControlObservation[0]).Select(c => c.id).ToArray()};
                WriteAtomic(Path.Combine(_evidence, "status.json"), JsonUtility.ToJson(status));
            }
            if (_capturing || !_ready) return;
            var path = Path.Combine(_evidence, "request.json");
            if (!File.Exists(path)) return;
            var request = JsonUtility.FromJson<PlayerRequest>(File.ReadAllText(path));
            if (request == null || request.nonce == _lastNonce) return;
            if (!SafeName(request.stage) || !SafeName(request.nonce)) throw new InvalidOperationException("Invalid driver checkpoint");
            _lastNonce = request.nonce;
            StartCoroutine(Capture(request));
        }

        private IEnumerator Capture(PlayerRequest request)
        {
            _capturing = true;
            yield return new WaitForEndOfFrame();
            var example = FindObjectOfType<ConsumerExample>();
            var handle = example != null ? example.CurrentHandle : FindObjectsOfType<Handle>().FirstOrDefault();
            var targets = example != null ? example.targets : (ReadSampleTargets?.Invoke() ?? new Transform[0]);
            var camera = Camera.main;
            var labelRegions = ReadLabelRegions?.Invoke() ?? new ControlObservation[0];
            if (_build.runId != runId || _build.source != source || _build.packageSource != packageSource)
                throw new InvalidOperationException("Native build and scene identities differ");
            var observation = new PlayerObservation
            {
                runId = runId, source = source, packageSource = packageSource, session = _session,
                lifecycleStage = _build.lifecycleStage, buildId = _build.buildId,
                stage = request.stage, nonce = request.nonce, unity = Application.unityVersion,
                platform = Application.platform.ToString(),
                scene = SceneManager.GetActiveScene().name, pipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.GetType().FullName : "Built-in",
                frame = Time.frameCount, width = Screen.width, height = Screen.height, focused = Application.isFocused,
                cameraPresent = camera != null,
                missingScripts = FindObjectsOfType<Transform>(true).Sum(t => t.GetComponents<MonoBehaviour>().Count(component => component == null)),
                visibleRenderers = FindObjectsOfType<Renderer>().Count(renderer => renderer.enabled && renderer.isVisible),
#if ENABLE_INPUT_SYSTEM
                input = 1,
#else
                input = 0,
#endif
                errors = _errors.ToArray(), cameraBackground = camera != null ? camera.backgroundColor : Color.clear,
                shaderSupported = FindObjectsOfType<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader")),
                positions = targets.Select(t => t.position).ToArray(),
                rotations = targets.Select(t => t.eulerAngles).ToArray(),
                scales = targets.Select(t => t.localScale).ToArray(),
                targetColors = targets.Select(t => t.GetComponent<Renderer>() != null ? t.GetComponent<Renderer>().sharedMaterial.color : Color.clear).ToArray(),
                targetPixels = camera != null ? targets.Select(t => Pixel(camera, t.position)).ToArray() : new Vector2[0],
                starts = _starts, changes = _changes, ends = _ends, destroyed = _destroyed,
                liveHandles = FindObjectsOfType<Handle>().Length, liveGhosts = FindObjectsOfType<Ghost>().Length,
                selections = example != null ? example.Selections : 0, grouped = example != null && example.Grouped,
                members = handle != null ? handle.Targets.Count : 0, handleEnabled = handle != null && handle.isActiveAndEnabled,
                type = handle != null ? handle.Type.ToString() : "none", space = handle != null ? handle.Space.ToString() : "none",
                pivot = handle != null && handle.Pivot != null ? handle.Pivot.position : Vector3.zero,
                handleRotation = handle != null ? handle.transform.eulerAngles : Vector3.zero,
                highlightColor = example != null && example.settings != null ? example.settings.HighlightColor : Color.clear,
                rotationShortcut = example != null && example.settings != null ? example.settings.RotationKey.ToString() : "none",
                handleSize = handle != null ? handle.AutoScaleSizeInPixels : 0,
                controls = ReadControls?.Invoke() ?? new ControlObservation[0],
                labels = labelRegions.Select(label => label.text).ToArray(), labelRegions = labelRegions,
                visuals = camera != null ? ReadVisuals(camera, targets) : new VisualObservation[0]
            };
            if (handle != null && camera != null)
            {
                observation.handleX = Pixel(camera, handle.transform.TransformPoint(new Vector3(.7f, 0, 0)));
                observation.handleY = Pixel(camera, handle.transform.TransformPoint(new Vector3(0, .7f, 0)));
                observation.handleRing = Pixel(camera, handle.transform.TransformPoint(new Vector3(.7f, .7f, 0)));
                observation.pivotPixel = Pixel(camera, handle.Pivot.position);
            }
            var stem = request.stage == "ready" ? "ready" : request.stage + "-" + request.nonce;
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(_evidence, stem + ".png"), image.EncodeToPNG());
            Destroy(image);
            var reportPath = Path.Combine(_evidence, stem + ".json");
            WriteAtomic(reportPath, JsonUtility.ToJson(observation, true));
            _capturing = false;
        }

        private static Vector2 Pixel(Camera camera, Vector3 world)
        {
            var pixel = camera.WorldToScreenPoint(world);
            return new Vector2(pixel.x, Screen.height - pixel.y);
        }

        private static VisualObservation[] ReadVisuals(Camera camera, Transform[] targets)
        {
            var result = new List<VisualObservation>();
            foreach (var renderer in FindObjectsOfType<Renderer>().Where(r => r.enabled && r.isVisible))
            {
                var handle = renderer.GetComponentInParent<Handle>() != null;
                if (!handle && !targets.Contains(renderer.transform)) continue;
                var material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty("_Color")) continue;
                var color = material.color;
                if (handle && Mathf.Max(color.r, color.g, color.b) - Mathf.Min(color.r, color.g, color.b) < .3f) continue;
                var bounds = renderer.bounds;
                var points = new List<Vector2>();
                for (var x = -1; x <= 1; x += 2)
                    for (var y = -1; y <= 1; y += 2)
                        for (var z = -1; z <= 1; z += 2)
                        {
                            var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                            if (camera.WorldToScreenPoint(point).z > 0) points.Add(Pixel(camera, point));
                        }
                if (points.Count != 8) continue;
                result.Add(new VisualObservation {kind = handle ? "handle" : "target", color = color,
                    left = Mathf.Clamp(Mathf.FloorToInt(points.Min(p => p.x)) - 2, 0, Screen.width - 1),
                    right = Mathf.Clamp(Mathf.CeilToInt(points.Max(p => p.x)) + 2, 0, Screen.width - 1),
                    top = Mathf.Clamp(Mathf.FloorToInt(points.Min(p => p.y)) - 2, 0, Screen.height - 1),
                    bottom = Mathf.Clamp(Mathf.CeilToInt(points.Max(p => p.y)) + 2, 0, Screen.height - 1)});
            }
            return result.ToArray();
        }

        private static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }

        private static bool SafeName(string value)
        {
            return !string.IsNullOrEmpty(value) && value.Length <= 100 && value.All(c => char.IsLetterOrDigit(c) || c == '-');
        }

        private static void WriteAtomic(string path, string text)
        {
            File.WriteAllText(path + ".pending", text);
            if (File.Exists(path)) File.Replace(path + ".pending", path, null);
            else File.Move(path + ".pending", path);
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            Application.logMessageReceived -= RecordLog;
            _instance = null;
        }
    }
}
