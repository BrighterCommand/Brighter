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

public class MessageGatewayGenerator(ILogger<MessageGatewayGenerator> logger)
    : BaseGenerator(logger)
{
    public async Task GenerateAsync(TestConfiguration configuration)
    {
        if (configuration.MessagingGateway != null)
        {
            await GenerateAsync(
                configuration,
                "MessagingGateway",
                "MessagingGateway",
                configuration.MessagingGateway
            );

            var prefix = configuration.MessagingGateway.Prefix;
            // await GenerateAsync(
            //     configuration,
            //     Path.Combine("MessagingGateway", prefix, "Generated", "Reactor"),
            //     Path.Combine("MessagingGateway", "Reactor"),
            //     configuration.MessagingGateway,
            //     filename => SkipTest(configuration.MessagingGateway, filename)
            // );

            await GenerateAsync(
                configuration,
                Path.Combine("MessagingGateway", prefix, "Generated", "Proactor"),
                Path.Combine("MessagingGateway", "Proactor"),
                configuration.MessagingGateway,
                filename => SkipTest(configuration.MessagingGateway, filename)
            );
        }
        else if (configuration.MessagingGateways != null)
        {
            foreach (var (key, messagingGatewayConfiguration) in configuration.MessagingGateways)
            {
                logger.LogInformation("Generating outbox test for {OutboxName}", key);
                var prefix = messagingGatewayConfiguration.Prefix;
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = key;
                }

                messagingGatewayConfiguration.Prefix = $".{prefix}";

                await GenerateAsync(
                    configuration,
                    Path.Combine("MessagingGateway", prefix),
                    "MessagingGateway",
                    messagingGatewayConfiguration,
                    filename => SkipTest(messagingGatewayConfiguration, filename)
                );

                await GenerateAsync(
                    configuration,
                    Path.Combine("MessagingGateway", prefix, "Generated", "Reactor"),
                    Path.Combine("MessagingGateway", "Reactor"),
                    messagingGatewayConfiguration,
                    filename => SkipTest(messagingGatewayConfiguration, filename)
                );

                await GenerateAsync(
                    configuration,
                    Path.Combine("MessagingGateway", prefix, "Generated", "Proactor"),
                    Path.Combine("MessagingGateway", "Proactor"),
                    messagingGatewayConfiguration,
                    filename => SkipTest(messagingGatewayConfiguration, filename)
                );
            }
        }
        else
        {
            logger.LogInformation("No messaging gateway configured");
        }
    }

    private static bool SkipTest(MessagingGatewayConfiguration configuration, string fileName)
    {
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
        if (model is MessagingGatewayConfiguration messagingGatewayConfiguration)
        {
            if (string.IsNullOrEmpty(messagingGatewayConfiguration.MessageFactory))
            {
                messagingGatewayConfiguration.MessageFactory = configuration.MessageFactory;
            }

            if (string.IsNullOrEmpty(messagingGatewayConfiguration.Namespace))
            {
                messagingGatewayConfiguration.Namespace = configuration.Namespace;
            }

            if (string.IsNullOrEmpty(messagingGatewayConfiguration.MessageAssertion))
            {
                messagingGatewayConfiguration.MessageAssertion = configuration.MessageAssertion;
            }
        }

        return base.GenerateAsync(configuration, prefix, templateFolderName, model, ignore);
    }
}
