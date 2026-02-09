# Requirements

> **Note**: This document captures the test coverage analysis findings and requirements for improving test coverage. No ADRs are needed as this is a testing initiative, not an architectural change.

## Problem Statement

As the Brighter codebase has grown to 79 source projects, test coverage has not kept pace uniformly. Analysis reveals significant gaps in unit test coverage for core components, leading to:

1. **Risk of regressions** when refactoring untested code
2. **Difficulty understanding behavior** of undocumented/untested components
3. **Reduced confidence** when making changes to critical infrastructure
4. **Inconsistent quality** across different parts of the codebase

## Analysis Summary

### Current Coverage by Project

| Source Project Area | Test Coverage | Status |
|---------------------|---------------|--------|
| CommandProcessor operations | Excellent | ~76 test classes |
| Pipeline building | Excellent | ~29 test classes |
| Exception policies | Excellent | ~18 test classes |
| Message serialization | Good | ~38 test classes |
| Message dispatch | Good | ~56 test classes |
| Observability/Tracing | Good | ~26 test classes |
| DI/Extensions | Partial | 13 test classes |
| In-Memory components | Partial | ~27 test classes |
| Core value types | **None** | 0 test classes |
| Builder classes | **None** | 0 test classes |
| Extension methods | **None** | 0 test classes |

### Critical Gaps Identified

#### Tier 1: Core Infrastructure (No Tests)

| Component | Location | Impact |
|-----------|----------|--------|
| `Message`, `MessageHeader`, `MessageBody` | `src/Paramore.Brighter/` | Fundamental types used everywhere |
| `CommandProcessorBuilder` | `src/Paramore.Brighter/` | Primary way to construct CommandProcessor |
| `Id`, `RoutingKey`, `PartitionKey` | `src/Paramore.Brighter/` | Core value types |
| `Subscription`, `Publication` | `src/Paramore.Brighter/` | Configuration classes |
| `OutboxSweeper` | `src/Paramore.Brighter/` | Background processing |
| `InMemoryArchiveProvider` | `src/Paramore.Brighter/` | Archive functionality |
| `ServiceProviderMapperFactory` | `Extensions.DependencyInjection` | DI factory with zero tests |

#### Tier 2: Significant Gaps (Minimal Tests)

| Component | Location | Current State |
|-----------|----------|---------------|
| All Extension methods | `src/Paramore.Brighter/Extensions/` | 8 utility classes, 0 tests |
| All JSON Converters | `src/Paramore.Brighter/JsonConverters/` | 5+ converters, 0 tests |
| `InMemoryTransactionProvider` | `src/Paramore.Brighter/` | Transaction lifecycle untested |
| `InMemorySubscription` | `src/Paramore.Brighter/` | Configuration untested |
| `UseRpc` | `Extensions.DependencyInjection` | RPC scenarios untested |
| `ServiceCollectionMessageMapperRegistryBuilder` | `Extensions.DependencyInjection` | Manual registration untested |

#### Tier 3: Incomplete Coverage

| Component | Location | Missing Tests |
|-----------|----------|---------------|
| `InMemoryMessageProducer` | `src/Paramore.Brighter/` | Async, delay, batch operations |
| `InMemoryMessageConsumer` | `src/Paramore.Brighter/` | Purge, invalid message routing |
| `InMemoryOutbox` | `src/Paramore.Brighter/` | Delete, GetOutstandingMessageCount |
| `InternalBus` | `src/Paramore.Brighter/` | Timeout, edge cases |
| Observability components | `src/Paramore.Brighter/Observability/` | Metrics processors, samplers |

## Requirements

### Functional Requirements

1. **Core Value Types** - Add comprehensive tests for:
   - `Message` construction, equality, serialization
   - `MessageHeader` all properties, bag operations
   - `MessageBody` encoding, value retrieval
   - `Id` creation, parsing, equality
   - `RoutingKey` validation, equality
   - `PartitionKey` behavior

2. **Builder Classes** - Add tests for:
   - `CommandProcessorBuilder` fluent API
   - Builder validation and error handling
   - Default value behavior

3. **Extension Methods** - Add tests for:
   - `CharacterEncodingExtensions`
   - `DateTimeOffsetExtensions`
   - `DictionaryExtensions`
   - `MethodInfoExtensions`
   - `ReflectionExtensions`
   - `RequestContextExtensions`
   - `TypeExtensions`

4. **JSON Converters** - Add tests for:
   - `IdConverter` round-trip serialization
   - `RoutingKeyConvertor` round-trip serialization
   - All `NJsonConverters` implementations

