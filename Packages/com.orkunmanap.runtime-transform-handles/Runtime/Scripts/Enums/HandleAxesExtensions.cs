namespace TransformHandles
{
    /// <summary>
    /// Extension methods for HandleAxes enum to simplify axis checking.
    /// </summary>
    public static class HandleAxesExtensions
    {
        /// <summary>
        /// Checks if the specified axis is included in this axes configuration.
        /// </summary>
        /// <param name="axes">The axes configuration to check.</param>
        /// <param name="axis">The single axis to check for (X, Y, or Z).</param>
        /// <returns>True if the axis is included, false otherwise.</returns>
        public static bool HasAxis(this HandleAxes axes, HandleAxes axis)
        {
            return axis switch
            {
                HandleAxes.X => axes is HandleAxes.X or HandleAxes.XY or HandleAxes.XZ or HandleAxes.XYZ,
                HandleAxes.Y => axes is HandleAxes.Y or HandleAxes.XY or HandleAxes.YZ or HandleAxes.XYZ,
                HandleAxes.Z => axes is HandleAxes.Z or HandleAxes.XZ or HandleAxes.YZ or HandleAxes.XYZ,
                _ => false
            };
        }

        /// <summary>
        /// Checks if both specified axes are included in this axes configuration.
        /// </summary>
        /// <param name="axes">The axes configuration to check.</param>
        /// <param name="axis1">The first axis to check for.</param>
        /// <param name="axis2">The second axis to check for.</param>
        /// <returns>True if both axes are included, false otherwise.</returns>
        public static bool HasBothAxes(this HandleAxes axes, HandleAxes axis1, HandleAxes axis2)
        {
            return axes.HasAxis(axis1) && axes.HasAxis(axis2);
        }

        /// <summary>
        /// Checks if this is a multi-axis configuration (more than one axis).
        /// </summary>
        /// <param name="axes">The axes configuration to check.</param>
        /// <returns>True if multiple axes are included, false otherwise.</returns>
        public static bool IsMultiAxis(this HandleAxes axes)
        {
            return axes is HandleAxes.XY or HandleAxes.XZ or HandleAxes.YZ or HandleAxes.XYZ;
        }
    }
}
