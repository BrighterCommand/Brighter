#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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


using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Inbox.DynamoDB.V4;

public class DynamoDbInbox : IAmAnInboxSync, IAmAnInboxAsync
{
    private readonly DynamoDBContext _context;
    private readonly SaveConfig  _saveConfig;
    private readonly FromQueryConfig _fromQueryConfig;
    private readonly DynamoDbInboxConfiguration _configuration;
    private readonly InstrumentationOptions _instrumentationOptions;

    private const string DYNAMO_DB_NAME = "inbox";

    /// <inheritdoc/>
    public bool ContinueOnCapturedContext { get; set; }

    /// <inheritdoc/>
    public IAmABrighterTracer Tracer { private get; set; }

    /// <summary>
    ///     Initialises a new instance of the <see cref="DynamoDbInbox"/> class.
    /// </summary>
    /// <param name="client">The Amazon Dynamo Db client to use</param>
    public DynamoDbInbox(IAmazonDynamoDB client, DynamoDbInboxConfiguration configuration, 
        InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
    {
        var builder = new DynamoDBContextBuilder();
        builder.WithDynamoDBClient(() => client);
        _context = builder.Build();
            
        _configuration = configuration;
        _instrumentationOptions = instrumentationOptions;
        _saveConfig = new SaveConfig
        {
            OverrideTableName = configuration.TableName
        };

        _fromQueryConfig = new FromQueryConfig 
        {
            OverrideTableName = configuration.TableName
        };
    }

    /// <summary>
    ///   Adds a command to the store.
    ///   Will block, and consume another thread for callback on threadpool; use within sync pipeline only.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="command">The command.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as DynamoDB handles timeout and retries</param>
    public void Add<T>(T command, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        // Note: Don't add a span here as we call AddAsync
        AddAsync(command, contextKey, requestContext)
            .ConfigureAwait(ContinueOnCapturedContext)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///   Finds a command with the specified identifier.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as DynamoDB handles timeout and retries</param>
    /// <returns><see cref="T"/></returns>
    public T Get<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        // Note: Don't add a span here as we call GetAsync
        return GetAsync<T>(id, contextKey, requestContext, timeoutInMilliseconds, CancellationToken.None)
            .ConfigureAwait(ContinueOnCapturedContext)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///   Checks whether a command with the specified identifier exists in the store.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as DynamoDB handles timeout and retries</param>
    /// <returns><see langword="true"/> if it exists, otherwise <see langword="false"/>.</returns>
    public bool Exists<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        // Note: Don't add a span here as we call ExistsAsync
        return ExistsAsync<T>(id, contextKey, requestContext)
            .ConfigureAwait(ContinueOnCapturedContext)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///   Awaitably adds a command to the store.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="command">The command.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as DynamoDB handles timeout and retries</param>
    /// <param name="cancellationToken">Allow the sender to cancel the operation, if the parameter is supplied</param>
    /// <returns><see cref="Task"/>.</returns>
    public async Task AddAsync<T>(T command, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.command.id", command.Id}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Dynamodb, DYNAMO_DB_NAME, BoxDbOperation.Add, _configuration.TableName, dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _instrumentationOptions);

        try
        {
            await _context
                .SaveAsync(new CommandItem<T>(command, contextKey), _saveConfig, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    ///   Awaitably finds a command with the specified identifier.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as DynamoDB handles timeout and retries</param>
    /// <param name="cancellationToken">Allow the sender to cancel the operation, if the parameter is supplied</param>
    /// <returns><see cref="Task{T}"/>.</returns>
    public async Task<T> GetAsync<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.command.id", id}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Dynamodb, DYNAMO_DB_NAME, BoxDbOperation.Get, _configuration.TableName, dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _instrumentationOptions);

        try
        {
            return await GetCommandAsync<T>(id, contextKey, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Checks if the command exists based on the id
    /// </summary>
    /// <param name="id">The identifier</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as DynamoDB handles timeout and retries</param>
    /// <param name="cancellationToken">Allow the sender to cancel the request, optional</param>
    /// <typeparam name="T">Type of command being checked</typeparam>
    /// <returns><see cref="Task{true}"/> if it exists, otherwise <see cref="Task{false}"/>.</returns>
    public async Task<bool> ExistsAsync<T>(string id, string contextKey, RequestContext requestContext, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.command.id", id}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Dynamodb, DYNAMO_DB_NAME, BoxDbOperation.Exists, _configuration.TableName, dbAttributes: dbAttributes),
            requestContext?.Span,
            options: _instrumentationOptions);

        try
        {
            var command = await GetCommandAsync<T>(id, contextKey, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            return command != null;
        }
        catch (RequestNotFoundException<T>)
        {
            return false;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    private async Task<T> GetCommandAsync<T>(string id, string contextKey, CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var queryConfig = new QueryOperationConfig
        {
            KeyExpression = new KeyIdContextExpression().Generate(id, contextKey),
            ConsistentRead = true
        };
           
        //block async to make this sync
        var messages = await PageAllMessagesAsync<T>(queryConfig).ConfigureAwait(ContinueOnCapturedContext);

        var result = messages.Select(msg => msg.ConvertToCommand()).FirstOrDefault();
        if (result == null)
            throw new RequestNotFoundException<T>(id);

        return result;
    }

    private async Task<IEnumerable<CommandItem<T>>> PageAllMessagesAsync<T>(QueryOperationConfig queryConfig) 
        where T: class, IRequest 
    {
        var asyncSearch = _context.FromQueryAsync<CommandItem<T>>(queryConfig, _fromQueryConfig);
            
        var messages = new List<CommandItem<T>>();
        do
        { 
            messages.AddRange(await asyncSearch.GetNextSetAsync().ConfigureAwait(ContinueOnCapturedContext));
        } while (!asyncSearch.IsDone);

        return messages;
    }
}
