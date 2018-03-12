# RMQ Clustering and Failure
## Mirrored and local queues

1. A queue is local to a node, unless it is marked high-availability in which case it is mirrored across all nodes
  1. A non-mirrored queue can be recreated on a new node if the queue is non-durable i.e. the subscription dies with the node, so it is ok to recreate elsewhere
2. So although a mirrored queue is one strategy to cope with node failure, you could opt for non-mirrored, non durable.
  1. Constraints: You may get message loss whilst there is no subscribing queue i.e. node is down, new queue not created; in addition, you have to start consumers before producers
3. With a mirrored queue if you connect to a slave node, the channel actually publishes and consumers from the master node
  1. For this to work, the routing data, the Exchange, is replicated to all nodes in the cluster
4. Publisher Confirms: let your app know when a message has been passed from the master to all slave queues
  1. If a mirrored queue’s master fails before the message has been routed to the slave that will be become the new master, the publisher confirmation will never arrive and you’ll know that the message may have been lost.
5. If a mirrored queue loses a slave node, any consumers attached to the mirrored queue don’t notice the loss. That’s because technically they’re attached to the queue’s master copy.
6. But if the node hosting the master copy fails, all of the queue’s consumers need to reattach to start listening to the new queue master. 
   1. For consumers that were connected through the node that actually failed, this isn’t hard. Since they’ve lost their TCP connection to the node, they’ll automatically pick up the new queue master when they reattach to a new node in the cluster.
   2. For consumers that were attached to the mirrored queue through a node that didn’t fail, RabbitMQ will send those consumers a consumer cancellation notification telling them they’re no longer attached to the queue master
   3. The default basic consumer in the RMQ .NET Client will set the consumer to isrunning false at this point (and the derived queuing basic consumer will close the shared queue).
7. Rabbit can’t tell the difference between acknowledgements that were lost during the failover and messages that weren’t acknowledged at all. So to be safe, consumed but unacknowledged messages are requeued to their original positions in the queue
8. Transaction: ensures the published message has been routed to a queue, before continuing
9. Publisher Confirm: notify when the published message is delivered to all nodes
10. RAM node: reduce time to replicate exchange data to all nodes by putting some nodes in memory; must have one, should have two disk nodes at least.
  1. Mainly useful in RPC scenarios where private queues being regularly created and destroyed


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

### Analysis

[Draw Diagram]

#### Non HA Queues

##### Setup
Assume I have a cluster with three nodes: A,B, C
Assume that I have a queue, orders, on A and it's is not mirrored, so not on B and C.
Assume that I am not using a durable queue, so the queue is created on a node by a consumer, if it does not already exist.

###### Scenarios
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

###### Setup
Assume I have a cluster with three nodes: A,B, C
Assume that I have a queue, orders, with the master on A and slaves on B and C.

1. Assume I connect to Node B. I consume from the master on A via Node B. (I don't consume from the slave, that is there in case A fails). Then I get a partition and I cannot talk to A. I have chosen an Ignore strategy.
  * If we chose a strategy of Ignore, we cannot reach the master and so we will get a connection error. 
2. Assume I connect to Node A. I consume from the master on A. Then I get a partition and  A cannot talk to B and C. I have chosen an Ignore strategy. 
  * B and C will vote to elect a new master. Let us assume B wins. 
  * Now we have two masters. A which we are connected to and B which we are not.
  * If we chose a strategy of Ignore, we cannot reach the master and so we will get a connection error. 
3. Assume I connect to Node B. I consume from the master on A via Node B. (I don't consume from the slave, that is there in case A fails). Then I will get a connection error as I cannot talk to A. I need to stop talking to B and talk to A or C.  We have chosen a strategy of Pause Minority on a partition
  * If we choose Pause Minority, RMQ will stop the partioned node

.
