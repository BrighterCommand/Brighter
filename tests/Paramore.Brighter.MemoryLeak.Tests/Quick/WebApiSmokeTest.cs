// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Threading.Tasks;
using Paramore.Brighter.MemoryLeak.Tests.Infrastructure;
using Xunit;

namespace Paramore.Brighter.MemoryLeak.Tests.Quick;

/// <summary>
/// As we have run another application in order to test it, and load up the JetBrains dotMemory tst framework for the tests here
/// this test provides confidence tests that running the web server works and can be exercised with load, before we add testing
/// for leaks. This leads to easier error diagnosis.
/// </summary>
public class WebApiSmokeTest
{
    [Fact]
    public async Task When_starting_a_web_server_for_testing()
    {
        await using var server = new WebApiTestServer();
        var loadGen = new LoadGenerator(server);
        // Act - Process 1000 commands that trigger handler instantiation
        Console.WriteLine("Sending 1000 requests to exercise command handlers...");
        var result = await loadGen.RunLoadAsync(totalRequests: 1000, concurrentRequests: 10, cancellationToken: TestContext.Current.CancellationToken);

        // Verify we actually processed requests successfully
        Assert.True(result.SuccessCount > 900,
            $"Expected > 900 successful requests but got {result.SuccessCount}. " +
            $"Test may not be exercising handlers properly.");
    }
    
}
