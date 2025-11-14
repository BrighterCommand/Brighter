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

using System.Collections.Generic;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Create a record to hold the span information for the outbox
/// The span is named for "db.operation db.name db.sql.table" or "db.operation db.name" if db.sql.table is null or empty 
/// </summary>
/// <param name="dbSystem">The DBMS product identifier</param>
/// <param name="dbName">The name of the database being accessed</param>
/// <param name="dbOperation">The name of the operation being executed</param>
/// <param name="dbTable">The name of the primary table that the operation is acting upon</param>
/// <param name="serverPort">Server port number</param>
/// <param name="dbInstanceId">An identifier (address, unique name, or any other identifier) of the database instance that is executing queries</param>
/// <param name="dbStatement">The database statement being executed</param>
/// <param name="dbUser">Username for accessing the database</param>
/// <param name="networkPeerAddress">Peer address of the database node</param>
/// <param name="networkPeerPort">Peer port number of the network connection</param>
/// <param name="serverAddress">Name of the database host</param>
/// <param name="dbAttributes">Other attributes (key-value pairs) not covered by the standard attributes</param>
public record BoxSpanInfo(
    string dbSystemName,
    string dbName,
    BoxDbOperation dbOperation,
    string dbTable,
    int serverPort = 0,
    string? dbInstanceId = null,
    string? dbStatement = null,
    string? dbUser = null,
    string? networkPeerAddress = null,
    int networkPeerPort = 0,
    string? serverAddress = null,
    Dictionary<string, string>? dbAttributes = null)
{
    public BoxSpanInfo(
        DbSystem dbSystem,
        string dbName,
        BoxDbOperation dbOperation, 
        string dbTable,
        int serverPort = 0, 
        string? dbInstanceId = null, 
        string? dbStatement = null,
        string? dbUser = null,
        string? networkPeerAddress = null,
        int networkPeerPort = 0,
        string? serverAddress = null,
        Dictionary<string, string>? dbAttributes = null)
        : this(dbSystem.ToDbName(), dbName, dbOperation, dbTable, serverPort, dbInstanceId, dbStatement, dbUser, networkPeerAddress, networkPeerPort, serverAddress, dbAttributes)
    {
        
        this.dbSystem = dbSystem;
    }
    
    public DbSystem dbSystem { get; }
}
