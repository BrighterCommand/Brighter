using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Paramore.Brighter.Azure.Tests.TestDoubles;
using Paramore.Brighter.Storage.Azure;

namespace Paramore.Brighter.Azure.TestDoubles.Tests;

public class AzureBlobArchiveProviderTests
{
    private AzureBlobArchiveProvider _provider;

    private SuperAwesomeCommand _command;
    private SuperAwesomeEvent _event;

    private JsonBodyMessageMapper<SuperAwesomeCommand> _commandMapper;
    private JsonBodyMessageMapper<SuperAwesomeEvent> _eventMapper;

    private Dictionary<string, string> _topicDirectory;

    [SetUp]
    public void Setup()
    {

        _command = new SuperAwesomeCommand { Message = $"do the thing {Guid.NewGuid()}" };
        _event = new SuperAwesomeEvent { Announcement = $"The thing was done {Guid.NewGuid()}" };

        _topicDirectory = new Dictionary<string, string>
        {
            { typeof(SuperAwesomeCommand).Name, $"{Guid.NewGuid()}-SuperAwesomeCommand" },
            { typeof(SuperAwesomeEvent).Name, $"{Guid.NewGuid()}-SuperAwesomeEvent" }
        };

        _commandMapper = new JsonBodyMessageMapper<SuperAwesomeCommand>(_topicDirectory);
        _eventMapper = new JsonBodyMessageMapper<SuperAwesomeEvent>(_topicDirectory);
    }

    [Test]
    public async Task GivenARequestToArchiveAMessage_TheMessageIsArchived()
    {
        var commandMessage = _commandMapper.MapToMessage(_command);

        var blobClient = GetClient(AccessTier.Cool).GetBlobClient(commandMessage.Id.ToString());
        
        await _provider.ArchiveMessageAsync(commandMessage, CancellationToken.None);

        Assert.That((bool)await blobClient.ExistsAsync(), Is.True);

        var tags = (await blobClient.GetTagsAsync()).Value.Tags;
        Assert.That(tags.Count, Is.EqualTo(0));

        var body = (await blobClient.DownloadContentAsync()).Value.Content.ToString();
        
        Assert.That(body, Is.EqualTo(commandMessage.Body.Value));

        var tier = await blobClient.GetPropertiesAsync();
        Assert.That(tier.Value.AccessTier, Is.EqualTo(AccessTier.Cool.ToString()));
        
    }

    [Test]
    public async Task GivenARequestToArchiveAMessage_WhenTagsAreTurnedOn_ThenTagsAreWritten()
    {
        var eventMessage = _eventMapper.MapToMessage(_event);
        
        var blobClient = GetClient(AccessTier.Hot, true).GetBlobClient(eventMessage.Id.ToString());
        
        await _provider.ArchiveMessageAsync(eventMessage, CancellationToken.None);
        
        var tier = await blobClient.GetPropertiesAsync();
        Assert.That(tier.Value.AccessTier, Is.EqualTo(AccessTier.Hot.ToString()));
        
        var tags = (await blobClient.GetTagsAsync()).Value.Tags;

        Assert.That(tags["topic"], Is.EqualTo(eventMessage.Header.Topic));
        Assert.That(Guid.Parse(tags["correlationId"]), Is.EqualTo(eventMessage.Header.CorrelationId));
        Assert.That(tags["message_type"], Is.EqualTo(eventMessage.Header.MessageType.ToString()));
        Assert.That(DateTime.Parse(tags["timestamp"]), Is.EqualTo(eventMessage.Header.TimeStamp));
        Assert.That(tags["content_type"], Is.EqualTo(eventMessage.Header.ContentType));
    }

    private BlobContainerClient GetClient(AccessTier tier , bool tags = false )
    {
        var options = new AzureBlobArchiveProviderOptions
        {
            BlobContainerUri = new Uri("https://brighterarchivertest.blob.core.windows.net/messagearchive"), 
            TokenCredential = new AzureCliCredential(),
            AccessTier = tier,
            TagBlobs = tags
            
        };
        _provider = new AzureBlobArchiveProvider(options);

        return new BlobContainerClient(options.BlobContainerUri, options.TokenCredential);
    }
}
