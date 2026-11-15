using System;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class TransformPipelineNullMapperLeaseTests
{
    [Fact]
    public void When_constructing_a_wrap_pipeline_with_a_null_mapper_lease_it_should_throw()
    {
        //arrange, act
        var exception = Assert.Throws<ArgumentNullException>(() => new WrapPipeline<MyTransformableCommand>(
            messageMapperLease: null!,
            messageTransformerFactory: null,
            transformLeases: Array.Empty<Lease<IAmAMessageTransform>>(),
            instrumentationOptions: InstrumentationOptions.All, loggerFactory: Initializer.TestLoggerFactory));

        //assert
        Assert.Equal("messageMapperLease", exception.ParamName);
    }

    [Fact]
    public void When_constructing_an_async_wrap_pipeline_with_a_null_mapper_lease_it_should_throw()
    {
        //arrange, act
        var exception = Assert.Throws<ArgumentNullException>(() => new WrapPipelineAsync<MyTransformableCommand>(
            messageMapperLease: null!,
            messageTransformerFactoryAsync: null,
            transformLeases: Array.Empty<Lease<IAmAMessageTransformAsync>>(),
            instrumentationOptions: InstrumentationOptions.All, loggerFactory: Initializer.TestLoggerFactory));

        //assert
        Assert.Equal("messageMapperLease", exception.ParamName);
    }
}
