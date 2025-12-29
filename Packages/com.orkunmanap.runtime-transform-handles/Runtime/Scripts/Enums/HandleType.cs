namespace TransformHandles
{
    /// <summary>
    /// Defines the type of transformation a handle can perform.
    /// </summary>
    public enum HandleType
    {
        /// <summary>Move objects along axes or planes.</summary>
        Position,
        /// <summary>Rotate objects around axes.</summary>
        Rotation,
        /// <summary>Scale objects along axes or uniformly.</summary>
        Scale,
        /// <summary>Combine position and rotation handles.</summary>
        PositionRotation,
        /// <summary>Combine position and scale handles.</summary>
        PositionScale,
        /// <summary>Combine rotation and scale handles.</summary>
        RotationScale,
        /// <summary>Show all handle types (position, rotation, and scale).</summary>
        All
    }
}