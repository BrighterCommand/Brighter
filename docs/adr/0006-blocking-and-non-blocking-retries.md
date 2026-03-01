# 6 Blocking and Non-Blocking Retry

Date: 2024-01-01

## Status

Modified

## Context

The operation performed by a given handler may fail. We have a number of choices on failure:

* If the failure is transient, we can retry the operation. For example, if we cannot get a connection to the database,
  we can retry the operation after a short delay.
* If the failure is permanent, we can log the failure and move on. For example, if we cannot parse a message, we can log
  the failure and move on to the next message.
* If the failure is permanent, we can log the failure and stop. For example, if we cannot connect to the message queue,
  we can log the failure and stop processing messages.

For context, if the exception occurs in a mapper, before we reach the handler, we do not retry. We assume that it will
always fail. We ack the record and move on.
However, because the message might be valid but misconfigured for this channel, we set an unacceptable message limit.
When this threshold has passed we shut down the channel.

## Decision

Brighter has a number of options for handling failures:

* We can retry the operation.
  * Brighter offers both a blocking and a non-blocking retry.
  * We use Polly to provide a blocking retry via the UsePolicy[Async] attribute. Because we have a Russian doll mode pipeline, Polly can be used to provide a retry policy for specific exceptions thrown from the handler - those that are transient.
    * We can throw a DeferMessageAction from out handler for a non-blocking retry.
      * If the broker supports a re-queue operation (e.g. RabbitMQ), then message will be re-queued. (Note that Kafka does not support a re-queue as it is a stream).
        * We maintain a count of the number of times a message has been re-queued. If this exceeds a threshold, we shunt        the message to a dead-letter channel.
* If the failure is not transient, you should the failure and move on.
  * We consider this to be an application error.
    * A retry will never fix this error, and this simply becomes a poison-pill message, so we ack it to delete it from
      the queue and log the issue for intervention.
    * We do not support an error queue for failed messages; if you desire this you will need to post the message
      yourself to an error queue; you can write an attribute and pipeline handler for this,
      and throw a user defined exception to add to your error queue. N.B. This is only worth doing if logs are less
      valuable for diagnosis than an error queue.
    * (Note that [#0045](./0045-provide-dlq-where-missing.md) modifies this to allow you to post to the DLQ using a `RejectMessageAction`)
* If the failure comes from a badly configured CommandProcessor or ServiceActivator we throw a ConfigurationException
  * We expect you to fail fast in this case and resolve the error in the configuration of your app.

## Consequences

We support blocking and non-blocking retry for transient failures. We recommend logging for application errors that are
not transient.
