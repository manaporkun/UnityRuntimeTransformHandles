using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Handles scale manipulation along a single axis.
    /// </summary>
    public class ScaleAxis : HandleBase
    {
        private const float ScaleCubeSize = 0.75f;

        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer cubeMeshRenderer;
        [SerializeField] private MeshRenderer lineMeshRenderer;

        private Camera _handleCamera;

        private Vector3 _axis;
        private Vector3 _startScale;
        private Vector2 _startMousePosition;

        private float _cubeRestDistance;
        private float _cubeHalfExtent;
        private float _lineMeshLength;
        private float _lastDelta = float.NaN;

        private Material _cubeMaterial;
        private Material _lineMaterial;

        /// <summary>
        /// Initializes the scale axis component.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        /// <param name="axis">The axis of scaling.</param>
        public void Initialize(Handle handle, Vector3 axis)
        {
            ParentHandle = handle;
            _axis = axis;
            DefaultColor = defaultColor;

            _handleCamera = ParentHandle.HandleCamera;

            // Instantiate materials once; Initialize is re-runnable via Handle.ChangeAxes
            // and MeshRenderer.material allocates a new instance on every access.
            if (_cubeMaterial == null) _cubeMaterial = cubeMeshRenderer.material;
            if (_lineMaterial == null) _lineMaterial = lineMeshRenderer.material;

            Delta = 0f;
            _lastDelta = float.NaN;
            CacheVisualRestLengths();
            ApplyVisualDelta(1f);
        }

        private void CacheVisualRestLengths()
        {
            _cubeRestDistance = Mathf.Abs(Vector3.Dot(cubeMeshRenderer.transform.localPosition, _axis));
            if (_cubeRestDistance <= 0f)
                _cubeRestDistance = ScaleCubeSize;

            var meshFilter = lineMeshRenderer.GetComponent<MeshFilter>();
            var mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            _lineMeshLength = mesh != null ? mesh.bounds.size.y : 0f;
            if (_lineMeshLength <= 0f)
                _lineMeshLength = ScaleCubeSize;

            var cubeMeshFilter = cubeMeshRenderer.GetComponent<MeshFilter>();
            var cubeMesh = cubeMeshFilter != null ? cubeMeshFilter.sharedMesh : null;
            var cubeMeshSize = cubeMesh != null ? cubeMesh.bounds.size : Vector3.one;
            var cubeScale = cubeMeshRenderer.transform.localScale;
            var cubeExtents = Vector3.Scale(cubeMeshSize, cubeScale) * 0.5f;
            _cubeHalfExtent = Mathf.Abs(Vector3.Dot(cubeExtents, _axis));
        }

        private void ApplyVisualDelta(float scaleFactor)
        {
            var cubeReach = _cubeRestDistance * scaleFactor;
            var lineScaleY = HandleTransformUtility.LineScaleForCubeReach(cubeReach, _cubeHalfExtent, _lineMeshLength);
            lineMeshRenderer.transform.localScale = new Vector3(1f, lineScaleY, 1f);
            cubeMeshRenderer.transform.localPosition = _axis * cubeReach;
        }

        protected void Update()
        {
            // Skip redundant transform writes when delta hasn't changed (e.g. idle handle, delta == 0).
            if (Delta == _lastDelta) return;
            _lastDelta = Delta;

            ApplyVisualDelta(1f + Delta);
        }

        /// <inheritdoc/>
        public override void Interact(Vector3 previousPosition)
        {
            if (_handleCamera == null) return;

            var position = ParentHandle.Target.position;
            var direction = GetRotatedAxis(_axis);
            var handleSize = HandleTransformUtility.GetHandleSize(position, _handleCamera);
            var lineTranslation = HandleTransformUtility.CalcLineTranslation(
                _startMousePosition,
                InputWrapper.MousePosition,
                position,
                direction,
                _handleCamera);

            // Unity SliderScale.DoAxis: dist = 1 + CalcLineTranslation(...) / handleSize; scale = start * dist.
            var dist = 1f + lineTranslation / handleSize;

            var snap = Mathf.Abs(Vector3.Dot(ParentHandle.ScaleSnap, _axis));
            if (snap != 0)
            {
                if (ParentHandle.SnappingType == SnappingType.Relative)
                {
                    dist = SnapUtils.Snap(dist, snap);
                }
                else
                {
                    var axisStartScale = GetAxisStartScale();
                    if (axisStartScale > 0f)
                        dist = SnapUtils.Snap(axisStartScale * dist, snap) / axisStartScale;
                    else
                        dist = SnapUtils.Snap(dist, snap);
                }
            }

            Delta = dist - 1f;
            var scale = Vector3.Scale(_startScale, _axis * Delta + Vector3.one);

            ParentHandle.Target.localScale = scale;

            base.Interact(previousPosition);
        }

        /// <inheritdoc/>
        public override void StartInteraction(Vector3 hitPoint)
        {
            base.StartInteraction(hitPoint);
            _startScale = ParentHandle.Target.localScale;
            _startMousePosition = InputWrapper.MousePosition;
        }

        /// <inheritdoc/>
        public override void EndInteraction()
        {
            base.EndInteraction();
            _lastDelta = float.NaN;
            ApplyVisualDelta(1f);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Destroy the material instances created in Initialize; renderer.material clones
            // leak per handle create/destroy cycle otherwise.
            if (_cubeMaterial != null) Destroy(_cubeMaterial);
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        /// <inheritdoc/>
        public override void SetColor(Color color)
        {
            if (_cubeMaterial.color != color) _cubeMaterial.color = color;
            if (_lineMaterial.color != color) _lineMaterial.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            if (_cubeMaterial.color != DefaultColor) _cubeMaterial.color = DefaultColor;
            if (_lineMaterial.color != DefaultColor) _lineMaterial.color = DefaultColor;
        }

        private float GetAxisStartScale()
        {
            if (Mathf.Abs(_axis.x) > 0.5f) return Mathf.Abs(_startScale.x);
            if (Mathf.Abs(_axis.y) > 0.5f) return Mathf.Abs(_startScale.y);
            return Mathf.Abs(_startScale.z);
        }
    }
}
