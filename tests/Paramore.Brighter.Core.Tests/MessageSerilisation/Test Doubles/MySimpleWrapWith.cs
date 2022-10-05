using System;

namespace Paramore.Brighter.Core.Tests.MessageSerilisation.Test_Doubles;

public class MySimpleWrapWith: WrapWithAttribute
{
    public MySimpleWrapWith(int step) : base(step)
    {
    }

    public override Type GetHandlerType()
    {
        throw new NotImplementedException();
    }
}
