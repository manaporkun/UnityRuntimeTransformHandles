using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TransformHandles;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using InputWrapper = TransformHandles.Utils.InputWrapper;

namespace TransformHandles.Samples.DemoBrowser
{
    // Navigation must claim its press before the handle manager polls raw input.
    [DefaultExecutionOrder(-10000)]
    public sealed class DemoBrowser : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset uiLayout;
        [SerializeField] private PanelSettings uiPanelSettings;
        private static DemoBrowser _instance;
        private string _galleryPath;
        private UIDocument _document;
        private PanelSettings _panel;
        private GameObject _eventSystem;
        private VisualElement _gallery, _cards;
        private Button _navigation;
        private UIDocument _navigationDocument;
        private Label _status;
        private bool _loading, _claimedPress;
        private int _releasedFrame = -1;
        private TransformHandleManager _suspendedManager;
        private readonly List<string> _availablePaths = new List<string>();

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _galleryPath = gameObject.scene.path;
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
            _panel = Instantiate(uiPanelSettings);
            _panel.sortingOrder = 100;
            var ui = new GameObject("Demo Browser UI");
            ui.SetActive(false);
            ui.transform.SetParent(transform, false);
            _document = ui.AddComponent<UIDocument>();
            _document.panelSettings = _panel;
            _document.visualTreeAsset = uiLayout;
            ui.SetActive(true);
            var root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            _gallery = root.Q("gallery");
            _cards = root.Q("cards");
            _status = root.Q<Label>("status");
            PopulateGallery();
            SceneManager.sceneLoaded += SceneLoaded;
            ShowGallery(true);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                _eventSystem = new GameObject("Demo Browser Event System", typeof(EventSystem));
                _eventSystem.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM && TH_INPUTSYSTEM
                _eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
                _eventSystem.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        // Full paths avoid loading an unrelated scene with the same short name.
        public static string ResolveScenePath(IEnumerable<string> buildPaths, string sceneName)
        {
            var matches = buildPaths.Where(path => Path.GetFileNameWithoutExtension(path) == sceneName).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Ambiguous demo scene: " + sceneName);
            return matches.Length == 1 ? matches[0] : null;
        }

        private void PopulateGallery()
        {
            var paths = Enumerable.Range(0, SceneManager.sceneCountInBuildSettings)
                .Select(SceneUtility.GetScenePathByBuildIndex).ToArray();
            AddCard(paths, "ModularBuilding", "01", "Modular workshop", "Build a wall study", "Arrange walls and corners on a one-unit grid. Move and turn each piece.");
            AddCard(paths, "LevelEditor", "02", "Prop workshop", "Compose a small scene", "Select several props. Explore World and Local movement, rotation and scale.");
            AddCard(paths, "RuntimeTransformHandlesDemo", "03", "Handle showcase", "Explore every tool", "Try handle types, axis constraints, snapping and interaction events.");
            AddCard(paths, "ProductConfigurator", "04", "Lamp assembly", "Build an articulated lamp", "Attach parts to sockets, adjust constrained hinges and detach complete assemblies.");
            _cards.EnableInClassList("many-workspaces", _availablePaths.Count > 3);
            _status.text = _availablePaths.Count == 0
                ? "No demo scenes in this build. Import a demo and add its scene to Build Settings."
                : "Choose a workspace. Returning here resets that demo’s scene.";
        }

        private void AddCard(string[] paths, string scene, string number, string title, string subtitle, string description)
        {
            string path;
            try { path = ResolveScenePath(paths, scene); }
            catch (InvalidOperationException exception) { Debug.LogError(exception.Message); return; }
            if (path == null) return;
            _availablePaths.Add(path);
            var card = new Button(() => OpenDemo(path));
            card.AddToClassList("demo-card");
            card.Add(new Label(number) { name = "number", pickingMode = PickingMode.Ignore });
            card.Add(new Label(title) { name = "title", pickingMode = PickingMode.Ignore });
            card.Add(new Label(subtitle) { name = "subtitle", pickingMode = PickingMode.Ignore });
            card.Add(new Label(description) { name = "description", pickingMode = PickingMode.Ignore });
            card.Add(new Label("Open workspace  →") { name = "open", pickingMode = PickingMode.Ignore });
            _cards.Add(card);
        }

        public void OpenDemo(string path)
        {
            if (_loading || SceneManager.GetActiveScene().path != _galleryPath || !_availablePaths.Contains(path)) return;
            StartCoroutine(LoadScene(path));
        }

