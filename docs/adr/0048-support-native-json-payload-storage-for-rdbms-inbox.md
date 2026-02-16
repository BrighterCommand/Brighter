# 48. Support Native JSON Payload Storage for RDBMS Inbox

## Status

Proposed

## Context

Brighter’s RDBMS Inbox pattern persists the message payload in a text/binary column.
This provides broad compatibility but prevents use of database-native JSON capabilities which allows for easier querying.

Several relational providers supported by Brighter offer native JSON column types:

PostgreSQL: JSON and JSONB

MySQL: JSON

Spanner: JSON

Storing JSON payloads in native JSON columns improves alignment with the payload’s structure and enables database JSON semantics.

## Decision
Extend the RDBMS Inbox pattern to allow the payload column to be stored as a native JSON type where supported.
                                              
A new flag, 'JsonMessagePayload' will be added to IAmARelationalDatabaseConfiguration, which when enabled will store message payloads as a native JSON type.
The existing 'BinaryMessagePayload' flag will be used to decide between JSON and JSONB where both are available.

Additional DDL statements will be added to supported DB inbox builders, and an additional parameter will be added to these GetDDL methods (defaulted to false).  

Default behaviour remains unchanged.
        

## Consequences

Positive
    1.Better querying (allow usage of JSON specific operators/functions) for inbox message payloads when stored as a native JSON type.
    2.Payloads are stored in a format that aligns with their JSON structure.
    3.Backward compatibility is preserved by default.


Negative
    1.Increasing complexity - extra DDL statements, and branching to allow for adding JSON SQL parameters.
    2.Users opting into native JSON payload storage would have to manage schema migrations explicitly if they have a pre-existing invoice table.
    3.JSON column types not available for all supported providers, introducing provider-specific behaviour.