#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

using System;
using System.Linq;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class BaggageCapacityUpdateTests
{
    [Fact]
    public void When_updating_full_baggage_should_preserve_its_capacity_limit()
    {
        // Arrange
        var baggage = new Baggage();
        for (var i = 0; i < 32; i++) baggage.Add("key" + i, "original");

        // Act
        baggage.Add("key0", "updated");

        // Assert
        Assert.Equal(32, baggage.Count());
        Assert.Equal("updated", baggage.Single(pair => pair.Key == "key0").Value);
        Assert.Throws<InvalidOperationException>(() => baggage.Add("extra", "overflow"));
        Assert.Equal(32, baggage.Count());
        Assert.All(baggage.Where(pair => pair.Key != "key0"), pair => Assert.Equal("original", pair.Value));
    }
}
