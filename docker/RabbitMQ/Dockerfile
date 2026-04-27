FROM rabbitmq:4.2-management

RUN apt-get update

RUN apt-get install -y curl && apt-get install -y zip

RUN curl -LO https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/download/v4.2.0-rc.1/rabbitmq_delayed_message_exchange-4.2.0-rc.1.ez --output-dir $RABBITMQ_HOME/plugins

RUN rabbitmq-plugins enable --offline rabbitmq_delayed_message_exchange

RUN rabbitmq-plugins enable --offline rabbitmq_consistent_hash_exchange