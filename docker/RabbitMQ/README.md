# Native delayed-message tests

From the repository root:

```sh
docker compose -f docker-compose-rmq-native-delay.yaml up --build -d --wait
dotnet test tests/Paramore.Brighter.RMQ.Async.Tests --filter 'Category=RMQNativeDelay'
dotnet test tests/Paramore.Brighter.RMQ.Sync.Tests --filter 'Category=RMQNativeDelay'
docker compose -f docker-compose-rmq-native-delay.yaml down -v
```

The dedicated broker listens on localhost:5673. Set `RMQ_NATIVE_DELAY_URI` to use another
compatible plugin-enabled broker. Existing scheduler tests use the stock broker on port 5672;
exclude `Category=RMQNativeDelay` when running only that infrastructure. CI has separate jobs
for the stock and native configurations.

The Dockerfile pins RabbitMQ 4.2.6 and delayed-message plugin 4.2.0-rc.1, verified by checksum.
The compose fixture disables Khepri on first boot because the plugin has a
[reported topic-routing limitation](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/discussions/327)
with Khepri. Use a fresh test volume when changing this setting. The upstream plugin is archived;
this fixture does not establish compatibility with RabbitMQ 4.3+.

The image removes any Erlang cookie generated during plugin setup. RabbitMQ creates its cookie
at first startup, owned by the runtime user, so a build-time root-owned cookie cannot prevent
startup as `rabbitmq`. This applies to fresh containers; it does not repair cookies in existing volumes.
The Compose fixture starts directly as `rabbitmq`, and CI checks that user and broker health before
running the native-delay tests. Startup cannot rely on a root entrypoint repairing cookie ownership.

If pulling `rabbitmq:4.2.6-management` fails with `unable to retrieve auth token: invalid username/password`,
refresh the saved Docker Hub credentials for your container engine, then retry the pull and Compose build.
This is a registry authentication failure, separate from the broker's cookie permissions.

With `Exchange.SupportDelay = true`, delayed retries publish to `<exchange-name>.requeue`, an
`x-delayed-message` exchange with `x-delayed-type=direct`, the original exchange's durability,
and `autoDelete=false`. Each consumer queue binds with its own queue name as the routing key.
This keeps retries on their original queue even when the original exchange uses topic or fanout routing.
Zero, negative, and omitted retry delays use the default exchange immediately.

`OnMissingChannel.Create` provisions this retry exchange and binding. For `Validate` or `Assume`,
provision both externally; `Validate` passively checks the exchange, and `Assume` skips declaration.
AMQP passive declaration cannot validate bindings, so operators must ensure the queue binding exists.
RabbitMQ consumers clear `Header.Delayed` on receipt and retain the raw `x-delay` value in `Header.Bag`.
