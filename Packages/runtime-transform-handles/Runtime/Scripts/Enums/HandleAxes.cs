using System;

namespace TransformHandles
{
    /// <summary>
    /// Defines the axes or combinations of axes for handle operations.
    /// </summary>
    [Flags]
    public enum HandleAxes
    {
        None = 0,
        X = 1 << 0,
        Y = 1 << 1,
        Z = 1 << 2,
        XY = X | Y,
        XZ = X | Z,
        YZ = Y | Z,
        XYZ = X | Y | Z
    }
}