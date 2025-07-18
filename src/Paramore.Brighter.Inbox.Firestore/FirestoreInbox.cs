using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.Firestore.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.Firestore.Extensions;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;
using Document = Google.Cloud.Firestore.V1.Document;
using Value = Google.Cloud.Firestore.V1.Value;

namespace Paramore.Brighter.Inbox.Firestore;

/// <summary>
/// Implements the Brighter inbox pattern using Google Cloud Firestore.
/// This class is responsible for tracking processed messages to prevent duplicate processing
/// in a distributed system, ensuring "at-most-once" delivery semantics for consumers.
/// </summary>
/// <remarks>
/// The inbox stores a record for each processed message, typically including the message ID
/// and a context key (e.g., the handler's name) to uniquely identify the processing event.
/// </remarks>
public class FirestoreInbox : IAmAnInboxSync, IAmAnInboxAsync
{
    private readonly IAmAFirestoreConnectionProvider _connectionProvider;
    private readonly FirestoreConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="FirestoreInbox"/> class with just
    /// the Firestore configuration. This constructor internally creates a default
    /// <see cref="FirestoreConnectionProvider"/> based on the provided configuration.
    /// </summary>
    /// <param name="configuration">The configuration settings for connecting to Firestore,
    /// including project ID, database ID, and collection names for inbox entries.</param>
    public FirestoreInbox(FirestoreConfiguration configuration)
        : this(new FirestoreConnectionProvider(configuration), configuration)
    {
    }

    /// <summary>
    /// Implements the Brighter inbox pattern using Google Cloud Firestore.
    /// This class is responsible for tracking processed messages to prevent duplicate processing
    /// in a distributed system, ensuring "at-most-once" delivery semantics for consumers.
    /// </summary>
    /// <remarks>
    /// The inbox stores a record for each processed message, typically including the message ID
    /// and a context key (e.g., the handler's name) to uniquely identify the processing event.
    /// </remarks>
    public FirestoreInbox(IAmAFirestoreConnectionProvider connectionProvider, FirestoreConfiguration configuration)
    {
        _connectionProvider = connectionProvider;
        _configuration = configuration;

        if (string.IsNullOrEmpty(configuration.Inbox))
        {
            throw new ArgumentException("inbox collection can't be null or empty", nameof(configuration));
        }
    }

    /// <inheritdoc />
    public IAmABrighterTracer? Tracer { get; set; }
    
    /// <inheritdoc />
    public void Add<T>(T command, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.command.id"] = command.Id,
            ["db.operation.parameter.command.context_key"] = contextKey,
        };
        
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore,
                _configuration.Database,
                BoxDbOperation.Add,
                _configuration.Inbox!,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            var request = new CommitRequest
            {
                Database = _configuration.Database,
                Writes =
                {
                    new Write
                    {
                        Update = ToDocument(contextKey, command),
                        CurrentDocument = new Precondition { Exists = false }
                    }
                }
            };

