# Implementation Tasks

This document outlines the tasks for implementing improved test coverage as specified in the requirements.

## TDD Workflow

Each task follows a Test-Driven Development workflow with manual approval:

1. **RED**: Write a failing test that specifies the behavior
2. **APPROVAL**: Present the test for user review before proceeding
   - Stop after writing the test
   - User reviews the test in their IDE
   - Wait for explicit approval before continuing
3. **GREEN**: Run tests to verify behavior (most tests here are for existing code)
4. **REFACTOR**: Improve test design while keeping tests green

### Workflow for Each Task

```
Agent writes test → STOP → User reviews → User approves → Agent runs test → Agent marks task complete
```

**Important Notes:**
- Since these tests are for existing production code, the "GREEN" phase is typically just running the test
- If a test fails unexpectedly, it may indicate a bug in production code - document and discuss before proceeding
- Do NOT batch multiple tests - complete one test at a time with approval

Tests are organized by phase and priority. Each phase can be worked on independently.

## Task Overview

| Phase | Focus Area | New Test Classes | Priority |
|-------|------------|------------------|----------|
| 1 | Core Value Types | 15 | P1 - Critical |
| 2 | Builders & Configuration | 8 | P2 - High |
| 3 | Extension Methods | 10 | P3 - Medium |
| 4 | JSON Converters | 12 | P3 - Medium |
| 5 | In-Memory Components | 18 | P4 - Medium |
| 6 | DI Extensions | 17 | P5 - Lower |
| 7 | Observability & Misc | 22 | P3 - Medium |

---

## Phase 1: Core Value Types (Priority 1 - Critical)

These are fundamental types used throughout the codebase. Testing them ensures the foundation is solid.

### 1.1 Message Types

- [ ] **TEST: When creating a message**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Messages/`
  - Test file: `When_creating_a_message.cs`
  - Test should verify:
    - Message can be created with header and body
    - Message.Id returns the header's message id
    - Message.Header and Message.Body are accessible
    - Empty message body is handled correctly

- [ ] **TEST: When comparing messages for equality**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Messages/`
  - Test file: `When_comparing_messages_for_equality.cs`
  - Test should verify:
    - Messages with same Id are equal
    - Messages with different Ids are not equal
    - Null comparison works correctly
    - GetHashCode is consistent with Equals

- [ ] **TEST: When creating a message header**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Messages/`
  - Test file: `When_creating_a_message_header.cs`
  - Test should verify:
    - Header can be created with required parameters
    - All properties are set correctly (MessageId, Topic, MessageType, etc.)
    - Timestamp is set appropriately
    - CorrelationId, ReplyTo, ContentType work correctly

- [ ] **TEST: When adding to message header bag**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Messages/`
  - Test file: `When_adding_to_message_header_bag.cs`
  - Test should verify:
    - Items can be added to the bag
    - Items can be retrieved from the bag
    - Missing items return default/throw appropriately
    - Bag enumeration works

