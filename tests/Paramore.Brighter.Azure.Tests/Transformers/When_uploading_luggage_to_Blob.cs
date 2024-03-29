﻿using Azure.Identity;
using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Azure.Tests.Helpers;
using Paramore.Brighter.Transformers.Azure;
using Policy = Polly.Policy;

namespace Paramore.Brighter.Azure.Tests.Transformers;

public class AzureBlobUploadTests : IDisposable
{
    private readonly BlobContainerClient _client;
    private readonly string _bucketName;
    private Uri _bucketUrl;

    public AzureBlobUploadTests()
    {
        //arrange
        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        _bucketUrl = new Uri($"{TestHelper.BlobLocation}{_bucketName}");

        _client = new BlobContainerClient(_bucketUrl, new AzureCliCredential());
    }
    
    [Test]
    public async Task When_uploading_luggage_to_Blob()
    {
        //arrange
        await _client.CreateIfNotExistsAsync();
        var luggageStore = new AzureBlobLuggageStore(_bucketUrl, new AzureCliCredential());
        
        //act
        //Upload the test stream to Azure
        var testContent = "Well, always know that you shine Brighter";
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        await streamWriter.WriteAsync(testContent);
        await streamWriter.FlushAsync();
        stream.Position = 0;

        var claim = await luggageStore.StoreAsync(stream, CancellationToken.None);

        //assert
        //do we have a claim?
        (await luggageStore.HasClaimAsync(claim, CancellationToken.None)).Should().BeTrue();
        
        //check for the contents indicated by the claim id on S3
        var result = await luggageStore.RetrieveAsync(claim, CancellationToken.None);
        var resultAsString = await new StreamReader(result).ReadToEndAsync();
        resultAsString.Should().Be(testContent);

        await luggageStore.DeleteAsync(claim, CancellationToken.None);

    }

    public void Dispose()
    {
        _client.Delete();
    }
}
