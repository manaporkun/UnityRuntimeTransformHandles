using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace TransformHandles
{
    public struct PosRotScale
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        
        /// <summary>
        /// Initializes a new instance of the PosRotScale struct.
        /// </summary>
        /// <param name="position">The position in 3D space.</param>
        /// <param name="rotation">The rotation as a quaternion.</param>
        /// <param name="scale">The scale in 3D space.</param>
        public PosRotScale(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        /// <summary>
        /// Creates a PosRotScale struct from a Transform component.
        /// </summary>
        /// <param name="transform">The Transform to create the PosRotScale from.</param>
        /// <returns>A new PosRotScale struct with the Transform's position, rotation, and local scale.</returns>
        public static PosRotScale FromTransform(Transform transform)
        {
            return new PosRotScale(transform.position, transform.rotation, transform.lossyScale);
        }

        /// <summary>
        /// Applies the position, rotation, and scale to a Transform component.
        /// </summary>
        /// <param name="transform">The Transform to apply the PosRotScale to.</param>
        public void ApplyToTransform(Transform transform)
        {
            transform.SetPositionAndRotation(Position, Rotation);
            transform.localScale = Scale;
        }
    }
}