- [ ] **TEST: When creating a message body**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Messages/`
  - Test file: `When_creating_a_message_body.cs`
  - Test should verify:
    - Body can be created with string value
    - Body can be created with byte array
    - Value property returns correct content
    - Bytes property returns correct encoding
    - Empty body is handled

### 1.2 Id Type

- [ ] **TEST: When creating an id**
  - Test location: `tests/Paramore.Brighter.Core.Tests/ValueTypes/`
  - Test file: `When_creating_an_id.cs`
  - Test should verify:
    - Id.New() creates a valid Id
    - Id can be created from Guid
    - Id can be created from string
    - Id.Empty returns empty Id
    - Id.Value returns the underlying value

- [ ] **TEST: When parsing an id from string**
  - Test location: `tests/Paramore.Brighter.Core.Tests/ValueTypes/`
  - Test file: `When_parsing_an_id_from_string.cs`
  - Test should verify:
    - Valid Guid string parses correctly
    - Invalid string throws or returns empty
    - Null/empty string handling
    - ToString() returns expected format

- [ ] **TEST: When comparing ids for equality**
  - Test location: `tests/Paramore.Brighter.Core.Tests/ValueTypes/`
  - Test file: `When_comparing_ids_for_equality.cs`
  - Test should verify:
    - Same Ids are equal
    - Different Ids are not equal
    - Id.Empty equals Id.Empty
    - Equality operators work (==, !=)
    - GetHashCode is consistent

### 1.3 RoutingKey Type

- [ ] **TEST: When creating a routing key**
  - Test location: `tests/Paramore.Brighter.Core.Tests/ValueTypes/`
  - Test file: `When_creating_a_routing_key.cs`
  - Test should verify:
    - RoutingKey can be created with valid string
    - Value property returns the key
    - ToString() returns expected format
    - Empty RoutingKey behavior

- [ ] **TEST: When validating a routing key**
  - Test location: `tests/Paramore.Brighter.Core.Tests/ValueTypes/`
  - Test file: `When_validating_a_routing_key.cs`
  - Test should verify:
    - Valid routing keys are accepted
    - Null/empty strings handled appropriately
    - Equality comparison works
    - GetHashCode is consistent

### 1.4 PartitionKey Type

- [ ] **TEST: When creating a partition key**
  - Test location: `tests/Paramore.Brighter.Core.Tests/ValueTypes/`
  - Test file: `When_creating_a_partition_key.cs`
  - Test should verify:
    - PartitionKey can be created with valid string
    - Value property returns the key
    - Empty PartitionKey behavior
    - Equality and GetHashCode work correctly

### 1.5 Subscription & Publication

- [ ] **TEST: When creating a subscription**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Configuration/`
  - Test file: `When_creating_a_subscription.cs`
  - Test should verify:
    - Subscription can be created with required parameters
    - DataType, ChannelName, RoutingKey are set
    - Default values are appropriate
    - NoOfPerformers, TimeOut, RequeueCount work

- [ ] **TEST: When creating a publication**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Configuration/`
  - Test file: `When_creating_a_publication.cs`
  - Test should verify:
    - Publication can be created with required parameters
    - Topic property is set correctly
    - RequestType is set correctly
    - MakeChannels option works

- [ ] **TEST: When configuring subscription options**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Configuration/`
  - Test file: `When_configuring_subscription_options.cs`
  - Test should verify:
    - BufferSize can be configured
    - LockTimeout can be configured
    - UnacceptableMessageLimit works
    - RequeueDelayInMs works

---

## Phase 2: Builders & Configuration (Priority 2 - High)

### 2.1 CommandProcessorBuilder

- [ ] **TEST: When building a command processor**
  - Test location: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/`
  - Test file: `When_building_a_command_processor.cs`
  - Test should verify:
    - Builder creates valid CommandProcessor
    - Handlers are registered correctly
    - Default configuration is appropriate

- [ ] **TEST: When building a command processor with inbox**
  - Test location: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/`
  - Test file: `When_building_a_command_processor_with_inbox.cs`
  - Test should verify:
    - Inbox is configured correctly
    - InboxConfiguration options are respected

- [ ] **TEST: When building a command processor with outbox**
  - Test location: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/`
  - Test file: `When_building_a_command_processor_with_outbox.cs`
  - Test should verify:
    - Outbox is configured correctly
    - OutboxConfiguration options are respected
    - Producer registry is set

- [ ] **TEST: When building a command processor with policies**
  - Test location: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/`
  - Test file: `When_building_a_command_processor_with_policies.cs`
  - Test should verify:
    - Resilience policies are registered
    - Policy names are accessible
    - Default policies work

- [ ] **TEST: When building a command processor with invalid config**
  - Test location: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/`
  - Test file: `When_building_a_command_processor_with_invalid_config.cs`
  - Test should verify:
    - Missing required components throw
    - Invalid configuration is rejected
    - Error messages are clear

- [ ] **TEST: When building a command processor with handlers**
  - Test location: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/`
  - Test file: `When_building_a_command_processor_with_handlers.cs`
  - Test should verify:
    - Subscriber registry is set
    - Handler factory is configured
    - Handlers can be resolved

- [ ] **TEST: When building a command processor with mappers**
  - Test location: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/`
  - Test file: `When_building_a_command_processor_with_mappers.cs`
  - Test should verify:
    - Message mapper registry is configured
    - Mappers can be resolved for request types

- [ ] **TEST: When building a command processor with transforms**
  - Test location: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/`
  - Test file: `When_building_a_command_processor_with_transforms.cs`
  - Test should verify:
    - Transform registry is configured
    - Transform factory is set

---

## Phase 3: Extension Methods (Priority 3 - Medium)

