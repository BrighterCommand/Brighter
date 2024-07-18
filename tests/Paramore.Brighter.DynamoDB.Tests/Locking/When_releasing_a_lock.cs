using System;
using System.Threading.Tasks;
using Amazon.Auth.AccessControlPolicy;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Resources;
using Paramore.Brighter.Locking.DynamoDb;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Locking
{
    [Trait("Category", "DynamoDB")]
    public class DynamoDbReleasingLockTests : DynamoDBLockingBaseTest
    {
        private readonly DynamoDbLockingProviderOptions _options;
        private readonly FakeTimeProvider _timeProvider;

        public DynamoDbReleasingLockTests()
        {
            _options = new DynamoDbLockingProviderOptions("brighter_distributed_lock", Guid.NewGuid().ToString());
            _timeProvider = new FakeTimeProvider();
        }

        [Fact]
        public async Task When_manual_lock_release_is_enabled()
        {
            _options.ManuallyReleaseLock = true;
            var provider = new DynamoDbLockingProvider(Client, _options, _timeProvider);

            var resource = Guid.NewGuid().ToString();
            var result = await provider.ObtainLockAsync(resource);
            result.Should().NotBeNull();

            var lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resource}");
            lockItem.Should().NotBeNull();

            await provider.ReleaseLockAsync(resource, result);
            lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resource}");
            lockItem.Should().BeNull();
        }

        [Fact]
        public async Task When_manual_lock_release_is_disabled()
        {
            _options.ManuallyReleaseLock = false;
            var provider = new DynamoDbLockingProvider(Client, _options, _timeProvider);

            var resource = Guid.NewGuid().ToString();
            var result = await provider.ObtainLockAsync(resource);
            result.Should().NotBeNull();

            var lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resource}");
            lockItem.Should().NotBeNull();

            await provider.ReleaseLockAsync(resource, result);
            lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resource}");
            lockItem.Should().NotBeNull();
        }

        [Fact]
        public async Task When_one_of_multiple_locks_is_released()
        {
            _options.ManuallyReleaseLock = true;
            var provider = new DynamoDbLockingProvider(Client, _options, _timeProvider);

            var resourceA = Guid.NewGuid().ToString();
            var resultA = await provider.ObtainLockAsync(resourceA);
            resultA.Should().NotBeNull();

            var resourceB = Guid.NewGuid().ToString();
            var resultB = await provider.ObtainLockAsync(resourceB);
            resultB.Should().NotBeNull();

            await provider.ReleaseLockAsync(resourceA, resultA);
            var lockItemA = await GetLockItem($"{_options.LeaseholderGroupId}_{resourceA}");
            var lockItemB = await GetLockItem($"{_options.LeaseholderGroupId}_{resourceB}");
            lockItemA.Should().BeNull();
            lockItemB.Should().NotBeNull();
        }
    }
}
