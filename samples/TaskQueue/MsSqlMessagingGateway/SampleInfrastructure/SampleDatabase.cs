#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace SampleInfrastructure;

/// <summary>
/// The one database these four applications share, and the names of the three tables in it.
/// They are constants rather than a comment asking two files to agree, because a mismatch here
/// is silent: the sender writes to one table and the receiver reads an empty one.
/// </summary>
public static class SampleDatabase
{
    public const string Name = "BrighterSqlQueue";

    /// <summary>The queue the transport reads and writes, created by <c>EnsureQueueTable</c>
    /// because the MSSQL gateway will not.</summary>
    public const string QueueTable = "QueueData";

    public const string OutboxTable = "Outbox";
    public const string InboxTable = "InboxMessages";

    public const string GreetingTopic = "greeting.event";
    public const string CompetingTopic = "multipleconsumer.command";

    /// <summary>
    /// The SQLEXPRESS instance this sample was written against. Set ConnectionStrings__Brighter
    /// in the environment to point it somewhere else — a container, say — without editing source.
    /// </summary>
    /// <remarks>
    /// TrustServerCertificate is required against SQL Express, which presents a self-signed
    /// certificate: Microsoft.Data.SqlClient defaults <c>Encrypt</c> to true from 4.0. See the
    /// README before carrying it into production.
    /// </remarks>
    public const string DefaultConnectionString =
        @"Database=" + Name + @";Server=.\sqlexpress;Integrated Security=SSPI;TrustServerCertificate=True;";

    /// <summary>
    /// Reads the connection string from configuration, falling back to <see cref="DefaultConnectionString"/>.
    /// Tests for whitespace rather than null, because a configuration key that exists but is
    /// blank — which an environment variable makes easy — yields "" and would skip a ?? fallback.
    /// </summary>
    public static string ConnectionString(string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? DefaultConnectionString : configured;
}
