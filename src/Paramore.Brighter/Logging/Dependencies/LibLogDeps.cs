using System;
using System.Diagnostics.CodeAnalysis;

namespace Paramore.Brighter.Logging.Dependencies
{
     //HACK! This is a dummy method, it is intended to allow us to force some deps that LibLog needs, so that we can build
     //TODO: Remove LibLog, rely on .NET defined logging interface
     [SuppressMessage("Microsoft.Performance", "CA1812: Avoid uninstantiated internal classes")]
    internal class LibLogDeps
    {
    }
}
