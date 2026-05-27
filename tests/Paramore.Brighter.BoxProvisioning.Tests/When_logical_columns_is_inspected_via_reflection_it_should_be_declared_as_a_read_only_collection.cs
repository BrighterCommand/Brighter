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

using System.Collections.Generic;
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

public class LogicalColumnsPublicApiTests
{
    // The XML doc on IAmABoxMigration.LogicalColumns (and the implementation contract across all
    // backends) says callers must treat the column set as read-only and never mutate it after
    // construction. Exposing it as ISet<string> permits caller mutation through the public
    // surface (Add/Remove/Clear) and contradicts the documented invariant. The fix is to declare
    // it as IReadOnlyCollection<string>: callers can still enumerate it, the runner's
    // IsSupersetOf/SetEquals/etc. accept IEnumerable<T> arguments, and mutation is no longer
    // possible through the public API. Source-breaking on netstandard2.0 (no DIM workaround).

    [Fact]
    public void When_iamaboxmigration_logical_columns_is_inspected_it_should_be_declared_as_a_read_only_collection()
    {
        //Arrange + Act
        var property = typeof(IAmABoxMigration).GetProperty(nameof(IAmABoxMigration.LogicalColumns));

        //Assert
        Assert.NotNull(property);
        Assert.Equal(typeof(IReadOnlyCollection<string>), property!.PropertyType);
    }

    [Fact]
    public void When_boxmigration_record_logical_columns_is_inspected_it_should_be_declared_as_a_read_only_collection()
    {
        //Arrange + Act
        var property = typeof(BoxMigration).GetProperty(nameof(BoxMigration.LogicalColumns));

        //Assert
        Assert.NotNull(property);
        Assert.Equal(typeof(IReadOnlyCollection<string>), property!.PropertyType);
    }
}
