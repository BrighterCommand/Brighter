# Pinned to RabbitMQ 4.2.6 — the last 4.2.x patch tested against the
# delayed-message-exchange plugin. The plugin (Mnesia-based, archived
# upstream 2026-01-29) only ships up to v4.2.0-rc.1; no stable 4.2
# release exists. RabbitMQ 4.3+ removed Mnesia (Khepri-only) and is
# incompatible with this plugin.
FROM rabbitmq:4.2.6-management

ARG DELAYED_PLUGIN_VERSION=4.2.0-rc.1
ARG DELAYED_PLUGIN_SHA256=cdd6b59c3cac917ff51d81c62f968903efd62a41686dd3fd792dc0d5df453688

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && curl -fsSL -o "$RABBITMQ_HOME/plugins/rabbitmq_delayed_message_exchange-${DELAYED_PLUGIN_VERSION}.ez" \
      "https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/download/v${DELAYED_PLUGIN_VERSION}/rabbitmq_delayed_message_exchange-${DELAYED_PLUGIN_VERSION}.ez" \
 && echo "${DELAYED_PLUGIN_SHA256}  $RABBITMQ_HOME/plugins/rabbitmq_delayed_message_exchange-${DELAYED_PLUGIN_VERSION}.ez" | sha256sum -c - \
 && rabbitmq-plugins enable --offline rabbitmq_delayed_message_exchange rabbitmq_consistent_hash_exchange \
 && apt-get purge -y --auto-remove curl \
 && rm -rf /var/lib/apt/lists/*
