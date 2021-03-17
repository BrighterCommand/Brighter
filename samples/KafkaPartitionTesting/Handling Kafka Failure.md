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

### Usage
Although blockade offers its own docker compose-like syntax for configuring services in a network, its easier to just create the network with docker-compose directly, and then use blockade add to add the containers from that network into the blockade. You are likely to use the command line, or a script anyway, to run blockade partition and blockade join to move nodes in and out of a partition.

### Notes
It may sound obvious, but you need to plan your testing. What are the scenarios? Most importantly: identify what do you expect to happen and what actually happens. Plan how you get the system into the starting condition (for example you might need to keep restarting clients until they connect to the correct node). Ad-hoc is tempting. 'What happens if I partition the system?' But the reality is this often proves confusing. Was what happened expected? If not, what is the expected behaviour?

I spent a lot of time running a test, checking to see via the RMQ Management console what node I was connected to, and figuring out which scenario that would help with

So plan. 

### Point of Failure
The point of failure needs to be taken into account when determinig how our code should respond. Generally, we have four steps when we set up messaging with an RMQ broker

1. Establish a socket-based connection to the broker
2. Create a channel on that connection
3. Create our assets: exchange, queues, bindings via that channel
4. Publish to the channel or listen for messages on a queue

Failure might occur at any one of these points, and results in different errors being raised via the client, and different recover strategies.

Generally, as we connect to a node, the connection must be torn down, and channels recreated when we lose connectivity to a node through failure (or because it is paused via a partition).

If we multiplex channels across a shared connection, then all channels in use will be impacted by the failure of that connection, so when we tear down the connection because an operation fails on a channel, other channels will also cease to exist. So we cannot assume the presence of a channel before using a connection.

### Analysis

[Draw Diagram]

#### Non HA Queues

##### Setup
Assume I have a cluster with three nodes: A,B, C
Assume that I have a queue, orders, on A and it's is not mirrored, so not on B and C.
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

#### HA Queues

##### Setup
Assume I have a cluster with three nodes: A,B, C
Assume that I have a queue, orders, with the master on A and slaves on B and C.
We have chosen a strategy of Pause Minority on a partition

1. Assume I connect to Node A. I consume from the master which is on A. (I don't consume from the slave, that is there in case A fails). Then I get a partition and I cannot talk to A. 
    * RMQ will pause the partioned node
    * We will timeout on our connection
    * A new master will be elected on B or C
    * I need to stop talking to A and talk to B or C.
    * I should resume talking on B or C

    ###### Results

2. Assume I connect to Node A. I consume from the master which is on A. (I don't consume from the slave, that is there in case A fails). Then I get a partition and I cannot talk to B. I have chosen an Pause Minority strategy.
    * RMQ will pause the partioned node
    * We are not impacted by the partition and should continue

    ###### Results
  
3. Assume I connect to Node B. I consume from the master on A via Node B. (I don't consume from the slave, that is there in case A fails). Then B gets a partition and I cannot see A or C. I need to re-connect to A via A or C. I have chosen an Pause Minority strategy.
    * RMQ will pause the partioned node
    * We will timeout on our connection
    * I need to stop talking to B and talk to A or C.

    ###### Results


4. Assume I connect to Node B. I consume from the master on A via Node B. (I don't consume from the slave, that is there in case A fails). Then C gets a partition and cannot be seen. I have chosen an Pause Minority strategy.
    * RMQ will pause the partioned node
    * We are not impacted by the partition and should continue
    ###### Results


###### Setup
Assume I have a cluster with three nodes: A,B, C
Assume that I have a queue, orders, with the master on A and slaves on B and C.
We have chosen a strategy of Ignore on a partition



