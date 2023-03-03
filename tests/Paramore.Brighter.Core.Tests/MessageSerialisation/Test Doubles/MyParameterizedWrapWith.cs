using System;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyParameterizedWrapWith : WrapWithAttribute
{
    private readonly string _displayFormat;

    public MyParameterizedWrapWith(int step, string displayFormat) : base(step)
    {
        _displayFormat = displayFormat;
    }

    public override Type GetHandlerType()
    {
        return typeof(MyParameterizedTransformAsync);
    }

    public override object[] InitializerParams()
    {
        return new[] { _displayFormat };
    }
}
