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

using System;
using System.Collections.Generic;

namespace Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;

/// <summary>
/// Minimal <see cref="IAmABoxMigrationCatalog"/> stub used to satisfy the
/// <c>SqlBoxMigrationRunner</c> ctor in tests that do not exercise the chain itself.
/// <see cref="All"/> returns <see cref="Migrations"/> (default empty) and
/// <see cref="FreshInstallDdl"/> returns <see cref="FreshInstallDdlText"/> (default empty).
/// Tests that need a malformed chain (e.g. monotonicity guard) set <see cref="Migrations"/>;
/// tests that probe the null-chain branch set <see cref="AllReturnsNull"/> to true.
/// </summary>
internal sealed class StubBoxMigrationCatalog : IAmABoxMigrationCatalog
{
    public IReadOnlyList<IAmABoxMigration> Migrations { get; set; } = Array.Empty<IAmABoxMigration>();
    public bool AllReturnsNull { get; set; }
    public string FreshInstallDdlText { get; set; } = string.Empty;

    public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
        => AllReturnsNull ? null! : Migrations;

    public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
        => FreshInstallDdlText;
}
