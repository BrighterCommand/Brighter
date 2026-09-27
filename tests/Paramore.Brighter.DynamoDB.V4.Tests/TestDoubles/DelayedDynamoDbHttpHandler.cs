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

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.DynamoDB.V4.Tests.TestDoubles;

internal sealed class DelayedDynamoDbHttpHandler : HttpMessageHandler
{
    private readonly TaskCompletionSource<bool> _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<CancellationToken> _operationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _requestCount;

    public int RequestCount => _requestCount;

    public Task<CancellationToken> OperationStarted => _operationStarted.Task;

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return DescribeTable(request);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var target = request.Headers.GetValues("X-Amz-Target").Single();
        if (target.EndsWith(".DescribeTable", StringComparison.Ordinal))
            return DescribeTable(request);

        Interlocked.Increment(ref _requestCount);
        _operationStarted.TrySetResult(cancellationToken);
        await _response.Task.WaitAsync(cancellationToken);
        return Response(target.Split('.')[1] switch
        {
            "GetItem" or "PutItem" or "UpdateItem" or "TransactWriteItems" => "{}",
            "BatchGetItem" => "{\"Responses\":{\"brighter_outbox\":[]}}",
            "BatchWriteItem" => "{\"UnprocessedItems\":{}}",
            "Query" or "Scan" => "{\"Items\":[],\"Count\":0,\"ScannedCount\":0}",
            _ => throw new InvalidOperationException($"Unexpected DynamoDB operation: {target}")
        });
    }

    public void CompleteOperation() => _response.TrySetResult(true);

    private static HttpResponseMessage DescribeTable(HttpRequestMessage request)
    {
        var target = request.Headers.GetValues("X-Amz-Target").Single();
        if (!target.EndsWith(".DescribeTable", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected synchronous DynamoDB operation: {target}");

        return Response("""
            {
              "Table": {
                "TableName": "brighter_outbox",
                "KeySchema": [
                  {
                    "AttributeName": "MessageId",
                    "KeyType": "HASH"
                  }
                ],
                "AttributeDefinitions": [
                  {
                    "AttributeName": "MessageId",
                    "AttributeType": "S"
                  },
                  {
                    "AttributeName": "TopicShard",
                    "AttributeType": "S"
                  },
                  {
                    "AttributeName": "OutstandingCreatedTime",
                    "AttributeType": "N"
                  },
                  {
                    "AttributeName": "DeliveryTime",
                    "AttributeType": "N"
                  },
                  {
                    "AttributeName": "CausationId",
                    "AttributeType": "S"
                  }
                ],
                "GlobalSecondaryIndexes": [
                  {
                    "IndexName": "Outstanding",
                    "KeySchema": [
                      {
                        "AttributeName": "TopicShard",
                        "KeyType": "HASH"
                      },
                      {
                        "AttributeName": "OutstandingCreatedTime",
                        "KeyType": "RANGE"
                      }
                    ],
                    "Projection": {
                      "ProjectionType": "ALL"
                    }
                  },
                  {
                    "IndexName": "Delivered",
                    "KeySchema": [
                      {
                        "AttributeName": "TopicShard",
                        "KeyType": "HASH"
                      },
                      {
                        "AttributeName": "DeliveryTime",
                        "KeyType": "RANGE"
                      }
                    ],
                    "Projection": {
                      "ProjectionType": "ALL"
                    }
                  },
                  {
                    "IndexName": "OutstandingAllTopics",
                    "KeySchema": [
                      {
                        "AttributeName": "OutstandingCreatedTime",
                        "KeyType": "HASH"
                      },
                      {
                        "AttributeName": "MessageId",
                        "KeyType": "RANGE"
                      }
                    ],
                    "Projection": {
                      "ProjectionType": "ALL"
                    }
                  },
                  {
                    "IndexName": "DeliveredAllTopics",
                    "KeySchema": [
                      {
                        "AttributeName": "DeliveryTime",
                        "KeyType": "HASH"
                      },
                      {
                        "AttributeName": "MessageId",
                        "KeyType": "RANGE"
                      }
                    ],
                    "Projection": {
                      "ProjectionType": "ALL"
                    }
                  },
                  {
                    "IndexName": "Causation",
                    "KeySchema": [
                      {
                        "AttributeName": "CausationId",
                        "KeyType": "HASH"
                      }
                    ],
                    "Projection": {
                      "ProjectionType": "ALL"
                    }
                  }
                ]
              }
            }
            """);
    }

    private static HttpResponseMessage Response(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/x-amz-json-1.0")
    };
}
