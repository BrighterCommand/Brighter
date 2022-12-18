using System;

namespace Paramore.Brighter.Core.Tests.FeatureSwitch.TestDoubles;

public class MyCommandAsync : Command
{
    public MyCommandAsync() : base(Guid.NewGuid())
    {
    }
}
