""""
File         : kombu_gateway_tests.py
Author           : ian
Created          : 09-01-2016

Last Modified By : ian
Last Modified On : 09-01-2016
***********************************************************************
The MIT License (MIT)
Copyright © 2016 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
**********************************************************************i*
"""

from typing import Dict
from uuid import uuid4
import logging
from datetime import datetime

from kombu import BrokerConnection, Consumer, Exchange, Producer as Producer, Queue
from kombu.pools import connections
from kombu import exceptions as kombu_exceptions
from kombu.message import Message as KombuMessage

from core.exceptions import ChannelFailureException
from core.messaging import BrightsideConsumer, BrightsideMessage, BrightsideProducer, BrightsideMessageHeader, BrightsideMessageBody, BrightsideMessageType
from arame.messaging import ArameMessageFactory, KombuMessageFactory


class ArameConnection:
    """Contains the details required to connect to a RMQ broker: the amqp uri and the exchange"""
    def __init__(self, amqp_uri: str, exchange: str, exchange_type: str = "direct", is_durable: bool = False) -> None:
        self._amqp_uri = amqp_uri
        self._exchange = exchange
        self._exchange_type = exchange_type
        self._is_durable = is_durable

    @property
    def amqp_uri(self) -> str:
        return self._amqp_uri

    @amqp_uri.setter
    def amqp_uri(self, value: str):
        self._amqp_uri = value

    @property
    def exchange(self) -> str:
        return self._exchange

    @exchange.setter
    def exchange(self, value: str):
        self._exchange = value

    @property
    def exchange_type(self) -> str:
        return self._exchange_type

    @exchange_type.setter
    def exchange_type(self, value: str):
        self._exchange_type = value

    @property
    def is_durable(self):
        return self._is_durable

    @is_durable.setter
    def is_durable(self, value):
        self._is_durable = value


class ArameProducer(BrightsideProducer):
    """Implements sending a message to a RMQ broker. It does not use a queue, just a connection to the broker
    """
    RETRY_OPTIONS = {
        'interval_start': 1,
        'interval_step': 1,
        'interval_max': 1,
        'max_retries': 3,
    }

    def __init__(self, connection: ArameConnection, logger:logging.Logger=None) -> None:
        self._amqp_uri = connection.amqp_uri
        self._cnx = BrokerConnection(hostname=connection.amqp_uri)
        self._exchange = Exchange(connection.exchange, type=connection.exchange_type, durable=connection.is_durable)
        self._logger = logger or logging.getLogger(__name__)

    def send(self, message: BrightsideMessage):
        # we want to expose our logger to the functions defined in inner scope, so put it in their outer scope

        logger = self._logger

        def _build_message_header(msg: BrightsideMessage) -> Dict:
            return KombuMessageFactory(msg).create_message_header()

        def _publish(sender: Producer) -> None:
            logger.debug("Send message {body} to broker {amqpuri} with routing key {routing_key}"
                         .format(body=message, amqpuri=self._amqp_uri, routing_key=message.header.topic))
            sender.publish(message.body.value,
                           headers=_build_message_header(message),
                           exchange=self._exchange,
                           serializer='json',   # todo: fix this for the mime type of the message
                           routing_key=message.header.topic,
                           declare=[self._exchange])

        def _error_callback(e, interval) -> None:
            logger.debug('Publishing error: {e}. Will retry in {interval} seconds', e, interval)

        self._logger.debug("Connect to broker {amqpuri}".format(amqpuri=self._amqp_uri))

        with connections[self._cnx].acquire(block=True) as conn:
            with Producer(conn) as producer:
                ensure_kwargs = self.RETRY_OPTIONS.copy()
                ensure_kwargs['errback'] = _error_callback
                safe_publish = conn.ensure(producer, _publish, **ensure_kwargs)
                safe_publish(producer)


