﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class AsyncMessageWrapCleanupTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    public static string s_released;
    private readonly Publication _publication;

    public AsyncMessageWrapCleanupTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyTransformableCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyTransformableCommand, MyTransformableCommandMessageMapperAsync>();

        _myCommand = new MyTransformableCommand();
        
        _publication = new Publication{Topic = new RoutingKey("MyTransformableCommand"), RequestType= typeof(MyTransformableCommand)};
        
        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, new MyReleaseTrackingTransformFactoryAsync());
    }
    
    [Fact]
    public async Task When_Wrapping_Clean_Up_The_Pipeline()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand, _publication);
        _transformPipeline.Dispose();
        
        //assert
        s_released.Should().Be("|MySimpleTransformAsync");

    }
    
    private class MyReleaseTrackingTransformFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        public IAmAMessageTransformAsync Create(Type transformerType)
        {
            return new MySimpleTransformAsync();
        }

        public void Release(IAmAMessageTransformAsync transformer)
        {
            var disposable = transformer as IDisposable;
            disposable?.Dispose();

            s_released += "|" + transformer.GetType().Name;
        }
    }

}