- [ ] **TEST: When using character encoding extensions**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Extensions/`
  - Test file: `When_using_character_encoding_extensions.cs`
  - Test should verify all methods in `CharacterEncodingExtensions`

- [ ] **TEST: When using datetime offset extensions**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Extensions/`
  - Test file: `When_using_datetime_offset_extensions.cs`
  - Test should verify all methods in `DateTimeOffsetExtensions`

- [ ] **TEST: When using dictionary extensions**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Extensions/`
  - Test file: `When_using_dictionary_extensions.cs`
  - Test should verify all methods in `DictionaryExtensions`

- [ ] **TEST: When using method info extensions**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Extensions/`
  - Test file: `When_using_method_info_extensions.cs`
  - Test should verify all methods in `MethodInfoExtensions`

- [ ] **TEST: When using reflection extensions**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Extensions/`
  - Test file: `When_using_reflection_extensions.cs`
  - Test should verify all methods in `ReflectionExtensions`

- [ ] **TEST: When using request context extensions**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Extensions/`
  - Test file: `When_using_request_context_extensions.cs`
  - Test should verify all methods in `RequestContextExtensions`

- [ ] **TEST: When using type extensions**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Extensions/`
  - Test file: `When_using_type_extensions.cs`
  - Test should verify all methods in `TypeExtensions`

- [ ] **TEST: When using resilience pipeline extensions**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Extensions/`
  - Test file: `When_using_resilience_pipeline_extensions.cs`
  - Test should verify all methods in `ResiliencePipelineRegistryExtensions`

- [ ] **TEST: When extending string for encoding**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Extensions/`
  - Test file: `When_extending_string_for_encoding.cs`
  - Test edge cases for string encoding methods

- [ ] **TEST: When extending dictionaries for merge**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Extensions/`
  - Test file: `When_extending_dictionaries_for_merge.cs`
  - Test dictionary merge/combine operations

---

## Phase 4: JSON Converters (Priority 3 - Medium)

### 4.1 System.Text.Json Converters

- [ ] **TEST: When serializing id to json**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_serializing_id_to_json.cs`
  - Test should verify IdConverter serialization

- [ ] **TEST: When deserializing id from json**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_deserializing_id_from_json.cs`
  - Test should verify IdConverter deserialization

- [ ] **TEST: When serializing routing key to json**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_serializing_routing_key_to_json.cs`
  - Test should verify RoutingKeyConvertor serialization

- [ ] **TEST: When deserializing routing key from json**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_deserializing_routing_key_from_json.cs`
  - Test should verify RoutingKeyConvertor deserialization

- [ ] **TEST: When serializing subscription name to json**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_serializing_subscription_name_to_json.cs`
  - Test should verify SubscriptionNameConverter

- [ ] **TEST: When serializing trace parent to json**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_serializing_trace_parent_to_json.cs`
  - Test should verify TraceParentConverter

- [ ] **TEST: When serializing trace state to json**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_serializing_trace_state_to_json.cs`
  - Test should verify TraceStateConverter

### 4.2 Newtonsoft.Json Converters

- [ ] **TEST: When serializing with newtonsoft id converter**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_serializing_with_newtonsoft_id_converter.cs`
  - Test should verify NJson IdConverter

- [ ] **TEST: When deserializing with newtonsoft id converter**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_deserializing_with_newtonsoft_id_converter.cs`
  - Test should verify NJson IdConverter deserialization

- [ ] **TEST: When serializing with newtonsoft routing key converter**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_serializing_with_newtonsoft_routing_key_converter.cs`
  - Test should verify NJson RoutingKeyConverter

### 4.3 Error Handling

- [ ] **TEST: When handling null in json converters**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_handling_null_in_json_converters.cs`
  - Test null handling for all converters

- [ ] **TEST: When handling invalid json in converters**
  - Test location: `tests/Paramore.Brighter.Core.Tests/JsonConverters/`
  - Test file: `When_handling_invalid_json_in_converters.cs`
  - Test error handling for malformed input

---

## Phase 5: In-Memory Components (Priority 4 - Medium)

### 5.1 InMemoryArchiveProvider

- [ ] **TEST: When archiving a message**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Archive/`
  - Test file: `When_archiving_a_message.cs`
  - Test should verify:
    - ArchiveMessage stores the message
    - Archived message can be retrieved
    - Archive metadata is correct

- [ ] **TEST: When archiving a message async**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Archive/`
  - Test file: `When_archiving_a_message_async.cs`
  - Test should verify async version of archiving

- [ ] **TEST: When archiving multiple messages**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Archive/`
  - Test file: `When_archiving_multiple_messages.cs`
  - Test should verify:
    - ArchiveMessagesAsync handles batches
    - All messages are stored
    - Order is preserved (if applicable)

