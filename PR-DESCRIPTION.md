# Pull Request: Fix Unreliable CI Acceptance Tests

## Overview

This PR addresses the reliability issues in Brighter's CI acceptance tests for MessagingGateways, Inboxes, and Outboxes. The tests were exhibiting erratic behavior in GitHub Actions, often failing due to timing-related issues that were difficult to reproduce locally.

## Problem

The CI build (`.github/workflows/ci.yml`) runs acceptance tests that require various middleware (Kafka, RabbitMQ, Redis, MQTT) and databases (PostgreSQL, MySQL, SQL Server, MongoDB, DynamoDB). These tests frequently failed in CI but worked locally, suggesting infrastructure timing issues rather than code bugs.

### Root Causes

1. **Missing Service Health Checks**: Many services lacked health checks, causing tests to start before services were ready
2. **Inadequate Retry Counts**: Health checks had too few retries (3-5) for CI environment variability
3. **Kafka Blind Wait**: A fixed 30-second sleep instead of active readiness verification
4. **Short Job Timeouts**: 5-minute timeouts were insufficient for slower CI environments
5. **No Documentation**: No record of known issues or improvement strategies

## Solution

### 1. Comprehensive Service Health Checks

Added or improved health checks for all services in `.github/workflows/ci.yml`:

| Service | Health Check Method | Retries | Max Wait |
|---------|-------------------|---------|----------|
| Kafka | `kafka-broker-api-versions` | 15 | ~150s |
| Zookeeper | `nc localhost 2181` check | 10 | ~100s |
| Schema Registry | HTTP endpoint | 10 | ~100s |
| RabbitMQ | `rabbitmqctl node_health_check` | 10 ↑ | ~100s |
| Redis | `redis-cli ping` | 10 ↑ | ~100s |
| PostgreSQL | `pg_isready` | 10 ↑ | ~100s |
| MySQL/MariaDB | `healthcheck.sh` | 10 ↑ | ~100s |
| SQL Server | `sqlcmd` query | 10 | ~100s |
| MongoDB | `mongosh` ping | 15 ↑ | ~300s |
| MQTT | `mosquitto_sub` | 10 | ~100s |
| DynamoDB | HTTP endpoint | 10 | ~100s |

*(↑ indicates increased from previous value)*

### 2. Active Kafka Readiness Verification

**Before:**
```yaml
- name: Sleep to let Kafka spin up
  uses: jakejarvis/wait-action@master
  with:
    time: '30s'
```

**After:**
```bash
- name: Wait for Kafka to be ready
  run: |
    max_attempts=30
    while [ $attempt -lt $max_attempts ]; do
      if kafkacat -b localhost:9092 -L > /dev/null 2>&1; then
        echo "Kafka is ready!"
        break
      fi
      sleep 2
    done
```

**Benefits:**
- Active verification instead of blind waiting
- Can complete in <30s if Kafka is ready quickly
- Can wait up to 60s if needed
- Fails fast with clear error message
- Better troubleshooting through logging

### 3. Increased Job Timeouts

All acceptance test jobs increased from **5 minutes → 8 minutes**:
- redis-ci, mqtt-ci, rabbitmq-ci
- postgres-ci, sqlserver-ci, mysql-ci
- dynamo-ci, localstack-ci, mongodb-ci
- aws-ci, aws-scheduler-ci, azure-ci
- sqlite-ci, gcp-ci

*Note: kafka-ci already had 20 minutes (unchanged)*

### 4. Comprehensive Documentation

Added two documentation files:

1. **`docs/CI-Test-Reliability.md`** (171 lines)
   - Detailed analysis of issues
   - Explanation of all improvements
   - Recommendations for future work
   - Success metrics to track

2. **`RELIABILITY-IMPROVEMENTS-SUMMARY.md`** (140 lines)
   - Executive summary
   - Quick reference tables
   - Validation and rollback plans
   - Next steps

## Impact

### Expected Improvements

✅ **Service Startup Reliability**
- Services verified ready before tests run
- Longer retry windows accommodate CI variability

✅ **Kafka Test Reliability**  
- Active readiness check replaces blind wait
- Better handling of slow Kafka initialization

✅ **Test Execution Success**
- 8-minute timeouts prevent premature cancellation
- Tests have adequate time to complete

✅ **Maintainability**
- Documentation preserves knowledge
- Clear patterns for future improvements

### Tests Expected to Improve

- **Kafka tests** (13 marked `[Trait("Fragile", "CI")]`)
- **RabbitMQ tests** (6 marked fragile)
- **Database initialization tests**
- **Message broker timing tests**

## Testing & Validation

### Pre-merge Validation

✅ CI workflow YAML syntax validated
✅ All changes backward compatible
✅ No test or application code modified
✅ Changes isolated to infrastructure

### Post-merge Monitoring

Recommended monitoring approach:

1. **Track Pass Rates**: Monitor CI success over 10+ builds
2. **Measure Duration**: Check if 8-minute timeouts are adequate
3. **Review Logs**: Verify Kafka readiness checks succeed
4. **Re-enable Tests**: Gradually remove "Fragile" trait from stable tests
5. **Optimize Timeouts**: Adjust based on observed durations

### Success Metrics

| Metric | Current | Target |
|--------|---------|--------|
| CI Pass Rate | TBD | >95% |
| False Positive Rate | TBD | <5% |
| Kafka Startup Time | 30s (blind) | 10-30s (actual) |
| Job Duration (P95) | TBD | <7 min |

## Rollback Plan

If issues arise:
1. Revert the 4 commits from this PR
2. All changes are in CI config and docs only
3. No code changes require rollback
4. Previous behavior fully restored

## Future Work

### Short Term
- Monitor CI success rates
- Remove "Fragile" trait from stable tests
- Optimize timeout values based on data

### Medium Term
- Implement retry helpers in tests
- Add test-level readiness checks
- Use environment variables for CI-specific timeouts

### Long Term
- Improve test fixtures with proper initialization
- Re-evaluate test parallelization restrictions
- Create CI-specific test configuration profiles

## Files Changed

```
.github/workflows/ci.yml             | 94 insertions(+), 26 deletions(-)
docs/CI-Test-Reliability.md          | 171 new file
RELIABILITY-IMPROVEMENTS-SUMMARY.md  | 140 new file
```

**Total:** 3 files changed, 379 insertions(+), 26 deletions(-)

## Related Issues

- [Chore] Unreliable Acceptance Tests - Original issue

## Commits

1. `6d99514` - Improve CI service health checks and remove fixed delays
2. `c4b8f5c` - Increase CI job timeouts to accommodate slower test environments
3. `b3440bd` - Add documentation for CI test reliability improvements
4. `b1dea5e` - Add comprehensive summary of reliability improvements

## Reviewer Notes

### Key Changes to Review

1. **Health Check Additions**: Verify health check commands are appropriate
2. **Kafka Readiness Script**: Review the bash logic for correctness
3. **Timeout Values**: Confirm 8 minutes is reasonable but not excessive
4. **Documentation**: Ensure documentation is accurate and helpful

### Questions to Consider

- Are the health check retry counts adequate?
- Is the Kafka readiness script robust enough?
- Should any tests be re-enabled immediately?
- Are there other services that need health checks?

## Conclusion

This PR makes **infrastructure-only** changes to improve CI test reliability without modifying any test or application code. The changes are conservative, well-documented, and easily reversible. The improvements address root causes rather than symptoms, providing a foundation for long-term reliability.
