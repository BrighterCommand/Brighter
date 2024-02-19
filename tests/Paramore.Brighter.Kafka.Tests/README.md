# Kafka Test Dependencies

## Running the docker-based tests

Tests which talk to a Docker hosted Kafka instance assume the following:

* No security
* A Kafka instance running on `localhost:9092`
* A Zookeeper instance running on `localhost:2181`
* A schema registry running on `localhost:8081`


## Running the confluent-based tests

Tests which talk to a Confluent hosted Kafka instance have an attribute trait of "Confluent". They assume the following:

* You have set environment variables for the following:
  * CONFLUENT_BOOSTRAP_SERVER - the Kafka instance to connect to
  * CONFLUENT_SASL_USERNAME - the username to use for SASL
  * CONFLUENT_SASL_PASSWORD - the password to use for SASL

  
