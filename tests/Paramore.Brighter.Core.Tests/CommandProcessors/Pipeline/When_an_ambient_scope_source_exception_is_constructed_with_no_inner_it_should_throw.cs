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

using System;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class AmbientScopeSourceExceptionConstructionTests
    {
        [Fact]
        public void When_an_ambient_scope_source_exception_is_constructed_with_no_inner_it_should_throw()
        {
            // Arrange — no inner exception to carry
            Exception? inner = null;

            // Act & Assert — the constructor must guard the never-null invariant itself
            Assert.Throws<ArgumentNullException>(() => new AmbientScopeSourceException(inner!));
        }

        [Fact]
        public void When_an_ambient_scope_source_exception_is_constructed_with_an_inner_it_should_carry_it()
        {
            // Arrange
            var providerFault = new InvalidOperationException("the provider's own fault");

            // Act
            var courier = new AmbientScopeSourceException(providerFault);

            // Assert — the inner exception passed is the one carried, and it is never null
            Assert.Same(providerFault, courier.InnerException);
        }
    }
}
