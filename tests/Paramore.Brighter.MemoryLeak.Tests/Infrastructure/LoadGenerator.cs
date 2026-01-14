using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MemoryLeak.Tests.Infrastructure;

/// <summary>
/// Generates HTTP load against the WebAPI test server with configurable concurrency.
/// Tracks success/failure counts for verification.
/// </summary>
public class LoadGenerator(WebApiTestServer server)
{
    private readonly WebApiTestServer _server = server ?? throw new ArgumentNullException(nameof(server));

    /// <summary>
    /// Runs a load test by sending multiple HTTP requests with controlled concurrency.
    /// </summary>
    /// <param name="totalRequests">Total number of requests to send</param>
    /// <param name="concurrentRequests">Maximum number of concurrent requests</param>
    /// <param name="cancellationToken">Cancellation token to stop the load test</param>
    /// <returns>Result with success/failure counts</returns>
    public async Task<LoadTestResult> RunLoadAsync(
        int totalRequests,
        int concurrentRequests,
        CancellationToken cancellationToken = default)
    {
        var result = new LoadTestResult();
        var semaphore = new SemaphoreSlim(concurrentRequests, concurrentRequests);
        var tasks = new List<Task>();

        for (int i = 0; i < totalRequests; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await semaphore.WaitAsync(cancellationToken);

            var requestNumber = i;
            var task = Task.Run(async () =>
            {
                try
                {
                    // Create a person first
                    var personName = $"Person{requestNumber}";
                    var createPersonResponse = await _server.CreatePersonAsync(personName);

                    if (createPersonResponse.IsSuccessStatusCode)
                    {
                        // Send a greeting to exercise the full pipeline
                        var greetingResponse = await _server.SendGreetingAsync(
                            personName,
                            $"Hello from test {requestNumber}");

                        if (greetingResponse.IsSuccessStatusCode)
                        {
                            Interlocked.Increment(ref result._successCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref result._failureCount);
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref result._failureCount);
                    }
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref result._failureCount);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        // Wait for all requests to complete
        await Task.WhenAll(tasks);

        return result;
    }

    /// <summary>
    /// Runs a simpler load test by only creating people (lighter weight).
    /// Useful for tests that don't need the full command pipeline.
    /// </summary>
    /// <param name="totalRequests">Total number of requests to send</param>
    /// <param name="concurrentRequests">Maximum number of concurrent requests</param>
    /// <param name="cancellationToken">Cancellation token to stop the load test</param>
    /// <returns>Result with success/failure counts</returns>
    public async Task<LoadTestResult> RunSimpleLoadAsync(
        int totalRequests,
        int concurrentRequests,
        CancellationToken cancellationToken = default)
    {
        var result = new LoadTestResult();
        var semaphore = new SemaphoreSlim(concurrentRequests, concurrentRequests);
        var tasks = new List<Task>();

        for (int i = 0; i < totalRequests; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await semaphore.WaitAsync(cancellationToken);

            var requestNumber = i;
            var task = Task.Run(async () =>
            {
                try
                {
                    var response = await _server.CreatePersonAsync($"Person{requestNumber}");

                    if (response.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref result._successCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref result._failureCount);
                    }
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref result._failureCount);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        return result;
    }
}

/// <summary>
/// Result of a load test run.
/// </summary>
public class LoadTestResult
{
    internal int _successCount;
    internal int _failureCount;

    public int SuccessCount => _successCount;
    public int FailureCount => _failureCount;
    public int TotalRequests => SuccessCount + FailureCount;

    public double SuccessRate => TotalRequests > 0
        ? (double)SuccessCount / TotalRequests * 100
        : 0;

    public override string ToString()
    {
        return $"Load Test: {TotalRequests} total, {SuccessCount} success, " +
               $"{FailureCount} failure ({SuccessRate:F1}% success rate)";
    }
}