        public void ReturnToGallery()
        {
            if (_loading || SceneManager.GetActiveScene().path == _galleryPath) return;
            StartCoroutine(LoadScene(_galleryPath));
        }

        private IEnumerator LoadScene(string path)
        {
            _loading = true;
            _cards.SetEnabled(false);
            _navigation?.SetEnabled(false);
            _status.text = "Opening workspace…";
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single); }
            catch (Exception exception) { Debug.LogException(exception); }
            if (operation != null) yield return operation;
            // Scene owners destroy persistent managers at end of frame. Do not let the next
            // workspace borrow one of those managers before that destruction has completed.
            yield return null;
            while (InputWrapper.GetMouseButton(0) || InputWrapper.GetMouseButton(1) || InputWrapper.GetMouseButton(2)) yield return null;
            ReleaseManager();
            _loading = false;
            _cards.SetEnabled(true);
            _navigation?.SetEnabled(true);
            _status.text = operation == null ? "Could not open this workspace. Try again." : "Returning here resets the demo’s scene.";
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
            ShowGallery(scene.path == _galleryPath);
        }

        private void ShowGallery(bool visible)
        {
            _gallery.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshNavigation();
        }

        private void Update()
        {
            if (_document == null) return;
            RefreshNavigation();
            if (_navigation == null || _navigation.panel == null)
            {
                if (_claimedPress && !_loading) ReleaseManager();
                return;
            }
            var position = RuntimePanelUtils.ScreenToPanel(_navigation.panel,
                new Vector2(InputWrapper.PointerPosition.x, Screen.height - InputWrapper.PointerPosition.y));
            bool overNavigation = _navigation.enabledInHierarchy && _navigation.worldBound.Contains(position);
            if (overNavigation && (InputWrapper.GetMouseButtonDown(0) || InputWrapper.GetMouseButtonDown(1) || InputWrapper.GetMouseButtonDown(2)))
            {
                _claimedPress = true;
                _releasedFrame = -1;
                var manager = FindObjectOfType<TransformHandleManager>();
                if (manager != null && manager.enabled)
                {
                    _suspendedManager = manager;
                    manager.enabled = false; // Also cancels an in-progress drag before leaving.
                }
            }
            if (!_claimedPress || _loading) return;
            if (InputWrapper.GetMouseButton(0) || InputWrapper.GetMouseButton(1) || InputWrapper.GetMouseButton(2)) return;
            if (_releasedFrame < 0) _releasedFrame = Time.frameCount;
            else if (Time.frameCount > _releasedFrame) ReleaseManager();
        }

        // Each independent sample declares the same named slot; no sample assembly is referenced.
        // UIDocument recreates its tree on re-enable, so sceneLoaded alone is not sufficient.
        private void RefreshNavigation()
        {
            if (SceneManager.GetActiveScene().path == _galleryPath)
            {
                UnbindNavigation();
                _navigationDocument = null;
                return;
            }
            if (_navigationDocument == null)
            {
                foreach (var document in FindObjectsOfType<UIDocument>())
                {
                    if (document == _document || document.rootVisualElement == null) continue;
                    if (document.rootVisualElement.Q<Button>("demo-gallery") == null) continue;
                    _navigationDocument = document;
                    break;
                }
            }
            var button = _navigationDocument != null && _navigationDocument.isActiveAndEnabled
                ? _navigationDocument.rootVisualElement?.Q<Button>("demo-gallery") : null;
            if (button == _navigation) return;
            UnbindNavigation();
            _navigation = button;
            if (_navigation == null) return;
            _navigation.AddToClassList("available");
            _navigation.SetEnabled(!_loading);
            _navigation.clicked += ReturnToGallery;
        }

        private void UnbindNavigation()
        {
            if (_navigation == null) return;
            _navigation.clicked -= ReturnToGallery;
            _navigation.RemoveFromClassList("available");
            _navigation = null;
        }

        private void ReleaseManager()
        {
            if (_suspendedManager != null) _suspendedManager.enabled = true;
            _suspendedManager = null;
            _claimedPress = false;
            _releasedFrame = -1;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            SceneManager.sceneLoaded -= SceneLoaded;
            UnbindNavigation();
            ReleaseManager();
            if (_panel != null) Destroy(_panel);
            _instance = null;
        }
    }
}
