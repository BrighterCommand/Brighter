#region Licence
 /* The MIT License (MIT)
 Copyright © 2021 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
 
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

using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace Paramore.Brighter.MySql
{
    /// <summary>
    /// Use to get a connection for a MySql store, used with the Outbox to ensure that we can have a transaction that spans the entity and the message to be sent
    /// </summary>
    public interface IMySqlConnectionProvider
    {
        /// <summary>
        /// Gets the connection we should use for the database
        /// </summary>
        /// <returns>A Sqlite connection</returns>
        MySqlConnection GetConnection();
        
        /// <summary>
        /// Gets the connections we should use for the database
        /// </summary>
        /// <param name="cancellationToken">Cancels the operation</param>
        /// <returns>A Sqlite connection</returns>
        Task<MySqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
       
        /// <summary>
        /// Is there an ambient transaction? If so return it 
        /// </summary>
        /// <returns>A Sqlite Transaction</returns>
        MySqlTransaction GetTransaction();
        
        /// <summary>
        /// Is there an open transaction
        /// </summary>
        bool HasOpenTransaction { get; }
        
        /// <summary>
        /// Is this connection created externally? In which case don't close it as you don't own it.
        /// </summary>
        bool IsSharedConnection { get; }
    }
}
