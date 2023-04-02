﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.Tranformers.AWS;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.Transformers
{
    [Trait("Category", "AWS")] 
    [Trait("Fragile", "CI")]
    public class LargeMessagePaylodUnwrapTests : IDisposable
    {
        private readonly TransformPipelineBuilder _pipelineBuilder;
        private readonly AmazonS3Client _client;
        private readonly string _bucketName;
        private readonly S3LuggageStore _luggageStore;

        public LargeMessagePaylodUnwrapTests()
        {
            //arrange
            TransformPipelineBuilder.ClearPipelineCache();

            var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()))
            {
                { typeof(MyLargeCommand), typeof(MyLargeCommandMessageMapper) }
            };
            
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();

            _client = new AmazonS3Client(credentials, region);
            AmazonSecurityTokenServiceClient stsClient = new(credentials, region);

            var services = new ServiceCollection();
            services.AddHttpClient();
            var provider = services.BuildServiceProvider();
            IHttpClientFactory httpClientFactory = provider.GetService<IHttpClientFactory>();

            _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
            
            _luggageStore = S3LuggageStore
                .CreateAsync(
                    client: _client,
                    bucketName: _bucketName,
                    storeCreation: S3LuggageStoreCreation.CreateIfMissing,
                    httpClientFactory: httpClientFactory,
                    stsClient: stsClient,
#pragma warning disable CS0618 // although obsolete, the region string on the replacement is wrong for our purpose
                    bucketRegion: S3Region.EUW1,
#pragma warning restore CS0618 
                    tags: new List<Tag> { new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" } },
                    acl: S3CannedACL.Private,
                    abortFailedUploadsAfterDays: 1,
                    deleteGoodUploadsAfterDays: 1)
                .GetAwaiter()
                .GetResult();

            var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_luggageStore));

            _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
        }
    
        [Fact]
        public async Task When_unwrapping_a_large_message()
        {
            //arrange
            await Task.Delay(3000); //allow bucket definition to propagate
            
            //store our luggage and get the claim check
            var contents = DataGenerator.CreateString(6000);
            var myCommand = new MyLargeCommand(1) { Value = contents };
            var commandAsJson = JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General));
        
            var stream = new MemoryStream();                                                                               
            var writer = new StreamWriter(stream);
            await writer.WriteAsync(commandAsJson);
            await writer.FlushAsync();
            stream.Position = 0;
            var id = await _luggageStore.StoreAsync(stream);

            //pretend we ran through the claim check
            myCommand.Value = $"Claim Check {id}";
 
            //set the headers, so that we have a claim check listed
            var message = new Message(
                new MessageHeader(myCommand.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
            );

            message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK] = id;

            //act
            var transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyLargeCommand>();
            var transformedMessage = await transformPipeline.UnwrapAsync(message);
        
            //assert
            //contents should be from storage
            transformedMessage.Value.Should().Be(contents);
            (await _luggageStore.HasClaimAsync(id)).Should().BeFalse();
        }

        public void Dispose()
        {
            //The bucket should be empty, allowing us to delete it
            _client.DeleteBucketAsync(_bucketName).GetAwaiter().GetResult();
        }
    }
}
