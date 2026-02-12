# Ralph Tasks: Test Coverage Improvement

> Auto-generated from tasks.md for unattended TDD execution.
> Each task is self-contained with all context a fresh Claude session needs.

## Spec Context

- **Spec**: 0003-test-coverage-improvement
- **Requirements**: specs/0003-test-coverage-improvement/requirements.md
- **ADRs**: None (test coverage improvement spec)

## Tasks

---

## Phase 1: Core Value Types (Priority 1 - Critical)

- [ ] **Message creation and properties**
  - **Behavior**: When creating a Message with a MessageHeader and MessageBody, the Message should expose Id, Header, and Body properties correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Messages/When_creating_a_message.cs`
  - **Test should verify**:
    - Message can be created with header and body
    - Message.Id returns the header's message id
    - Message.Header and Message.Body are accessible
    - Empty message body is handled correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Message.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_a_message"`
  - **References**: `src/Paramore.Brighter/Message.cs`, `src/Paramore.Brighter/MessageHeader.cs`, `src/Paramore.Brighter/MessageBody.cs`

- [ ] **Message equality comparison**
  - **Behavior**: When comparing two Messages for equality, messages with the same Id should be equal and have consistent hash codes
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Messages/When_comparing_messages_for_equality.cs`
  - **Test should verify**:
    - Messages with same Id are equal
    - Messages with different Ids are not equal
    - Null comparison works correctly
    - GetHashCode is consistent with Equals
  - **Implementation files**:
    - `src/Paramore.Brighter/Message.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_comparing_messages_for_equality"`
  - **References**: `src/Paramore.Brighter/Message.cs`

- [ ] **MessageHeader creation and properties**
  - **Behavior**: When creating a MessageHeader with required parameters, all properties should be set correctly including MessageId, Topic, MessageType, and Timestamp
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Messages/When_creating_a_message_header.cs`
  - **Test should verify**:
    - Header can be created with required parameters
    - All properties are set correctly (MessageId, Topic, MessageType, etc.)
    - Timestamp is set appropriately
    - CorrelationId, ReplyTo, ContentType work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/MessageHeader.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_a_message_header"`
  - **References**: `src/Paramore.Brighter/MessageHeader.cs`, `src/Paramore.Brighter/RoutingKey.cs`

- [ ] **MessageHeader bag operations**
  - **Behavior**: When adding items to the MessageHeader's bag, they should be retrievable and enumerable
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Messages/When_adding_to_message_header_bag.cs`
  - **Test should verify**:
    - Items can be added to the bag
    - Items can be retrieved from the bag
    - Missing items return default/throw appropriately
    - Bag enumeration works
  - **Implementation files**:
    - `src/Paramore.Brighter/MessageHeader.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_adding_to_message_header_bag"`
  - **References**: `src/Paramore.Brighter/MessageHeader.cs`

- [ ] **MessageBody creation and encoding**
  - **Behavior**: When creating a MessageBody with string or byte array content, the Value and Bytes properties should return correct content
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Messages/When_creating_a_message_body.cs`
  - **Test should verify**:
    - Body can be created with string value
    - Body can be created with byte array
    - Value property returns correct content
    - Bytes property returns correct encoding
    - Empty body is handled
  - **Implementation files**:
    - `src/Paramore.Brighter/MessageBody.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_a_message_body"`
  - **References**: `src/Paramore.Brighter/MessageBody.cs`

- [ ] **Id creation and value access**
  - **Behavior**: When creating an Id using New(), from Guid, or from string, the Value property should return the underlying value correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ValueTypes/When_creating_an_id.cs`
  - **Test should verify**:
    - Id.New() creates a valid Id
    - Id can be created from Guid
    - Id can be created from string
    - Id.Empty returns empty Id
    - Id.Value returns the underlying value
  - **Implementation files**:
    - `src/Paramore.Brighter/Id.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_an_id"`
  - **References**: `src/Paramore.Brighter/Id.cs`

- [ ] **Id parsing from string**
  - **Behavior**: When parsing an Id from a string, valid Guid strings should parse correctly and invalid strings should be handled appropriately
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ValueTypes/When_parsing_an_id_from_string.cs`
  - **Test should verify**:
    - Valid Guid string parses correctly
    - Invalid string throws or returns empty
    - Null/empty string handling
    - ToString() returns expected format
  - **Implementation files**:
    - `src/Paramore.Brighter/Id.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_parsing_an_id_from_string"`
  - **References**: `src/Paramore.Brighter/Id.cs`

- [ ] **Id equality comparison**
  - **Behavior**: When comparing Ids for equality, same Ids should be equal with consistent hash codes
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ValueTypes/When_comparing_ids_for_equality.cs`
  - **Test should verify**:
    - Same Ids are equal
    - Different Ids are not equal
    - Id.Empty equals Id.Empty
    - Equality operators work (==, !=)
    - GetHashCode is consistent
  - **Implementation files**:
    - `src/Paramore.Brighter/Id.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_comparing_ids_for_equality"`
  - **References**: `src/Paramore.Brighter/Id.cs`

- [ ] **RoutingKey creation**
  - **Behavior**: When creating a RoutingKey with a valid string, the Value property should return the key correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ValueTypes/When_creating_a_routing_key.cs`
  - **Test should verify**:
    - RoutingKey can be created with valid string
    - Value property returns the key
    - ToString() returns expected format
    - Empty RoutingKey behavior
  - **Implementation files**:
    - `src/Paramore.Brighter/RoutingKey.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_a_routing_key"`
  - **References**: `src/Paramore.Brighter/RoutingKey.cs`

- [ ] **RoutingKey validation and equality**
  - **Behavior**: When validating and comparing RoutingKeys, valid keys should be accepted and equality should work correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ValueTypes/When_validating_a_routing_key.cs`
  - **Test should verify**:
    - Valid routing keys are accepted
    - Null/empty strings handled appropriately
    - Equality comparison works
    - GetHashCode is consistent
  - **Implementation files**:
    - `src/Paramore.Brighter/RoutingKey.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_validating_a_routing_key"`
  - **References**: `src/Paramore.Brighter/RoutingKey.cs`

- [ ] **PartitionKey creation and equality**
  - **Behavior**: When creating a PartitionKey with a valid string, properties and equality should work correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ValueTypes/When_creating_a_partition_key.cs`
  - **Test should verify**:
    - PartitionKey can be created with valid string
    - Value property returns the key
    - Empty PartitionKey behavior
    - Equality and GetHashCode work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/PartitionKey.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_a_partition_key"`
  - **References**: `src/Paramore.Brighter/PartitionKey.cs`

- [ ] **Subscription creation**
  - **Behavior**: When creating a Subscription with required parameters, all properties should be set correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Configuration/When_creating_a_subscription.cs`
  - **Test should verify**:
    - Subscription can be created with required parameters
    - DataType, ChannelName, RoutingKey are set
    - Default values are appropriate
    - NoOfPerformers, TimeOut, RequeueCount work
  - **Implementation files**:
    - `src/Paramore.Brighter/Subscription.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_a_subscription"`
  - **References**: `src/Paramore.Brighter/Subscription.cs`, `src/Paramore.Brighter/ChannelName.cs`, `src/Paramore.Brighter/RoutingKey.cs`

- [ ] **Publication creation**
  - **Behavior**: When creating a Publication with required parameters, Topic and RequestType should be set correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Configuration/When_creating_a_publication.cs`
  - **Test should verify**:
    - Publication can be created with required parameters
    - Topic property is set correctly
    - RequestType is set correctly
    - MakeChannels option works
  - **Implementation files**:
    - `src/Paramore.Brighter/Publication.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_a_publication"`
  - **References**: `src/Paramore.Brighter/Publication.cs`, `src/Paramore.Brighter/RoutingKey.cs`

- [ ] **Subscription options configuration**
  - **Behavior**: When configuring Subscription options like BufferSize and LockTimeout, they should be stored correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Configuration/When_configuring_subscription_options.cs`
  - **Test should verify**:
    - BufferSize can be configured
    - LockTimeout can be configured
    - UnacceptableMessageLimit works
    - RequeueDelayInMs works
  - **Implementation files**:
    - `src/Paramore.Brighter/Subscription.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_configuring_subscription_options"`
  - **References**: `src/Paramore.Brighter/Subscription.cs`

---

## Phase 2: Builders & Configuration (Priority 2 - High)

