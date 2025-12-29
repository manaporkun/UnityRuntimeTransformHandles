namespace TransformHandles
{
    /// <summary>
    /// Defines the origin point for handle transformations.
    /// </summary>
    public enum Origin
    {
        /// <summary>Use the object's pivot point as the transformation origin.</summary>
        Pivot = 0,
        /// <summary>Use the object's bounds center as the transformation origin.</summary>
        Center = 1
    }
}