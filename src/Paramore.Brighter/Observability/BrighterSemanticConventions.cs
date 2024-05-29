#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Observability;

/// <summary>
/// What are the names for spans, events, and attributes in Brighter
/// </summary>
public static class BrighterSemanticConventions
{
    public const string AddToOutbox = "paramore.brighter.outbox.add";
    public const string CeMessageId = "cloudevents.event_id";
    public const string DbInstanceId = "db.instance.id";
    public const string DbName = "db.name";
    public const string DbOperation = "db.operation";
    public const string DbStatement = "db.statement";
    public const string DbSystem = "db.system";
    public const string DbTable = "db.table";
    public const string DbUser = "db.user";
    public const string GetFromOutbox = "paramore.brighter.outbox.get";
    public const string HandlerName = "paramore.brighter.handler.name";
    public const string HandlerType = "paramore.brighter.handler.type";
    public const string IsSink = "paramore.brighter.is_sink";
    public const string MapperName = "paramore.brighter.mapper.name";
    public const string MapperType = "paramore.brighter.mapper.type";
    public const string MessageBody = "messaging.message.body";
    public const string MessageHeaders = "messaging.message.headers";
    public const string MessageId = "messaging.message.id";
    public const string MessageType = "messaging.message.type";
    public const string MessagingDestination = "messaging.destination";
    public const string MessagingDestinationPartitionId = "messaging.destination.partition.id";
    public const string MessageBodySize = "messaging.message.body.size";
    public const string NetworkPeerAddress = "network.peer.address";
    public const string NetworkPeerPort = "network.peer.port";
    public const string Operation = "paramore.brighter.operation";
    public const string OutboxSharedTransaction = "paramore.brighter.outbox.shared_transaction";
    public const string OutboxType = "paramore.brighter.outbox.type";
    public const string RequestId = "paramore.brighter.request.id"; 
    public const string RequestType = "paramore.brighter.request.type"; 
    public const string RequestBody = "paramore.brighter.request.body";
    public const string ServerAddress = "server.address";
    public const string ServerPort ="server.port";
    public const string SourceName = "Paramore.Brighter";         
}