- [ ] **CommandProcessor building with defaults**
  - **Behavior**: When building a CommandProcessor with the builder, it should create a valid instance with default configuration
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/When_building_a_command_processor.cs`
  - **Test should verify**:
    - Builder creates valid CommandProcessor
    - Handlers are registered correctly
    - Default configuration is appropriate
  - **Implementation files**:
    - `src/Paramore.Brighter/CommandProcessorBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_building_a_command_processor"`
  - **References**: `src/Paramore.Brighter/CommandProcessorBuilder.cs`, `src/Paramore.Brighter/CommandProcessor.cs`

- [ ] **CommandProcessor building with inbox**
  - **Behavior**: When building a CommandProcessor with an Inbox, the inbox configuration should be applied correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/When_building_a_command_processor_with_inbox.cs`
  - **Test should verify**:
    - Inbox is configured correctly
    - InboxConfiguration options are respected
  - **Implementation files**:
    - `src/Paramore.Brighter/CommandProcessorBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_building_a_command_processor_with_inbox"`
  - **References**: `src/Paramore.Brighter/CommandProcessorBuilder.cs`, `src/Paramore.Brighter/InboxConfiguration.cs`

- [ ] **CommandProcessor building with outbox**
  - **Behavior**: When building a CommandProcessor with an Outbox, the outbox and producer registry should be configured correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/When_building_a_command_processor_with_outbox.cs`
  - **Test should verify**:
    - Outbox is configured correctly
    - OutboxConfiguration options are respected
    - Producer registry is set
  - **Implementation files**:
    - `src/Paramore.Brighter/CommandProcessorBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_building_a_command_processor_with_outbox"`
  - **References**: `src/Paramore.Brighter/CommandProcessorBuilder.cs`, `src/Paramore.Brighter/OutboxConfiguration.cs`

- [ ] **CommandProcessor building with policies**
  - **Behavior**: When building a CommandProcessor with resilience policies, the policies should be registered and accessible
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/When_building_a_command_processor_with_policies.cs`
  - **Test should verify**:
    - Resilience policies are registered
    - Policy names are accessible
    - Default policies work
  - **Implementation files**:
    - `src/Paramore.Brighter/CommandProcessorBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_building_a_command_processor_with_policies"`
  - **References**: `src/Paramore.Brighter/CommandProcessorBuilder.cs`

- [ ] **CommandProcessor building with invalid config**
  - **Behavior**: When building a CommandProcessor with invalid or missing configuration, appropriate exceptions should be thrown
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/When_building_a_command_processor_with_invalid_config.cs`
  - **Test should verify**:
    - Missing required components throw
    - Invalid configuration is rejected
    - Error messages are clear
  - **Implementation files**:
    - `src/Paramore.Brighter/CommandProcessorBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_building_a_command_processor_with_invalid_config"`
  - **References**: `src/Paramore.Brighter/CommandProcessorBuilder.cs`

- [ ] **CommandProcessor building with handlers**
  - **Behavior**: When building a CommandProcessor with handlers, the subscriber registry and handler factory should be configured
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/When_building_a_command_processor_with_handlers.cs`
  - **Test should verify**:
    - Subscriber registry is set
    - Handler factory is configured
    - Handlers can be resolved
  - **Implementation files**:
    - `src/Paramore.Brighter/CommandProcessorBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_building_a_command_processor_with_handlers"`
  - **References**: `src/Paramore.Brighter/CommandProcessorBuilder.cs`, `src/Paramore.Brighter/SubscriberRegistry.cs`

- [ ] **CommandProcessor building with mappers**
  - **Behavior**: When building a CommandProcessor with message mappers, the mapper registry should be configured
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/When_building_a_command_processor_with_mappers.cs`
  - **Test should verify**:
    - Message mapper registry is configured
    - Mappers can be resolved for request types
  - **Implementation files**:
    - `src/Paramore.Brighter/CommandProcessorBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_building_a_command_processor_with_mappers"`
  - **References**: `src/Paramore.Brighter/CommandProcessorBuilder.cs`, `src/Paramore.Brighter/MessageMapperRegistry.cs`

- [ ] **CommandProcessor building with transforms**
  - **Behavior**: When building a CommandProcessor with transforms, the transform registry and factory should be configured
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/CommandProcessors/Build/When_building_a_command_processor_with_transforms.cs`
  - **Test should verify**:
    - Transform registry is configured
    - Transform factory is set
  - **Implementation files**:
    - `src/Paramore.Brighter/CommandProcessorBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_building_a_command_processor_with_transforms"`
  - **References**: `src/Paramore.Brighter/CommandProcessorBuilder.cs`, `src/Paramore.Brighter/TransformerFactory.cs`

---

## Phase 3: Extension Methods (Priority 3 - Medium)

- [ ] **CharacterEncodingExtensions usage**
  - **Behavior**: When using CharacterEncodingExtensions methods, they should correctly encode and decode strings
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_using_character_encoding_extensions.cs`
  - **Test should verify**:
    - All methods in CharacterEncodingExtensions work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/CharacterEncodingExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_character_encoding_extensions"`
  - **References**: `src/Paramore.Brighter/Extensions/CharacterEncodingExtensions.cs`

- [ ] **DateTimeOffsetExtensions usage**
  - **Behavior**: When using DateTimeOffsetExtensions methods, they should correctly manipulate date/time values
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_using_datetime_offset_extensions.cs`
  - **Test should verify**:
    - All methods in DateTimeOffsetExtensions work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/DateTimeOffsetExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_datetime_offset_extensions"`
  - **References**: `src/Paramore.Brighter/Extensions/DateTimeOffsetExtensions.cs`

- [ ] **DictionaryExtensions usage**
  - **Behavior**: When using DictionaryExtensions methods, they should correctly manipulate dictionaries
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_using_dictionary_extensions.cs`
  - **Test should verify**:
    - All methods in DictionaryExtensions work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/DictionaryExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_dictionary_extensions"`
  - **References**: `src/Paramore.Brighter/Extensions/DictionaryExtensions.cs`

- [ ] **MethodInfoExtensions usage**
  - **Behavior**: When using MethodInfoExtensions methods, they should correctly inspect method information
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_using_method_info_extensions.cs`
  - **Test should verify**:
    - All methods in MethodInfoExtensions work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/MethodInfoExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_method_info_extensions"`
  - **References**: `src/Paramore.Brighter/Extensions/MethodInfoExtensions.cs`

- [ ] **ReflectionExtensions usage**
  - **Behavior**: When using ReflectionExtensions methods, they should correctly inspect types via reflection
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_using_reflection_extensions.cs`
  - **Test should verify**:
    - All methods in ReflectionExtensions work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/ReflectionExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_reflection_extensions"`
  - **References**: `src/Paramore.Brighter/Extensions/ReflectionExtensions.cs`

- [ ] **RequestContextExtensions usage**
  - **Behavior**: When using RequestContextExtensions methods, they should correctly manipulate request context
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_using_request_context_extensions.cs`
  - **Test should verify**:
    - All methods in RequestContextExtensions work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/RequestContextExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_request_context_extensions"`
  - **References**: `src/Paramore.Brighter/Extensions/RequestContextExtensions.cs`

- [ ] **TypeExtensions usage**
  - **Behavior**: When using TypeExtensions methods, they should correctly inspect and manipulate types
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_using_type_extensions.cs`
  - **Test should verify**:
    - All methods in TypeExtensions work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/TypeExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_type_extensions"`
  - **References**: `src/Paramore.Brighter/Extensions/TypeExtensions.cs`

- [ ] **ResiliencePipelineExtensions usage**
  - **Behavior**: When using ResiliencePipelineRegistryExtensions methods, they should correctly configure resilience pipelines
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_using_resilience_pipeline_extensions.cs`
  - **Test should verify**:
    - All methods in ResiliencePipelineRegistryExtensions work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/ResiliencePipelineRegistryExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_resilience_pipeline_extensions"`
  - **References**: `src/Paramore.Brighter/Extensions/ResiliencePipelineRegistryExtensions.cs`

- [ ] **String encoding edge cases**
  - **Behavior**: When using string encoding extensions with edge cases, they should handle them correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_extending_string_for_encoding.cs`
  - **Test should verify**:
    - Edge cases for string encoding methods are handled
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/CharacterEncodingExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_extending_string_for_encoding"`
  - **References**: `src/Paramore.Brighter/Extensions/CharacterEncodingExtensions.cs`

