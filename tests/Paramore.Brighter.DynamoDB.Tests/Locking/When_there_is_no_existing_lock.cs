using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Locking.DynamoDb;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Locking
{
    [Trait("Category", "DynamoDB")]
    public class DynamoDbNoExistingLockTests : DynamoDBLockingBaseTest
    {
        private readonly DynamoDbLockingProvider _provider;
        private readonly DynamoDbLockingProviderOptions _options;
        private readonly FakeTimeProvider _timeProvider;

        public DynamoDbNoExistingLockTests()
        {
            _options = new DynamoDbLockingProviderOptions("brighter_distributed_lock", Guid.NewGuid().ToString());
            _timeProvider = new FakeTimeProvider();
            _provider = new DynamoDbLockingProvider(Client, _options, _timeProvider);
        }

        [Fact]
        public async Task When_there_is_no_existing_lock_for_resource()
        {
            var resource = Guid.NewGuid().ToString();
            var result = await _provider.ObtainLockAsync(resource);

            result.Should().NotBeNull();

            var lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resource}");

            lockItem.Should().NotBeNull();
            lockItem.LockId.Should().Be(result);
            lockItem.LeaseExpiry.Should().Be(_timeProvider.GetUtcNow().Add(_options.LeaseValidity).ToUnixTimeMilliseconds());
        }

        [Fact]
        public async Task When_there_is_existing_expired_lock_for_resource()
        {
            var resource = Guid.NewGuid().ToString();
            var result = await _provider.ObtainLockAsync(resource);
            result.Should().NotBeNull();

            _timeProvider.Advance(TimeSpan.FromMinutes(5));
            result = await _provider.ObtainLockAsync(resource);
            result.Should().NotBeNull();

            var lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resource}");

            lockItem.Should().NotBeNull();
            lockItem.LockId.Should().Be(result);
            lockItem.LeaseExpiry.Should().Be(_timeProvider.GetUtcNow().Add(_options.LeaseValidity).ToUnixTimeMilliseconds());
        }

        [Fact]
        public async Task When_there_is_existing_lock_for_different_resource()
        {
            var resourceA = Guid.NewGuid().ToString();
            var result = await _provider.ObtainLockAsync(resourceA);
            result.Should().NotBeNull();

            var resourceB = Guid.NewGuid().ToString();
            result = await _provider.ObtainLockAsync(resourceB);
            result.Should().NotBeNull();

            var lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resourceB}");

            lockItem.Should().NotBeNull();
            lockItem.LockId.Should().Be(result);
            lockItem.LeaseExpiry.Should().Be(_timeProvider.GetUtcNow().Add(_options.LeaseValidity).ToUnixTimeMilliseconds());
        }
    }
}
