using System;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyParameterizedUnwrapWith : UnwrapWithAttribute
{
    private readonly string _template;

    public MyParameterizedUnwrapWith(int step, string template) : base(step)
    {
        _template = template;
    }

    public override Type GetHandlerType()
    {
        return typeof(MyParameterizedTransformAsync);
    }

    public override object[] InitializerParams()
    {
        return new[] { _template };
    }
}
