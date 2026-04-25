using System;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

public class MyDescribableUnwrapWith : UnwrapWithAttribute
{
    public MyDescribableUnwrapWith(int step) : base(step) { }

    public override Type GetHandlerType() => typeof(MyDescribableTransform);
}
