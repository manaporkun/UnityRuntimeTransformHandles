using System.Runtime.CompilerServices;

// The editor assembly and the test assemblies consume internal members of the runtime
// assembly (e.g. Ghost interaction callbacks, TransformGroup maps/update methods).
[assembly: InternalsVisibleTo("TransformHandles.Editor")]
[assembly: InternalsVisibleTo("TransformHandles.Tests.Editor")]
[assembly: InternalsVisibleTo("TransformHandles.Tests.Runtime")]
