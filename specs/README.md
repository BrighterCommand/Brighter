# Specifications

This directory contains feature specifications for Brighter, following a spec-driven development workflow (requirements, design, tasks, implementation).

## Numbering

Spec numbers are not strictly sequential. Gaps exist to accommodate parallel workstreams developed on separate branches. This avoids merge conflicts when multiple feature tracks are in progress simultaneously.

| Range | Workstream |
|-------|------------|
| 0001-0009 | General / early specs |
| 0010-0019 | Universal DLQ - transport implementations |

## Specifications

| Spec | Name | Status |
|------|------|--------|
| 0001 | [Kafka Dead Letter Queue](0001-kafka-dead-letter-queue/) | Complete (merged) |
| 0002 | [Backstop Error Handler](0002-backstop-error-handler/) | Phase 1 complete |
| 0010 | [AWS SQS Dead Letter Queue](0010-aws-sqs-dead-letter-queue/) | Requirements draft |
| 0011 | [Redis Dead Letter Queue](0011-redis-dead-letter-queue/) | Requirements draft |
| 0012 | [MsSql Dead Letter Queue](0012-mssql-dead-letter-queue/) | Requirements draft |
| 0013 | [PostgreSQL Dead Letter Queue](0013-postgres-dead-letter-queue/) | Requirements draft |
| 0014 | [RocketMQ Dead Letter Queue](0014-rocketmq-dead-letter-queue/) | Requirements draft |
| 0015 | [MQTT Dead Letter Queue](0015-mqtt-dead-letter-queue/) | Requirements draft |
