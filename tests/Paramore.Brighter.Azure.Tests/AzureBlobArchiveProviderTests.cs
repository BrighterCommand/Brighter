using Azure.Identity;
using Azure.Storage.Blobs;
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

    private BlobContainerClient _containerClient;

    [SetUp]
    public void Setup()
    {
        var options = new AzureBlobArchiveProviderOptions()
        {
            BlobContainerUri = new Uri("https://brighterarchivertest.blob.core.windows.net/messagearchive"),
            TokenCredential = new VisualStudioCredential()
        };
        _provider = new AzureBlobArchiveProvider(options);

        _containerClient = new BlobContainerClient(options.BlobContainerUri, options.TokenCredential);

        _command = new SuperAwesomeCommand() { Message = $"do the thing {Guid.NewGuid()}" };
        _event = new SuperAwesomeEvent() { Announcement = $"The thing was done {Guid.NewGuid()}" };

        _topicDirectory = new Dictionary<string, string>()
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

        await _provider.ArchiveMessageAsync(commandMessage, CancellationToken.None);

        var blobClient = _containerClient.GetBlobClient(commandMessage.Id.ToString());

        Assert.That((bool)await blobClient.ExistsAsync(), Is.True);

        // var tags = (await blobClient.GetTagsAsync()).Value.Tags;

        var body = (await blobClient.DownloadContentAsync()).Value.Content.ToString();

        Assert.That(body, Is.EqualTo(commandMessage.Body.Value));

        // Assert.That(tags["topic"], Is.EqualTo(commandMessage.Header.Topic));
        // Assert.That(Guid.Parse(tags["correlationId"]), Is.EqualTo(commandMessage.Header.CorrelationId));
        // Assert.That(tags["message_type"], Is.EqualTo(commandMessage.Header.MessageType.ToString()));
        // Assert.That(DateTime.Parse(tags["timestamp"]), Is.EqualTo(commandMessage.Header.TimeStamp));
        // Assert.That(tags["content_type"], Is.EqualTo(commandMessage.Header.ContentType));
    }
}
