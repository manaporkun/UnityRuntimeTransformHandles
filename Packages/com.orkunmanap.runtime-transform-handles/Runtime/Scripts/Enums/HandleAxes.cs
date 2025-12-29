namespace TransformHandles
{
    /// <summary>
    /// Defines which axes are active for a handle.
    /// </summary>
    public enum HandleAxes
    {
        /// <summary>X axis only (red).</summary>
        X,
        /// <summary>Y axis only (green).</summary>
        Y,
        /// <summary>Z axis only (blue).</summary>
        Z,
        /// <summary>X and Y axes (red and green).</summary>
        XY,
        /// <summary>X and Z axes (red and blue).</summary>
        XZ,
        /// <summary>Y and Z axes (green and blue).</summary>
        YZ,
        /// <summary>All three axes (X, Y, and Z).</summary>
        XYZ
    }
}