#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Outbox.DynamoDB.V4;

namespace Paramore.Brighter.DynamoDB.V4.Tests.TestDoubles;

internal sealed class InMemoryDynamoDbScanHttpHandler(FakeTimeProvider timeProvider) : HttpMessageHandler
{
    public Dictionary<int, string[]> Topics { get; } = new();
    public List<(int Segment, int Start)> Requests { get; } = new();
    public int? CancelSegment { get; set; }
    public int? FailSegment { get; set; }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var target = request.Headers.GetValues("X-Amz-Target").Single();
        if (target.EndsWith(".DescribeTable", StringComparison.Ordinal))
        {
            var table = new DynamoDbTableFactory().GenerateCreateTableRequest<MessageItem>(new DynamoDbCreateProvisionedThroughput());
            return Response(new
            {
                Table = new
                {
                    table.TableName,
                    KeySchema = table.KeySchema.Select(key => new { key.AttributeName, KeyType = key.KeyType.Value }),
                    AttributeDefinitions = table.AttributeDefinitions.Select(attribute =>
                        new { attribute.AttributeName, AttributeType = attribute.AttributeType.Value }),
                    GlobalSecondaryIndexes = table.GlobalSecondaryIndexes.Select(index => new
                    {
                        index.IndexName,
                        KeySchema = index.KeySchema.Select(key => new { key.AttributeName, KeyType = key.KeyType.Value }),
                        Projection = new { ProjectionType = "ALL" }
                    })
                }
            });
        }

        if (!target.EndsWith(".Scan", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected DynamoDB operation: {target}");
        }

        using var document = JsonDocument.Parse(request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
        var scan = document.RootElement;
        var segment = scan.GetProperty("Segment").GetInt32();
        var start = scan.TryGetProperty("ExclusiveStartKey", out var key)
            ? int.Parse(key.GetProperty("MessageId").GetProperty("S").GetString()!.Split('-')[1]) + 1
            : 0;
        lock (Requests)
        {
            Requests.Add((segment, start));
            timeProvider.Advance(CancelSegment == segment ? TimeSpan.FromDays(1) : TimeSpan.FromMilliseconds(100));
            cancellationToken.ThrowIfCancellationRequested();
            if (FailSegment == segment)
            {
                throw new InvalidOperationException("Scan failed.");
            }

            var topics = Topics[segment];
            var end = Math.Min(start + scan.GetProperty("Limit").GetInt32(), topics.Length);
            var items = Enumerable.Range(start, end - start).Select(index => new Dictionary<string, object>
            {
                ["MessageId"] = new { S = $"{segment}-{index}" },
                ["Topic"] = new { S = topics[index] },
                ["OutstandingCreatedTime"] = new { N = "1" },
                ["CreatedTime"] = new { N = "1" },
                ["MessageType"] = new { S = "MT_EVENT" },
                ["HeaderBag"] = new { S = "{}" },
                ["Body"] = new { B = Convert.ToBase64String(Encoding.UTF8.GetBytes("body")) }
            }).ToArray();
            var lastKey = end < topics.Length
                ? new Dictionary<string, object>
                {
                    ["MessageId"] = new { S = $"{segment}-{end - 1}" },
                    ["OutstandingCreatedTime"] = new { N = "1" }
                }
                : new Dictionary<string, object>();
            return Response(new { Items = items, Count = items.Length, ScannedCount = items.Length, LastEvaluatedKey = lastKey });
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(Send(request, cancellationToken));

    private static HttpResponseMessage Response(object content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/x-amz-json-1.0")
    };
}
