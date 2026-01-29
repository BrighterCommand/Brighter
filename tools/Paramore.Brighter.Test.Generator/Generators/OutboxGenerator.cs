#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Generators;

public class OutboxGenerator(ILogger<OutboxGenerator> logger) : BaseGenerator(logger)
{
    public async Task GenerateAsync(TestConfiguration configuration)
    {
        if (configuration.Outbox != null)
        {
            await GenerateAsync(configuration, "Outbox", "Outbox", configuration.Outbox);

            var prefix = configuration.Outbox.Prefix;
            await GenerateAsync(
                configuration,
                Path.Combine("Outbox", prefix, "Generated", "Sync"),
                Path.Combine("Outbox", "Sync"),
                configuration.Outbox,
                filename => SkipTest(configuration.Outbox, filename)
            );

            await GenerateAsync(
                configuration,
                Path.Combine("Outbox", prefix, "Generated", "Async"),
                Path.Combine("Outbox", "Async"),
                configuration.Outbox,
                filename => SkipTest(configuration.Outbox, filename)
            );
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

                await GenerateAsync(
                    configuration,
                    Path.Combine("Outbox", prefix),
                    "Outbox",
                    outboxConfiguration,
                    filename => SkipTest(outboxConfiguration, filename)
                );

                await GenerateAsync(
                    configuration,
                    Path.Combine("Outbox", prefix, "Generated", "Sync"),
                    Path.Combine("Outbox", "Sync"),
                    outboxConfiguration,
                    filename => SkipTest(outboxConfiguration, filename)
                );

                await GenerateAsync(
                    configuration,
                    Path.Combine("Outbox", prefix, "Generated", "Async"),
                    Path.Combine("Outbox", "Async"),
                    outboxConfiguration,
                    filename => SkipTest(outboxConfiguration, filename)
                );
            }
        }
        else
        {
            logger.LogInformation("No outbox configured");
        }
    }

    private static bool SkipTest(OutboxConfiguration outboxConfiguration, string fileName)
    {
        if (
            !outboxConfiguration.SupportsTransactions
            && fileName.Contains("Transaction", StringComparison.InvariantCultureIgnoreCase)
        )
        {
            return true;
        }

        return false;
    }

    protected override Task GenerateAsync(
        TestConfiguration configuration,
        string prefix,
        string templateFolderName,
        object model,
        Func<string, bool>? ignore = null
    )
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

        return base.GenerateAsync(configuration, prefix, templateFolderName, model, ignore);
    }
}
