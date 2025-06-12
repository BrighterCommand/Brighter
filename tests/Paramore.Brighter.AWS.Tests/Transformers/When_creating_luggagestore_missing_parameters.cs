﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.Tranformers.AWS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.Transformers;

public class S3LuggageUploadMissingParametersTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _bucketName;

    public S3LuggageUploadMissingParametersTests()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        _httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
    }

    [Fact]
    public void When_creating_luggagestore_missing_client()
    {
        //arrange
        var exception = Catch.Exception(() => new S3LuggageStore(new S3LuggageOptions(null!,  null!)));

        Assert.NotNull(exception);
        Assert.True(exception is ArgumentNullException);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void When_creating_luggagestore_missing_bucketName(string? bucketName)
    {
        //arrange
        var exception = Catch.Exception(() => new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(),  bucketName!)));

        Assert.NotNull(exception);
        Assert.True(exception is ArgumentNullException);
    }
    
    [Fact]
    public async Task When_creating_luggagestore_bad_bucketName()
    {
        //arrange
        var exception = Catch.Exception(() => new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), "A" )));

        Assert.NotNull(exception);
        Assert.True(exception is ArgumentException);
    }
    
    [Fact]
    public async Task When_creating_luggagestore_missing_httpClient()
    {
        //arrange
        var exception = await Catch.ExceptionAsync(async () =>
        {
            var store = new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), _bucketName));
            await store.EnsureStoreExistsAsync();
        });

        Assert.NotNull(exception);
        Assert.True(exception is ArgumentNullException);
    }
    
    [Fact]
    public async Task When_creating_luggagestore_missing_ACL() 
    {
        //arrange
        var exception = await Catch.ExceptionAsync(async () =>
        {
            var store = new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), _bucketName)
            {
                HttpClientFactory = _httpClientFactory
            });
            await store.EnsureStoreExistsAsync();
        });
    
        Assert.NotNull(exception);
        Assert.True(exception is ArgumentNullException);
    }
}
