using System;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MySimpleWrapWith: WrapWithAttribute
{
    public MySimpleWrapWith(int step) : base(step)
    {
    }

    public override Type GetHandlerType()
    {
        return typeof(MySimpleTransformAsync);
    }
}
