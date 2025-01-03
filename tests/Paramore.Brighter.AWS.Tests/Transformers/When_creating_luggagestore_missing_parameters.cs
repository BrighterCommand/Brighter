using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.Tranformers.AWS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.Transformers;

public class S3LuggageUploadMissingParametersTests
{
    private readonly AmazonS3Client _client;
    private readonly AmazonSecurityTokenServiceClient _stsClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _bucketName;

    public S3LuggageUploadMissingParametersTests()
    {
        //arrange
        var factory = new AWSClientFactory(GatewayFactory.CreateFactory());

        _client = factory.CreateS3Client();
        _stsClient = factory.CreateStsClient(); 

        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        _httpClientFactory = provider.GetService<IHttpClientFactory>();
        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
    }

    [Fact]
    public async Task When_creating_luggagestore_missing_client()
    {
        //arrange
        var exception = await Catch.ExceptionAsync(async () =>
        {
            var luggageStore = await S3LuggageStore.CreateAsync(
                client: null,
                bucketName: _bucketName,
                storeCreation: S3LuggageStoreCreation.CreateIfMissing,
                httpClientFactory: _httpClientFactory,
                stsClient: _stsClient,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
                bucketRegion: S3Region.EUW1,
#pragma warning restore CS0618 // turn warning back on
                tags: new List<Tag> { new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" } },
                acl: S3CannedACL.Private,
                policy: null, 
                abortFailedUploadsAfterDays: 1, 
                deleteGoodUploadsAfterDays: 1);
        });

        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentNullException>();
    }

    [Fact]
    public async Task When_creating_luggagestore_missing_bucketName()
    {
        //arrange
        var exception = await Catch.ExceptionAsync(async () =>
        {
            var luggageStore = await S3LuggageStore.CreateAsync(
                client: _client,
                bucketName: null,
                storeCreation: S3LuggageStoreCreation.CreateIfMissing,
                httpClientFactory: _httpClientFactory,
                stsClient: _stsClient,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
                bucketRegion: S3Region.EUW1,
#pragma warning restore CS0618 // turn warning back on
                tags: new List<Tag> { new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" } },
                acl: S3CannedACL.Private,
                policy: null, 
                abortFailedUploadsAfterDays: 1, 
                deleteGoodUploadsAfterDays: 1);
        });

        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentNullException>();
    }
    
    [Fact]
    public async Task When_creating_luggagestore_bad_bucketName()
    {
        //arrange
        var exception = await Catch.ExceptionAsync(async () =>
        {
            var luggageStore = await S3LuggageStore.CreateAsync(
                client: _client,
                bucketName: "A",
                storeCreation: S3LuggageStoreCreation.CreateIfMissing,
                httpClientFactory: _httpClientFactory,
                stsClient: _stsClient,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
                bucketRegion: S3Region.EUW1,
#pragma warning restore CS0618 // turn warning back on
                tags: new List<Tag> { new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" } },
                acl: S3CannedACL.Private,
                policy: null, 
                abortFailedUploadsAfterDays: 1, 
                deleteGoodUploadsAfterDays: 1);
        });

        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentException>();
    }
    
    [Fact]
    public async Task When_creating_luggagestore_missing_httpClient()
    {
        //arrange
        var exception = await Catch.ExceptionAsync(async () =>
        {
            var luggageStore = await S3LuggageStore.CreateAsync(
                client: _client,
                bucketName: _bucketName,
                storeCreation: S3LuggageStoreCreation.CreateIfMissing,
                httpClientFactory: null,
                stsClient: _stsClient,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
                bucketRegion: S3Region.EUW1,
#pragma warning restore CS0618 // turn warning back on
                tags: new List<Tag> { new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" } },
                acl: S3CannedACL.Private,
                policy: null, 
                abortFailedUploadsAfterDays: 1, 
                deleteGoodUploadsAfterDays: 1);
        });

        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentNullException>();
    }

    [Fact]
    public async Task When_creating_luggagestore_missing_stsClient()
    {
        //arrange
        var exception = await Catch.ExceptionAsync(async () =>
        {
            var luggageStore = await S3LuggageStore.CreateAsync(
                client: _client,
                bucketName: _bucketName,
                storeCreation: S3LuggageStoreCreation.CreateIfMissing,
                httpClientFactory: _httpClientFactory,
                stsClient: null,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
                bucketRegion: S3Region.EUW1,
#pragma warning restore CS0618 // turn warning back on
                tags: new List<Tag> { new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" } },
                acl: S3CannedACL.Private,
                policy: null, 
                abortFailedUploadsAfterDays: 1, 
                deleteGoodUploadsAfterDays: 1);
        });

        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentNullException>();
    }
    
    [Fact]
    public async Task When_creating_luggagestore_missing_bucketRegion()
    {
        //arrange
        var exception = await Catch.ExceptionAsync(async () =>
        {
            var luggageStore = await S3LuggageStore.CreateAsync(
                client: _client,
                bucketName: _bucketName,
                storeCreation: S3LuggageStoreCreation.CreateIfMissing,
                httpClientFactory: _httpClientFactory,
                stsClient: _stsClient,
                bucketRegion: null,
                tags: new List<Tag> { new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" } },
                acl: S3CannedACL.Private,
                policy: null, 
                abortFailedUploadsAfterDays: 1, 
                deleteGoodUploadsAfterDays: 1);
        });

        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentNullException>();
    }
    
      [Fact]
        public async Task When_creating_luggagestore_missing_ACL()
        {
            //arrange
            var exception = await Catch.ExceptionAsync(async () =>
            {
                var luggageStore = await S3LuggageStore.CreateAsync(
                    client: _client,
                    bucketName: _bucketName,
                    storeCreation: S3LuggageStoreCreation.CreateIfMissing,
                    httpClientFactory: _httpClientFactory,
                    stsClient: _stsClient,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
                    bucketRegion: S3Region.EUW1,
#pragma warning restore CS0618 // turn warning back on
                    tags: new List<Tag> { new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" } },
                    acl: null,
                    policy: null, 
                    abortFailedUploadsAfterDays: 1, 
                    deleteGoodUploadsAfterDays: 1);
            });
    
            exception.Should().NotBeNull();
            exception.Should().BeOfType<ArgumentNullException>();
        }
}