- [ ] **Dictionary merge operations**
  - **Behavior**: When using dictionary merge/combine extension methods, they should correctly merge dictionaries
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_extending_dictionaries_for_merge.cs`
  - **Test should verify**:
    - Dictionary merge/combine operations work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/DictionaryExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_extending_dictionaries_for_merge"`
  - **References**: `src/Paramore.Brighter/Extensions/DictionaryExtensions.cs`

---

## Phase 4: JSON Converters (Priority 3 - Medium)

- [ ] **Id serialization to JSON (System.Text.Json)**
  - **Behavior**: When serializing an Id to JSON using System.Text.Json, it should produce the correct JSON string
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_serializing_id_to_json.cs`
  - **Test should verify**:
    - IdConverter serializes Id to JSON correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/JsonConverters/IdConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_id_to_json"`
  - **References**: `src/Paramore.Brighter/JsonConverters/IdConverter.cs`, `src/Paramore.Brighter/Id.cs`

- [ ] **Id deserialization from JSON (System.Text.Json)**
  - **Behavior**: When deserializing an Id from JSON using System.Text.Json, it should produce the correct Id value
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_deserializing_id_from_json.cs`
  - **Test should verify**:
    - IdConverter deserializes JSON to Id correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/JsonConverters/IdConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_deserializing_id_from_json"`
  - **References**: `src/Paramore.Brighter/JsonConverters/IdConverter.cs`, `src/Paramore.Brighter/Id.cs`

- [ ] **RoutingKey serialization to JSON (System.Text.Json)**
  - **Behavior**: When serializing a RoutingKey to JSON using System.Text.Json, it should produce the correct JSON string
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_serializing_routing_key_to_json.cs`
  - **Test should verify**:
    - RoutingKeyConvertor serializes RoutingKey to JSON correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/JsonConverters/RoutingKeyConvertor.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_routing_key_to_json"`
  - **References**: `src/Paramore.Brighter/JsonConverters/RoutingKeyConvertor.cs`, `src/Paramore.Brighter/RoutingKey.cs`

- [ ] **RoutingKey deserialization from JSON (System.Text.Json)**
  - **Behavior**: When deserializing a RoutingKey from JSON using System.Text.Json, it should produce the correct RoutingKey value
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_deserializing_routing_key_from_json.cs`
  - **Test should verify**:
    - RoutingKeyConvertor deserializes JSON to RoutingKey correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/JsonConverters/RoutingKeyConvertor.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_deserializing_routing_key_from_json"`
  - **References**: `src/Paramore.Brighter/JsonConverters/RoutingKeyConvertor.cs`, `src/Paramore.Brighter/RoutingKey.cs`

- [ ] **SubscriptionName serialization to JSON (System.Text.Json)**
  - **Behavior**: When serializing a SubscriptionName to JSON using System.Text.Json, it should produce the correct JSON string
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_serializing_subscription_name_to_json.cs`
  - **Test should verify**:
    - SubscriptionNameConverter works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/JsonConverters/SubscriptionNameConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_subscription_name_to_json"`
  - **References**: `src/Paramore.Brighter/JsonConverters/SubscriptionNameConverter.cs`

- [ ] **TraceParent serialization to JSON (System.Text.Json)**
  - **Behavior**: When serializing a TraceParent to JSON using System.Text.Json, it should produce the correct JSON string
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_serializing_trace_parent_to_json.cs`
  - **Test should verify**:
    - TraceParentConverter works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/JsonConverters/TraceParentConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_trace_parent_to_json"`
  - **References**: `src/Paramore.Brighter/JsonConverters/TraceParentConverter.cs`

- [ ] **TraceState serialization to JSON (System.Text.Json)**
  - **Behavior**: When serializing a TraceState to JSON using System.Text.Json, it should produce the correct JSON string
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_serializing_trace_state_to_json.cs`
  - **Test should verify**:
    - TraceStateConverter works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/JsonConverters/TraceStateConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_trace_state_to_json"`
  - **References**: `src/Paramore.Brighter/JsonConverters/TraceStateConverter.cs`

- [ ] **Id serialization with Newtonsoft.Json**
  - **Behavior**: When serializing an Id using Newtonsoft.Json, it should produce the correct JSON string
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_serializing_with_newtonsoft_id_converter.cs`
  - **Test should verify**:
    - NJson IdConverter serializes correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/NJsonConverters/IdConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_with_newtonsoft_id_converter"`
  - **References**: `src/Paramore.Brighter/NJsonConverters/IdConverter.cs`, `src/Paramore.Brighter/Id.cs`

- [ ] **Id deserialization with Newtonsoft.Json**
  - **Behavior**: When deserializing an Id using Newtonsoft.Json, it should produce the correct Id value
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_deserializing_with_newtonsoft_id_converter.cs`
  - **Test should verify**:
    - NJson IdConverter deserializes correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/NJsonConverters/IdConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_deserializing_with_newtonsoft_id_converter"`
  - **References**: `src/Paramore.Brighter/NJsonConverters/IdConverter.cs`, `src/Paramore.Brighter/Id.cs`

- [ ] **RoutingKey serialization with Newtonsoft.Json**
  - **Behavior**: When serializing a RoutingKey using Newtonsoft.Json, it should produce the correct JSON string
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_serializing_with_newtonsoft_routing_key_converter.cs`
  - **Test should verify**:
    - NJson RoutingKeyConverter works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/NJsonConverters/RoutingKeyConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_with_newtonsoft_routing_key_converter"`
  - **References**: `src/Paramore.Brighter/NJsonConverters/RoutingKeyConverter.cs`, `src/Paramore.Brighter/RoutingKey.cs`

- [ ] **Null handling in JSON converters**
  - **Behavior**: When JSON converters encounter null values, they should handle them correctly without throwing
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_handling_null_in_json_converters.cs`
  - **Test should verify**:
    - Null handling works for all converters
  - **Implementation files**:
    - `src/Paramore.Brighter/JsonConverters/` - All converters
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_handling_null_in_json_converters"`
  - **References**: `src/Paramore.Brighter/JsonConverters/IdConverter.cs`, `src/Paramore.Brighter/JsonConverters/RoutingKeyConvertor.cs`

- [ ] **Invalid JSON handling in converters**
  - **Behavior**: When JSON converters encounter invalid JSON, they should throw appropriate exceptions
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_handling_invalid_json_in_converters.cs`
  - **Test should verify**:
    - Error handling for malformed input works
  - **Implementation files**:
    - `src/Paramore.Brighter/JsonConverters/` - All converters
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_handling_invalid_json_in_converters"`
  - **References**: `src/Paramore.Brighter/JsonConverters/IdConverter.cs`, `src/Paramore.Brighter/JsonConverters/RoutingKeyConvertor.cs`

---

## Phase 5: In-Memory Components (Priority 4 - Medium)

- [ ] **InMemoryArchiveProvider message archiving**
  - **Behavior**: When archiving a message with InMemoryArchiveProvider, it should be stored and retrievable
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Archive/When_archiving_a_message.cs`
  - **Test should verify**:
    - ArchiveMessage stores the message
    - Archived message can be retrieved
    - Archive metadata is correct
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryArchiveProvider.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_archiving_a_message"`
  - **References**: `src/Paramore.Brighter/InMemoryArchiveProvider.cs`

- [ ] **InMemoryArchiveProvider async archiving**
  - **Behavior**: When archiving a message asynchronously with InMemoryArchiveProvider, it should be stored correctly
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Archive/When_archiving_a_message_async.cs`
  - **Test should verify**:
    - Async version of archiving works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryArchiveProvider.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_archiving_a_message_async"`
  - **References**: `src/Paramore.Brighter/InMemoryArchiveProvider.cs`

- [ ] **InMemoryArchiveProvider batch archiving**
  - **Behavior**: When archiving multiple messages in a batch, all messages should be stored correctly
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Archive/When_archiving_multiple_messages.cs`
  - **Test should verify**:
    - ArchiveMessagesAsync handles batches
    - All messages are stored
    - Order is preserved (if applicable)
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryArchiveProvider.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_archiving_multiple_messages"`
  - **References**: `src/Paramore.Brighter/InMemoryArchiveProvider.cs`

- [ ] **InMemoryArchiveProvider retrieval**
  - **Behavior**: When retrieving archived messages, all previously archived messages should be returned
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Archive/When_retrieving_archived_messages.cs`
  - **Test should verify**:
    - ArchivedMessages property returns all archived
    - Empty archive returns empty collection
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryArchiveProvider.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_retrieving_archived_messages"`
  - **References**: `src/Paramore.Brighter/InMemoryArchiveProvider.cs`

