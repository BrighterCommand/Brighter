using System;
using System.IO;
using System.IO.Compression;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transformers.JustSaying;
using Xunit;

namespace Paramore.Brighter.Transforms.Adaptors.Tests.JustSaying;

public class JustSayingCompressionTransformTest
{
    [Fact]
    public void Unwrap_when_content_encoding_marks_gzip_base64_should_decompress_body()
    {
        var command = new Payload { Name = new string('x', 4096) };
        var compressed = GzipBase64Encode(command);

        var header = new MessageHeader { ContentType = new ContentType("application/json") };
        header.Bag[JustSayingAttributesName.ContentEncoding] = JustSayingAttributesName.GzipBase64ContentEncoding;
        var message = new Message(header, new MessageBody(Encoding.ASCII.GetBytes(compressed), new ContentType("application/json")));

        using var transform = new JustSayingCompressionTransform();
        var result = transform.Unwrap(message);

        var decoded = JsonSerializer.Deserialize<Payload>(result.Body.Memory.Span, JsonSerialisationOptions.Options);
        Assert.NotNull(decoded);
        Assert.Equal(command.Name, decoded!.Name);
    }

    [Fact]
    public void Unwrap_when_content_encoding_header_is_absent_should_leave_body_untouched()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new Payload { Name = "plain" }, JsonSerialisationOptions.Options);
        var message = new Message(new MessageHeader(), new MessageBody(json, new ContentType("application/json")));

        using var transform = new JustSayingCompressionTransform();
        var result = transform.Unwrap(message);

        Assert.True(result.Body.Memory.Span.SequenceEqual(json));
    }

    [Fact]
    public void Unwrap_when_content_encoding_is_some_other_value_should_leave_body_untouched()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new Payload { Name = "plain" }, JsonSerialisationOptions.Options);
        var header = new MessageHeader();
        header.Bag[JustSayingAttributesName.ContentEncoding] = "br";
        var message = new Message(header, new MessageBody(json, new ContentType("application/json")));

        using var transform = new JustSayingCompressionTransform();
        var result = transform.Unwrap(message);

        Assert.True(result.Body.Memory.Span.SequenceEqual(json));
    }

    [Fact]
    public void Wrap_is_a_no_op()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new Payload { Name = "plain" }, JsonSerialisationOptions.Options);
        var message = new Message(new MessageHeader(), new MessageBody(json, new ContentType("application/json")));

        using var transform = new JustSayingCompressionTransform();
        var result = transform.Wrap(message, new Publication());

        Assert.True(result.Body.Memory.Span.SequenceEqual(json));
    }

    private static string GzipBase64Encode<T>(T value)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value, JsonSerialisationOptions.Options);
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gz.Write(json, 0, json.Length);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    private sealed class Payload
    {
        public string Name { get; set; } = string.Empty;
    }
}
