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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.CommandStore.DynamoDB
{
    /// <summary>
    ///     Class DynamoDbCommandStore
    /// </summary>
    public class DynamoDbCommandStore : IAmACommandStore, IAmACommandStoreAsync
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<DynamoDbCommandStore>);
       
        private readonly DynamoDBContext _context;
        private readonly DynamoDbCommandStoreConfiguration _configuration;
        private readonly DynamoDBOperationConfig _operationConfig;
        private readonly DynamoDBOperationConfig _queryOperationConfig;

        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Initialises a new instance of the <see cref="DynamoDbCommandStore"/> class.
        /// </summary>
        /// <param name="context">The DynamoDBContext</param>
        /// <param name="configuration">The DynamoDB Operation Configuration</param>
        public DynamoDbCommandStore(DynamoDBContext context, DynamoDbCommandStoreConfiguration configuration)
        {            
            _context = context;
            _configuration = configuration;
            _operationConfig = new DynamoDBOperationConfig
            {
                OverrideTableName = configuration.TableName,
                ConsistentRead = configuration.UseStronglyConsistentRead   
            };
            
            _queryOperationConfig = new DynamoDBOperationConfig
            {
                OverrideTableName = configuration.TableName,
                ConsistentRead = false,
                IndexName = configuration.CommandIdIndex
            };
        }

        /// <summary>
        ///     Adds a command to the store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command to be stored</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        public void Add<T>(T command, int timeoutInMilliseconds = -1) where T : class, IRequest
        {            
            AddAsync(command).ConfigureAwait(ContinueOnCapturedContext).GetAwaiter().GetResult();
        }

        /// <summary>
        ///     Finds a command with the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <returns><see cref="T"/></returns>
        public T Get<T>(Guid id, int timeoutInMilliseconds = -1) where T : class, IRequest, new()
        {
            return GetCommandFromDynamo<T>(id).ConfigureAwait(ContinueOnCapturedContext).GetAwaiter().GetResult();
        }

        /// <summary>
        ///     Adds a command to the store
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command to be stored</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>D
        public async Task AddAsync<T>(T command, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var storedCommand = new DynamoDbCommand<T>(command);

            await _context.SaveAsync(storedCommand, _operationConfig, cancellationToken)
                          .ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <summary>
        ///     Finds the command based on the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="cancellationToken">Allow the sender to cancel the request, optional</param>
        /// <returns><see cref="Task{T}"/></returns>
        public async Task<T> GetAsync<T>(Guid id, int timeoutInMilliseconds = -1, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest, new()
        {
            return await GetCommandFromDynamo<T>(id, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }        

        /// <summary>
        ///     Finds commands based on the id of the command
        /// </summary>
        /// <param name="id">The identifier</param>
        /// <param name="cancellationToken">Allow the sender to cancel the request, optional</param>
        /// <typeparam name="T">Type of command to be returned</typeparam>
        /// <returns></returns>
        private async Task<T> GetCommandFromDynamo<T>(Guid id, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest, new()
        {
            var storedId = id.ToString();

            _queryOperationConfig.QueryFilter = new List<ScanCondition>
            {
                new ScanCondition(_configuration.CommandIdIndex, ScanOperator.Equal, storedId)
            };
                       
            var storedCommand = 
                await _context.QueryAsync<DynamoDbCommand<T>>(storedId, _queryOperationConfig)
                    .GetNextSetAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);            

            return storedCommand.FirstOrDefault()?.ConvertToCommand() ?? new T {Id = Guid.Empty};
        }

        /// <summary>
        ///     Get list of commands based on date and time
        /// </summary>
        /// <param name="date">The date you want to retireve messages for. Second part of the partition key for Message Store.</param>
        /// <param name="startTime">Time to retrieve messages from on given date.</param>
        /// <param name="endTime">Time to retrieve message until on given date.</param>
        /// <typeparam name="T"></typeparam>
        /// <returns><see cref="T:List Paramore.Brighter.Message"/></returns>
        /// <exception cref="NotImplementedException"></exception>
        public IList<T> Get<T>(DateTime date, DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var type = typeof(T).Name;            
            var primaryKey = $"{type}+{date:yyyy-MM-dd}";

            var filter = GenerateFilter(startTime, endTime);
            
            var query = _context.QueryAsync<DynamoDbCommand<T>>(primaryKey, filter.Operator, filter.Values, _operationConfig)
                .GetRemainingAsync(cancellationToken)
                .GetAwaiter()
                .GetResult();

            var results = query;
             
            return results.Select(r => r.ConvertToCommand()).ToList();            
        }
        
        private static Filter GenerateFilter(DateTime? startTime, DateTime? endTime)
        {
            var start = $"{startTime?.Ticks ?? DateTime.MinValue.Ticks}";
            var end = $"{endTime?.Ticks ?? DateTime.MaxValue.Ticks}";
            
            return startTime is null && endTime is null || startTime.HasValue && endTime.HasValue
                ? new Filter(QueryOperator.Between, new[] { start, end })
                : startTime is null
                    ? new Filter(QueryOperator.LessThanOrEqual, new[] { end })
                    : new Filter(QueryOperator.GreaterThanOrEqual, new[] { start });
        }

        private class Filter
        {
            public QueryOperator Operator { get; }
            public IEnumerable<string> Values { get; }

            public Filter(QueryOperator @operator, IEnumerable<string> values)
                => (Operator, Values) = (@operator, values);
        }
    }

    public class DynamoDbCommand<T> where T : class, IRequest
    {
        [DynamoDBHashKey("Command+Date")]
        public string CommandDate { get; set; }
        [DynamoDBRangeKey()]
        public string Time { get; set; }
        [DynamoDBGlobalSecondaryIndexHashKey("CommandId")]
        public string CommandId { get; set; }
        [DynamoDBProperty]
        public string CommandType { get; set; }
        [DynamoDBProperty]
        public string CommandBody { get; set; }
        [DynamoDBProperty]
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;

        public DynamoDbCommand() {}

        public DynamoDbCommand(T command)
        {
            var type = typeof(T).Name;
            
            CommandDate = $"{type}+{TimeStamp:yyyy-MM-dd}";
            Time = $"{TimeStamp.Ticks}";
            CommandId = command.Id.ToString();
            CommandType = typeof(T).Name;
            CommandBody = JsonConvert.SerializeObject(command);
        }

        public T ConvertToCommand() => JsonConvert.DeserializeObject<T>(CommandBody);
    }
}