- [ ] **InMemoryTransactionProvider getting transaction**
  - **Behavior**: When getting a transaction from InMemoryTransactionProvider, it should return a valid transaction
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Transaction/When_getting_a_transaction.cs`
  - **Test should verify**:
    - GetTransaction returns a transaction
    - GetTransactionAsync returns a transaction
    - HasOpenTransaction is true after getting
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryTransactionProvider.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_getting_a_transaction"`
  - **References**: `src/Paramore.Brighter/InMemoryTransactionProvider.cs`

- [ ] **InMemoryTransactionProvider committing**
  - **Behavior**: When committing a transaction with InMemoryTransactionProvider, HasOpenTransaction should become false
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Transaction/When_committing_a_transaction.cs`
  - **Test should verify**:
    - Commit completes successfully
    - CommitAsync completes successfully
    - HasOpenTransaction is false after commit
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryTransactionProvider.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_committing_a_transaction"`
  - **References**: `src/Paramore.Brighter/InMemoryTransactionProvider.cs`

- [ ] **InMemoryTransactionProvider rollback**
  - **Behavior**: When rolling back a transaction with InMemoryTransactionProvider, HasOpenTransaction should become false
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Transaction/When_rolling_back_a_transaction.cs`
  - **Test should verify**:
    - Rollback completes successfully
    - RollbackAsync completes successfully
    - HasOpenTransaction is false after rollback
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryTransactionProvider.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_rolling_back_a_transaction"`
  - **References**: `src/Paramore.Brighter/InMemoryTransactionProvider.cs`

- [ ] **InMemoryTransactionProvider state checking**
  - **Behavior**: When checking transaction state, HasOpenTransaction and IsSharedConnection should return correct values
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Transaction/When_checking_transaction_state.cs`
  - **Test should verify**:
    - HasOpenTransaction reflects correct state
    - IsSharedConnection returns expected value
    - Close works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryTransactionProvider.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_checking_transaction_state"`
  - **References**: `src/Paramore.Brighter/InMemoryTransactionProvider.cs`

- [ ] **InMemorySubscription creation**
  - **Behavior**: When creating an InMemorySubscription, all parameters should be stored correctly
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Subscription/When_creating_inmemory_subscription.cs`
  - **Test should verify**:
    - Subscription can be created with all parameters
    - Default values are appropriate
    - Generic version works
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemorySubscription.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_creating_inmemory_subscription"`
  - **References**: `src/Paramore.Brighter/InMemorySubscription.cs`

- [ ] **InMemorySubscription dead letter configuration**
  - **Behavior**: When configuring dead letter routing on an InMemorySubscription, the routing keys should be set correctly
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Subscription/When_configuring_subscription_dead_letter.cs`
  - **Test should verify**:
    - DeadLetterRoutingKey can be set
    - InvalidMessageRoutingKey can be set
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemorySubscription.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_configuring_subscription_dead_letter"`
  - **References**: `src/Paramore.Brighter/InMemorySubscription.cs`

- [ ] **InMemoryMessageProducer async sending**
  - **Behavior**: When sending a message asynchronously with InMemoryMessageProducer, it should be delivered correctly
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Producer/When_sending_message_async.cs`
  - **Test should verify**:
    - SendAsync works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryMessageProducer.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_sending_message_async"`
  - **References**: `src/Paramore.Brighter/InMemoryMessageProducer.cs`

- [ ] **InMemoryMessageProducer delayed sending**
  - **Behavior**: When sending a message with delay using InMemoryMessageProducer, the delay should be respected
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Producer/When_sending_message_with_delay.cs`
  - **Test should verify**:
    - SendWithDelay schedules correctly
    - SendWithDelayAsync works
    - Delay is respected
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryMessageProducer.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_sending_message_with_delay"`
  - **References**: `src/Paramore.Brighter/InMemoryMessageProducer.cs`

- [ ] **InMemoryMessageProducer batch sending**
  - **Behavior**: When sending a batch of messages with InMemoryMessageProducer, all messages should be delivered
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Producer/When_sending_batch_of_messages.cs`
  - **Test should verify**:
    - Batch send works
    - CreateBatchesAsync creates correct batches
    - All messages in batch are sent
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryMessageProducer.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_sending_batch_of_messages"`
  - **References**: `src/Paramore.Brighter/InMemoryMessageProducer.cs`

- [ ] **InMemoryMessageProducer event publication**
  - **Behavior**: When InMemoryMessageProducer publishes a message, the OnMessagePublished event should be raised
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Producer/When_producer_publishes_event.cs`
  - **Test should verify**:
    - OnMessagePublished event is raised
    - Event args contain correct message
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryMessageProducer.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_producer_publishes_event"`
  - **References**: `src/Paramore.Brighter/InMemoryMessageProducer.cs`

- [ ] **InMemoryMessageConsumer purging**
  - **Behavior**: When purging messages from InMemoryMessageConsumer, all messages should be removed
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Consumer/When_purging_messages_from_consumer.cs`
  - **Test should verify**:
    - Purge removes all messages
    - PurgeAsync removes all messages
    - Consumer is empty after purge
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryMessageConsumer.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_purging_messages_from_consumer"`
  - **References**: `src/Paramore.Brighter/InMemoryMessageConsumer.cs`

- [ ] **InMemoryMessageConsumer invalid message rejection**
  - **Behavior**: When rejecting a message as invalid, it should be routed to the InvalidMessageTopic
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Consumer/When_rejecting_to_invalid_message_channel.cs`
  - **Test should verify**:
    - Invalid messages route to InvalidMessageTopic
    - Rejection reason is Unacceptable
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryMessageConsumer.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_rejecting_to_invalid_message_channel"`
  - **References**: `src/Paramore.Brighter/InMemoryMessageConsumer.cs`

- [ ] **InMemoryChannelFactory async channel creation**
  - **Behavior**: When creating an async channel with InMemoryChannelFactory, it should return a valid channel
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Consumer/When_creating_async_channel.cs`
  - **Test should verify**:
    - CreateAsyncChannel creates valid channel
    - CreateAsyncChannelAsync works
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryChannelFactory.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_creating_async_channel"`
  - **References**: `src/Paramore.Brighter/InMemoryChannelFactory.cs`

- [ ] **InMemoryProducerFactory multiple publications**
  - **Behavior**: When creating a producer with multiple publications, each topic should have a producer
  - **Test file**: `tests/Paramore.Brighter.InMemory.Tests/Producer/When_creating_producer_with_multiple_publications.cs`
  - **Test should verify**:
    - Multiple publications are registered
    - Each topic has a producer
  - **Implementation files**:
    - `src/Paramore.Brighter/InMemoryProducerFactory.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.InMemory.Tests/ --filter "FullyQualifiedName~When_creating_producer_with_multiple_publications"`
  - **References**: `src/Paramore.Brighter/InMemoryProducerFactory.cs`

---

## Phase 6: DI Extensions (Priority 5 - Lower)

- [ ] **ServiceProviderMapperFactory singleton lifetime**
  - **Behavior**: When creating a mapper with singleton lifetime, the same instance should be returned on subsequent requests
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/MapperFactory/When_creating_mapper_with_singleton_lifetime.cs`
  - **Test should verify**:
    - Singleton behavior for mappers
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_creating_mapper_with_singleton_lifetime"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs`

- [ ] **ServiceProviderMapperFactory scoped lifetime**
  - **Behavior**: When creating a mapper with scoped lifetime, different instances should be returned for different scopes
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/MapperFactory/When_creating_mapper_with_scoped_lifetime.cs`
  - **Test should verify**:
    - Scoped behavior for mappers
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_creating_mapper_with_scoped_lifetime"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs`

- [ ] **ServiceProviderMapperFactory transient lifetime**
  - **Behavior**: When creating a mapper with transient lifetime, a new instance should be returned on each request
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/MapperFactory/When_creating_mapper_with_transient_lifetime.cs`
  - **Test should verify**:
    - Transient behavior for mappers
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_creating_mapper_with_transient_lifetime"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs`

- [ ] **ServiceProviderMapperFactory missing mapper handling**
  - **Behavior**: When requesting a mapper that is not registered, appropriate error handling should occur
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/MapperFactory/When_mapper_factory_handles_missing_mapper.cs`
  - **Test should verify**:
    - Error handling for unregistered mappers
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_mapper_factory_handles_missing_mapper"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceProviderMapperFactory.cs`

