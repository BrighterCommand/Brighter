# Kafka Clustering and Failure
## Mirrored and local queues

1: A Kafka topic is divided into partitions. Each partition preserves ordering within the partition.
2: A partition is written to at least one node, but can have replicas on other nodes.
3: A partition has a leader node, and followers. All events are written to and consumed from the leader, other replicas are there for availability.
4: An in-sync replica (ISR) is either the leader or a follower that has fetched the most recent messages in the last 10s and sent a heartbeat within 6 seconds.
5: Kafka does not wait to confirm writes out of sync replicas.
6: A new leader is always from an ISR
7: Electing a leader requires connection to Zookeeper (unless unclean leader election is configured)
8: Kafka was designed as CA, to run in a data-centre. Zookeeper is designed as CP, it drops nodes that are partitioned. The combination of Zookeeper and Kafka is CP - the ISR pool only includes consistent nodes, we can't reach those that become inconsistent.

## Simulating partitions for testing the cluster
We are going to use Blockade, running against our docker containers, to simulate failures

blockade.readthedocs.io/en/latest

We need to install blockade, you may want to use a virtual env to do this

pip install blockade

You can confirm with 

blockade -h

On MacOs Blockade is broken due to the version of greenlet it

### Usage
Although blockade offers its own docker compose-like syntax for configuring services in a network, its easier to just create the network with docker-compose directly, and then use blockade add to add the containers from that network into the blockade. You are likely to use the command line, or a script anyway, to run blockade partition and blockade join to move nodes in and out of a partition.

## Tests

### Setup
Assume the cluster is not available
1: The Producer should exit after trying to send events to the topic
   -- The number of failures should be part of the producer configuration via the Polly policy
   

### Setup
Assume I have a cluster with three nodes: A,B, C
Assume that I have a stream, greeting event on A and 
Assume that I am not using a durable queue, so the queue is created on a node by a consumer, if it does not already exist.

1. Assume that A partitions and can no longer talk to B and C. 
    * I might assume that I could use an ignore strategy 
    * The issue here is that ignore allows a partition, so new connections sent to the B,C partition by my load balancer will create a new queue
    * I should use a pause minority strategy
    * Now I will need to switch nodes as my node had failed
    * This is safe.
2. Assume that A fails. Now I need to failover to another node in the cluster, B or C. 
    * Once I have failed over there is no queue though, so I must redeclare a queue to be resilient in this situation. 
    * Brighter supports this as it always ensures that exchanges and queues exist before using them, using EnsureConsumer and EnsureConnection.
    * This is not 'safe' as there may be lost messages where the publisher sent them and RMQ discarded them.
    * So to support this approach you have to be able to replay message sent during that window (Brighter has a message box for this).

