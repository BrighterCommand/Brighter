using System.Diagnostics;
using System.Reflection;

namespace Paramore.Brighter.Observability;

public static class BrighterTracer
{
    private static readonly AssemblyName AssemblyName = typeof(BrighterTracer).Assembly.GetName();
    public static readonly string SourceName = AssemblyName.Name;
    public static readonly string Version = AssemblyName.Version.ToString();
    public static readonly ActivitySource ActivitySource = new (SourceName, Version); 
}