5. **In-Memory Components** - Add tests for:
   - `InMemoryArchiveProvider` all operations
   - `InMemoryTransactionProvider` lifecycle
   - `InMemorySubscription` configuration
   - `InMemoryMessageProducer` async/batch operations

6. **DI Extensions** - Add tests for:
   - `ServiceProviderMapperFactory` all lifetimes
   - `ServiceCollectionMessageMapperRegistryBuilder` manual registration
   - `UseRpc` configuration
   - `UseScheduler`, `UsePublicationFinder`, `UseExternalLuggageStore` extensions

### Non-Functional Requirements

- **Test Quality**: All tests should follow existing BDD naming conventions (`When_<scenario>`)
- **Independence**: Tests must run without external dependencies (no Docker, no network)
- **Performance**: Test suite should complete within reasonable time (<5 minutes for unit tests)
- **Maintainability**: Tests should be clear, focused, and easy to understand
- **Coverage**: Aim for >80% line coverage on newly tested components

### Constraints

- Use xUnit as the testing framework (consistent with existing tests)
- Use FakeItEasy for mocking (consistent with existing tests)
- Follow MIT license header convention
- No changes to production code unless bugs are discovered

### Out of Scope

- Integration tests requiring Docker (Group 2 tests)
- Performance/load testing
- End-to-end testing
- Tests for external provider implementations (AWS, Azure, Kafka, etc.)

## Acceptance Criteria

### Phase 1: Core Value Types
1. `Message` has tests for construction, equality, and all properties
2. `MessageHeader` has tests for all properties and bag operations
3. `MessageBody` has tests for encoding and value handling
4. `Id` has tests for creation, parsing, equality, and edge cases
5. `RoutingKey` has tests for validation and equality
6. All tests pass and follow naming conventions

### Phase 2: Builders and Configuration
1. `CommandProcessorBuilder` has tests for fluent API usage
2. `Subscription` and `Publication` have configuration tests
3. `InMemorySubscription` has dedicated tests

### Phase 3: Extension Methods
1. All extension method classes have dedicated test classes
2. Edge cases and error conditions are tested

### Phase 4: JSON Converters
1. All JSON converters have round-trip serialization tests
2. Error handling for invalid input is tested

### Phase 5: In-Memory Components
1. `InMemoryArchiveProvider` has full test coverage
2. `InMemoryTransactionProvider` has lifecycle tests
3. `InMemoryMessageProducer` async/batch operations tested
4. `InMemoryMessageConsumer` purge/invalid message tested

### Phase 6: DI Extensions
1. `ServiceProviderMapperFactory` has lifetime tests
2. Manual registration methods have tests
3. All `Use*` extension methods have tests

### Final Acceptance
1. All new tests pass
2. Existing tests continue to pass (no regressions)
3. Test coverage improved by ~100 new test classes
4. Documentation updated where needed

## Priority Matrix

| Priority | Components | Estimated Effort | Risk if Untested |
|----------|------------|------------------|------------------|
| P1 - Critical | Message types, Id, RoutingKey | 3-4 days | High - Core types |
| P2 - High | CommandProcessorBuilder, OutboxSweeper | 2-3 days | High - Key functionality |
| P3 - Medium | Extensions, JSON Converters | 2-3 days | Medium - Utilities |
| P4 - Medium | In-Memory components | 2-3 days | Medium - Test infrastructure |
| P5 - Lower | DI Extensions | 2-3 days | Lower - Configuration |

**Total Estimated Effort**: 11-16 days

## Test Inventory

### New Test Classes Needed

#### Paramore.Brighter.Core.Tests (~67 new classes)

**Value Types (15 classes)**
- `When_creating_a_message.cs`
- `When_comparing_messages_for_equality.cs`
- `When_creating_a_message_header.cs`
- `When_adding_to_message_header_bag.cs`
- `When_creating_a_message_body.cs`
- `When_creating_an_id.cs`
- `When_parsing_an_id_from_string.cs`
- `When_comparing_ids_for_equality.cs`
- `When_creating_a_routing_key.cs`
- `When_validating_a_routing_key.cs`
- `When_creating_a_partition_key.cs`
- `When_creating_a_subscription.cs`
- `When_creating_a_publication.cs`
- `When_configuring_subscription_options.cs`
- `When_configuring_publication_options.cs`

**Builders (8 classes)**
- `When_building_a_command_processor.cs`
- `When_building_a_command_processor_with_inbox.cs`
- `When_building_a_command_processor_with_outbox.cs`
- `When_building_a_command_processor_with_policies.cs`
- `When_building_a_command_processor_with_invalid_config.cs`
- `When_building_a_command_processor_with_handlers.cs`
- `When_building_a_command_processor_with_mappers.cs`
- `When_building_a_command_processor_with_transforms.cs`