- [ ] **Manual message mapper registration**
  - **Behavior**: When registering a message mapper manually, it should be resolvable for the specified request type
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/RegistryBuilder/When_registering_message_mapper_manually.cs`
  - **Test should verify**:
    - Register<TRequest, TMapper>() works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/MessageMapperRegistryBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_registering_message_mapper_manually"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/MessageMapperRegistryBuilder.cs`

- [ ] **Manual async message mapper registration**
  - **Behavior**: When registering an async message mapper manually, it should be resolvable for the specified request type
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/RegistryBuilder/When_registering_async_message_mapper_manually.cs`
  - **Test should verify**:
    - RegisterAsync<TRequest, TMapper>() works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/MessageMapperRegistryBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_registering_async_message_mapper_manually"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/MessageMapperRegistryBuilder.cs`

- [ ] **Default message mapper setting**
  - **Behavior**: When setting a default message mapper, it should be used for request types without explicit mappings
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/RegistryBuilder/When_setting_default_message_mapper.cs`
  - **Test should verify**:
    - SetDefaultMessageMapper() works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/MessageMapperRegistryBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_setting_default_message_mapper"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/MessageMapperRegistryBuilder.cs`

- [ ] **Manual subscriber registration**
  - **Behavior**: When registering a subscriber manually, it should be in the subscriber registry
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/RegistryBuilder/When_registering_subscriber_manually.cs`
  - **Test should verify**:
    - Subscriber registry registration works
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/SubscriberRegistryBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_registering_subscriber_manually"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/SubscriberRegistryBuilder.cs`

- [ ] **Manual async subscriber registration**
  - **Behavior**: When registering an async subscriber manually, it should be in the subscriber registry
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/RegistryBuilder/When_registering_async_subscriber_manually.cs`
  - **Test should verify**:
    - Async subscriber registration works
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/SubscriberRegistryBuilder.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_registering_async_subscriber_manually"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/SubscriberRegistryBuilder.cs`

- [ ] **Scheduler extension usage**
  - **Behavior**: When using the UseScheduler extension, the scheduler should be registered correctly
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/Extensions/When_using_scheduler_extension.cs`
  - **Test should verify**:
    - UseScheduler<T>() works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_using_scheduler_extension"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`

- [ ] **Publication finder extension usage**
  - **Behavior**: When using the UsePublicationFinder extension, the finder should be registered correctly
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/Extensions/When_using_publication_finder_extension.cs`
  - **Test should verify**:
    - UsePublicationFinder<T>() works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_using_publication_finder_extension"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`

- [ ] **External luggage store extension usage**
  - **Behavior**: When using the UseExternalLuggageStore extension, the store should be registered correctly
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/Extensions/When_using_external_luggage_store_extension.cs`
  - **Test should verify**:
    - UseExternalLuggageStore<T>() overloads work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_using_external_luggage_store_extension"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`

- [ ] **JSON serialization configuration**
  - **Behavior**: When configuring JSON serialization, the options should be applied correctly
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/Extensions/When_configuring_json_serialisation.cs`
  - **Test should verify**:
    - ConfigureJsonSerialisation() works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_configuring_json_serialisation"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`

- [ ] **RPC configuration usage**
  - **Behavior**: When using the UseRpc configuration, RPC should be set up correctly
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/Extensions/When_using_rpc_configuration.cs`
  - **Test should verify**:
    - UseRpc configuration works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter.Extensions.DependencyInjection/UseRpc.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_using_rpc_configuration"`
  - **References**: `src/Paramore.Brighter.Extensions.DependencyInjection/UseRpc.cs`

- [ ] **ConsumersOptions configuration**
  - **Behavior**: When configuring ConsumersOptions, all options should be stored correctly
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/ServiceActivator/When_configuring_consumers_options.cs`
  - **Test should verify**:
    - ConsumersOptions configuration works
  - **Implementation files**:
    - `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ConsumersOptions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_configuring_consumers_options"`
  - **References**: `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ConsumersOptions.cs`

- [ ] **Consumers with func overload**
  - **Behavior**: When adding consumers with Func<IServiceProvider, ConsumersOptions> overload, it should be configured correctly
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/ServiceActivator/When_adding_consumers_with_func_overload.cs`
  - **Test should verify**:
    - Func<IServiceProvider, ConsumersOptions> overload works
  - **Implementation files**:
    - `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_adding_consumers_with_func_overload"`
  - **References**: `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`

- [ ] **Inbox configuration for consumers**
  - **Behavior**: When configuring an inbox for consumers, it should be applied correctly
  - **Test file**: `tests/Paramore.Brighter.Extensions.Tests/ServiceActivator/When_configuring_inbox_for_consumers.cs`
  - **Test should verify**:
    - Inbox configuration in ServiceActivator works
  - **Implementation files**:
    - `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Extensions.Tests/ --filter "FullyQualifiedName~When_configuring_inbox_for_consumers"`
  - **References**: `src/Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs`

---

## Phase 7: Observability & Miscellaneous (Priority 3 - Medium)

- [ ] **BrighterMetricsFromTracesProcessor metrics processing**
  - **Behavior**: When BrighterMetricsFromTracesProcessor processes traces, it should extract and record metrics
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/When_processing_metrics_from_traces.cs`
  - **Test should verify**:
    - BrighterMetricsFromTracesProcessor works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Observability/BrighterMetricsFromTracesProcessor.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_processing_metrics_from_traces"`
  - **References**: `src/Paramore.Brighter/Observability/BrighterMetricsFromTracesProcessor.cs`

- [ ] **TailSamplerProcessor usage**
  - **Behavior**: When using TailSamplerProcessor, it should correctly sample traces based on configuration
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Observability/Sampling/When_using_tail_sampler_processor.cs`
  - **Test should verify**:
    - TailSamplerProcessor works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Observability/TailSamplerProcessor.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_tail_sampler_processor"`
  - **References**: `src/Paramore.Brighter/Observability/TailSamplerProcessor.cs`

- [ ] **DbMeter metrics recording**
  - **Behavior**: When DbMeter records database metrics, they should be captured correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/When_recording_db_metrics.cs`
  - **Test should verify**:
    - DbMeter records metrics correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Observability/DbMeter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_recording_db_metrics"`
  - **References**: `src/Paramore.Brighter/Observability/DbMeter.cs`

- [ ] **MessagingMeter metrics recording**
  - **Behavior**: When MessagingMeter records messaging metrics, they should be captured correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Observability/Metrics/When_recording_messaging_metrics.cs`
  - **Test should verify**:
    - MessagingMeter records metrics correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Observability/MessagingMeter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_recording_messaging_metrics"`
  - **References**: `src/Paramore.Brighter/Observability/MessagingMeter.cs`

- [ ] **TextContextPropagator context propagation**
  - **Behavior**: When TextContextPropagator propagates context, it should correctly inject and extract context
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Observability/Propagation/When_propagating_text_context.cs`
  - **Test should verify**:
    - TextContextPropogator works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Observability/TextContextPropogator.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_propagating_text_context"`
  - **References**: `src/Paramore.Brighter/Observability/TextContextPropogator.cs`

- [ ] **OutboxSweeper basic sweeping**
  - **Behavior**: When OutboxSweeper sweeps outstanding messages, they should be dispatched
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Sweeper/When_sweeping_outstanding_messages.cs`
  - **Test should verify**:
    - Basic sweeper functionality works
  - **Implementation files**:
    - `src/Paramore.Brighter/OutboxSweeper.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_sweeping_outstanding_messages"`
  - **References**: `src/Paramore.Brighter/OutboxSweeper.cs`

- [ ] **OutboxSweeper batch size configuration**
  - **Behavior**: When OutboxSweeper is configured with a batch size, it should respect that limit
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Sweeper/When_sweeping_with_batch_size.cs`
  - **Test should verify**:
    - Batch size configuration is respected
  - **Implementation files**:
    - `src/Paramore.Brighter/OutboxSweeper.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_sweeping_with_batch_size"`
  - **References**: `src/Paramore.Brighter/OutboxSweeper.cs`

- [ ] **OutboxSweeper age threshold**
  - **Behavior**: When OutboxSweeper is configured with an age threshold, only messages older than the threshold should be swept
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Sweeper/When_sweeping_with_age_threshold.cs`
  - **Test should verify**:
    - Age-based message selection works
  - **Implementation files**:
    - `src/Paramore.Brighter/OutboxSweeper.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_sweeping_with_age_threshold"`
  - **References**: `src/Paramore.Brighter/OutboxSweeper.cs`