- [ ] **TEST: When retrieving archived messages**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Archive/`
  - Test file: `When_retrieving_archived_messages.cs`
  - Test should verify:
    - ArchivedMessages property returns all archived
    - Empty archive returns empty collection

### 5.2 InMemoryTransactionProvider

- [ ] **TEST: When getting a transaction**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Transaction/`
  - Test file: `When_getting_a_transaction.cs`
  - Test should verify:
    - GetTransaction returns a transaction
    - GetTransactionAsync returns a transaction
    - HasOpenTransaction is true after getting

- [ ] **TEST: When committing a transaction**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Transaction/`
  - Test file: `When_committing_a_transaction.cs`
  - Test should verify:
    - Commit completes successfully
    - CommitAsync completes successfully
    - HasOpenTransaction is false after commit

- [ ] **TEST: When rolling back a transaction**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Transaction/`
  - Test file: `When_rolling_back_a_transaction.cs`
  - Test should verify:
    - Rollback completes successfully
    - RollbackAsync completes successfully
    - HasOpenTransaction is false after rollback

- [ ] **TEST: When checking transaction state**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Transaction/`
  - Test file: `When_checking_transaction_state.cs`
  - Test should verify:
    - HasOpenTransaction reflects correct state
    - IsSharedConnection returns expected value
    - Close works correctly

### 5.3 InMemorySubscription

- [ ] **TEST: When creating inmemory subscription**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Subscription/`
  - Test file: `When_creating_inmemory_subscription.cs`
  - Test should verify:
    - Subscription can be created with all parameters
    - Default values are appropriate
    - Generic version works

- [ ] **TEST: When configuring subscription dead letter**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Subscription/`
  - Test file: `When_configuring_subscription_dead_letter.cs`
  - Test should verify:
    - DeadLetterRoutingKey can be set
    - InvalidMessageRoutingKey can be set

### 5.4 InMemoryMessageProducer Extensions

- [ ] **TEST: When sending message async**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Producer/`
  - Test file: `When_sending_message_async.cs`
  - Test should verify SendAsync works correctly

- [ ] **TEST: When sending message with delay**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Producer/`
  - Test file: `When_sending_message_with_delay.cs`
  - Test should verify:
    - SendWithDelay schedules correctly
    - SendWithDelayAsync works
    - Delay is respected

- [ ] **TEST: When sending batch of messages**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Producer/`
  - Test file: `When_sending_batch_of_messages.cs`
  - Test should verify:
    - Batch send works
    - CreateBatchesAsync creates correct batches
    - All messages in batch are sent

- [ ] **TEST: When producer publishes event**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Producer/`
  - Test file: `When_producer_publishes_event.cs`
  - Test should verify:
    - OnMessagePublished event is raised
    - Event args contain correct message

### 5.5 InMemoryMessageConsumer Extensions

- [ ] **TEST: When purging messages from consumer**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Consumer/`
  - Test file: `When_purging_messages_from_consumer.cs`
  - Test should verify:
    - Purge removes all messages
    - PurgeAsync removes all messages
    - Consumer is empty after purge

- [ ] **TEST: When rejecting to invalid message channel**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Consumer/`
  - Test file: `When_rejecting_to_invalid_message_channel.cs`
  - Test should verify:
    - Invalid messages route to InvalidMessageTopic
    - Rejection reason is Unacceptable

### 5.6 Factory Extensions

- [ ] **TEST: When creating async channel**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Consumer/`
  - Test file: `When_creating_async_channel.cs`
  - Test should verify:
    - CreateAsyncChannel creates valid channel
    - CreateAsyncChannelAsync works

- [ ] **TEST: When creating producer with multiple publications**
  - Test location: `tests/Paramore.Brighter.InMemory.Tests/Producer/`
  - Test file: `When_creating_producer_with_multiple_publications.cs`
  - Test should verify:
    - Multiple publications are registered
    - Each topic has a producer

---

## Phase 6: DI Extensions (Priority 5 - Lower)

### 6.1 ServiceProviderMapperFactory

