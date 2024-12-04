using System;
using System.Diagnostics;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal class MyCommandToFail() : Command(Guid.NewGuid().ToString());
}
