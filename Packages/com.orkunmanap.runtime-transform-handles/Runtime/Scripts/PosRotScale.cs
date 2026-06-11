using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace TransformHandles
{
    /// <summary>
    /// Plain position/rotation/scale triple used to describe a group's average transform.
    /// </summary>
    public struct PosRotScale
    {
        /// <summary>World-space position.</summary>
        public Vector3 Position;
        /// <summary>World-space rotation.</summary>
        public Quaternion Rotation;
        /// <summary>Local scale.</summary>
        public Vector3 Scale;
    }
}