- [ ] **TEST: When creating mapper with singleton lifetime**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/MapperFactory/`
  - Test file: `When_creating_mapper_with_singleton_lifetime.cs`
  - Test should verify singleton behavior for mappers

- [ ] **TEST: When creating mapper with scoped lifetime**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/MapperFactory/`
  - Test file: `When_creating_mapper_with_scoped_lifetime.cs`
  - Test should verify scoped behavior for mappers

- [ ] **TEST: When creating mapper with transient lifetime**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/MapperFactory/`
  - Test file: `When_creating_mapper_with_transient_lifetime.cs`
  - Test should verify transient behavior for mappers

- [ ] **TEST: When mapper factory handles missing mapper**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/MapperFactory/`
  - Test file: `When_mapper_factory_handles_missing_mapper.cs`
  - Test should verify error handling for unregistered mappers

### 6.2 Registry Builders

- [ ] **TEST: When registering message mapper manually**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/RegistryBuilder/`
  - Test file: `When_registering_message_mapper_manually.cs`
  - Test should verify Register<TRequest, TMapper>()

- [ ] **TEST: When registering async message mapper manually**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/RegistryBuilder/`
  - Test file: `When_registering_async_message_mapper_manually.cs`
  - Test should verify RegisterAsync<TRequest, TMapper>()

- [ ] **TEST: When setting default message mapper**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/RegistryBuilder/`
  - Test file: `When_setting_default_message_mapper.cs`
  - Test should verify SetDefaultMessageMapper()

- [ ] **TEST: When registering subscriber manually**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/RegistryBuilder/`
  - Test file: `When_registering_subscriber_manually.cs`
  - Test should verify subscriber registry registration

- [ ] **TEST: When registering async subscriber manually**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/RegistryBuilder/`
  - Test file: `When_registering_async_subscriber_manually.cs`
  - Test should verify async subscriber registration

### 6.3 Extension Methods

- [ ] **TEST: When using scheduler extension**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/Extensions/`
  - Test file: `When_using_scheduler_extension.cs`
  - Test should verify UseScheduler<T>()

- [ ] **TEST: When using publication finder extension**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/Extensions/`
  - Test file: `When_using_publication_finder_extension.cs`
  - Test should verify UsePublicationFinder<T>()

- [ ] **TEST: When using external luggage store extension**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/Extensions/`
  - Test file: `When_using_external_luggage_store_extension.cs`
  - Test should verify UseExternalLuggageStore<T>() overloads

- [ ] **TEST: When configuring json serialisation**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/Extensions/`
  - Test file: `When_configuring_json_serialisation.cs`
  - Test should verify ConfigureJsonSerialisation()

- [ ] **TEST: When using rpc configuration**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/Extensions/`
  - Test file: `When_using_rpc_configuration.cs`
  - Test should verify UseRpc configuration

### 6.4 ServiceActivator Extensions

- [ ] **TEST: When configuring consumers options**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/ServiceActivator/`
  - Test file: `When_configuring_consumers_options.cs`
  - Test should verify ConsumersOptions configuration

- [ ] **TEST: When adding consumers with func overload**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/ServiceActivator/`
  - Test file: `When_adding_consumers_with_func_overload.cs`
  - Test should verify Func<IServiceProvider, ConsumersOptions> overload

- [ ] **TEST: When configuring inbox for consumers**
  - Test location: `tests/Paramore.Brighter.Extensions.Tests/ServiceActivator/`
  - Test file: `When_configuring_inbox_for_consumers.cs`
  - Test should verify inbox configuration in ServiceActivator

---

## Phase 7: Observability & Miscellaneous (Priority 3 - Medium)

### 7.1 Observability Components

- [ ] **TEST: When processing metrics from traces**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/`
  - Test file: `When_processing_metrics_from_traces.cs`
  - Test should verify BrighterMetricsFromTracesProcessor

- [ ] **TEST: When using tail sampler processor**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Observability/Sampling/`
  - Test file: `When_using_tail_sampler_processor.cs`
  - Test should verify TailSamplerProcessor

- [ ] **TEST: When recording db metrics**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/`
  - Test file: `When_recording_db_metrics.cs`
  - Test should verify DbMeter

- [ ] **TEST: When recording messaging metrics**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/`
  - Test file: `When_recording_messaging_metrics.cs`
  - Test should verify MessagingMeter

- [ ] **TEST: When propagating text context**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Observability/Propagation/`
  - Test file: `When_propagating_text_context.cs`
  - Test should verify TextContextPropogator

### 7.2 OutboxSweeper

- [ ] **TEST: When sweeping outstanding messages**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Sweeper/`
  - Test file: `When_sweeping_outstanding_messages.cs`
  - Test should verify basic sweeper functionality

