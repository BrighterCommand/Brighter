using System;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

public class MyDescribableWrapWith : WrapWithAttribute
{
    public MyDescribableWrapWith(int step) : base(step) { }

    public override Type GetHandlerType() => typeof(MyDescribableTransform);
}
