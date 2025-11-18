using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

public class SharedGenerator(ILogger<SharedGenerator> logger) : BaseGenerator(logger)
{
    public async Task GenerateAsync(TestConfigurationConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.MessageFactory))
        {
            configuration.MessageFactory = "DefaultMessageFactory";
        }
        
        logger.LogInformation("Generating shared class for testing");
        await GenerateAsync(configuration,
            "",
            "",
            configuration);
    }
}
