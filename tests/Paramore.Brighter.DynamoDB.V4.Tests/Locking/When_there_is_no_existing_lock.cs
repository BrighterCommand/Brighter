using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Locking.DynamoDB.V4;
using Xunit;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Locking;

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

        Assert.NotNull(result);

        var lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resource}");

        Assert.NotNull(lockItem);
        Assert.Equal(result, lockItem.LockId);
        Assert.Equal(_timeProvider.GetUtcNow().Add(_options.LeaseValidity).ToUnixTimeMilliseconds(), lockItem.LeaseExpiry);
    }

    [Fact]
    public async Task When_there_is_existing_expired_lock_for_resource()
    {
        var resource = Guid.NewGuid().ToString();
        var result = await _provider.ObtainLockAsync(resource);
        Assert.NotNull(result);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        result = await _provider.ObtainLockAsync(resource);
        Assert.NotNull(result);

        var lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resource}");

        Assert.NotNull(lockItem);
        Assert.Equal(result, lockItem.LockId);
        Assert.Equal(_timeProvider.GetUtcNow().Add(_options.LeaseValidity).ToUnixTimeMilliseconds(), lockItem.LeaseExpiry);
    }

    [Fact]
    public async Task When_there_is_existing_lock_for_different_resource()
    {
        var resourceA = Guid.NewGuid().ToString();
        var result = await _provider.ObtainLockAsync(resourceA);
        Assert.NotNull(result);

        var resourceB = Guid.NewGuid().ToString();
        result = await _provider.ObtainLockAsync(resourceB);
        Assert.NotNull(result);

        var lockItem = await GetLockItem($"{_options.LeaseholderGroupId}_{resourceB}");

        Assert.NotNull(lockItem);
        Assert.Equal(result, lockItem.LockId);
        Assert.Equal(_timeProvider.GetUtcNow().Add(_options.LeaseValidity).ToUnixTimeMilliseconds(), lockItem.LeaseExpiry);
    }
}