- [ ] **OutboxSweeper error handling**
  - **Behavior**: When OutboxSweeper encounters errors during sweeping, they should be handled gracefully
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Sweeper/When_sweeper_encounters_errors.cs`
  - **Test should verify**:
    - Error handling works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/OutboxSweeper.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_sweeper_encounters_errors"`
  - **References**: `src/Paramore.Brighter/OutboxSweeper.cs`

- [ ] **RequestContext creation**
  - **Behavior**: When creating a RequestContext, all properties should be initialized correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Context/When_creating_request_context.cs`
  - **Test should verify**:
    - RequestContext creation and properties work
  - **Implementation files**:
    - `src/Paramore.Brighter/RequestContext.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_request_context"`
  - **References**: `src/Paramore.Brighter/RequestContext.cs`

- [ ] **InternalBus additional scenarios**
  - **Behavior**: When using InternalBus in additional scenarios, it should work correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Bus/When_using_internal_bus.cs`
  - **Test should verify**:
    - InternalBus additional scenarios work
  - **Implementation files**:
    - `src/Paramore.Brighter/InternalBus.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_internal_bus"`
  - **References**: `src/Paramore.Brighter/InternalBus.cs`

- [ ] **ProducerRegistry creation**
  - **Behavior**: When creating a ProducerRegistry, producers should be registered and retrievable
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Producers/When_creating_producer_registry.cs`
  - **Test should verify**:
    - ProducerRegistry works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/ProducerRegistry.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_producer_registry"`
  - **References**: `src/Paramore.Brighter/ProducerRegistry.cs`

- [ ] **HandlerConfiguration usage**
  - **Behavior**: When using HandlerConfiguration, it should store and provide handler configuration correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Configuration/When_using_handler_configuration.cs`
  - **Test should verify**:
    - HandlerConfiguration works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/HandlerConfiguration.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_handler_configuration"`
  - **References**: `src/Paramore.Brighter/HandlerConfiguration.cs`

- [ ] **CombinedChannelFactory usage**
  - **Behavior**: When using CombinedChannelFactory, it should correctly combine multiple channel factories
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Channel/When_using_combined_channel_factory.cs`
  - **Test should verify**:
    - CombinedChannelFactory works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/CombinedChannelFactory.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_combined_channel_factory"`
  - **References**: `src/Paramore.Brighter/CombinedChannelFactory.cs`

- [ ] **CombinedProducerRegistryFactory usage**
  - **Behavior**: When using CombinedProducerRegistryFactory, it should correctly combine multiple producer registry factories
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Producers/When_using_combined_producer_registry_factory.cs`
  - **Test should verify**:
    - CombinedProducerRegistryFactory works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/CombinedProducerRegistryFactory.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_combined_producer_registry_factory"`
  - **References**: `src/Paramore.Brighter/CombinedProducerRegistryFactory.cs`

- [ ] **Channel creation**
  - **Behavior**: When creating a Channel, it should be properly initialized with the correct properties
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Channel/When_creating_channel.cs`
  - **Test should verify**:
    - Channel creation works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Channel.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_channel"`
  - **References**: `src/Paramore.Brighter/Channel.cs`

- [ ] **ChannelAsync creation**
  - **Behavior**: When creating a ChannelAsync, it should be properly initialized with the correct properties
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Channel/When_creating_channel_async.cs`
  - **Test should verify**:
    - ChannelAsync creation works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/ChannelAsync.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_creating_channel_async"`
  - **References**: `src/Paramore.Brighter/ChannelAsync.cs`

- [ ] **TransformLifetimeScope usage**
  - **Behavior**: When using TransformLifetimeScope, it should correctly manage transform lifetimes
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Transforms/When_using_transform_lifetime_scope.cs`
  - **Test should verify**:
    - TransformLifetimeScope works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/TransformLifetimeScope.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_transform_lifetime_scope"`
  - **References**: `src/Paramore.Brighter/TransformLifetimeScope.cs`

- [ ] **WrapPipeline and UnwrapPipeline usage**
  - **Behavior**: When using WrapPipeline and UnwrapPipeline, they should correctly wrap and unwrap messages
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Transforms/When_using_wrap_unwrap_pipelines.cs`
  - **Test should verify**:
    - WrapPipeline and UnwrapPipeline work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/WrapPipeline.cs` - Existing implementation to test
    - `src/Paramore.Brighter/UnwrapPipeline.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_wrap_unwrap_pipelines"`
  - **References**: `src/Paramore.Brighter/WrapPipeline.cs`, `src/Paramore.Brighter/UnwrapPipeline.cs`

---

## Phase 8: Additional 0% Coverage Classes (Priority 3 - Medium)

- [ ] **NChannelNameConverter Newtonsoft serialization**
  - **Behavior**: When serializing and deserializing ChannelName with Newtonsoft.Json, it should work correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/Newtonsoft/When_serializing_channel_name_with_newtonsoft.cs`
  - **Test should verify**:
    - NChannelNameConverter serialization and deserialization work
  - **Implementation files**:
    - `src/Paramore.Brighter/NJsonConverters/NChannelNameConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_channel_name_with_newtonsoft"`
  - **References**: `src/Paramore.Brighter/NJsonConverters/NChannelNameConverter.cs`, `src/Paramore.Brighter/ChannelName.cs`

- [ ] **NPartitionKeyConverter Newtonsoft serialization**
  - **Behavior**: When serializing and deserializing PartitionKey with Newtonsoft.Json, it should work correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/Newtonsoft/When_serializing_partition_key_with_newtonsoft.cs`
  - **Test should verify**:
    - NPartitionKeyConverter serialization and deserialization work
  - **Implementation files**:
    - `src/Paramore.Brighter/NJsonConverters/NPartitionKeyConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_partition_key_with_newtonsoft"`
  - **References**: `src/Paramore.Brighter/NJsonConverters/NPartitionKeyConverter.cs`, `src/Paramore.Brighter/PartitionKey.cs`

- [ ] **NSubscriptionNameConverter Newtonsoft serialization**
  - **Behavior**: When serializing and deserializing SubscriptionName with Newtonsoft.Json, it should work correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/Newtonsoft/When_serializing_subscription_name_with_newtonsoft.cs`
  - **Test should verify**:
    - NSubscriptionNameConverter serialization and deserialization work
  - **Implementation files**:
    - `src/Paramore.Brighter/NJsonConverters/NSubscriptionNameConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_subscription_name_with_newtonsoft"`
  - **References**: `src/Paramore.Brighter/NJsonConverters/NSubscriptionNameConverter.cs`

- [ ] **NTraceParentConverter Newtonsoft serialization**
  - **Behavior**: When serializing and deserializing TraceParent with Newtonsoft.Json, it should work correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/Newtonsoft/When_serializing_trace_parent_with_newtonsoft.cs`
  - **Test should verify**:
    - NTraceParentConverter serialization and deserialization work
  - **Implementation files**:
    - `src/Paramore.Brighter/NJsonConverters/NTraceParentConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_trace_parent_with_newtonsoft"`
  - **References**: `src/Paramore.Brighter/NJsonConverters/NTraceParentConverter.cs`

- [ ] **NTraceStateConverter Newtonsoft serialization**
  - **Behavior**: When serializing and deserializing TraceState with Newtonsoft.Json, it should work correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/Newtonsoft/When_serializing_trace_state_with_newtonsoft.cs`
  - **Test should verify**:
    - NTraceStateConverter serialization and deserialization work
  - **Implementation files**:
    - `src/Paramore.Brighter/NJsonConverters/NTraceStateConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_serializing_trace_state_with_newtonsoft"`
  - **References**: `src/Paramore.Brighter/NJsonConverters/NTraceStateConverter.cs`

- [ ] **TimeoutPolicyHandler timeout behavior**
  - **Behavior**: When TimeoutPolicyHandler encounters a timeout, it should cancel the operation and throw appropriately
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Policies/When_timeout_policy_handler_times_out.cs`
  - **Test should verify**:
    - TimeoutPolicyHandler cancels after timeout
    - CancellationToken is properly propagated
    - TimeoutException is thrown appropriately
  - **Implementation files**:
    - `src/Paramore.Brighter/Policies/Handlers/TimeoutPolicyHandler.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_timeout_policy_handler_times_out"`
  - **References**: `src/Paramore.Brighter/Policies/Handlers/TimeoutPolicyHandler.cs`