            var client = _connectionProvider.GetFirestoreClient();
            client.Commit(request, CallSettings.FromExpiration(timeoutInMilliseconds.ToExpiration()));
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public T Get<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.command.id"] = id,
            ["db.operation.parameter.command.context_key"] = contextKey,
        };

        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore,
                _configuration.Database,
                BoxDbOperation.Get,
                _configuration.Inbox!,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            var query = new StructuredQuery
            {
                From = { new StructuredQuery.Types.CollectionSelector { CollectionId = _configuration.Inbox! } },
                Where = new StructuredQuery.Types.Filter
                {
                    CompositeFilter = new StructuredQuery.Types.CompositeFilter
                    {
                        Op = StructuredQuery.Types.CompositeFilter.Types.Operator.And,
                        Filters =
                        {
                            new StructuredQuery.Types.Filter
                            {
                                FieldFilter = new StructuredQuery.Types.FieldFilter
                                {
                                    Field =
                                        new StructuredQuery.Types.FieldReference { FieldPath = "Id" },
                                    Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                                    Value = new Value { StringValue = id }
                                }
                            },
                            new StructuredQuery.Types.Filter
                            {
                                FieldFilter = new StructuredQuery.Types.FieldFilter
                                {
                                    Field =
                                        new StructuredQuery.Types.FieldReference { FieldPath = "ContextKey" },
                                    Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                                    Value = new Value { StringValue = contextKey }
                                }
                            },
                        }
                    }
                },
                Limit = 1
            };

            var request = new RunQueryRequest
            {
                Parent = $"{_configuration.DatabasePath}/documents", StructuredQuery = query
            };

            var client = _connectionProvider.GetFirestoreClient();
            using var response = client.RunQuery(request);
            var stream = response.GetResponseStream();

            return BrighterAsyncContext.Run(async () =>
            {
                if (!await stream.MoveNextAsync())
                {
                    throw new RequestNotFoundException<T>(id);
                }
                
                var document = stream.Current.Document;
                return ToRequest<T>(document);
            });
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public bool Exists<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.command.id"] = id,
            ["db.operation.parameter.command.context_key"] = contextKey,
        };

        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore,
                _configuration.Database,
                BoxDbOperation.Exists,
                _configuration.Inbox!,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            var query = new StructuredQuery
            {
                From = { new StructuredQuery.Types.CollectionSelector { CollectionId = _configuration.Inbox! } },
                Where = new StructuredQuery.Types.Filter
                {
                    CompositeFilter = new StructuredQuery.Types.CompositeFilter
                    {
                        Op = StructuredQuery.Types.CompositeFilter.Types.Operator.And,
                        Filters =
                        {
                            new StructuredQuery.Types.Filter
                            {
                                FieldFilter = new StructuredQuery.Types.FieldFilter
                                {
                                    Field =
                                        new StructuredQuery.Types.FieldReference { FieldPath = "Id" },
                                    Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                                    Value = new Value { StringValue = id }
                                }
                            },
                            new StructuredQuery.Types.Filter
                            {
                                FieldFilter = new StructuredQuery.Types.FieldFilter
                                {
                                    Field =
                                        new StructuredQuery.Types.FieldReference { FieldPath = "ContextKey" },
                                    Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                                    Value = new Value { StringValue = contextKey }
                                }
                            },
                        }
                    }
                },
                Limit = 1
            };

            var request = new RunQueryRequest
            {
                Parent = $"{_configuration.DatabasePath}/documents", StructuredQuery = query
            };

            var client = _connectionProvider.GetFirestoreClient();
            using var response = client.RunQuery(request);
            var stream = response.GetResponseStream();
            return BrighterAsyncContext.Run(async () => await stream.MoveNextAsync());
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task AddAsync<T>(T command, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.command.id"] = command.Id,
            ["db.operation.parameter.command.context_key"] = contextKey,
        };
        
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore,
                _configuration.Database,
                BoxDbOperation.Add,
                _configuration.Inbox!,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            var request = new CommitRequest
            {
                Database = _configuration.Database,
                Writes =
                {
                    new Write
                    {
                        Update = ToDocument(contextKey, command),
                        CurrentDocument = new Precondition { Exists = false }
                    }
                }
            };

            var client = await _connectionProvider
                .GetFirestoreClientAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            
            await client
                .CommitAsync(request, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
          var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.command.id"] = id,
            ["db.operation.parameter.command.context_key"] = contextKey,
        };

        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore,
                _configuration.Database,
                BoxDbOperation.Get,
                _configuration.Inbox!,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            var query = new StructuredQuery
            {
                From = { new StructuredQuery.Types.CollectionSelector { CollectionId = _configuration.Inbox! } },
                Where = new StructuredQuery.Types.Filter
                {
                    CompositeFilter = new StructuredQuery.Types.CompositeFilter
                    {
                        Op = StructuredQuery.Types.CompositeFilter.Types.Operator.And,
                        Filters =
                        {
                            new StructuredQuery.Types.Filter
                            {
                                FieldFilter = new StructuredQuery.Types.FieldFilter
                                {
                                    Field =
                                        new StructuredQuery.Types.FieldReference { FieldPath = "Id" },
                                    Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                                    Value = new Value { StringValue = id }
                                }
                            },
                            new StructuredQuery.Types.Filter
                            {
                                FieldFilter = new StructuredQuery.Types.FieldFilter
                                {
                                    Field =
                                        new StructuredQuery.Types.FieldReference { FieldPath = "ContextKey" },
                                    Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                                    Value = new Value { StringValue = contextKey }
                                }
                            },
                        }
                    }
                },
                Limit = 1
            };

            var request = new RunQueryRequest
            {
                Parent = $"{_configuration.DatabasePath}/documents", StructuredQuery = query
            };

            var client = await _connectionProvider
                .GetFirestoreClientAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            
            using var response = client.RunQuery(request);
            var stream = response.GetResponseStream();
            if (!await stream.MoveNextAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                var document = stream.Current.Document;
                return ToRequest<T>(document);
            }

            throw new RequestNotFoundException<T>(id);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>
        {
            ["db.operation.parameter.command.id"] = id,
            ["db.operation.parameter.command.context_key"] = contextKey,
        };

        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Firestore,
                _configuration.Database,
                BoxDbOperation.Exists,
                _configuration.Inbox!,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _configuration.Instrumentation);

        try
        {
            var query = new StructuredQuery
            {
                From = { new StructuredQuery.Types.CollectionSelector { CollectionId = _configuration.Inbox! } },
                Where = new StructuredQuery.Types.Filter
                {
                    CompositeFilter = new StructuredQuery.Types.CompositeFilter
                    {
                        Op = StructuredQuery.Types.CompositeFilter.Types.Operator.And,
                        Filters =
                        {
                            new StructuredQuery.Types.Filter
                            {
                                FieldFilter = new StructuredQuery.Types.FieldFilter
                                {
                                    Field =
                                        new StructuredQuery.Types.FieldReference { FieldPath = "Id" },
                                    Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                                    Value = new Value { StringValue = id }
                                }
                            },
                            new StructuredQuery.Types.Filter
                            {
                                FieldFilter = new StructuredQuery.Types.FieldFilter
                                {
                                    Field =
                                        new StructuredQuery.Types.FieldReference { FieldPath = "ContextKey" },
                                    Op = StructuredQuery.Types.FieldFilter.Types.Operator.Equal,
                                    Value = new Value { StringValue = contextKey }
                                }
                            },
                        }
                    }
                },
                Limit = 1
            };

            var request = new RunQueryRequest
            {
                Parent = $"{_configuration.DatabasePath}/documents", StructuredQuery = query
            };

            var client = await _connectionProvider
                .GetFirestoreClientAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            
            using var response = client.RunQuery(request);
            var stream = response.GetResponseStream();
            return await stream
                .MoveNextAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public bool ContinueOnCapturedContext { get; set; }

    private Document ToDocument<T>(string contextKey, T request)
        where T : class, IRequest
    {
        return new Document 
        { 
            Name = _configuration.GetDocumentName(_configuration.Inbox!, request.Id),
            Fields =
            {
                ["Id"] = new Value { StringValue = request.Id },
                ["Body"] = new Value { BytesValue = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(request, JsonSerialisationOptions.Options)) },
                ["ContextKey"] = new Value { StringValue = contextKey },
                ["Timestamp"] = new Value { TimestampValue = Timestamp.FromDateTimeOffset(_configuration.TimeProvider.GetUtcNow()) },
                ["Type"] = new Value { StringValue = typeof(T).FullName }
            }
        };
    }

    private T ToRequest<T>(Document document)
        where T : class, IRequest
    {
        return JsonSerializer.Deserialize<T>(document.Fields["Body"].BytesValue.ToByteArray(), JsonSerialisationOptions.Options)!;
    }
}
