using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.Firestore.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;
using Enum = System.Enum;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;
using Value = Google.Cloud.Firestore.V1.Value;

namespace Paramore.Brighter.Outbox.Firestore;

/// <summary>
/// Provides an implementation of the Brighter outbox pattern using Google Cloud Firestore.
/// This class allows for reliable message sending by persisting messages to Firestore
/// within a transaction before they are dispatched. It supports both synchronous
/// and asynchronous operations.
/// </summary>
/// <remarks>
/// This outbox ensures that messages are durably stored in Firestore as part of a
/// transaction, preventing data loss in case of application failures before
/// message dispatch.
/// </remarks>
public class FirestoreOutbox : IAmAnOutboxSync<Message, FirestoreTransaction>, IAmAnOutboxAsync<Message, FirestoreTransaction>
{
    private readonly IAmAFirestoreConnectionProvider _connectionProvider;
    private readonly FirestoreConfiguration _configuration;
    private readonly FirestoreCollection _outboxCollection;

    /// <summary>
    /// Initializes a new instance of the <see cref="FirestoreOutbox"/> class with just
    /// the Firestore configuration. This constructor internally creates a default
    /// <see cref="FirestoreConnectionProvider"/> based on the provided configuration.
    /// </summary>
    /// <param name="configuration">The configuration settings for connecting to Firestore,
    /// including project ID, database ID, and collection names.</param>
    public FirestoreOutbox(FirestoreConfiguration configuration)
        : this(new FirestoreConnectionProvider(configuration), configuration)
    {
    }

    /// <summary>
    /// Provides an implementation of the Brighter outbox pattern using Google Cloud Firestore.
    /// This class allows for reliable message sending by persisting messages to Firestore
    /// within a transaction before they are dispatched. It supports both synchronous
    /// and asynchronous operations.
    /// </summary>
    /// <remarks>
    /// This outbox ensures that messages are durably stored in Firestore as part of a
    /// transaction, preventing data loss in case of application failures before
    /// message dispatch.
    /// </remarks>
    public FirestoreOutbox(IAmAFirestoreConnectionProvider connectionProvider, FirestoreConfiguration configuration)
    {
        _connectionProvider = connectionProvider;
        _configuration = configuration;
        if (configuration.Outbox == null || string.IsNullOrEmpty(configuration.Outbox.Name))
        {
            throw new ArgumentException("inbox collection can't be null or empty", nameof(configuration));
        }
        
        _outboxCollection = configuration.Outbox;
    }

    private const string Dispatched = "Dispatched";
    private const string IsDispatched = "IsDispatched";
    private const string Topic = "Topic";
    
    /// <inheritdoc />
    public IAmABrighterTracer? Tracer { get; set; }

    /// <inheritdoc />
    public void Add(Message message, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<FirestoreTransaction>? transactionProvider = null)
    {
        var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.message.id"] = message.Id
        };
        
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, 
                _configuration.Database,
                BoxDbOperation.Add, 
                _outboxCollection.Name,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            var write = new Write
            {
                Update = ToDocument(message), CurrentDocument = new Precondition { Exists = false }
            };

            if (transactionProvider != null)
            {
                var transaction = transactionProvider.GetTransaction();
                transaction.Add(write);
                return;
            }

