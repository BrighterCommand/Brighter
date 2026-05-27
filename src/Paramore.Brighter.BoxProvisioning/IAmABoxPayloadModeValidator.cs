#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// The payload-mode-validator role for a BoxProvisioning backend. Implementations
/// decide whether the live payload column type on an existing box table matches
/// the configured payload mode (binary vs text), throwing
/// <see cref="ConfigurationException"/> on mismatch and returning quietly on match.
/// </summary>
/// <remarks>
/// One implementation per backend (MSSQL, Postgres, MySQL, SQLite, Spanner).
/// Spanner is not exempt: although Spanner's payload column type is fixed
/// (<c>BYTES</c> or <c>STRING</c> per the schema), the role is still meaningful —
/// the Spanner implementation checks the live type against the configuration
/// using <c>STARTS_WITH</c> against <c>information_schema.columns</c>.
/// <para>
/// Implementations are stateless services and are safe to register as DI singletons.
/// </para>
/// </remarks>
/// <typeparam name="TConnection">The backend-specific <see cref="DbConnection"/> subtype.</typeparam>
public interface IAmABoxPayloadModeValidator<TConnection>
    where TConnection : DbConnection
{
    /// <summary>
    /// Inspects the live payload column type and throws <see cref="ConfigurationException"/>
    /// if it does not match <paramref name="binaryMessagePayload"/>. Returns quietly on match.
    /// </summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="tableName">The unqualified box table name whose column type is being read.</param>
    /// <param name="schemaName">Optional. Null is substituted with the backend default by each
    /// implementation — see implementing class for the substitution rule
    /// (MSSQL → <c>"dbo"</c>, Postgres → <c>"public"</c>, MySQL → <c>connection.Database</c>;
    /// SQLite and Spanner accept and ignore the parameter).</param>
    /// <param name="columnName">The unqualified payload column name to inspect.</param>
    /// <param name="binaryMessagePayload">The payload mode the caller has configured.
    /// True expects a binary column type (e.g. <c>VARBINARY(MAX)</c>, <c>bytea</c>, <c>BYTES</c>);
    /// false expects a text column type (e.g. <c>NVARCHAR(MAX)</c>, <c>text</c>, <c>STRING</c>).</param>
    /// <param name="cancellationToken">Optional cancellation.</param>
    Task ValidateAsync(
        TConnection connection,
        string tableName,
        string? schemaName,
        string columnName,
        bool binaryMessagePayload,
        CancellationToken cancellationToken = default);
}
