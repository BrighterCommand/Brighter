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
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Locking.DynamoDb
{
    public partial class DynamoDbLockingProvider : IDistributedLock
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly DynamoDbLockingProviderOptions _options;
        private readonly TimeProvider _timeProvider;

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<DynamoDbLockingProvider>();

        public DynamoDbLockingProvider(IAmazonDynamoDB dynamoDb, DynamoDbLockingProviderOptions options)
            :this(dynamoDb, options, TimeProvider.System)
        {
        }

        public DynamoDbLockingProvider(IAmazonDynamoDB dynamoDb, DynamoDbLockingProviderOptions options, TimeProvider timeProvider)
        {
            _dynamoDb = dynamoDb;
            _options = options;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Attempt to obtain a lock on a resource
        /// </summary>
        /// <param name="resource">The name of the resource to Lock</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>The id of the lock that has been acquired or null if no lock was able to be acquired</returns>
        public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken = default)
        {
            var lockId = Uuid.NewAsString();

            try
            {
                await _dynamoDb.PutItemAsync(BuildLockRequest(resource, lockId), cancellationToken);
            }
            catch (ConditionalCheckFailedException)
            {
                Log.UnableToObtainLockForResource(s_logger, resource);
                return null;
            }

            Log.ObtainedLockForResource(s_logger, lockId, resource);
            return lockId;
        }

        /// <summary>
        /// Release a lock
        /// </summary>
        /// <param name="resource">The name of the resource to Lock</param>
        /// <param name="lockId">The lock Id that was provided when the lock was obtained</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Awaitable Task</returns>
        public async Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken = default)
        {
            if (_options.ManuallyReleaseLock)
            {
                try
                {
                    await _dynamoDb.DeleteItemAsync(BuildReleaseRequest(resource, lockId), cancellationToken);
                }
                catch (ConditionalCheckFailedException)
                {
                    Log.UnableToReleaseLockForResource(s_logger, lockId, resource);
                }
            }
        }

        private PutItemRequest BuildLockRequest(string resource, string lockId)
        {
            var now = _timeProvider.GetUtcNow();
            var leaseExpiry = now.Add(_options.LeaseValidity);

            return new PutItemRequest
            {
                TableName = _options.LockTableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    {"ResourceId", new AttributeValue{ S = $"{_options.LeaseholderGroupId}_{resource}"} },
                    {"LeaseExpiry", new AttributeValue{ N = leaseExpiry.ToUnixTimeMilliseconds().ToString()} },
                    {"LockId", new AttributeValue{S = lockId} }
                },
                ConditionExpression = "attribute_not_exists(#r) OR (attribute_exists(#r) AND #e <= :t)",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    {"#r", "LockId"},
                    {"#e", "LeaseExpiry"}
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {":t", new AttributeValue {N = now.ToUnixTimeMilliseconds().ToString()} }
                }
            };
        }

        private DeleteItemRequest BuildReleaseRequest(string resource, string leaseId)
        {
            return new DeleteItemRequest
            {
                TableName = _options.LockTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    {"ResourceId", new AttributeValue{S = $"{_options.LeaseholderGroupId}_{resource}"} }
                },
                ConditionExpression = "attribute_exists(#r) AND #l = :l",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    {"#r", "ResourceId"},
                    {"#l", "LockId"}
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {":l", new AttributeValue{S = leaseId} }
                }
            };
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "Unable to obtain lock for resource {Resource}, an existing lock is in place")]
            public static partial void UnableToObtainLockForResource(ILogger logger, string resource);

            [LoggerMessage(LogLevel.Information, "Obtained lock {LockId} for resource {Resource}")]
            public static partial void ObtainedLockForResource(ILogger logger, string lockId, string resource);

            [LoggerMessage(LogLevel.Information, "Unable to release lock {LockId} for resource {ResourceId} - lock has expired")]
            public static partial void UnableToReleaseLockForResource(ILogger logger, string lockId, string resourceId);
        }
    }
}

