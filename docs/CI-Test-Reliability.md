# CI Test Reliability Improvements

## Overview

This document describes the reliability improvements made to Brighter's CI acceptance tests, which test MessagingGateways, Inboxes, and Outboxes against various middleware and database services.

## Monitoring and Metrics

A new automated monitoring workflow (`.github/workflows/ci-metrics.yml`) has been implemented to track CI health:

### Features
- **Automatic tracking**: Runs after each CI workflow completion
- **Daily summaries**: Scheduled reports at 00:00 UTC  
- **Pass rate monitoring**: Tracks success/failure/cancellation rates over the last 30 runs
- **Duration tracking**: Monitors job execution times and identifies slow jobs
- **Automated alerts**: Warns when metrics exceed thresholds

### Alert Thresholds
The monitoring workflow generates alerts when:
- Pass rate drops below **80%**
- More than **30%** of runs take longer than 7 minutes
- Average duration exceeds **10 minutes**

### Accessing Metrics
1. View metrics in the **Actions** tab → **CI Metrics and Monitoring** workflow
2. Each run generates a summary showing:
   - Total runs, pass rate, success/failure/cancellation counts
   - Average and maximum durations
   - Number of slow runs (>7 minutes)
   - Job-level performance breakdown
3. Daily summaries provide trend analysis over time

### Using Metrics for Improvements
- Identify consistently slow jobs that need optimization
- Track the impact of timeout/retry adjustments
- Detect performance regressions early
- Make data-driven decisions about resource allocation

## Problem Summary

CI tests were exhibiting unreliable behavior, often failing due to timing issues that were difficult to reproduce locally. The primary issues were:

1. **Missing or inadequate service health checks** - Tests would start before services (Kafka, MySQL, etc.) were fully ready
2. **Fixed delays instead of readiness polling** - Tests used hardcoded `Task.Delay()` calls instead of actively checking service readiness
3. **Insufficient timeouts** - 5-minute job timeouts were too short for CI environments
4. **Limited retry logic** - Tests would retry only 3 times with fixed 1-second delays

## Implemented Improvements

### 1. Service Health Check Improvements in CI Workflow

All existing health checks in `.github/workflows/ci.yml` now have increased retry counts to accommodate slower CI environments:

#### Databases
- **Redis**: 10 retries (was 5) - up to ~100 seconds
- **PostgreSQL**: 10 retries (was 5) - up to ~100 seconds
- **MySQL/MariaDB**: 10 retries (was 3) - up to ~100 seconds
- **MongoDB**: 15 retries (was 10) - up to ~300 seconds

#### Message Queues
- **RabbitMQ**: 10 retries (was 5) - up to ~100 seconds

**Note on Health Checks**: Initial attempts to add health checks for MQTT, Zookeeper, Kafka, Schema Registry, SQL Server, and DynamoDB were reverted because the required commands (`mosquitto_sub`, `nc`, `kafka-broker-api-versions`, `curl`, `sqlcmd`) were not available in the respective container images. These services now start without health checks, relying on the increased job timeouts and the Kafka readiness verification script instead.

### 2. Kafka Readiness Verification

Replaced the blind 30-second wait with an active readiness check:

```bash
max_attempts=30
attempt=0
while [ $attempt -lt $max_attempts ]; do
  if kafkacat -b localhost:9092 -L > /dev/null 2>&1; then
    echo "Kafka is ready!"
    break
  fi
  attempt=$((attempt + 1))
  echo "Attempt $attempt/$max_attempts: Kafka not ready yet, waiting 2 seconds..."
  sleep 2
done
```

This provides:
- Up to 60 seconds to wait for Kafka (vs. fixed 30s)
- Active verification that Kafka is actually responding
- Clear logging of connection attempts
- Failure detection if Kafka doesn't become ready

### 3. Increased Job Timeouts

All acceptance test jobs now have 8-minute timeouts (increased from 5 minutes), except:
- Kafka: Already had 20 minutes (unchanged)
- Build job: 30 minutes (unchanged)

This accommodates:
- Slower CI runner resource allocation
- Variable network latency
- Service initialization time
- Test execution overhead

## Test Patterns Observed

### Common Issues in Test Code

While the CI infrastructure improvements address the primary issues, some test patterns remain that could benefit from future improvements:

