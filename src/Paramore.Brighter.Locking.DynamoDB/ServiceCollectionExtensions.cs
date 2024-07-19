#region Licence
/* The MIT License (MIT)
Copyright © 2024 Dominic Hickie <dominichickie@gmail.com>

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
using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Locking.DynamoDb;

namespace Paramore.Brighter.Locking.DynamoDB
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a DynamoDb distributed locking provider. This helper registers
        ///  - IDistributedLock
        ///
        /// You will need to register IAmazonDynamoDb BEFORE calling this extension
        /// We do not register this, as we assume you will need to register them for your code's access to DynamoDb  
        /// So we assume that prerequisite has taken place beforehand 
        /// </summary>
        /// <param name="configAction">An action for customising the lock config before registering the provider</param>
        /// <param name="serviceLifetime">The lifetime of the locking provider</param>
        /// <returns></returns>
        public static IBrighterBuilder UseDynamoDbDistributedLock(this IBrighterBuilder builder, 
            string lockTableName,
            string leaseholderGroupId,
            Action<DynamoDbLockingProviderOptions>? configAction = null,
            ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        {
            var config = new DynamoDbLockingProviderOptions(lockTableName, leaseholderGroupId);

            if (configAction != null)
            {
                configAction(config);
            }

            builder.Services.AddSingleton(config);
            builder.Services.Add(new ServiceDescriptor(typeof(IDistributedLock), BuildLockingProvider, serviceLifetime));

            return builder;
        }

        private static DynamoDbLockingProvider BuildLockingProvider(IServiceProvider serviceProvider)
        {
            var config = serviceProvider.GetService<DynamoDbLockingProviderOptions>();
            if (config == null)
                throw new InvalidOperationException("No service of type DynamoDbLockingProviderOptions could be found, please register before calling this method");
            var dynamoDb = serviceProvider.GetService<IAmazonDynamoDB>();
            if (dynamoDb == null)
                throw new InvalidOperationException("No service of type IAmazonDynamoDb was found. Please register before calling this method");

            return new DynamoDbLockingProvider(dynamoDb, config);
        }
    }
}
