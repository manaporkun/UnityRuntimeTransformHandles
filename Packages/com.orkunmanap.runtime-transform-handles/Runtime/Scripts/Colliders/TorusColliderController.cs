using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Generates and applies a torus mesh to the collider and filter components.
    /// </summary>
    public class TorusColliderController : ColliderControllerBase
    {
        [Header("Torus Settings")]
        [SerializeField] private float radius;
        [SerializeField] private float thickness;
        [SerializeField] private int segmentCount = 32;
        [SerializeField] private int sideCount = 15;

        protected override void UpdateCollider()
        {
            var mesh = MeshUtils.CreateTorus(radius, thickness, segmentCount, sideCount);
            mesh.name = "torus";
            ApplyMesh(mesh);
        }
    }
}
