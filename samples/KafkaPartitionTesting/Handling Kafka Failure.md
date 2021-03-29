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

On MacOs Blockade is broken due to the version of greenlet it so you need to build it locally from the source as PRs exist which have updated it (though you will still get warnings)

### Usage
Although blockade offers its own docker compose-like syntax for configuring services in a network, its easier to just create the network with docker-compose directly, and then use blockade add to add the containers from that network into the blockade. You are likely to use the command line, or a script anyway, to run blockade partition and blockade join to move nodes in and out of a partition.

blockade add CONTAINER -- adds a container to blockade
blockade status -- lists all the containers in the blockade and their partition
blockade partition CONTAINERS -- adds containers to a partition
blockade join -- heal the partition

## Tests

### Cluster Down
Assume the cluster is not available
1: The Producer should exit after trying to send events to the topic
   -- If we are validating or creating the topic, this fails first
   -- -- It fails when we timeout on topic creation
   -- If we assume the topic exists 
   -- -- Then failed messages just sit in the Outbox
   -- -- The number of failures before we error should be part of the producer configuration


### Rolling Partitions
Assume I have a cluster with three nodes: 1,2,3

1. Assume that 2 partitions and can no longer talk to 1 and 3.
    * Any leader partitions on 2 need to move to 1 and 3
    * 3 no longer has an ISRs
1a: Now assume that 3 partitions and can no longer talk to 1.
    * Any leader partitions on 3 need to move to 1
    * 2 & 3 no longer has an ISRs
1c: Join the Partition

### Broker Death
Assume I have a cluster with three nodes: 1,2,3; assume I am connected to 3

1. 2 stops
    * Now cluster only has two nodes and leaders needed on 1, 3
2. 3 stops
    * Now cluster only has nodes on 1
3: Restart 2 and 3
    * Now reconnects and picks up messages

### Zookeeper Death
Assume I have a cluster with three nodes: 1,2,3; assume I am connected to 3

1. Kill Zookeeper
2: Force leader re-election with Zookeeper down




