﻿using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class AsyncMessageWrapRequestTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyTransformableCommand _myCommand;
    private readonly Publication _publication;

    public AsyncMessageWrapRequestTests()
    {
        //arrange
         TransformPipelineBuilder.ClearPipelineCache();

         var mapperRegistry = new MessageMapperRegistry(
             null,
             new SimpleMessageMapperFactoryAsync(_ => new MyTransformableCommandMessageMapperAsync()));
         mapperRegistry.RegisterAsync<MyTransformableCommand, MyTransformableCommandMessageMapperAsync>();

        _myCommand = new MyTransformableCommand();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => new MySimpleTransformAsync()));
        
        _publication = new Publication{Topic = new RoutingKey("MyTransformableCommand"), RequestType= typeof(MyTransformableCommand)};

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);
    }
    
    [Fact]
    public async Task When_Wrapping_A_Message_Mapper()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand, _publication);
        
        //assert
        message.Body.Value.Should().Be(JsonSerializer.Serialize(_myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)).ToString());
        message.Header.Bag[MySimpleTransformAsync.HEADER_KEY].Should().Be(MySimpleTransformAsync.TRANSFORM_VALUE);
    }
}
