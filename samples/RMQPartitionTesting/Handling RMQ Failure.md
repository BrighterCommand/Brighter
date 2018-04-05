# RMQ Clustering and Failure
## Mirrored and local queues

1. A queue is local to a node, unless it is marked high-availability in which case it is mirrored across all nodes
    * A non-mirrored queue can be recreated on a new node if the queue is non-durable i.e. the subscription dies with the node, so it is ok to recreate elsewhere
2. So although a mirrored queue is one strategy to cope with node failure, you could opt for non-mirrored, non durable.
    * Constraints: You may get message loss whilst there is no subscribing queue i.e. node is down, new queue not created; in addition, you have to start consumers before producers
3. With a mirrored queue if you connect to a slave node, the channel actually publishes and consumers from the master node
    * For this to work, the routing data, the Exchange, is replicated to all nodes in the cluster
4. Publisher Confirms: let your app know when a message has been passed from the master to all slave queues
    * If a mirrored queue’s master fails before the message has been routed to the slave that will be become the new master, the publisher confirmation will never arrive and you’ll know that the message may have been lost.
5. If a mirrored queue loses a slave node, any consumers attached to the mirrored queue don’t notice the loss. That’s because technically they’re attached to the queue’s master copy.
6. But if the node hosting the master copy fails, all of the queue’s consumers need to reattach to start listening to the new queue master. 
    * For consumers that were connected through the node that actually failed, this isn’t hard. Since they’ve lost their TCP connection to the node, they’ll automatically pick up the new queue master when they reattach to a new node in the cluster.
    * For consumers that were attached to the mirrored queue through a node that didn’t fail, RabbitMQ will send those consumers a consumer cancellation notification telling them they’re no longer attached to the queue master
    * The default basic consumer in the RMQ .NET Client will set the consumer to isrunning false at this point (and the derived queuing basic consumer will close the shared queue).
7. Rabbit can’t tell the difference between acknowledgements that were lost during the failover and messages that weren’t acknowledged at all. So to be safe, consumed but unacknowledged messages are requeued to their original positions in the queue
8. Transaction: ensures the published message has been routed to a queue, before continuing
9. Publisher Confirm: notify when the published message is delivered to all nodes
10. RAM node: reduce time to replicate exchange data to all nodes by putting some nodes in memory; must have one, should have two disk nodes at least.
    * Mainly useful in RPC scenarios where private queues being regularly created and destroyed


## Clustering across Multiple AZs
https://techblog.bozho.net/rabbitmq-in-multiple-aws-availability-zones/

## Simulating partitions for testing the cluster
We are going to use Blockade, running against our docker containers, to simulate failures

blockade.readthedocs.io/en/latest

Under Windows, we need to install this into docker toolbox. Before we can do that we need to install the Build Tools for VS 2017, so that we can build python tools. 

wiki.python.org/main/WindowsCompilers

With that we should be able to run the Docker Toolbox console and run

pip install blockade to get blockade running

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
    * Consumer is Idle
      * We get a System.Timeout exception that the socket has timed out.
         * In this case from EnsureConsumer->EnsureChannelBind
         * We should terminate the connection and try to create a new one.
         * But we seem to pause - the Timeout should have become ChannelFailure, we should wait and retry!!

4. Assume I connect to Node B. I consume from the master on A via Node B. (I don't consume from the slave, that is there in case A fails). Then C gets a partition and cannot be seen. I have chosen an Pause Minority strategy.
    * RMQ will pause the partioned node
    * We are not impacted by the partition and should continue
    ###### Results
    * Consumer is Idle
      * RMQ Mangement Console reports that queue only now has one mirror; connection remains live
      * When partition ends, node rejoins as slave
    * Consumer is busy
      * RMQ Mangement Console reports that queue only now has one mirror; connection remains live; messages are processed
      * When partition ends, node rejoins as slave but is unsychronized.
        * RMQ advice is to prefer 'eventual synchronization' as messages are drained that node has not seen, see https://www.rabbitmq.com/ha.html



###### Setup
Assume I have a cluster with three nodes: A,B, C
Assume that I have a queue, orders, with the master on A and slaves on B and C.
We have chosen a strategy of Ignore on a partition