- [ ] **TimeoutPolicyHandler completion within timeout**
  - **Behavior**: When TimeoutPolicyHandler completes within the timeout, no exception should be thrown
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Policies/When_timeout_policy_handler_completes_within_timeout.cs`
  - **Test should verify**:
    - Handler completes normally when within timeout
    - No exception is thrown
  - **Implementation files**:
    - `src/Paramore.Brighter/Policies/Handlers/TimeoutPolicyHandler.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_timeout_policy_handler_completes_within_timeout"`
  - **References**: `src/Paramore.Brighter/Policies/Handlers/TimeoutPolicyHandler.cs`

- [ ] **TimeoutPolicyAttribute configuration**
  - **Behavior**: When TimeoutPolicyAttribute is configured, it should store timeout value and return correct handler type
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Policies/When_timeout_policy_attribute_is_configured.cs`
  - **Test should verify**:
    - Timeout value is set correctly
    - Step and order are configured
    - GetHandlerType returns TimeoutPolicyHandler
  - **Implementation files**:
    - `src/Paramore.Brighter/Policies/Attributes/TimeoutPolicyAttribute.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_timeout_policy_attribute_is_configured"`
  - **References**: `src/Paramore.Brighter/Policies/Attributes/TimeoutPolicyAttribute.cs`

- [ ] **RequestLoggingHandlerAsync logging**
  - **Behavior**: When RequestLoggingHandlerAsync handles a request, it should log before and after handling
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Logging/When_request_logging_handler_async_logs_request.cs`
  - **Test should verify**:
    - Request is logged before handling
    - Request is logged after handling
    - Log level is appropriate
  - **Implementation files**:
    - `src/Paramore.Brighter/Logging/Handlers/RequestLoggingHandlerAsync.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_request_logging_handler_async_logs_request"`
  - **References**: `src/Paramore.Brighter/Logging/Handlers/RequestLoggingHandlerAsync.cs`

- [ ] **RequestLoggingAsyncAttribute configuration**
  - **Behavior**: When RequestLoggingAsyncAttribute is configured, it should store timing mode and return correct handler type
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Logging/When_request_logging_async_attribute_is_configured.cs`
  - **Test should verify**:
    - Timing mode is set correctly
    - Step and order are configured
    - GetHandlerType returns RequestLoggingHandlerAsync
  - **Implementation files**:
    - `src/Paramore.Brighter/Logging/Attributes/RequestLoggingAsyncAttribute.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_request_logging_async_attribute_is_configured"`
  - **References**: `src/Paramore.Brighter/Logging/Attributes/RequestLoggingAsyncAttribute.cs`

- [ ] **NullLuggageStore no-op operations**
  - **Behavior**: When using NullLuggageStore, all operations should be no-ops that don't throw
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ClaimCheck/When_using_null_luggage_store.cs`
  - **Test should verify**:
    - Store returns null for retrieve
    - Store does not throw on store operations
    - All methods are no-ops
  - **Implementation files**:
    - `src/Paramore.Brighter/Transforms/Storage/NullLuggageStore.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_null_luggage_store"`
  - **References**: `src/Paramore.Brighter/Transforms/Storage/NullLuggageStore.cs`

- [ ] **NullOutboxArchiveProvider no-op operations**
  - **Behavior**: When using NullOutboxArchiveProvider, all operations should be no-ops that don't throw
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Archive/When_using_null_outbox_archive_provider.cs`
  - **Test should verify**:
    - Archive operations complete without error
    - Provider does not actually store messages
    - All methods are no-ops
  - **Implementation files**:
    - `src/Paramore.Brighter/NullOutboxArchiveProvider.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_using_null_outbox_archive_provider"`
  - **References**: `src/Paramore.Brighter/NullOutboxArchiveProvider.cs`

- [ ] **CloudEventsAttribute configuration**
  - **Behavior**: When CloudEventsAttribute is configured, it should store the configuration correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Transforms/Attributes/When_cloud_events_attribute_is_configured.cs`
  - **Test should verify**:
    - CloudEventsAttribute configuration works
  - **Implementation files**:
    - `src/Paramore.Brighter/Transforms/Attributes/CloudEventsAttribute.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_cloud_events_attribute_is_configured"`
  - **References**: `src/Paramore.Brighter/Transforms/Attributes/CloudEventsAttribute.cs`

- [ ] **CompressAttribute configuration**
  - **Behavior**: When CompressAttribute is configured, it should store compression method and threshold
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Transforms/Attributes/When_compress_attribute_is_configured.cs`
  - **Test should verify**:
    - CompressionMethod is set
    - Threshold is configurable
    - GetHandlerType returns correct transform
  - **Implementation files**:
    - `src/Paramore.Brighter/Transforms/Attributes/CompressAttribute.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_compress_attribute_is_configured"`
  - **References**: `src/Paramore.Brighter/Transforms/Attributes/CompressAttribute.cs`

- [ ] **DecompressAttribute configuration**
  - **Behavior**: When DecompressAttribute is configured, it should store the configuration correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Transforms/Attributes/When_decompress_attribute_is_configured.cs`
  - **Test should verify**:
    - DecompressAttribute configuration works
  - **Implementation files**:
    - `src/Paramore.Brighter/Transforms/Attributes/DecompressAttribute.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_decompress_attribute_is_configured"`
  - **References**: `src/Paramore.Brighter/Transforms/Attributes/DecompressAttribute.cs`

- [ ] **ReadFromCloudEventsAttribute configuration**
  - **Behavior**: When ReadFromCloudEventsAttribute is configured, it should store the configuration correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Transforms/Attributes/When_read_from_cloud_events_attribute_is_configured.cs`
  - **Test should verify**:
    - ReadFromCloudEventsAttribute configuration works
  - **Implementation files**:
    - `src/Paramore.Brighter/Transforms/Attributes/ReadFromCloudEventsAttribute.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_read_from_cloud_events_attribute_is_configured"`
  - **References**: `src/Paramore.Brighter/Transforms/Attributes/ReadFromCloudEventsAttribute.cs`

- [ ] **EmptyMessageTransform wrap and unwrap**
  - **Behavior**: When EmptyMessageTransform wraps or unwraps a message, it should return the message unchanged
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Transforms/When_empty_message_transform_wraps_message.cs`
  - **Test should verify**:
    - EmptyMessageTransform.Wrap returns message unchanged
    - EmptyMessageTransform.Unwrap returns message unchanged
  - **Implementation files**:
    - `src/Paramore.Brighter/Transforms/EmptyMessageTransform.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_empty_message_transform_wraps_message"`
  - **References**: `src/Paramore.Brighter/Transforms/EmptyMessageTransform.cs`

- [ ] **EmptyMessageTransformAsync wrap and unwrap**
  - **Behavior**: When EmptyMessageTransformAsync wraps or unwraps a message, it should return the message unchanged
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Transforms/When_empty_message_transform_async_wraps_message.cs`
  - **Test should verify**:
    - EmptyMessageTransformAsync.WrapAsync returns message unchanged
    - EmptyMessageTransformAsync.UnwrapAsync returns message unchanged
  - **Implementation files**:
    - `src/Paramore.Brighter/Transforms/EmptyMessageTransformAsync.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_empty_message_transform_async_wraps_message"`
  - **References**: `src/Paramore.Brighter/Transforms/EmptyMessageTransformAsync.cs`

- [ ] **RequestHandlers collection enumeration**
  - **Behavior**: When enumerating RequestHandlers<T>, it should return handlers in order
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Handlers/When_request_handlers_collection_is_enumerated.cs`
  - **Test should verify**:
    - RequestHandlers<T> can hold multiple handlers
    - Enumeration returns handlers in order
    - Count is accurate
  - **Implementation files**:
    - `src/Paramore.Brighter/RequestHandlers.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_request_handlers_collection_is_enumerated"`
  - **References**: `src/Paramore.Brighter/RequestHandlers.cs`

- [ ] **AsyncRequestHandlers collection enumeration**
  - **Behavior**: When enumerating AsyncRequestHandlers<T>, it should return handlers in order
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Handlers/When_async_request_handlers_collection_is_enumerated.cs`
  - **Test should verify**:
    - AsyncRequestHandlers<T> can hold multiple handlers
    - Enumeration returns handlers in order
    - Count is accurate
  - **Implementation files**:
    - `src/Paramore.Brighter/AsyncRequestHandlers.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_async_request_handlers_collection_is_enumerated"`
  - **References**: `src/Paramore.Brighter/AsyncRequestHandlers.cs`

