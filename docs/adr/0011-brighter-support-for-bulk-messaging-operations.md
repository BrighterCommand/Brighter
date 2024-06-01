# 11. Support for Bulk Messaging Operations 

Date: 2019-08-01

## Status

Proposed

## Context
   
To support Transactional Messaging the Command Processor uses an Outbox when producing a message via an external bus. This means that the CommandProcessor has two associated calls:

* A `DepositPost` call to translate the `IRequest` into a `Message` and store it in an `Outbox`
* A `ClearOutbox` call to retrieve the `Message` from the `Outbox` and dispatch it via a `Producer`

Within these two steps we perform a number of activities.

For `DepositPost` we:

* Lookup the `IAmAProducer` associated with a `Request`
* Translate an `IRequest` into the `Message`, executing the `IAmAMessageTransform` and `IAmAMessageMapper` pipeline
* Store the resulting `Message` in an `Outbox`

For `ClearOutbox` we:

* Retrieve the `Message` from the `Outbox`
* Lookup the `IAmAProducer` associated with a `Request`
* We then produce the `Message` via the `Producer`

For the sake of efficiency we chose to support passing an array of `IRequest` to `DepositPost`. We want to bulk write this set of messages to the Outbox, over writing each one individually. For this reason we would like to be able to batch up our writes to the `Outbox` in `DepositPost`

We always pass an array of `Message` identifiers to `ClearOutbox`. For efficiency we obtain the set of matching messages by id from the `Outbox`

A question arises from the relationship between the single `DepositPost` and the bulk `DepositPost`.

The bulk version of `DepositPost` requires two modifications to the deposit behavior:

* As it is an `IEnumerable<IRequest>` the collection passed to the bulk `DepositPost` does not bind a generic parameter of the derived type of `IRequest` that we can use to lookup the associated mapper/transform pipeline and producer.
* As it is a batch, we want to accumulate all the writes to the `Outbox` over making each one individually.

In addition though we would like to re-use the `Deposit Post` functionality as far as possible to avoid divergent code paths reducing modifiability such as when adding OTel or fixing bugs. 

In V9 our solution was as follows:

* Bucket any batch of `IRequest` by their type
* By batch late bind a `BulkMessageMap` and then run it through the collection
* Feed the resulting set of messages to a bulk version of the `Outbox` via `AddToOutbox`

The main issue with this approach is that the flow for `DepositPost` differs between bulk and single versions, which makes it more complex to modify. 

## Decision

It is worth noting the following:

* The `BulkMessageMap` is a bulk, iterative operation; it's main purpose is late-binding of the type from an `IEnumerable<IRequest>`
* Producing a `Message` via a `Producer` is not a bulk operation in Brighter

The only bulk operations we have in the flow are the read and write from the `Outbox`

* All reads from the `Outbox` are a bulk operation in V9, that iterate over the `Message` collection and pass it to the `Producer`

It is possible for us to late bind the bulk `DepositPost` to a single `DepositPost` call using reflection to invoke the method. This would resolve the binding issue. We can tag the method with an attribute to indicate that it may be called via late binding for documentation and to simplify the reflection code.

This leaves us with the main difference being the bulk write to the `Outbox`

We have two alternatives here:

* Always use a bulk `Add` method on the `Outbox`; as with `Clear` simply treat the first parameter as a collection of `IRequest`
* Provide a mechanism to indicate that the `DepositPost` should be considered as part of a batch.
                                                                                               
Whilst the first seems simpler, two complications arise. The first is the need to do late-binding, the second is the different semantics for a bulk operation in OTel (this might also push us to add a single `Clear` operation and call that repeatedly from a batch parent).

So in this case we intend to opt for the second option.

* In a bulk `DepositPost`, where we have a batch, begin a new batch via `StartBatch` that returns a `BatchId`
* Pass an optional `BatchId` to a late bound call to the single `IRequest` version of `DepositPost` (which provides late binding) 
* Where we have an optional `BatchId` within the individual `DepositPost` call `AddToBatch` on the `Outbox` instead of `Add`
* In the batch `DepositPost` call `EndBatch` to write the batch

Both `StartBatch` and `EndBatch` are `Outbox` code. As they will be shared by all `Outbox` types they need to be implemented in an abstract base class.

`EndBatch` may need to group the writes by `RoutingKey` as a batch endpoint might be called for multiple topics.

## Consequences
      
* We have a single version of `DepositPost` for the pipeline, but we can call it as part of a batch
* We should consider having a single message id `Clear` that does not create a batch span
