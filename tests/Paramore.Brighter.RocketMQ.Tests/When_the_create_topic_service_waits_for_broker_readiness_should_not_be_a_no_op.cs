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
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests;

/// <summary>
/// <c>docker-compose-rocketmq.yaml</c>'s <c>create-topic</c> service waits for the broker to
/// accept connections before it starts creating topics.
/// </summary>
/// <remarks>
/// <para>
/// The wait condition used to be <c>until curl -s http://broker:10911/ &amp;&gt;/dev/null; do …
/// done</c>. <c>&amp;&gt;</c> is a bash-ism; under this image's <c>sh</c> (<c>dash</c>) it parses
/// as two simple commands - <c>curl … &amp;</c> (backgrounded, so the shell does not wait for it)
/// followed by a bare <c>&gt;/dev/null</c> redirection with no command, which succeeds
/// immediately and unconditionally. The <c>until</c> loop's condition was therefore always true
/// on the first check, so the retry body - the only place that would prove the loop ever waited -
/// never ran, and <c>create-topic</c> proceeded before the broker had necessarily registered with
/// the nameserver.
/// </para>
/// <para>
/// Fixing only the redirect operator surfaced a second, independent defect live: <c>curl</c>
/// against port 10911 is RocketMQ's own binary protocol, not HTTP, so a real curl request there
/// never completes - it retried forever rather than racing forever. The wait condition now tests
/// the TCP connection directly instead.
/// </para>
/// <para>
/// Exercised without a broker, by reading the compose file's own text - there is no broker-side
/// behaviour to invoke, since the defect was in the wait condition's shell syntax and choice of
/// probe, not in any application code.
/// </para>
/// </remarks>
[Trait("Category", "RocketMQ")]
[Trait("Category", "RocketMQBrokerFree")]
public class RocketMqComposeBrokerReadinessWaitTests
{
    private const string SolutionFileName = "Brighter.slnx";
    private const string ComposeFileName = "docker-compose-rocketmq.yaml";

    [Fact]
    public void When_the_create_topic_service_waits_for_broker_readiness_should_not_be_a_no_op()
    {
        // Arrange - the compose file's own text is the thing under test; no broker is involved
        var composeText = File.ReadAllText(ComposeFilePath());

        // Act - find the `until <condition>;` the create-topic service runs right after announcing
        // it is waiting for the broker, as distinct from the later `until $$MQADMIN clusterList …`
        // wait for nameserver registration
        var brokerWait = Regex.Match(
            composeText,
            @"Waiting for broker to be healthy[^\n]*\n\s*until\s+(?<condition>[^\n;]*);");
        Assert.True(brokerWait.Success,
            $"expected to find a broker-readiness `until <condition>;` wait in {ComposeFileName}");
        var condition = brokerWait.Groups["condition"].Value;

        // Assert - a bare `&>` after a backgroundable command is the dash-ism that makes the
        // condition succeed immediately regardless of the probe's actual result
        Assert.DoesNotMatch(@"&>", condition);

        // Assert - non-vacuity: the condition must actually probe the broker's port, not just
        // avoid the broken operator while checking nothing
        Assert.Contains("broker", condition);
        Assert.Contains("10911", condition);
    }

    private static string ComposeFilePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory != null;
             directory = directory.Parent)
        {
            var composeFilePath = Path.Combine(directory.FullName, ComposeFileName);
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName))
                && File.Exists(composeFilePath))
            {
                return composeFilePath;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate {ComposeFileName} by walking up from {AppContext.BaseDirectory}; " +
            $"expected to find a directory holding both {SolutionFileName} and {ComposeFileName}.");
    }
}
