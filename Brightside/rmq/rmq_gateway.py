#!/usr/bin/env python
""""
File         : rmq_gateway_tests.py
Author           : ian
Created          : 09-01-2016

Last Modified By : ian
Last Modified On : 09-01-2016
***********************************************************************
The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

from kombu import BrokerConnection, Queue
from kombu.pools import connections
import logging
from core.messaging import Consumer, Message, Producer


class RmqConnection:
    """Contains the details required to connect to a RMQ broker: the amqp uri and the exchange"""
    def __init__(self, amqp_uri: str, exchange: str) -> None:
        self._amqp_uri = amqp_uri
        self._exchange = exchange

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


class RmqProducer(Producer):
    """Implements sending a message to a RMQ broker. It does not use a queue, just a connection to the broker
    """
    def __init__(self, connection: RmqConnection, logger=None):
        self._amqp_uri = connection.amqp_uri
        self._cnx = BrokerConnection(hostname=connection.amqp_uri)
        self._exchange = connection.exchange
        self._logger = logger or logging.getLogger(__name__)

    def send(self, message: Message):
        # we want to expose our logger to the functions defined in inner scope, so put it in their outer scope

        logger = self._logger

        def _build_message_header():
            return {'MessageType': message.header.message_type}

        def _publish(sender):
            logger.debug("Send message {body} to broker {amqpuri} with routing key {routing_key}"
                         .format(body=message, amqpuri=self._amqp_uri, routing_key=message.header.topic))
            sender.publish(message.body.value,
                           headers=_build_message_header(),
                           exchange=self._exchange,
                           serializer='json',   # todo: fix this for the mime type of the message
                           routing_key=message.header.topic,
                           declare=[self._exchange, queue])

        def _error_callback(e, interval):
            logger.debug('Publishing error: {e}. Will retry in {interval} seconds', e, interval)

        self._logger.debug("Connect to broker {amqpuri}".format(amqpuri=self._amqp_uri))

        with connections[self._cnx].acquire(block=True) as conn:
            with conn.Producer() as producer:
                ensure_kwargs = self.RETRY_OPTIONS.copy()
                ensure_kwargs['errback'] = _error_callback
                safe_publish = conn.ensure(producer, _publish, **ensure_kwargs)
                safe_publish(producer)


class RmqConsumer(Consumer):
    """ Implements reading a message from an RMQ broker. It uses a queue, created by subscribing to a message topic

    """
    def purge(self, ):
        pass

    def receive(self, timeout:int) -> Message:
        pass

