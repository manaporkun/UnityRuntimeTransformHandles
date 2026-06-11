using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Handles uniform scale manipulation across all axes.
    /// </summary>
    public class ScaleGlobal : HandleBase
    {
        [SerializeField] private Color defaultColor;
        [SerializeField] private MeshRenderer cubeMeshRenderer;

        private Vector3 _axis;
        private Vector3 _startScale;
        private Vector2 _startMousePosition;
        private Vector3 _uniformScaleDirection;
        private Material _cubeMaterial;

        /// <summary>
        /// Initializes the global scale component.
        /// </summary>
        /// <param name="handle">The parent handle.</param>
        /// <param name="axis">The axes to scale along.</param>
        public void Initialize(Handle handle, Vector3 axis)
        {
            ParentHandle = handle;
            _axis = axis;
            DefaultColor = defaultColor;

            // Instantiate the material once; Initialize is re-runnable via Handle.ChangeAxes
            // and MeshRenderer.material allocates a new instance on every access.
            if (_cubeMaterial == null) _cubeMaterial = cubeMeshRenderer.material;
        }

        /// <inheritdoc/>
        public override void Interact(Vector3 previousPosition)
        {
            var camera = ParentHandle.HandleCamera;
            if (camera == null) return;

            var position = ParentHandle.Target.position;
            var handleSize = HandleTransformUtility.GetHandleSize(position, camera);
            var lineTranslation = HandleTransformUtility.CalcLineTranslation(
                _startMousePosition,
                InputWrapper.MousePosition,
                position,
                _uniformScaleDirection,
                camera);

            // Unity SliderScale.DoCenter: value = (Snap(CalcLineTranslation(...) / size) + 1) * startScale.
            var dist = lineTranslation / handleSize;
            var snap = GetActiveScaleSnap();
            if (snap != 0)
            {
                if (ParentHandle.SnappingType == SnappingType.Relative)
                    dist = SnapUtils.Snap(dist, snap);
                else
                    dist = SnapUtils.Snap(dist + 1f, snap) - 1f;
            }

            Delta = dist;
            ParentHandle.Target.localScale = _startScale + Vector3.Scale(_startScale, _axis) * Delta;

            base.Interact(previousPosition);
        }

        /// <inheritdoc/>
        public override void StartInteraction(Vector3 hitPoint)
        {
            base.StartInteraction(hitPoint);
            _startScale = ParentHandle.Target.localScale;
            _startMousePosition = InputWrapper.MousePosition;

            var camera = ParentHandle.HandleCamera;
            if (camera == null)
            {
                _uniformScaleDirection = Vector3.one;
                return;
            }

            var cameraTransform = camera.transform;
            _uniformScaleDirection = (cameraTransform.right + cameraTransform.up).normalized;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Destroy the material instance created in Initialize; renderer.material clones
            // leak per handle create/destroy cycle otherwise.
            if (_cubeMaterial != null) Destroy(_cubeMaterial);
        }

        /// <inheritdoc/>
        public override void SetColor(Color color)
        {
            if (_cubeMaterial.color != color) _cubeMaterial.color = color;
        }

        /// <inheritdoc/>
        public override void SetDefaultColor()
        {
            if (_cubeMaterial.color != DefaultColor) _cubeMaterial.color = DefaultColor;
        }

        private float GetActiveScaleSnap()
        {
            var snap = ParentHandle.ScaleSnap;
            var max = 0f;
            if (_axis.x > 0f) max = Mathf.Max(max, snap.x);
            if (_axis.y > 0f) max = Mathf.Max(max, snap.y);
            if (_axis.z > 0f) max = Mathf.Max(max, snap.z);
            return max;
        }
    }
}
