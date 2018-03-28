Forked from: https://github.com/bijukunjummen/docker-rabbitmq-cluster
Changes:
  * Added support for HA-Proxy
  * Swiched to use of standard rmq containers
Used for testing partition scenarios using Blockade 
  * http://blockade.readthedocs.io/en/latest/guide.html#guide
  * Goal is not to test RMQ, but how we behave when RMQ cluster partitions or fails

