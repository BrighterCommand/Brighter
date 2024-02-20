using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Paramore.Brighter.Azure.TestDoubles.Tests;
using Paramore.Brighter.Azure.Tests.TestDoubles;
using Paramore.Brighter.Storage.Azure;

namespace Paramore.Brighter.Azure.Tests;

public class AzureBlobArchiveProviderTests
{
    private AzureBlobArchiveProvider? _provider;

    private readonly SuperAwesomeCommand _command;
    private readonly SuperAwesomeEvent _event;

    private readonly JsonBodyMessageMapper<SuperAwesomeCommand> _commandMapper;
    private readonly JsonBodyMessageMapper<SuperAwesomeEvent> _eventMapper;

    private readonly Func<Message, string> _storageLocationFunction;

    public AzureBlobArchiveProviderTests()
    {
        _command = new SuperAwesomeCommand($"do the thing {Guid.NewGuid()}");
        _event = new SuperAwesomeEvent($"The thing was done {Guid.NewGuid()}");

        Dictionary<string, string> topicDirectory = new()
        {
            { nameof(SuperAwesomeCommand), $"{Guid.NewGuid()}-SuperAwesomeCommand" },
            { nameof(SuperAwesomeEvent), $"{Guid.NewGuid()}-SuperAwesomeEvent" }
        };

        _commandMapper = new JsonBodyMessageMapper<SuperAwesomeCommand>(topicDirectory);
        _eventMapper = new JsonBodyMessageMapper<SuperAwesomeEvent>(topicDirectory);

        _storageLocationFunction = (message) => $"{message.Header.Topic}/{message.Id}".ToLower();
    }
    
    [Test]
    public async Task GivenARequestToArchiveAMessage_TheMessageIsArchived()
    {
        var commandMessage = _commandMapper?.MapToMessage(_command);

        if (commandMessage == null)
        {
            Assert.Fail("Failed to map command to message");
            return;
        }

        var blobClient = GetClient(AccessTier.Cool).GetBlobClient(_storageLocationFunction?.Invoke(commandMessage));
        
        _provider?.ArchiveMessage(commandMessage);

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
        
        var blobClient = GetClient(AccessTier.Hot, true).GetBlobClient(_storageLocationFunction.Invoke(eventMessage));
        
        _provider?.ArchiveMessage(eventMessage);
        
        var tier = await blobClient.GetPropertiesAsync();
        Assert.That(tier.Value.AccessTier, Is.EqualTo(AccessTier.Hot.ToString()));
        
        var tags = (await blobClient.GetTagsAsync()).Value.Tags;

        Assert.That(tags["topic"], Is.EqualTo(eventMessage.Header.Topic));
        Assert.That(Guid.Parse(tags["correlationId"]), Is.EqualTo(eventMessage.Header.CorrelationId));
        Assert.That(tags["message_type"], Is.EqualTo(eventMessage.Header.MessageType.ToString()));
        Assert.That(DateTime.Parse(tags["timestamp"]), Is.EqualTo(eventMessage.Header.TimeStamp));
        Assert.That(tags["content_type"], Is.EqualTo(eventMessage.Header.ContentType));
    }

    [Test]
    public async Task GivenARequestToArchiveAMessageAsync_TheMessageIsArchived()
    {
        var commandMessage = _commandMapper.MapToMessage(_command);
        
        if (commandMessage == null)
        {
            Assert.Fail("Failed to map command to message");
            return;
        }

        var blobClient = GetClient(AccessTier.Cool).GetBlobClient(_storageLocationFunction.Invoke(commandMessage));
        
        await _provider?.ArchiveMessageAsync(commandMessage, CancellationToken.None)!;

        Assert.That((bool)await blobClient.ExistsAsync(), Is.True);

        var tags = (await blobClient.GetTagsAsync()).Value.Tags;
        Assert.That(tags.Count, Is.EqualTo(0));

        var body = (await blobClient.DownloadContentAsync()).Value.Content.ToString();
        
        Assert.That(body, Is.EqualTo(commandMessage.Body.Value));

        var tier = await blobClient.GetPropertiesAsync();
        Assert.That(tier.Value.AccessTier, Is.EqualTo(AccessTier.Cool.ToString()));
        
    }

    [Test]
    public async Task GivenARequestToArchiveAMessageAsync_WhenParallel_TheMessageIsArchived()
    {
        var superAwesomeCommands = new List<SuperAwesomeCommand>();
        var superAwesomeEvents = new List<SuperAwesomeEvent>();

        var containerClient = GetClient(AccessTier.Cool);

        for (var i = 0; i < 10; i++)
        {
            superAwesomeCommands.Add(new SuperAwesomeCommand($"do the thing {Guid.NewGuid()}"));
            superAwesomeEvents.Add(new SuperAwesomeEvent($"The thing was done {Guid.NewGuid()}"));
        }

        var messages = new List<Message>();
        messages.AddRange(superAwesomeCommands.Select(c => _commandMapper.MapToMessage(c)));
        messages.AddRange(superAwesomeEvents.Select(c => _eventMapper.MapToMessage(c)));

        await _provider?.ArchiveMessagesAsync(messages.ToArray(), CancellationToken.None)!;

        foreach (var message in messages)
        {
            var blobClient = containerClient.GetBlobClient(_storageLocationFunction.Invoke(message));
            Assert.That((bool)await blobClient.ExistsAsync(), Is.True);

            var tags = (await blobClient.GetTagsAsync()).Value.Tags;
            Assert.That(tags.Count, Is.EqualTo(0));

            var body = (await blobClient.DownloadContentAsync()).Value.Content.ToString();

            string brighterBody = "";
            if (message.Header.MessageType == MessageType.MT_COMMAND)
                brighterBody = JsonSerializer.Serialize(superAwesomeCommands.First(c => c.Id == message.Id));
            else if (message.Header.MessageType == MessageType.MT_EVENT)
                brighterBody = JsonSerializer.Serialize(superAwesomeEvents.First(c => c.Id == message.Id));
            
            Assert.That(body, Is.EqualTo(brighterBody));

            var tier = await blobClient.GetPropertiesAsync();
            Assert.That(tier.Value.AccessTier, Is.EqualTo(AccessTier.Cool.ToString()));
        }

    }

    [Test]
    public async Task GivenARequestToArchiveAMessageAsync_WhenTagsAreTurnedOn_ThenTagsAreWritten()
    {
        var eventMessage = _eventMapper.MapToMessage(_event);
        
        var blobClient = GetClient(AccessTier.Hot, true).GetBlobClient(_storageLocationFunction.Invoke(eventMessage));
        
        await _provider?.ArchiveMessageAsync(eventMessage, CancellationToken.None)!;
        
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
        (
            new Uri("https://brighterarchivertest.blob.core.windows.net/messagearchive"), 
            new AzureCliCredential(),
            tier,
            tags
        );
        _provider = new AzureBlobArchiveProvider(options);

        return new BlobContainerClient(options.BlobContainerUri, options.TokenCredential);
    }
}
