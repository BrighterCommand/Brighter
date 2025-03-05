using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection("CommandProcessor")]
 public class AsyncTransformPipelineMissingRegistryTests
{
    [Fact]
    public void When_Creating_A_Pipeline_Builder_Without_A_Registry()
    {
         //arrange
          TransformPipelineBuilder.ClearPipelineCache();
          
          var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync((_ => new MySimpleTransformAsync()));

         //act
         var exception = Catch.Exception(() => new TransformPipelineBuilderAsync(null, messageTransformerFactory));
         
         //assert
         Assert.NotNull(exception);
         Assert.True((exception) is ConfigurationException);
          
    }
}
