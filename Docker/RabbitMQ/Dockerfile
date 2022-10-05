FROM rabbitmq:3-management

RUN apt-get update

RUN apt-get install -y curl && apt-get install -y zip

RUN curl -O https://dl.bintray.com/rabbitmq/community-plugins/3.8.x/rabbitmq_delayed_message_exchange/rabbitmq_delayed_message_exchange-20191008-3.8.x.zip\
&& unzip rabbitmq_delayed_message_exchange-20191008-3.8.x.zip -d $RABBITMQ_HOME/plugins \
&& rm rabbitmq_delayed_message_exchange-20191008-3.8.x.zip

RUN rabbitmq-plugins enable --offline rabbitmq_delayed_message_exchange

RUN rabbitmq-plugins enable --offline rabbitmq_consistent_hash_exchange