class ArameConsumer(BrightsideConsumer):
    """ Implements reading a message from an RMQ broker. It uses a queue, created by subscribing to a message topic

    """
    RETRY_OPTIONS = {
        'interval_start': 1,
        'interval_step': 1,
        'interval_max': 1,
        'max_retries': 3,
    }

    def __init__(self, connection: ArameConnection, queue_name: str, routing_key: str, prefetch_count: int=1,
                 is_durable: bool=False, logger: logging.Logger=None) -> None:
        self._exchange = Exchange(connection.exchange, type=connection.exchange_type, durable=connection.is_durable)
        self._routing_key = routing_key
        self._amqp_uri = connection.amqp_uri
        self._queue_name = queue_name
        self._routing_key = routing_key
        self._prefetch_count = prefetch_count
        self._is_durable = is_durable
        self._message_factory = ArameMessageFactory()
        self._logger = logger or logging.getLogger(__name__)
        self._queue = Queue(self._queue_name, exchange=self._exchange, routing_key=self._routing_key)
        self._msg = None  # Kombu Message
        self._message = None # Brightside Message

        # TODO: Need to fix the argument types with default types issue

    def acknowledge(self, message: BrightsideMessage):
        if (self._message is not None) and self._message.id == message.id:
            self._msg.ack()
            self._msg = None

    def has_acknowledged(self, message):
        if (self._message is not None) and self._message.id == message.id:
            if self._msg is None:
                return True
            else:
                return False

    def purge(self, timeout: int = 5) -> None:

        def _purge_errors(exc, interval):
            self._logger.error('Purging error: %s, will retry triggering in %s seconds', exc, interval, exc_info=True)

        def _purge_messages(cnsmr: BrightsideConsumer):
            cnsmr.purge()
            self._message = None

        connection = BrokerConnection(hostname=self._amqp_uri)
        with connections[connection].acquire(block=True) as conn:
            self._logger.debug('Got connection: %s', conn.as_uri())
            with Consumer([self._queue], callbacks=[_purge_messages]) as consumer:
                ensure_kwargs = self.RETRY_OPTIONS.copy()
                ensure_kwargs['errback'] = _purge_errors
                safe_purge = conn.ensure(consumer, _purge_messages, **ensure_kwargs)
                safe_purge(consumer)

    def receive(self, timeout: int) -> BrightsideMessage:

        self._message = BrightsideMessage(BrightsideMessageHeader(uuid4(), "", BrightsideMessageType.none), BrightsideMessageBody(""))

        def _consume(cnx: BrokerConnection, timesup: int) -> None:
            try:
                cnx.drain_events(timeout=timesup)
            except kombu_exceptions.TimeoutError:
                pass
            except(kombu_exceptions.ChannelLimitExceeded,
                   kombu_exceptions.ConnectionLimitExceeded,
                   kombu_exceptions.OperationalError,
                   kombu_exceptions.NotBoundError,
                   kombu_exceptions.MessageStateError,
                   kombu_exceptions.LimitExceeded) as err:
                raise ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", err)

        def _consume_errors(exc, interval: int)-> None:
            self._logger.error('Draining error: %s, will retry triggering in %s seconds', exc, interval, exc_info=True)

        def _read_message(body: str, msg: KombuMessage) -> None:
            self._logger.debug("Monitoring event received at: %s headers: %s payload: %s", datetime.utcnow().isoformat(), msg.headers, body)
            self._msg = msg
            self._message = self._message_factory.create_message(msg)

        connection = BrokerConnection(hostname=self._amqp_uri)
        with connections[connection].acquire(block=True) as conn:
            self._logger.debug('Got connection: %s', conn.as_uri())
            with Consumer(conn, queues=[self._queue], callbacks=[_read_message]) as consumer:
                consumer.qos(prefetch_count=1)
                ensure_kwargs = self.RETRY_OPTIONS.copy()
                ensure_kwargs['errback'] = _consume_errors
                safe_drain = conn.ensure(consumer, _consume, **ensure_kwargs)
                safe_drain(conn, timeout)

        return self._message