            var commit = new CommitRequest { Database = _configuration.DatabasePath, Writes = { write } };
            var client = _connectionProvider.GetFirestoreClient();
            client.Commit(commit);
        }
        catch (RpcException ex)when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Ignoring Duplicated
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public void Add(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<FirestoreTransaction>? transactionProvider = null)
    {
        messages = messages.ToList();

        var spans = messages.ToDictionary(
            message => message.Id.Value,
            message =>
            {
                var dbAttributes = new Dictionary<string, string>
                {
                    ["db.operation.parameter.message.id"] = message.Id
                };

                return Tracer?.CreateDbSpan(
                    new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.Add,
                        _outboxCollection.Name,
                        dbAttributes: dbAttributes),
                        requestContext?.Span,
                        options: _configuration.Instrumentation);
            });

        try
        {
            var writes = messages.Select(message => new Write
            {
                Update = ToDocument(message), 
                CurrentDocument = new Precondition { Exists = false }
            });

            if (transactionProvider != null)
            {
                transactionProvider.GetTransaction().AddRange(writes);
            }
            else
            {
                var request = new CommitRequest { Database = _configuration.DatabasePath };
                request.Writes.AddRange(writes);

                var client = _connectionProvider.GetFirestoreClient();
                client.Commit(request, CallSettings.FromExpiration(ToExpiration(outBoxTimeout)));
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Ignoring duplicated
        }
        finally
        {
            Tracer?.EndSpans(new ConcurrentDictionary<string, Activity>(spans.Where(x => x.Value != null)!));
        }
    }

    /// <inheritdoc />
    public void Delete(Id[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null)
    {
        var spans = messageIds.ToDictionary(
            id => id.Value,
            id =>
            {
                var dbAttributes = new Dictionary<string, string>
                {
                    ["db.operation.parameter.message.id"] = id.Value
                };

                return Tracer?.CreateDbSpan(
                    new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.Delete,
                        _outboxCollection.Name,
                        dbAttributes: dbAttributes),
                    requestContext?.Span,
                    options: _configuration.Instrumentation);
            });

        try
        {
            var writes = messageIds.Select(value => new Write
            {
                Delete = _configuration.GetDocumentName(_outboxCollection.Name, value)
            });   
        
            var request = new CommitRequest { Database = _configuration.DatabasePath };
            request.Writes.AddRange(writes);
            
            var client = _connectionProvider.GetFirestoreClient();
            client.Commit(request);
        }
        finally
        {
            Tracer?.EndSpans(new ConcurrentDictionary<string, Activity>(spans.Where(x => x.Value != null)!));
        }
    }

    /// <inheritdoc />
    public IEnumerable<Message> DispatchedMessages(TimeSpan dispatchedSince, RequestContext? requestContext, int pageSize = 100,
        int pageNumber = 1, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
    {
        return BrighterAsyncContext.Run(async () => await DispatchedMessagesAsync(dispatchedSince, requestContext, pageSize, pageNumber, outBoxTimeout, args));
    }

    /// <inheritdoc />
    public Message Get(Id messageId, RequestContext? requestContext, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.id", messageId.Value }
        };
        
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.Get, _outboxCollection.Name, dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            var client = _connectionProvider.GetFirestoreClient();
            var document = client.GetDocument(
                new GetDocumentRequest { Name = _configuration.GetDocumentName(_outboxCollection.Name, messageId) },
                CallSettings.FromExpiration(ToExpiration(outBoxTimeout)));

            return ToMessage(document);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return new Message();
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public IEnumerable<Message> Get(IEnumerable<Id> messageIds, RequestContext requestContext, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
    {
        return BrighterAsyncContext.Run(async () => await GetAsync(messageIds, requestContext, outBoxTimeout, args));
    }

    /// <inheritdoc />
    public void MarkDispatched(Id id, RequestContext? requestContext, DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null)
    {
        var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.message.id"] = id.Value
        };
        
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.MarkDispatched, _outboxCollection.Name, dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            dispatchedAt ??= _configuration.TimeProvider.GetUtcNow();
            var client = _connectionProvider.GetFirestoreClient();
            var document = new Document
            {
                Name = _configuration.GetDocumentName(_outboxCollection.Name, id),
                Fields =
                {
                    [Dispatched] = new Value
                    {
                        TimestampValue = Timestamp.FromDateTimeOffset(dispatchedAt.Value)
                    },
                    [IsDispatched] = new Value
                    {
                        BooleanValue = true
                    }
                }
            };
            client.Commit(new CommitRequest
            {
                Database = _configuration.DatabasePath,
                Writes =
                {
                    new Write
                    {
                        Update = document, 
                        UpdateMask = new DocumentMask { FieldPaths = { Dispatched, IsDispatched } },
                        CurrentDocument = new Precondition { Exists = true }
                    }
                }
            });
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public IEnumerable<Message> OutstandingMessages(TimeSpan dispatchedSince, RequestContext? requestContext, int pageSize = 100,
        int pageNumber = 1, IEnumerable<RoutingKey>? trippedTopics = null, Dictionary<string, object>? args = null)
    {
        return BrighterAsyncContext.Run(async () => await OutstandingMessagesAsync(dispatchedSince, requestContext ?? new RequestContext(), pageSize, pageNumber, trippedTopics, args));
    }
    
    /// <summary>
    /// Returns all messages in the store
    /// </summary>
    /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
    /// <param name="pageNumber">Page number of results to return (default = 1)</param>
    /// <param name="args">Additional parameters required for search, if any</param>
    /// <returns>A list of messages</returns>
    public IList<Message> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object>? args = null)
    {
       var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.Get, _outboxCollection.Name),
            null,
            options: _configuration.Instrumentation);

       try
       {
           var query = new StructuredQuery
           {
               From = { new StructuredQuery.Types.CollectionSelector { CollectionId = _outboxCollection.Name  } },
               OrderBy =  
               {
                   new StructuredQuery.Types.Order
                   {
                       Field = new StructuredQuery.Types.FieldReference { FieldPath = nameof(MessageHeader.TimeStamp) },
                       Direction = StructuredQuery.Types.Direction.Ascending
                   }
               },
               Offset = pageSize * Math.Max(pageNumber - 1, 0),
               Limit = pageSize
           };

           if (args != null && args.TryGetValue("Topic", out var topic))
           {
               query.Where = new StructuredQuery.Types.Filter
               {
                   FieldFilter = new StructuredQuery.Types.FieldFilter
                   {
                       Field = new StructuredQuery.Types.FieldReference { FieldPath = Topic },
                       Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                       Value = new Value { StringValue = topic.ToString() }
                   }
               };
           }
           
           var request = new RunQueryRequest 
           {
               Parent = $"{_configuration.DatabasePath}/documents", 
               StructuredQuery = query
           };
        
           var client = _connectionProvider.GetFirestoreClient();
           return BrighterAsyncContext.Run(async () =>
           {
               var messages = new List<Message>(pageSize);

               using var response = client.RunQuery(request);
               await foreach (var doc in response.GetResponseStream())
               {
                   if (doc.Document != null)
                   {
                       messages.Add(ToMessage(doc.Document));
                   }
               }
            
               return messages;
           });
       }
       finally
       {
           Tracer?.EndSpan(span);
       }
    }

    /// <inheritdoc />
    public int GetOutstandingMessageCount(TimeSpan dispatchedSince, RequestContext? requestContext, int maxCount = 100, Dictionary<string, object>? args = null)
    {
       var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, 
                _configuration.Database,
                BoxDbOperation.Get,
                _outboxCollection.Name),
            null,
            options: _configuration.Instrumentation);

       try
       {
           var structuredAggregationQuery = new StructuredAggregationQuery
           {
               StructuredQuery = new StructuredQuery
               {
                   From =
                   {
                       new StructuredQuery.Types.CollectionSelector { CollectionId = _outboxCollection.Name }
                   },
                   Where = new StructuredQuery.Types.Filter
                   {
                       FieldFilter = new StructuredQuery.Types.FieldFilter
                       {
                           Field = new StructuredQuery.Types.FieldReference { FieldPath = IsDispatched },
                           Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                           Value = new Value { BooleanValue = false }
                       }
                   }
               },
               Aggregations =
               {
                   new StructuredAggregationQuery.Types.Aggregation
                   {
                       Count = new StructuredAggregationQuery.Types.Aggregation.Types.Count(),
                       Alias = "total_count" // Alias for the result in the response
                   }
               }
           };
           
           var request = new RunAggregationQueryRequest
           {
               Parent = _configuration.DatabasePath + "/documents", // Path to all documents in the database
               StructuredAggregationQuery = structuredAggregationQuery
           };
        
           var client = _connectionProvider.GetFirestoreClient();
           var response = client.RunAggregationQuery(request);
           return BrighterAsyncContext.Run(async () =>
           {
               await foreach (var resp in response.GetResponseStream())
               {
                   if (resp.Result.AggregateFields.TryGetValue("total_count", out var value))
                   {
                       // The aggregated count is typically returned as an Int64Value in the Value proto
                       return value.IntegerValue > int.MaxValue ? int.MaxValue : (int)value.IntegerValue;
                   }
               }

               return 0;
           });
       }
       finally
       {
           Tracer?.EndSpan(span);
       }
    }
    
    /// <inheritdoc />
    public bool ContinueOnCapturedContext { get; set; }

    /// <inheritdoc />
    public async Task AddAsync(Message message, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<FirestoreTransaction>? transactionProvider = null, CancellationToken cancellationToken = default)
    {
        var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.message.id"] = message.Id
        };
        
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.Add, _outboxCollection.Name, dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            var write = new Write
            {
                Update = ToDocument(message), CurrentDocument = new Precondition { Exists = false }
            };

            if (transactionProvider != null)
            {
                var transaction = await transactionProvider
                    .GetTransactionAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);

                transaction.Add(write);
                return;
            }

            var commit = new CommitRequest { Database = _configuration.DatabasePath, Writes = { write } };

            var client = await _connectionProvider
                .GetFirestoreClientAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            await client
                .CommitAsync(commit, CallSettings.FromCancellationToken(cancellationToken))
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Ignoring duplicated
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<FirestoreTransaction>? transactionProvider = null, CancellationToken cancellationToken = default)
    {
        messages = messages.ToList();

        var spans = messages.ToDictionary(
            message => message.Id.Value,
            message =>
            {
                var dbAttributes = new Dictionary<string, string>
                {
                    ["db.operation.parameter.message.id"] = message.Id
                };

                return Tracer?.CreateDbSpan(
                    new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.Add,
                        _outboxCollection.Name,
                        dbAttributes: dbAttributes),
                    requestContext?.Span,
                    options: _configuration.Instrumentation);
            });
        
        try
        {
            var writes = messages.Select(message => new Write
            {
                Update = ToDocument(message),
                CurrentDocument = new Precondition { Exists = false }
            });

            if (transactionProvider != null)
            {
                var transaction = await transactionProvider
                    .GetTransactionAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
                transaction.AddRange(writes);
            }
            else
            {
                var request = new CommitRequest { Database = _configuration.DatabasePath };
                request.Writes.AddRange(writes);

                var client = await _connectionProvider
                    .GetFirestoreClientAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
                
                await client
                    .CommitAsync(request, CallSettings.FromCancellationToken(cancellationToken))
                    .ConfigureAwait(ContinueOnCapturedContext);
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Ignoring duplicated
        }
        finally
        {
            Tracer?.EndSpans(new ConcurrentDictionary<string, Activity>(spans.Where(x => x.Value != null)!));
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Id[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        var spans = messageIds.ToDictionary(
            id => id.Value,
            id =>
            {
                var dbAttributes = new Dictionary<string, string>
                {
                    ["db.operation.parameter.message.id"] = id.Value
                };

                return Tracer?.CreateDbSpan(
                    new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.Delete,
                        _outboxCollection.Name,
                        dbAttributes: dbAttributes),
                    requestContext?.Span,
                    options: _configuration.Instrumentation);
            });

        try
        {
            var writes = messageIds.Select(message => new Write
            {
                Delete = _configuration.GetDocumentName(_outboxCollection.Name, message),
            });

            var request = new CommitRequest { Database = _configuration.DatabasePath };
            request.Writes.AddRange(writes);

            var client = await _connectionProvider
                .GetFirestoreClientAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            
            await client
                .CommitAsync(request)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpans(new ConcurrentDictionary<string, Activity>(spans.Where(x => x.Value != null)!));
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Message>> DispatchedMessagesAsync(TimeSpan dispatchedSince, RequestContext? requestContext, int pageSize = 100,
        int pageNumber = 1, int outboxTimeout = -1, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.DispatchedMessages, _outboxCollection.Name),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            var offset = (pageNumber - 1) * pageSize;
            var timeStamp = _configuration.TimeProvider.GetUtcNow().Subtract(dispatchedSince);
            var query = new StructuredQuery
            {
                From = { new StructuredQuery.Types.CollectionSelector { CollectionId = _outboxCollection.Name } },
                Where = new StructuredQuery.Types.Filter
                {
                    FieldFilter = new StructuredQuery.Types.FieldFilter
                    {
                        Field = new StructuredQuery.Types.FieldReference { FieldPath = Dispatched },
                        Op = StructuredQuery.Types.FieldFilter.Types.Operator.LessThanOrEqual,
                        Value = new Value { TimestampValue = Timestamp.FromDateTimeOffset(timeStamp) }
                    }
                },
                OrderBy =
                {
                    new StructuredQuery.Types.Order
                    {
                        Field = new StructuredQuery.Types.FieldReference { FieldPath = nameof(MessageHeader.TimeStamp) },
                        Direction = StructuredQuery.Types.Direction.Descending
                    }
                },
                Offset = offset,
                Limit = pageSize
            };

            var request = new RunQueryRequest
            {
                Parent = $"{_configuration.DatabasePath}/documents", StructuredQuery = query
            };

            var client = await _connectionProvider
                .GetFirestoreClientAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            
            var messages = new List<Message>();
            using var response = client.RunQuery(request);
            await foreach (var doc in response.GetResponseStream())
            {
                if (doc.Document != null)
                {
                    messages.Add(ToMessage(doc.Document));
                }
            }

            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<Message> GetAsync(Id messageId, RequestContext? requestContext, int outBoxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.message.id"] = messageId.Value
        };
        
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.Get, _outboxCollection.Name, dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {

            var client = await _connectionProvider
                .GetFirestoreClientAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            
            var document = await client
                .GetDocumentAsync(new GetDocumentRequest { Name = _configuration.GetDocumentName(_outboxCollection.Name, messageId) }, CallSettings.FromCancellationToken(cancellationToken))
                .ConfigureAwait(ContinueOnCapturedContext);

            return ToMessage(document);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return new Message();
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task MarkDispatchedAsync(Id id, RequestContext? requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.message.id"] = id.Value
        };
        
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.MarkDispatched, _outboxCollection.Name, dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);
        
        try
        {
            dispatchedAt ??= _configuration.TimeProvider.GetUtcNow();
            var document = new Document
            {
                Name = _configuration.GetDocumentName(_outboxCollection.Name, id),
                Fields =
                {
                    [Dispatched] = new Value
                    {
                        TimestampValue = Timestamp.FromDateTimeOffset(dispatchedAt.Value)
                    },
                    [IsDispatched] = new Value
                    {
                        BooleanValue = true
                    }
                }
            };

            var client = await _connectionProvider
                .GetFirestoreClientAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            
            await client
                .CommitAsync(new CommitRequest
                {
                    Database = _configuration.DatabasePath,
                    Writes =
                    {
                        new Write
                        {
                            Update = document, 
                            UpdateMask = new DocumentMask { FieldPaths = { Dispatched, IsDispatched } },
                            CurrentDocument = new Precondition { Exists = true}
                        }
                    }
                })
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task MarkDispatchedAsync(IEnumerable<Id> ids, RequestContext? requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        ids = ids.ToList();
        var spans = ids.ToDictionary(
            id => id.Value,
            id =>
            {
                var dbAttributes = new Dictionary<string, string>
                {
                    ["db.operation.parameter.message.id"] = id.Value
                };

                return Tracer?.CreateDbSpan(
                    new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.MarkDispatched,
                        _outboxCollection.Name,
                        dbAttributes: dbAttributes),
                    requestContext?.Span,
                    options: _configuration.Instrumentation);
            });

        try
        {
            var writes = ids.Select(id => new Write
            {
                Update = new Document
                {
                    Name = _configuration.GetDocumentName(_outboxCollection.Name, id),
                    Fields =
                    {
                        [Dispatched] = new Value
                        {
                            TimestampValue = Timestamp.FromDateTimeOffset(_configuration.TimeProvider.GetUtcNow())
                        },
                        [IsDispatched] = new Value
                        {
                            BooleanValue = true
                        }
                    }
                },
                UpdateMask = new DocumentMask { FieldPaths = { Dispatched, IsDispatched } }
            });
            
            var request = new CommitRequest { Database = _configuration.DatabasePath };
            request.Writes.AddRange(writes);
            
            var client = await _connectionProvider
                .GetFirestoreClientAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            
            await client
                .CommitAsync(request)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpans(new ConcurrentDictionary<string, Activity>(spans.Where(x => x.Value != null)!));
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Message>> OutstandingMessagesAsync(TimeSpan dispatchedSince, RequestContext requestContext, int pageSize = 100,
        int pageNumber = 1, IEnumerable<RoutingKey>? trippedTopics = null, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    { 
        var offset = (pageNumber - 1) * pageSize;
        var timeStamp = _configuration.TimeProvider.GetUtcNow().Subtract(dispatchedSince);
        var query = new StructuredQuery
        {
            From = { new StructuredQuery.Types.CollectionSelector { CollectionId = _outboxCollection.Name } },
            Where = new StructuredQuery.Types.Filter
            {
                CompositeFilter =  new StructuredQuery.Types.CompositeFilter
                {
                    Op = StructuredQuery.Types.CompositeFilter.Types.Operator.And,
                    Filters =
                    {
                        new  StructuredQuery.Types.Filter
                        {
                            FieldFilter = new StructuredQuery.Types.FieldFilter
                            { 
                                Field = new StructuredQuery.Types.FieldReference { FieldPath = IsDispatched }, 
                                Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal, 
                                Value = new Value { BooleanValue = false}
                            }
                        },
                        new  StructuredQuery.Types.Filter
                        {
                            FieldFilter = new StructuredQuery.Types.FieldFilter
                            { 
                                Field = new StructuredQuery.Types.FieldReference { FieldPath = nameof(MessageHeader.TimeStamp) }, 
                                Op = StructuredQuery.Types.FieldFilter.Types.Operator.LessThan, 
                                Value = new Value { TimestampValue = Timestamp.FromDateTimeOffset(timeStamp) }
                            }
                        }
                    }
                }
            },
            OrderBy =  
            {
                new StructuredQuery.Types.Order
                {
                    Field = new StructuredQuery.Types.FieldReference { FieldPath = nameof(MessageHeader.TimeStamp) },
                    Direction = StructuredQuery.Types.Direction.Descending
                }
            },
            Offset = offset,
            Limit = pageSize
        };
        
        if (trippedTopics != null)
        {
            var arrayValue = new ArrayValue();
            arrayValue.Values.AddRange(trippedTopics.Select(topic => new Value { StringValue = topic.Value }));
            query.Where.CompositeFilter.Filters.Add(new StructuredQuery.Types.Filter
            {
                FieldFilter = new StructuredQuery.Types.FieldFilter
                {
                    Field = new StructuredQuery.Types.FieldReference { FieldPath = nameof(MessageHeader.Topic) },
                    Op = StructuredQuery.Types.FieldFilter.Types.Operator.NotIn,
                    Value = new Value { ArrayValue = arrayValue }
                }
            });
        }

        var request = new RunQueryRequest 
        {
            Parent = $"{_configuration.DatabasePath}/documents", 
            StructuredQuery = query
        };
        
        var client = await _connectionProvider
            .GetFirestoreClientAsync(cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
        
        var messages = new List<Message>(pageSize);

        using var response = client.RunQuery(request);
        await foreach (var doc in response.GetResponseStream())
        {
            if (doc.Document != null)
            {
                messages.Add(ToMessage(doc.Document));
            }
        }
        
        return messages;
    }

    /// <summary>
    /// Returns all messages in the store
    /// </summary>
    /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
    /// <param name="pageNumber">Page number of results to return (default = 1)</param>
    /// <param name="args">Additional parameters required for search, if any</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A list of messages</returns>
    public async Task<IEnumerable<Message>> GetAsync(int pageSize = 100, int pageNumber = 1, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
       var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, _configuration.Database, BoxDbOperation.Get, _outboxCollection.Name),
            null,
            options: _configuration.Instrumentation);

       try
       {
           var query = new StructuredQuery
           {
               From = { new StructuredQuery.Types.CollectionSelector { CollectionId = _outboxCollection.Name } },
               OrderBy =
               {
                   new StructuredQuery.Types.Order
                   {
                       Field = new StructuredQuery.Types.FieldReference { FieldPath = nameof(MessageHeader.TimeStamp) },
                       Direction = StructuredQuery.Types.Direction.Ascending
                   }
               },
               Offset = pageSize * Math.Max(pageNumber - 1, 0),
               Limit = pageSize
           };

           if (args != null && args.TryGetValue("Topic", out var topic))
           {
               query.Where.CompositeFilter.Filters.Add(new StructuredQuery.Types.Filter
               {
                   FieldFilter = new StructuredQuery.Types.FieldFilter
                   {
                       Field = new StructuredQuery.Types.FieldReference { FieldPath = Topic },
                       Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                       Value = new Value { StringValue = topic.ToString() }
                   }
               });
           }

           var request = new RunQueryRequest
           {
               Parent = $"{_configuration.DatabasePath}/documents", 
               StructuredQuery = query
           };

           var messages = new List<Message>();
           var client = await _connectionProvider.GetFirestoreClientAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
           using var response = client.RunQuery(request);
           await foreach (var doc in response.GetResponseStream())
           {
               if (doc.Document != null)
               {
                   messages.Add(ToMessage(doc.Document));
               }
           }

           return messages;
       }
       finally
       {
           Tracer?.EndSpan(span);
       }
    }

    /// <summary>
    /// Returns messages specified by the Ids
    /// </summary>
    /// <param name="messageIds">The Ids of the messages</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
    /// <param name="outBoxTimeout">The Timeout of the outbox.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A list of messages</returns>
    public async Task<IEnumerable<Message>> GetAsync(IEnumerable<Id> messageIds, RequestContext requestContext, int outBoxTimeout = -1, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        var ids = messageIds.Select(id => id.Value).ToArray();
        
        var dbAttributes = new Dictionary<string, string>
        {
            {"db.operation.parameter.message.ids", string.Join(",", ids)}
        };
        
       var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, 
                _configuration.Database,
                BoxDbOperation.Get,
                _outboxCollection.Name,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

       var arrayOfIds = new ArrayValue();
       arrayOfIds.Values.AddRange(ids.Select(id => new Value { StringValue = id}));
       try
       {
           var query = new StructuredQuery
           {
               From = { new StructuredQuery.Types.CollectionSelector { CollectionId = _outboxCollection.Name  } },
               Where = new StructuredQuery.Types.Filter
               {
                   FieldFilter = new StructuredQuery.Types.FieldFilter
                   {
                       Field = new StructuredQuery.Types.FieldReference { FieldPath = nameof(MessageHeader.MessageId) },
                       Op = StructuredQuery.Types.FieldFilter.Types.Operator.In,
                       Value = new Value { ArrayValue = arrayOfIds }
                   }
               },
               OrderBy =  
               {
                   new StructuredQuery.Types.Order
                   {
                       Field = new StructuredQuery.Types.FieldReference { FieldPath = nameof(MessageHeader.TimeStamp) },
                       Direction = StructuredQuery.Types.Direction.Ascending
                   }
               }
           };

           var request = new RunQueryRequest 
           {
               Parent = $"{_configuration.DatabasePath}/documents", 
               StructuredQuery = query
           };
        
           var client = await _connectionProvider.GetFirestoreClientAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
           var messages = new List<Message>();

           using var response = client.RunQuery(request);
           await foreach (var doc in response.GetResponseStream())
           {
               messages.Add(ToMessage(doc.Document));
           }
            
           return messages;
       }
       finally
       {
           Tracer?.EndSpan(span);
       }
    }

    /// <inheritdoc />
    public async Task<int> GetOutstandingMessageCountAsync(TimeSpan dispatchedSince, RequestContext? requestContext, int maxCount = 100, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
       var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore, 
                _configuration.Database,
                BoxDbOperation.Get,
                _outboxCollection.Name),
            null,
            options: _configuration.Instrumentation);

       try
       {
           var structuredAggregationQuery = new StructuredAggregationQuery
           {
               StructuredQuery = new StructuredQuery
               {
                   From =
                   {
                       new StructuredQuery.Types.CollectionSelector { CollectionId = _outboxCollection.Name }
                   },
                   Where = new StructuredQuery.Types.Filter
                   {
                       FieldFilter = new StructuredQuery.Types.FieldFilter
                       {
                           Field = new StructuredQuery.Types.FieldReference { FieldPath = IsDispatched },
                           Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                           Value = new Value { BooleanValue = false }
                       }
                   }
               },
               Aggregations =
               {
                   new StructuredAggregationQuery.Types.Aggregation
                   {
                       Count = new StructuredAggregationQuery.Types.Aggregation.Types.Count(),
                       Alias = "total_count" // Alias for the result in the response
                   }
               }
           };
           
           var request = new RunAggregationQueryRequest
           {
               Parent = $"{_configuration.DatabasePath}/documents", // Path to all documents in the database
               StructuredAggregationQuery = structuredAggregationQuery
           };
        
           var client = await _connectionProvider.GetFirestoreClientAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
           var response = client.RunAggregationQuery(request);
           await foreach (var resp in response.GetResponseStream())
           {
               if (resp.Result.AggregateFields.TryGetValue("total_count", out var value))
               {
                   // The aggregated count is typically returned as an Int64Value in the Value proto
                   return value.IntegerValue > int.MaxValue ? int.MaxValue : (int)value.IntegerValue;
               }
           }

           return 0;
       }
       finally
       {
           Tracer?.EndSpan(span);
       }
    }

    private static Expiration? ToExpiration(int outboxTimeout)
        => outboxTimeout == -1 ? null : Expiration.FromTimeout(TimeSpan.FromMilliseconds(outboxTimeout));

    private Document ToDocument(Message message)
    {
        Value ttl;
        if (_outboxCollection.Ttl.HasValue && _outboxCollection.Ttl != TimeSpan.Zero)
        {
            ttl = new Value { TimestampValue = Timestamp.FromDateTimeOffset(_configuration.TimeProvider.GetUtcNow() + _outboxCollection.Ttl.Value) };
        }
        else
        {
            ttl = new Value { NullValue = NullValue.NullValue };
        }
        
        var doc = new Document 
        { 
            Name = _configuration.GetDocumentName(_outboxCollection.Name, message.Id), 
            Fields = 
            { 
                ["Ttl"] = ttl,
                [Dispatched] = new Value { NullValue = NullValue.NullValue },
                [IsDispatched] = new Value { BooleanValue = false },
                [nameof(MessageHeader.HandledCount)] = new Value { IntegerValue = message.Header.HandledCount }, 
                [nameof(MessageHeader.MessageId)] = new Value { StringValue = message.Header.MessageId.ToString() },
                [nameof(MessageHeader.MessageType)] = new Value { StringValue = message.Header.MessageType.ToString() },
                [nameof(MessageHeader.SpecVersion)] = new Value { StringValue = message.Header.SpecVersion },
                [nameof(MessageHeader.Source)] = new Value { StringValue = message.Header.Source.ToString() },
                [nameof(MessageHeader.Topic)] = new Value { StringValue = message.Header.Topic.Value },
                [nameof(MessageHeader.TimeStamp)] = new Value { TimestampValue = Timestamp.FromDateTimeOffset(message.Header.TimeStamp) },
                [nameof(Message.Body)] = new Value { BytesValue = ByteString.CopyFrom(message.Body.Bytes) },
                [nameof(MessageHeader.ContentType)] = new Value { StringValue = message.Header.ContentType.ToString() }
            } 
        };

        if (message.Header.Type != CloudEventsType.Empty)
        {
            doc.Fields[nameof(MessageHeader.Type)] = new Value { StringValue = message.Header.Type };
        }

        if (message.Header.Bag.Count > 0)
        {
            doc.Fields[nameof(MessageHeader.Bag)] = new Value { BytesValue = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(message.Header.Bag, JsonSerialisationOptions.Options)) };
        }

        doc.Fields[nameof(MessageHeader.Baggage)] = new Value{ StringValue = message.Header.Baggage.ToString() };

        if (!Id.IsNullOrEmpty(message.Header.CorrelationId))
        {
            doc.Fields[nameof(MessageHeader.CorrelationId)] = new Value { StringValue = message.Header.CorrelationId.Value };
        }
        
        if (!string.IsNullOrEmpty(message.Header.DataRef))
        {
            doc.Fields[nameof(MessageHeader.DataRef)] = new Value { StringValue = message.Header.DataRef };
        }
        
        if (message.Header.DataSchema != null)
        {
            doc.Fields[nameof(MessageHeader.DataSchema)] = new Value { StringValue = message.Header.DataSchema.ToString() };
        }

        if (message.Header.Delayed != TimeSpan.Zero)
        {
            doc.Fields[nameof(MessageHeader.Delayed)] = new Value { StringValue = message.Header.Delayed.ToString() };
        }
        
        if (!PartitionKey.IsNullOrEmpty(message.Header.PartitionKey))
        {
            doc.Fields[nameof(MessageHeader.PartitionKey)] = new Value { StringValue = message.Header.PartitionKey.ToString() };
        }
        
        if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo))
        {
            doc.Fields[nameof(MessageHeader.ReplyTo)] = new Value { StringValue = message.Header.ReplyTo.Value };
        }
        
        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            doc.Fields[nameof(MessageHeader.Subject)] = new Value { StringValue = message.Header.Subject };
        }
        
        if (message.Header.TraceParent != null)
        {
            doc.Fields[nameof(MessageHeader.TraceParent)] = new Value { StringValue = message.Header.TraceParent.Value };
        }
        
        if (message.Header.TraceState != null)
        {
            doc.Fields[nameof(MessageHeader.TraceState)] = new Value { StringValue = message.Header.TraceState.Value };
        }

        if (!Id.IsNullOrEmpty(message.Header.WorkflowId))
        {
            doc.Fields[nameof(MessageHeader.WorkflowId)] = new Value { StringValue = message.Header.WorkflowId.Value };
        }
        
        if (!Id.IsNullOrEmpty(message.Header.JobId))
        {
            doc.Fields[nameof(MessageHeader.JobId)] = new Value { StringValue = message.Header.JobId.Value };
        }
        
        return doc;
    }

    private static Message ToMessage(Document document)
    {
        var messageId = Id.Create(document.Fields[nameof(MessageHeader.MessageId)].StringValue);
#if NETSTANDARD
        var messageType = (MessageType)Enum.Parse(typeof(MessageType), document.Fields[nameof(MessageHeader.MessageType)].StringValue);
#else
        var messageType = Enum.Parse<MessageType>(document.Fields[nameof(MessageHeader.MessageType)].StringValue);
#endif 
        var specVersion = document.Fields[nameof(MessageHeader.SpecVersion)].StringValue;
        var topic = new RoutingKey(document.Fields[nameof(MessageHeader.Topic)].StringValue);
        var timeStamp = document.Fields[nameof(MessageHeader.TimeStamp)].TimestampValue.ToDateTimeOffset();
        var body = document.Fields[nameof(Message.Body)].BytesValue.ToByteArray();
        
        var type = CloudEventsType.Empty;
        if (document.Fields.TryGetValue(nameof(MessageHeader.Type), out var typeValue))
        {
            type = new CloudEventsType(typeValue.StringValue);
        }
        
        var bag = new Dictionary<string, object>();
        if (document.Fields.TryGetValue(nameof(MessageHeader.Bag), out var bagValue))
        {
            bag = JsonSerializer.Deserialize<Dictionary<string, object>>(bagValue.BytesValue.Span, JsonSerialisationOptions.Options)!;
        }

        var baggage = new Baggage();
        if (document.Fields.TryGetValue(nameof(MessageHeader.Baggage), out var baggageValue))
        {
            baggage.LoadBaggage(baggageValue.StringValue);
        }
        
        ContentType? contentType = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.ContentType), out var contentTypeValue))
        {
            contentType = new ContentType(contentTypeValue.StringValue);
        }
        
        Id? correlationId = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.CorrelationId), out var correlationIdValue))
        {
            correlationId = new Id(correlationIdValue.StringValue);
        }
        
        string? dataRef = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.DataRef), out var dataRefValue))
        {
            dataRef = dataRefValue.StringValue;
        }
        
        Uri? dataSchema = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.DataSchema), out var dataSchemaValue))
        {
            Uri.TryCreate(dataSchemaValue.StringValue, UriKind.RelativeOrAbsolute, out dataSchema);
        }
        
        PartitionKey? partitionKey = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.PartitionKey), out var partitionKeyValue))
        {
            partitionKey = new PartitionKey(partitionKeyValue.StringValue);
        }
        
        RoutingKey? replyTo = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.ReplyTo), out var replyToValue))
        {
            replyTo = new RoutingKey(replyToValue.StringValue);
        }
        
        string? subject = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.Subject), out var subjectValue))
        {
            subject = subjectValue.StringValue;
        }
        
        Uri source = new Uri(MessageHeader.DefaultSource);
        if (document.Fields.TryGetValue(nameof(MessageHeader.Source), out var sourceValue)
            && Uri.TryCreate(sourceValue.StringValue, UriKind.RelativeOrAbsolute, out var tmp))
        {
            source = tmp;
        }
        
        TraceParent? traceParent = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.TraceParent), out var traceParentValue))
        {
            traceParent = new TraceParent(traceParentValue.StringValue);
        }
        
        TraceState? traceState = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.TraceState), out var traceStateValue))
        {
            traceState  = new TraceState(traceStateValue.StringValue);
        }

        Id? workflowId = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.WorkflowId), out var workflowIdValue))
        {
            workflowId = Id.Create(workflowIdValue.StringValue);
        }
        
        Id? jobId = null;
        if (document.Fields.TryGetValue(nameof(MessageHeader.JobId), out var jobIdValue))
        {
            jobId = Id.Create(jobIdValue.StringValue);
        }
        
        return new Message(
            new MessageHeader(
                messageId: messageId,
                topic: topic,
                messageType: messageType,
                source: source,
                type: type,
                timeStamp: timeStamp,
                correlationId: correlationId,
                replyTo: replyTo,
                contentType: contentType,
                partitionKey: partitionKey,
                dataSchema: dataSchema,
                subject: subject,
                handledCount: 0,
                delayed: TimeSpan.Zero,
                traceParent: traceParent,
                traceState: traceState,
                baggage: baggage,
                jobId: jobId,
                workflowId: workflowId)
            {
                Bag = bag,
                DataRef = dataRef,
                SpecVersion = specVersion
            },
            new MessageBody(body));
    }
}
