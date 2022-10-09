using System;

namespace Paramore.Brighter.Core.Tests.MessageSerilisation.Test_Doubles;

public class MySimpleUnwrapWith: UnwrapWithAttribute
{
    public MySimpleUnwrapWith(int step) : base(step)
    {
    }

    public override Type GetHandlerType()
    {
        return typeof(MySimpleTransformAsync);
    }
}
