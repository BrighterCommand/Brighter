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

#nullable enable

namespace Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;

/// <summary>
/// Minimal <see cref="IAmARelationalDatabaseConfiguration"/> stub used to satisfy the
/// <c>SqlBoxMigrationRunner</c> ctor. Properties return empty strings; tests
/// that exercise behaviour driven by configuration values should use a more specific
/// double or set fields explicitly.
/// </summary>
internal sealed class StubRelationalDatabaseConfiguration : IAmARelationalDatabaseConfiguration
{
    public bool BinaryMessagePayload => false;
    public bool JsonMessagePayload => false;
    public string ConnectionString => string.Empty;
    public string DatabaseName => string.Empty;
    public string InBoxTableName => string.Empty;
    public string OutBoxTableName => string.Empty;
    public string QueueStoreTable => string.Empty;
    public string? SchemaName => null;
}