- [ ] **ChannelNameConverter System.Text.Json conversion**
  - **Behavior**: When converting ChannelName with System.Text.Json, it should serialize and deserialize correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_channel_name_converter_converts.cs`
  - **Test should verify**:
    - ChannelNameConverter (System.Text.Json) works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/ChannelNameConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_channel_name_converter_converts"`
  - **References**: `src/Paramore.Brighter/ChannelNameConverter.cs`, `src/Paramore.Brighter/ChannelName.cs`

- [ ] **TraceStateConverter System.Text.Json conversion**
  - **Behavior**: When converting TraceState with System.Text.Json, it should serialize and deserialize correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/JsonConverters/When_trace_state_converter_converts.cs`
  - **Test should verify**:
    - TraceStateConverter (System.Text.Json) works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/JsonConverters/TraceStateConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_trace_state_converter_converts"`
  - **References**: `src/Paramore.Brighter/JsonConverters/TraceStateConverter.cs`

- [ ] **RoutingKeys collection usage**
  - **Behavior**: When using RoutingKeys collection, it should support adding, enumerating, and checking containment
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ValueTypes/When_routing_keys_collection_is_used.cs`
  - **Test should verify**:
    - RoutingKeys can hold multiple keys
    - Enumeration works correctly
    - Add/Contains operations work
  - **Implementation files**:
    - `src/Paramore.Brighter/RoutingKeys.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_routing_keys_collection_is_used"`
  - **References**: `src/Paramore.Brighter/RoutingKeys.cs`

- [ ] **MessageTelemetry span info extraction**
  - **Behavior**: When MessageTelemetry extracts span info, it should correctly parse telemetry data
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Observability/When_message_telemetry_extracts_span_info.cs`
  - **Test should verify**:
    - MessageTelemetry span extraction works
  - **Implementation files**:
    - `src/Paramore.Brighter/Observability/MessageTelemetry.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_message_telemetry_extracts_span_info"`
  - **References**: `src/Paramore.Brighter/Observability/MessageTelemetry.cs`

- [ ] **ClaimCheckSpanInfo creation**
  - **Behavior**: When creating ClaimCheckSpanInfo, it should store all properties correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ClaimCheck/When_claim_check_span_info_is_created.cs`
  - **Test should verify**:
    - ClaimCheckSpanInfo properties work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Observability/ClaimCheckSpanInfo.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_claim_check_span_info_is_created"`
  - **References**: `src/Paramore.Brighter/Observability/ClaimCheckSpanInfo.cs`

- [ ] **ProducersConfiguration setting**
  - **Behavior**: When setting ProducersConfiguration, all configuration options should be stored correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Configuration/When_producers_configuration_is_set.cs`
  - **Test should verify**:
    - ProducersConfiguration works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/ProducersConfiguration.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_producers_configuration_is_set"`
  - **References**: `src/Paramore.Brighter/ProducersConfiguration.cs`

- [ ] **PublicationTopicAttribute usage**
  - **Behavior**: When using PublicationTopicAttribute, it should store the topic correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Attributes/When_publication_topic_attribute_is_used.cs`
  - **Test should verify**:
    - PublicationTopicAttribute works correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/PublicationTopicAttribute.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_publication_topic_attribute_is_used"`
  - **References**: `src/Paramore.Brighter/PublicationTopicAttribute.cs`

---

## Phase 9: Low Coverage Improvements (Priority 4 - Lower)

- [ ] **ChannelName creation with valid string**
  - **Behavior**: When creating a ChannelName with a valid string, it should be stored correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ValueTypes/When_channel_name_is_created_with_valid_string.cs`
  - **Test should verify**:
    - Basic construction works
  - **Implementation files**:
    - `src/Paramore.Brighter/ChannelName.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_channel_name_is_created_with_valid_string"`
  - **References**: `src/Paramore.Brighter/ChannelName.cs`

- [ ] **ChannelName empty string handling**
  - **Behavior**: When creating a ChannelName with empty or null string, it should be handled correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ValueTypes/When_channel_name_handles_empty_string.cs`
  - **Test should verify**:
    - Empty/null handling works
  - **Implementation files**:
    - `src/Paramore.Brighter/ChannelName.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_channel_name_handles_empty_string"`
  - **References**: `src/Paramore.Brighter/ChannelName.cs`

- [ ] **ChannelName equality comparison**
  - **Behavior**: When comparing ChannelNames for equality, they should compare by value
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/ValueTypes/When_channel_names_are_compared_for_equality.cs`
  - **Test should verify**:
    - Equals, ==, !=, GetHashCode work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/ChannelName.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_channel_names_are_compared_for_equality"`
  - **References**: `src/Paramore.Brighter/ChannelName.cs`

- [ ] **TaskExtensions timeout handling**
  - **Behavior**: When using TaskExtensions with timeout, operations should timeout correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_task_extension_handles_timeout.cs`
  - **Test should verify**:
    - Timeout scenarios work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/TaskExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_task_extension_handles_timeout"`
  - **References**: `src/Paramore.Brighter/Extensions/TaskExtensions.cs`

- [ ] **TaskExtensions cancellation handling**
  - **Behavior**: When using TaskExtensions with cancellation, operations should cancel correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_task_extension_handles_cancellation.cs`
  - **Test should verify**:
    - Cancellation token handling works
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/TaskExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_task_extension_handles_cancellation"`
  - **References**: `src/Paramore.Brighter/Extensions/TaskExtensions.cs`

- [ ] **TaskExtensions exception handling**
  - **Behavior**: When using TaskExtensions with exceptions, they should be propagated correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_task_extension_handles_exceptions.cs`
  - **Test should verify**:
    - Exception propagation works
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/TaskExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_task_extension_handles_exceptions"`
  - **References**: `src/Paramore.Brighter/Extensions/TaskExtensions.cs`

- [ ] **BaggageConverter empty baggage handling**
  - **Behavior**: When BaggageConverter handles empty baggage, it should work correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Observability/When_baggage_converter_handles_empty_baggage.cs`
  - **Test should verify**:
    - Empty baggage handling works
  - **Implementation files**:
    - `src/Paramore.Brighter/Observability/BaggageConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_baggage_converter_handles_empty_baggage"`
  - **References**: `src/Paramore.Brighter/Observability/BaggageConverter.cs`

- [ ] **BaggageConverter special characters handling**
  - **Behavior**: When BaggageConverter handles special characters, they should be encoded/decoded correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Observability/When_baggage_converter_handles_special_characters.cs`
  - **Test should verify**:
    - Encoding/decoding of special characters works
  - **Implementation files**:
    - `src/Paramore.Brighter/Observability/BaggageConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_baggage_converter_handles_special_characters"`
  - **References**: `src/Paramore.Brighter/Observability/BaggageConverter.cs`

- [ ] **BaggageConverter multiple items handling**
  - **Behavior**: When BaggageConverter handles multiple baggage items, they should all be converted correctly
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Observability/When_baggage_converter_handles_multiple_items.cs`
  - **Test should verify**:
    - Multiple baggage items work correctly
  - **Implementation files**:
    - `src/Paramore.Brighter/Observability/BaggageConverter.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_baggage_converter_handles_multiple_items"`
  - **References**: `src/Paramore.Brighter/Observability/BaggageConverter.cs`

- [ ] **DbSystemExtensions known systems mapping**
  - **Behavior**: When DbSystemExtensions maps known database systems, it should return correct identifiers
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_db_system_extension_maps_known_systems.cs`
  - **Test should verify**:
    - All known database system mappings work
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/DbSystemExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_db_system_extension_maps_known_systems"`
  - **References**: `src/Paramore.Brighter/Extensions/DbSystemExtensions.cs`

- [ ] **DbSystemExtensions unknown system handling**
  - **Behavior**: When DbSystemExtensions encounters an unknown database system, it should handle it gracefully
  - **Test file**: `tests/Paramore.Brighter.Core.Tests/Extensions/When_db_system_extension_handles_unknown_system.cs`
  - **Test should verify**:
    - Fallback behavior for unknown systems works
  - **Implementation files**:
    - `src/Paramore.Brighter/Extensions/DbSystemExtensions.cs` - Existing implementation to test
  - **RALPH-VERIFY**: `dotnet test tests/Paramore.Brighter.Core.Tests/ --filter "FullyQualifiedName~When_db_system_extension_handles_unknown_system"`
  - **References**: `src/Paramore.Brighter/Extensions/DbSystemExtensions.cs`

---

## Summary

```
Ralph tasks generated: specs/0003-test-coverage-improvement/ralph-tasks.md
Total tasks: 142
Ready for: ./scripts/ralph.sh
```
