# Task Queue Projects

This directory contains projects that provide simple examples of using Brighter with each of our transports. As such they are a good place to start if you are new to Brighter, being less complex than the full WebAPI examples. We often use them to confirm the behaviour of Brighter with a new transport.

## ASBTaskQueue

This project demonstrates how to use Brighter with Azure Service Bus as a task queue. It is a simple console application that sends a message to a queue and then receives it. 

## AWSTaskQueue

This project demonstrates how to use Brighter with AWS SQS as a task queue. It is a simple console application that sends a message to a queue and then receives it.

## KafkaTaskQueue

This project demonstrates how to use Brighter with Kafka as a task queue. It is a simple console application that sends a message to a topic and then receives it.

## MsSQLMessagingGateway

This project demonstrates how to use Brighter with MsSQL as a task queue. It is a simple console application that sends a message to a table and then receives it.

## RedisTaskQueue

This project demonstrates how to use Brighter with Redis as a task queue. It is a simple console application that sends a message to a list and then receives it.

## RMQTaskQueue

This project demonstrates how to use Brighter with RabbitMQ as a task queue. It is a simple console application that sends a message to a queue and then receives it.

## RMQRequestReply

A slightly more complex example, this demonstrates how to user Brighter if an RPC interaction style is desired, through use of its `Call` method. It is a simple console application that sends a message to a queue and then receives a reply.

