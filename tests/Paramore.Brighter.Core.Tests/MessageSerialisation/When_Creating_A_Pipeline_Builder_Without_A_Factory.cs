using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class TransformPipelineMissingFactoryTests
{
    [Fact]
    public void When_Creating_A_Pipeline_Builder_Without_A_Factory()
    {
          //arrange
         var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()))
             { { typeof(MyTransformableCommand), typeof(MyTransformableCommandMessageMapper) } };

          //act
          var exception = Catch.Exception(() => new TransformPipelineBuilder(mapperRegistry, null));
          
          //assert
          exception.Should().NotBeNull();
          exception.Should().BeOfType<ConfigurationException>();
           
    }
    
}
