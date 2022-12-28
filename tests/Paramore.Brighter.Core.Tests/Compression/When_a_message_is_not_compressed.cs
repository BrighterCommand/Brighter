using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class UncompressedPayloadTests
{
 

    public UncompressedPayloadTests()
    {
        
    }
    
    [Fact]
    public void When_a_message_is_not_compressed()
    {
        //we should confirm that a payload is compressed before decompressing, as it might have fallen below the threshold
    }
}
