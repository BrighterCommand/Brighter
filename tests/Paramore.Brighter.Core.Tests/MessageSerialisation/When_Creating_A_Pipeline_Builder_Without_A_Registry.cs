using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class TransformPipelineMissingRegistryTests
{
    [Fact]
    public void When_Creating_A_Pipeline_Builder_Without_A_Registry()
    {
         //arrange
         var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransformAsync()));

         //act
         var exception = Catch.Exception(() => new TransformPipelineBuilder(null, messageTransformerFactory));
         
         //assert
         exception.Should().NotBeNull();
         exception.Should().BeOfType<ConfigurationException>();
          
    }
}
