using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

// public class AsyncOutboxGenerator(ILogger<AsyncOutboxGenerator> logger) : BaseGenerator(logger)
// {
//     protected override string FolderName { get; } = Path.Combine("Outbox", "Async");
//     
//     public async Task GenerateAsync(TestConfigurationConfiguration configuration)
//     {
//         if (configuration.Outbox != null)
//         {
//             logger.LogInformation("Generating outbox test");
//             await GenerateAsync(configuration, configuration.Prefix, configuration.Outbox);
//         }
//         else if (configuration.Outboxes != null)
//         {
//             foreach (var (key, outboxConfiguration) in configuration.Outboxes)
//             {
//                 logger.LogInformation("Generating outbox test for {OutboxName}", key);
//                 await GenerateAsync(configuration, key, outboxConfiguration);
//             }
//         }
//         else
//         {
//             logger.LogInformation("No outbox configured");
//         }
//     }
//
//     protected override Task GenerateAsync(TestConfigurationConfiguration configuration, string prefix, object model)
//     {
//         var outboxConfiguration = (OutboxConfiguration)model;
//         if (string.IsNullOrEmpty(outboxConfiguration.MessageFactory))
//         {
//             outboxConfiguration.MessageFactory = configuration.MessageFactory;
//         }
//
//         if (string.IsNullOrEmpty(outboxConfiguration.Namespace))
//         {
//             outboxConfiguration.Namespace = configuration.Namespace;
//         }
//         
//         if (!string.IsNullOrEmpty(outboxConfiguration.Prefix))
//         {
//             prefix = outboxConfiguration.Prefix;
//         }
//
//         outboxConfiguration.Prefix = $".{prefix}";
//         
//         return base.GenerateAsync(configuration, prefix, model);
//     }
// }