- [ ] **TEST: When sweeping with batch size**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Sweeper/`
  - Test file: `When_sweeping_with_batch_size.cs`
  - Test should verify batch size configuration

- [ ] **TEST: When sweeping with age threshold**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Sweeper/`
  - Test file: `When_sweeping_with_age_threshold.cs`
  - Test should verify age-based message selection

- [ ] **TEST: When sweeper encounters errors**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Sweeper/`
  - Test file: `When_sweeper_encounters_errors.cs`
  - Test should verify error handling

### 7.3 Miscellaneous Core Components

- [ ] **TEST: When creating request context**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Context/`
  - Test file: `When_creating_request_context.cs`
  - Test should verify RequestContext creation and properties

- [ ] **TEST: When using internal bus**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Bus/`
  - Test file: `When_using_internal_bus.cs`
  - Test should verify InternalBus additional scenarios

- [ ] **TEST: When creating producer registry**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Producers/`
  - Test file: `When_creating_producer_registry.cs`
  - Test should verify ProducerRegistry

- [ ] **TEST: When using handler configuration**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Configuration/`
  - Test file: `When_using_handler_configuration.cs`
  - Test should verify HandlerConfiguration

- [ ] **TEST: When using combined channel factory**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Channel/`
  - Test file: `When_using_combined_channel_factory.cs`
  - Test should verify CombinedChannelFactory

- [ ] **TEST: When using combined producer registry factory**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Producers/`
  - Test file: `When_using_combined_producer_registry_factory.cs`
  - Test should verify CombinedProducerRegistryFactory

- [ ] **TEST: When creating channel**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Channel/`
  - Test file: `When_creating_channel.cs`
  - Test should verify Channel creation

- [ ] **TEST: When creating channel async**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Channel/`
  - Test file: `When_creating_channel_async.cs`
  - Test should verify ChannelAsync creation

- [ ] **TEST: When using transform lifetime scope**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Transforms/`
  - Test file: `When_using_transform_lifetime_scope.cs`
  - Test should verify TransformLifetimeScope

- [ ] **TEST: When using wrap unwrap pipelines**
  - Test location: `tests/Paramore.Brighter.Core.Tests/Transforms/`
  - Test file: `When_using_wrap_unwrap_pipelines.cs`
  - Test should verify WrapPipeline and UnwrapPipeline

---

## Task Dependencies

Phases can be worked on in parallel by different contributors, but within each phase tasks should generally be completed in order.

```
Phase 1 (Core Value Types) ─────────────┐
Phase 2 (Builders)          ────────────┼──→ All phases can run in parallel
Phase 3 (Extensions)        ────────────┤
Phase 4 (JSON Converters)   ────────────┤
Phase 5 (In-Memory)         ────────────┤
Phase 6 (DI Extensions)     ────────────┤
Phase 7 (Observability)     ────────────┘
```

## Effort Estimates

| Phase | Estimated Effort | Test Classes |
|-------|------------------|--------------|
| Phase 1 | 3-4 days | 15 |
| Phase 2 | 2-3 days | 8 |
| Phase 3 | 2-3 days | 10 |
| Phase 4 | 2-3 days | 12 |
| Phase 5 | 3-4 days | 18 |
| Phase 6 | 2-3 days | 17 |
| Phase 7 | 3-4 days | 22 |
| **Total** | **17-24 days** | **102** |

## Risk Mitigation

- **Risk**: Tests reveal bugs in existing code
  - **Mitigation**: Document bugs found, create issues, fix in separate PRs

- **Risk**: Test infrastructure changes needed
  - **Mitigation**: Add TestDoubles as needed, following existing patterns

- **Risk**: Scope creep into integration tests
  - **Mitigation**: Strictly limit to unit tests; defer integration test improvements

## Notes

- **APPROVAL REQUIRED**: Each test must be reviewed and approved before marking complete
- All tests should follow the existing `When_<scenario>` BDD naming convention
- Use xUnit as the testing framework
- Use FakeItEasy for mocking where needed
- Prefer in-memory implementations (e.g., `InMemoryOutbox`) over mocks
- Follow MIT license header convention in all new files
- Run existing tests after each phase to ensure no regressions
- If a test reveals a bug in production code, create an issue and discuss before proceeding
