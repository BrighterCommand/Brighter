using System;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Observability;


namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class TransformPipelineNullMapperLeaseTests
{
    [Test]
    public async System.Threading.Tasks.Task When_constructing_a_wrap_pipeline_with_a_null_mapper_lease_it_should_throw()
    {
        //arrange, act
        var exception = await Assert.That(() => new WrapPipeline<MyTransformableCommand>(
            messageMapperLease: null!,
            messageTransformerFactory: null,
            transformLeases: Array.Empty<Lease<IAmAMessageTransform>>(),
            instrumentationOptions: InstrumentationOptions.All)).ThrowsExactly<ArgumentNullException>();

        //assert
        await Assert.That(exception.ParamName).IsEqualTo("messageMapperLease");
    }

    [Test]
    public async System.Threading.Tasks.Task When_constructing_an_async_wrap_pipeline_with_a_null_mapper_lease_it_should_throw()
    {
        //arrange, act
        var exception = await Assert.That(() => new WrapPipelineAsync<MyTransformableCommand>(
            messageMapperLease: null!,
            messageTransformerFactoryAsync: null,
            transformLeases: Array.Empty<Lease<IAmAMessageTransformAsync>>(),
            instrumentationOptions: InstrumentationOptions.All)).ThrowsExactly<ArgumentNullException>();

        //assert
        await Assert.That(exception.ParamName).IsEqualTo("messageMapperLease");
    }
}
