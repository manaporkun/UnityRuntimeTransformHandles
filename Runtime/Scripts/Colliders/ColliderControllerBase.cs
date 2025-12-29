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
        [SerializeField] protected KeyCode updateKey = KeyCode.K;

        protected MeshCollider MeshCollider;
        protected MeshFilter MeshFilter;

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
            if (InputWrapper.GetKeyDown(updateKey))
            {
                UpdateCollider();
            }
        }

        /// <summary>
        /// Updates the collider mesh. Override this method to create the specific mesh type.
        /// </summary>
        protected abstract void UpdateCollider();

        /// <summary>
        /// Applies the generated mesh to the mesh filter and collider.
        /// </summary>
        /// <param name="mesh">The mesh to apply.</param>
        protected void ApplyMesh(Mesh mesh)
        {
            if (MeshFilter != null)
            {
                MeshFilter.sharedMesh = mesh;
            }

            if (MeshCollider != null)
            {
                MeshCollider.sharedMesh = mesh;
            }
        }
    }
}
