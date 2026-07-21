using System;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;
public class MessageWrapCleanupTests
{
    private WrapPipeline<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    public static string s_released;
    private readonly Publication _publication;
    public MessageWrapCleanupTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()), null);
        mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();
        _myCommand = new MyTransformableCommand();
        _publication = new Publication
        {
            Topic = new RoutingKey("MyTransformableCommand")
        };
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, new MyReleaseTrackingTransformFactory());
    }

    [Test]
    public async Task When_Wrapping_Clean_Up_The_Pipeline()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = _transformPipeline.Wrap(_myCommand, new RequestContext(), _publication);
        _transformPipeline.Dispose();
        //assert
        await Assert.That(s_released).IsEqualTo("|MySimpleTransform");
    }

    private sealed class MyReleaseTrackingTransformFactory : Paramore.Brighter.IAmAMessageTransformerFactory
    {
        public IAmAMessageTransform Create(Type transformerType)
        {
            return new MySimpleTransform();
        }

        public void Release(IAmAMessageTransform transformer)
        {
            var disposable = transformer as IDisposable;
            disposable?.Dispose();
            s_released += "|" + transformer.GetType().Name;
        }
    }
}