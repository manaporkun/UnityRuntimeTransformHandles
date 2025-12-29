using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Generates and applies a cone mesh to the collider and filter components.
    /// </summary>
    public class ConeColliderController : ColliderControllerBase
    {
        [Header("Cone Settings")]
        [SerializeField] private float height;
        [SerializeField] private float bottomRadius;
        [SerializeField] private float topRadius = 0.02f;
        [SerializeField] private int sideCount = 15;
        [SerializeField] private int heightSegmentCount = 1;

        protected override void UpdateCollider()
        {
            var mesh = MeshUtils.CreateCone(
                height,
                bottomRadius,
                topRadius,
                sideCount,
                heightSegmentCount);

            mesh.name = "cone";
            ApplyMesh(mesh);
        }
    }
}
