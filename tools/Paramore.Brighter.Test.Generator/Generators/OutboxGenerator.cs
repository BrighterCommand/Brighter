using System;
using System.IO;
using System.Security;
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
                configuration.Outbox);
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
                    outboxConfiguration);
                
                await GenerateAsync(configuration, 
                    Path.Combine("Outbox", prefix, "Generated", "Sync"),
                    Path.Combine("Outbox", "Sync"),
                    outboxConfiguration);
                
                await GenerateAsync(configuration, 
                    Path.Combine("Outbox", prefix, "Generated", "Async"),
                    Path.Combine("Outbox", "Async"),
                    outboxConfiguration);
            }
        }
        else
        {
            logger.LogInformation("No outbox configured");
        }
    }

    protected override Task GenerateAsync(TestConfigurationConfiguration configuration, string prefix, string templateFolderName,
        object model)
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
