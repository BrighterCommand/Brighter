using System;
using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a DynamoDb Outbox. This helper registers
        ///  - IAmAnOutboxSync<Message>
        ///  - IAmAnOutboxAsync<Message>
        ///
        /// You will need to register the following BEFORE calling this extension
        ///  - IAmazonDynamoDb
        ///  - DynamoDbConfiguration
        /// We do not register these, as we assume you will need to register them for your code's access to DynamoDb  
        /// So we assume that prerequisite has taken place beforehand 
        /// </summary>
        /// <param name="serviceLifetime">The lifetime of the outbox connection</param>
        /// <returns></returns>
        public static IBrighterBuilder UseDynamoDbOutbox(
            this IBrighterBuilder brighterBuilder, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            brighterBuilder.Services.TryAdd(new ServiceDescriptor(typeof(IAmAnOutbox<Message>), GetOrBuildDynamoDbOutbox, serviceLifetime));
            brighterBuilder.Services.TryAdd(new ServiceDescriptor(typeof(IAmAnOutboxSync<Message>), GetOrBuildDynamoDbOutbox, serviceLifetime));
            brighterBuilder.Services.TryAdd(new ServiceDescriptor(typeof(IAmAnOutboxAsync<Message>), GetOrBuildDynamoDbOutbox, serviceLifetime));

            return brighterBuilder;
        }

        /// <summary>
        /// Use this transaction provider to ensure that the Outbox and the Entity Store are correct
        /// </summary>
        /// <param name="brighterBuilder">Allows extension method</param>
        /// <param name="connectionProvider">What is the type of the connection provider</param>
        /// <param name="serviceLifetime">What is the lifetime of registered interfaces</param>
        /// <returns>Allows fluent syntax</returns>
        /// This is paired with Use Outbox (above) when required
        /// Registers the following
        /// -- IAmABoxTransactionConnectionProvider: the provider of a connection for any existing transaction
        public static IBrighterBuilder UseDynamoDbTransactionConnectionProvider(
            this IBrighterBuilder brighterBuilder, Type connectionProvider,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            brighterBuilder.Services.Add(new ServiceDescriptor(typeof(IAmABoxTransactionConnectionProvider), connectionProvider, serviceLifetime));

            return brighterBuilder;
        }

        private static DynamoDbOutbox GetOrBuildDynamoDbOutbox(IServiceProvider provider)
        {
            var config = provider.GetService<DynamoDbConfiguration>();
            if (config == null)
                throw new InvalidOperationException("No service of type DynamoDbConfiguration could be found, please register before calling this method");
            var dynamoDb = provider.GetService<IAmazonDynamoDB>();
            if (dynamoDb == null)
                throw new InvalidOperationException("No service of type IAmazonDynamoDb was found. Please register before calling this method");

            return new DynamoDbOutbox(dynamoDb, config);
        }
    }
}
