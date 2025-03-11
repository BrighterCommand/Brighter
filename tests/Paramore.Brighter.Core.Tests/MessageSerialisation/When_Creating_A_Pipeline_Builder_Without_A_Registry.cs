using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
 public class TransformPipelineMissingRegistryTests
{
    [Fact]
    public void When_Creating_A_Pipeline_Builder_Without_A_Registry()
    {
         //arrange
          TransformPipelineBuilder.ClearPipelineCache();
          
          var messageTransformerFactory = new SimpleMessageTransformerFactory((_ => new MySimpleTransform()));

         //act
         var exception = Catch.Exception(() => new TransformPipelineBuilder(null, messageTransformerFactory));
         
         //assert
         Assert.NotNull(exception);
         Assert.True((exception) is ConfigurationException);
          
    }
}
