namespace TransformHandles
{
    /// <summary>
    /// Defines the types of snapping behavior for transformations.
    /// </summary>
    public enum SnappingType
    {
        /// <summary>
        /// Snaps to fixed points in world space, regardless of the object's current position.
        /// For example, with a grid size of 1, positions will always snap to whole numbers (0, 1, 2, etc.).
        /// </summary>
        Absolute,

        /// <summary>
        /// Snaps relative to the object's current position.
        /// Movement is quantized to multiples of the grid size from the starting position.
        /// For example, with a grid size of 1, an object at 0.5 would snap to 1.5, 2.5, etc.
        /// </summary>
        Relative
    }
}