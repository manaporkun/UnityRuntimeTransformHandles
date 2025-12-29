using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Generates and applies a tube mesh to the collider and filter components.
    /// </summary>
    public class TubeColliderController : ColliderControllerBase
    {
        [Header("Tube Settings")]
        [SerializeField] private float height;
        [SerializeField] private float bottomRadius;
        [SerializeField] private float topRadius;
        [SerializeField] private float bottomThickness;
        [SerializeField] private float topThickness;
        [SerializeField] private int sideCount;

        protected override void Awake()
        {
            // TubeColliderController uses local transform by default
            if (colliderTransform == null)
            {
                colliderTransform = transform;
            }
            base.Awake();
        }

        protected override void UpdateCollider()
        {
            var mesh = MeshUtils.CreateTube(
                height,
                sideCount,
                bottomRadius,
                bottomThickness,
                topRadius,
                topThickness);

            mesh.name = "tube";
            ApplyMesh(mesh);
        }
    }
}
