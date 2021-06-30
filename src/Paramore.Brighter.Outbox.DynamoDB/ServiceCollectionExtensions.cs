using System;
using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public static class ServiceCollectionExtensions
    {
        public static IBrighterHandlerBuilder UseDynamoDbOutbox(
            this IBrighterHandlerBuilder brighterBuilder, IAmazonDynamoDB connection, DynamoDbConfiguration configuration, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.AddSingleton<DynamoDbConfiguration>(configuration);
            brighterBuilder.Services.AddSingleton<IAmazonDynamoDB>(connection);

            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), BuildDynamoDbOutbox, serviceLifetime));
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), BuildDynamoDbOutbox, serviceLifetime));
            
            return brighterBuilder;
        }

        private static DynamoDbOutbox BuildDynamoDbOutbox(IServiceProvider provider)
        {
            var config = provider.GetService<DynamoDbConfiguration>();
            var connection = provider.GetService<IAmazonDynamoDB>();

            return new DynamoDbOutbox(connection, config);
        }
    }
}
