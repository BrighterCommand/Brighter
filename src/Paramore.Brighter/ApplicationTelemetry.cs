using System.Diagnostics;
using System.Reflection;

namespace Paramore.Brighter
{
    internal static class ApplicationTelemetry
    {
        internal static ActivitySource ActivitySource { get; }= new ActivitySource("Paramore.Brighter", Assembly.GetAssembly(typeof(ApplicationTelemetry)).GetName().Version.ToString());
    }
}
