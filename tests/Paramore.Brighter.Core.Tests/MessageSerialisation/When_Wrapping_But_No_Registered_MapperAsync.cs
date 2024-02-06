﻿using System;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
public class AsyncMessageWrapRequestMissingMapperTests
{
    private WrapPipelineAsync<MyTransformableCommand> _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;

    public AsyncMessageWrapRequestMissingMapperTests()
    {
        //arrange
         TransformPipelineBuilder.ClearPipelineCache();

         var mapperRegistry = new MessageMapperRegistry(
             new SimpleMessageMapperFactory(_ => null),
             null);
         mapperRegistry.Register<MyTransformableCommand, MyTransformableCommandMessageMapper>();

        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => new MySimpleTransformAsync()));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);
    }
    
    [Fact]
    public void When_Wrapping_But_No_Registered_Mapper()
    {
        //act
        var exception = Catch.Exception(() => _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyTransformableCommand>());
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ConfigurationException>();
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
    }
}
