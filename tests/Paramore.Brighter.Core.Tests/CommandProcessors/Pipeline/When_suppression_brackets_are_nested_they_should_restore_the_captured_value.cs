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

using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class AmbientScopeSuppressionTests
    {
        [Fact]
        public void When_suppression_brackets_are_nested_they_should_restore_the_captured_value()
        {
            // Arrange & Act & Assert — a reader outside any bracket sees false, and never throws
            Assert.False(AmbientScopeSuppression.IsSuppressed);

            // a lexically nested pair restores the captured value on dispose, not unconditionally false
            using (AmbientScopeSuppression.Suppress())
            {
                Assert.True(AmbientScopeSuppression.IsSuppressed);

                using (AmbientScopeSuppression.Suppress())
                {
                    Assert.True(AmbientScopeSuppression.IsSuppressed);
                }

                // the inner bracket's dispose restored what it captured — still suppressed here
                Assert.True(AmbientScopeSuppression.IsSuppressed);
            }

            // the outer bracket's dispose restored what it captured — back to unsuppressed
            Assert.False(AmbientScopeSuppression.IsSuppressed);
        }

        [Fact]
        public void When_a_suppression_bracket_is_disposed_twice_it_should_be_a_no_op()
        {
            // Arrange
            var bracket = AmbientScopeSuppression.Suppress();

            // Act — dispose once, restoring the captured false, then again
            bracket.Dispose();
            bracket.Dispose();

            // Assert — no exception, and the value is still the one the first dispose restored
            Assert.False(AmbientScopeSuppression.IsSuppressed);
        }

        [Fact]
        public async Task When_a_flow_branches_before_a_bracket_is_taken_the_branched_flow_should_not_observe_it()
        {
            // Arrange — a child flow is started (and so captures its own ExecutionContext) before
            // the parent flow takes the suppression bracket
            var gate = new ManualResetEventSlim(false);
            var branchedRead = Task.Run(() =>
            {
                gate.Wait();
                return AmbientScopeSuppression.IsSuppressed;
            });

            // Act — the parent flow's write happens after the child flow has already branched
            using var bracket = AmbientScopeSuppression.Suppress();
            gate.Set();
            var branchedValue = await branchedRead;

            // Assert — the branched flow never sees the parent's later write, exactly AsyncLocal<bool>'s
            // own semantics; the parent flow that took the bracket does see it
            Assert.False(branchedValue);
            Assert.True(AmbientScopeSuppression.IsSuppressed);
        }
    }
}
