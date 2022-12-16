using System;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

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
