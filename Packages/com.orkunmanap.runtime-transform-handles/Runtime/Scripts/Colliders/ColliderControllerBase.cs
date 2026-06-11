using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Base class for collider controllers that manage mesh colliders and filters.
    /// Provides common functionality for updating collider meshes at runtime.
    /// </summary>
    public abstract class ColliderControllerBase : MonoBehaviour
    {
        [SerializeField] protected Transform colliderTransform;

        /// <summary>
        /// Development-only hotkey that re-generates the collider mesh. Polled only in the
        /// Editor and in Development Builds; it has no effect in release players.
        /// </summary>
        [SerializeField] protected KeyCode updateKey = KeyCode.K;

        protected MeshCollider MeshCollider;
        protected MeshFilter MeshFilter;

        private Mesh _generatedMesh;

        protected virtual void Awake()
        {
            var targetTransform = colliderTransform != null ? colliderTransform : transform;
            MeshCollider = targetTransform.GetComponent<MeshCollider>();
            MeshFilter = targetTransform.GetComponent<MeshFilter>();
        }

        protected virtual void Start()
        {
            UpdateCollider();
        }

        protected virtual void Update()
        {
            // The refresh hotkey is a development aid; compile the polling out of release
            // players. The method itself stays so consumer overrides keep compiling.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InputWrapper.GetKeyDown(updateKey))
            {
                UpdateCollider();
            }
#endif
        }

        protected virtual void OnDestroy()
        {
            DestroyGeneratedMesh();
        }

        /// <summary>
        /// Updates the collider mesh. Override this method to create the specific mesh type.
        /// </summary>
        protected abstract void UpdateCollider();

        /// <summary>
        /// Applies the generated mesh to the mesh filter and collider, destroying the
        /// previously generated mesh so repeated rebuilds do not leak.
        /// </summary>
        /// <param name="mesh">The mesh to apply. Must be a runtime-generated mesh owned by
        /// this controller; meshes wired in prefabs are assets and are never destroyed here.</param>
        protected void ApplyMesh(Mesh mesh)
        {
            if (mesh != _generatedMesh)
            {
                DestroyGeneratedMesh();
                _generatedMesh = mesh;
            }

            if (MeshFilter != null)
            {
                MeshFilter.sharedMesh = mesh;
            }

            if (MeshCollider != null)
            {
                MeshCollider.sharedMesh = mesh;
            }
        }

        private void DestroyGeneratedMesh()
        {
            if (_generatedMesh == null) return;
            if (Application.isPlaying)
            {
                Destroy(_generatedMesh);
            }
            else
            {
                DestroyImmediate(_generatedMesh);
            }
            _generatedMesh = null;
        }
    }
}
