﻿using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Locking.DynamoDb;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Locking
{
    [Trait("Category", "DynamoDB")]
    public class DynamoDbExistingLockTests : DynamoDBLockingBaseTest
    {
        private readonly DynamoDbLockingProvider _provider;
        private readonly DynamoDbLockingProviderOptions _options;
        private readonly FakeTimeProvider _timeProvider;

        public DynamoDbExistingLockTests()
        {
            _options = new DynamoDbLockingProviderOptions("brighter_distributed_lock", Guid.NewGuid().ToString());
            _timeProvider = new FakeTimeProvider();
            _provider = new DynamoDbLockingProvider(Client, _options, _timeProvider);
        }

        [Fact]
        public async Task When_there_is_an_existing_lock_for_resource()
        {
            var startTime = _timeProvider.GetUtcNow();
            var resource = Guid.NewGuid().ToString();
            var resultA = await _provider.ObtainLockAsync(resource);
            resultA.Should().NotBeNull();

            _timeProvider.Advance(TimeSpan.FromSeconds(30));

            var resultB = await _provider.ObtainLockAsync(resource);
            resultB.Should().BeNull();

            var lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resource}");

            lockItem.Should().NotBeNull();
            lockItem.LockId.Should().Be(resultA);
            lockItem.LeaseExpiry.Should().Be(startTime.Add(_options.LeaseValidity).ToUnixTimeMilliseconds());
        }
    }
}
