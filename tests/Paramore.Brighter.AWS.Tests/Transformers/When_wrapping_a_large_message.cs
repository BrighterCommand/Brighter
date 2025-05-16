using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Tranformers.AWS;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.Transformers
{
    public class LargeMessagePayloadWrapTests : IDisposable
    {
        private WrapPipelineAsync<MyLargeCommand> _transformPipeline;
        private readonly TransformPipelineBuilderAsync _pipelineBuilder;
        private readonly MyLargeCommand _myCommand;
        private readonly S3LuggageStore _luggageStore;
        private readonly AmazonS3Client _client;
        private readonly string _bucketName;
        private string _id;
        private readonly Publication _publication;

        public LargeMessagePayloadWrapTests()
        {
            //arrange
            TransformPipelineBuilderAsync.ClearPipelineCache();

            var mapperRegistry =
                new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(
                    _ => new MyLargeCommandMessageMapperAsync())
                    );
           
            mapperRegistry.RegisterAsync<MyLargeCommand, MyLargeCommandMessageMapperAsync>();
            
            _myCommand = new MyLargeCommand(6000);

           var factory = new AWSClientFactory(GatewayFactory.CreateFactory());

            _client = factory.CreateS3Client();
            var stsClient = factory.CreateStsClient();

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
#pragma warning disable CS0618 // It is obsolete, but we want the string value here not the replacement one
                    bucketRegion: S3Region.EUWest1,
#pragma warning restore CS0618
                    tags: [new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" }],
                    acl: S3CannedACL.Private,
                    abortFailedUploadsAfterDays: 1,
                    deleteGoodUploadsAfterDays: 1)
                .GetAwaiter()
                .GetResult();

            var transformerFactoryAsync = new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformerAsync(_luggageStore));

            _publication = new Publication { Topic = new RoutingKey("MyLargeCommand"), RequestType = typeof(MyLargeCommand) };

            _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, transformerFactoryAsync);
        }

        [Fact]
        public async Task When_wrapping_a_large_message()
        {
            //act
            _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
            var message = await _transformPipeline.WrapAsync(_myCommand, new RequestContext(), _publication);

            //assert
            Assert.True(message.Header.Bag.ContainsKey(ClaimCheckTransformerAsync.CLAIM_CHECK));
            _id = (string)message.Header.Bag[ClaimCheckTransformerAsync.CLAIM_CHECK];
            Assert.Equal($"Claim Check {_id}", message.Body.Value);
            
            Assert.True((await _luggageStore.HasClaimAsync(_id)));
        }

        public void Dispose()
        {
            //We have to empty objects from a bucket before deleting it
            _luggageStore.DeleteAsync(_id).GetAwaiter().GetResult();
            _client.DeleteBucketAsync(_bucketName).GetAwaiter().GetResult();
        }
    }
}
