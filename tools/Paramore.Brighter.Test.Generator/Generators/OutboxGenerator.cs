using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

public class OutboxGenerator(ILogger<OutboxGenerator> logger) : BaseGenerator(logger)
{
    
    public async Task GenerateAsync(TestConfigurationConfiguration configuration)
    {
        if (configuration.Outbox != null)
        {
            await GenerateAsync(configuration, 
                "Outbox",
                "Outbox",
                configuration.Outbox);
            
            var prefix = configuration.Outbox.Prefix;
            await GenerateAsync(configuration, 
                Path.Combine("Outbox", prefix, "Sync"),
                Path.Combine("Outbox", "Sync"),
                configuration.Outbox,
                filename => SkipTest(configuration.Outbox, filename));
        }
        else if (configuration.Outboxes != null)
        {
            foreach (var (key, outboxConfiguration) in configuration.Outboxes)
            {
                logger.LogInformation("Generating outbox test for {OutboxName}", key);
                var prefix = outboxConfiguration.Prefix;
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = key;
                }
                
                outboxConfiguration.Prefix = $".{prefix}";

                await GenerateAsync(configuration,
                    Path.Combine("Outbox", prefix),
                    "Outbox", 
                    outboxConfiguration,
                    filename => SkipTest(outboxConfiguration, filename));
                
                await GenerateAsync(configuration, 
                    Path.Combine("Outbox", prefix, "Generated", "Sync"),
                    Path.Combine("Outbox", "Sync"),
                    outboxConfiguration,
                    filename => SkipTest(outboxConfiguration, filename));
                
                await GenerateAsync(configuration, 
                    Path.Combine("Outbox", prefix, "Generated", "Async"),
                    Path.Combine("Outbox", "Async"),
                    outboxConfiguration,
                    filename => SkipTest(outboxConfiguration, filename));
            }
        }
        else
        {
            logger.LogInformation("No outbox configured");
        }
        
    }
    
    private static bool SkipTest(OutboxConfiguration outboxConfiguration, string fileName)
    {
        if (!outboxConfiguration.HasSupportToTransaction && fileName.Contains("Transaction"))
        {
            return true;
        }

        return false;
    }
    
    protected override Task GenerateAsync(TestConfigurationConfiguration configuration, 
        string prefix, string templateFolderName,
        object model, Func<string, bool>? ignore = null)
    {
        if (model is OutboxConfiguration outboxConfiguration)
        {
            if (string.IsNullOrEmpty(outboxConfiguration.MessageFactory))
            {
                outboxConfiguration.MessageFactory = configuration.MessageFactory;
            }

            if (string.IsNullOrEmpty(outboxConfiguration.Namespace))
            {
                outboxConfiguration.Namespace = configuration.Namespace;
            }
        }

        return base.GenerateAsync(configuration, prefix, templateFolderName, model);
    }
}