**Extension Methods (10 classes)**
- `When_using_character_encoding_extensions.cs`
- `When_using_datetime_offset_extensions.cs`
- `When_using_dictionary_extensions.cs`
- `When_using_method_info_extensions.cs`
- `When_using_reflection_extensions.cs`
- `When_using_request_context_extensions.cs`
- `When_using_type_extensions.cs`
- `When_using_resilience_pipeline_extensions.cs`
- `When_extending_string_for_encoding.cs`
- `When_extending_dictionaries_for_merge.cs`

**JSON Converters (12 classes)**
- `When_serializing_id_to_json.cs`
- `When_deserializing_id_from_json.cs`
- `When_serializing_routing_key_to_json.cs`
- `When_deserializing_routing_key_from_json.cs`
- `When_serializing_subscription_name_to_json.cs`
- `When_deserializing_subscription_name_from_json.cs`
- `When_serializing_trace_parent_to_json.cs`
- `When_deserializing_trace_parent_from_json.cs`
- `When_serializing_with_newtonsoft_id_converter.cs`
- `When_deserializing_with_newtonsoft_id_converter.cs`
- `When_handling_null_in_json_converters.cs`
- `When_handling_invalid_json_in_converters.cs`

**Observability (8 classes)**
- `When_processing_metrics_from_traces.cs`
- `When_using_tail_sampler_processor.cs`
- `When_recording_db_metrics.cs`
- `When_recording_messaging_metrics.cs`
- `When_propagating_text_context.cs`
- `When_creating_brighter_tracer.cs`
- `When_tracing_command_processor_operations.cs`
- `When_tracing_message_dispatch_operations.cs`

**OutboxSweeper (4 classes)**
- `When_sweeping_outstanding_messages.cs`
- `When_sweeping_with_batch_size.cs`
- `When_sweeping_with_age_threshold.cs`
- `When_sweeper_encounters_errors.cs`

**Miscellaneous (10 classes)**
- `When_creating_request_context.cs`
- `When_using_internal_bus.cs`
- `When_creating_producer_registry.cs`
- `When_using_handler_configuration.cs`
- `When_using_combined_channel_factory.cs`
- `When_using_combined_producer_registry_factory.cs`
- `When_creating_channel.cs`
- `When_creating_channel_async.cs`
- `When_using_transform_lifetime_scope.cs`
- `When_using_wrap_unwrap_pipelines.cs`

#### Paramore.Brighter.Extensions.Tests (~17 new classes)

**Mapper Factory (4 classes)**
- `When_creating_mapper_with_singleton_lifetime.cs`
- `When_creating_mapper_with_scoped_lifetime.cs`
- `When_creating_mapper_with_transient_lifetime.cs`
- `When_mapper_factory_handles_missing_mapper.cs`

**Registry Builders (5 classes)**
- `When_registering_message_mapper_manually.cs`
- `When_registering_async_message_mapper_manually.cs`
- `When_setting_default_message_mapper.cs`
- `When_registering_subscriber_manually.cs`
- `When_registering_async_subscriber_manually.cs`

**Extension Methods (5 classes)**
- `When_using_scheduler_extension.cs`
- `When_using_publication_finder_extension.cs`
- `When_using_external_luggage_store_extension.cs`
- `When_configuring_json_serialisation.cs`
- `When_using_rpc_configuration.cs`

**ServiceActivator (3 classes)**
- `When_configuring_consumers_options.cs`
- `When_adding_consumers_with_func_overload.cs`
- `When_configuring_inbox_for_consumers.cs`

#### Paramore.Brighter.InMemory.Tests (~18 new classes)

**Archive Provider (4 classes)**
- `When_archiving_a_message.cs`
- `When_archiving_a_message_async.cs`
- `When_archiving_multiple_messages.cs`
- `When_retrieving_archived_messages.cs`

**Transaction Provider (4 classes)**
- `When_getting_a_transaction.cs`
- `When_committing_a_transaction.cs`
- `When_rolling_back_a_transaction.cs`
- `When_checking_transaction_state.cs`

**Subscription (2 classes)**
- `When_creating_inmemory_subscription.cs`
- `When_configuring_subscription_dead_letter.cs`

**Producer Expansion (4 classes)**
- `When_sending_message_async.cs`
- `When_sending_message_with_delay.cs`
- `When_sending_batch_of_messages.cs`
- `When_producer_publishes_event.cs`

**Consumer Expansion (2 classes)**
- `When_purging_messages_from_consumer.cs`
- `When_rejecting_to_invalid_message_channel.cs`

**Factory Expansion (2 classes)**
- `When_creating_async_channel.cs`
- `When_creating_producer_with_multiple_publications.cs`