1. **Hardcoded delays**: Many tests use `await Task.Delay(500)` or similar to "let topics propagate"
2. **Limited retries**: Most tests retry 3 times with fixed 1-second intervals
3. **No exponential backoff**: Retry logic doesn't account for services needing progressively more time
4. **Test serialization**: RabbitMQ tests have `[assembly: CollectionBehavior(DisableTestParallelization = true)]`

### Tests Marked as Fragile

Tests with `[Trait("Fragile", "CI")]` are excluded from CI runs using the filter `--filter "Fragile!=CI"`. These include:

- **Kafka**: 13 tests involving topic creation, offset management, and message ordering
- **RabbitMQ**: 6 tests involving DLQ, delayed messages, and retry limits
- **GCP**: 3 tests involving requeue and DLQ behavior
- **Azure Service Bus**: 3 tests involving message consumption and large messages
- **Core**: Several timeout policy and async tests

These tests may now be more reliable due to infrastructure improvements, but should be gradually re-enabled and monitored.

## Recommendations for Future Work

### Short Term (Low Hanging Fruit)

1. **Monitor CI Success Rates**: Track which jobs still fail frequently after these changes
2. **Gradually Remove "Fragile" Trait**: Start with 1-2 tests per component and monitor success over multiple runs
3. **Document Test Timing Requirements**: Add comments explaining why delays exist and what they're waiting for

### Medium Term (Test Code Improvements)

1. **Implement Helper Methods**: Create shared retry-with-backoff helpers that tests can use:
   ```csharp
   public static async Task<T> RetryWithBackoffAsync<T>(
       Func<Task<T>> operation,
       int maxAttempts = 5,
       int initialDelayMs = 100,
       int maxDelayMs = 5000)
   ```

2. **Add Service Readiness Checks**: Tests should verify service connectivity before operations:
   ```csharp
   await WaitForServiceAsync(async () => {
       try {
           await producer.PingAsync();
           return true;
       } catch {
           return false;
       }
   });
   ```

3. **Increase Test Timeouts in Code**: Use environment variables to increase timeouts in CI:
   ```csharp
   var timeout = Environment.GetEnvironmentVariable("CI") == "true" 
       ? TimeSpan.FromSeconds(30) 
       : TimeSpan.FromSeconds(10);
   ```

### Long Term (Architectural Changes)

1. **Test Fixtures with Proper Initialization**: Ensure services are ready in `IAsyncLifetime` or `IClassFixture` setup
2. **Consider Removing Test Parallelization Restrictions**: Investigate if RabbitMQ can handle parallel tests with proper isolation
3. **Add CI-Specific Configuration**: Create test configuration profiles optimized for CI environments
4. **Improve Logging**: Add more detailed logging in tests to diagnose failures when they occur
5. **Research Proper Health Checks**: Investigate what commands are actually available in each container image for future health check implementation

## Testing These Changes

To validate the improvements:

1. Run CI builds multiple times to establish a baseline success rate
2. Monitor particularly problematic jobs (Kafka, RabbitMQ, MongoDB)
3. Check if previously-failing tests now pass consistently
4. Measure average job duration to ensure timeouts are appropriate

## Success Metrics

Track these metrics over time:

- **Pass Rate**: Percentage of CI runs that pass without needing re-runs
- **False Positive Rate**: Tests that fail due to timing but pass on retry
- **Job Duration**: Average and P95 duration for each job type
- **Time to Ready**: How long services take to become healthy (from CI logs)

## Lessons Learned

### What Worked
- Increasing retry counts for existing health checks
- Active Kafka readiness verification using kafkacat
- Increased job timeouts
- Comprehensive documentation

### What Didn't Work
- Adding health checks with commands not available in containers
- Attempting to use `mosquitto_sub`, `nc`, `kafka-broker-api-versions`, `curl`, `sqlcmd` in health checks
- These commands either don't exist in the images or aren't in PATH

### Future Health Check Considerations
When adding health checks in the future:
1. Verify the command exists in the container image first
2. Test locally with the same image before committing
3. Consider using TCP port checks or simple HTTP requests where available
4. Some images may need custom health check scripts added via volume mounts

## Related Issues

- [Chore] Unreliable Acceptance Tests - Original issue
- PR with fixes and improvements

## Summary

The improvements focus on ensuring services have adequate time to start and that Kafka readiness is actively verified rather than relying on fixed delays. While we couldn't add all desired health checks due to container limitations, the increased retry counts for existing checks and longer job timeouts should significantly improve overall reliability.
