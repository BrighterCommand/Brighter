﻿#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using System.Net.Http;
using System.Transactions;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Greetings.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Tranformers.AWS;
using Paramore.Brighter.Transforms.Storage;
using Serilog;
using Serilog.Extensions.Logging;

namespace GreetingsSender
{
    public static class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory());
            
            if (new CredentialProfileStoreChain().TryGetAWSCredentials("default", out var credentials))
            {
                var awsConnection = new AWSMessagingGatewayConnection(credentials, RegionEndpoint.EUWest1);

                var topic = new RoutingKey(typeof(GreetingEvent).FullName.ToValidSNSTopicName());

                var producerRegistry = new SnsProducerRegistryFactory(
                    awsConnection,
                    new SnsPublication[]
                    {
                        new()
                        {
                            Topic = topic,
                            RequestType = typeof(GreetingEvent),
                            FindTopicBy = TopicFindBy.Convention,
                            MakeChannels = OnMissingChannel.Create
                        }
                    }
                ).Create();
                
                serviceCollection.AddBrighter()
                    .AddProducers((configure) =>
                    {
                        configure.ProducerRegistry = producerRegistry;
                    })
                    .UseExternalLuggageStore(provider => new S3LuggageStore(new S3LuggageOptions(
                        new AWSS3Connection(credentials, RegionEndpoint.EUWest1),
                        "brightersamplebucketb0561a06-70ec-11ed-a1eb-0242ac120002")
                    {
                        HttpClientFactory = provider.GetService<IHttpClientFactory>(),
                        Strategy = StorageStrategy.Validate
                    }))
                    .AutoFromAssemblies([typeof(GreetingEvent).Assembly]);

                //We need this for the check as to whether an S3 bucket exists
                serviceCollection.AddHttpClient();
                
                var serviceProvider = serviceCollection.BuildServiceProvider();

                var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
                
                Console.WriteLine($"Sending Event to SNS topic {topic} ");

                //create a 512K string, too large for a payload, that needs offloading
                commandProcessor.Post(new GreetingEvent($"Hi - {CreateString(524288)}"));
                
                Console.WriteLine($"Sent Event to SNS topic {topic} ");
            }
        }
        
        public static string CreateString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[new Random().Next(s.Length)]).ToArray());
        }    
    }
}
