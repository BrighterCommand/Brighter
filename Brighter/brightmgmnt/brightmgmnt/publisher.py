"""
File         : controlbus.py
Author           : ian
Created          : 02-16-2015

Last Modified By : ian
Last Modified On : 02-16-2015
***********************************************************************
The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
***********************************************************************
"""

from kombu import BrokerConnection, Queue
from kombu.pools import connections
import logging


class Publisher:

    RETRY_OPTIONS = {
        'interval_start': 1,
        'interval_step': 1,
        'interval_max': 1,
        'max_retries': 3,
    }

    def __init__(self, destination, exchange, logger=None):
        self._amqp_uri = destination
        self._cnx = BrokerConnection(hostname=self._amqp_uri)
        self._exchange = exchange
        self._logger = logger or logging.getLogger(__name__)

    def send(self, header, message, routing_key):

        def _create_queue(routing_key, exchange):

            # We don't publish over a queue, so this is optional, it just creates a consuming channel which consumers
            # can read from. The advantage of declaring it now is that we ensure messages won't be lost if no consumers
            # are currently running.

            return Queue('paramore.brighter.controlbus', exchange=exchange, routing_key=routing_key)

        def _publish(sender, key):
            print("Send message {body} to broker {amqpuri} with routing key {routing_key}".format(body=message, amqpuri=self._amqp_uri, routing_key=key))
            queue = _create_queue(key, self._exchange)
            sender.publish(message, headers=header, exchange=self._exchange, serializer='json', routing_key=key, declare=[self._exchange, queue])

        def _error_callback(e, interval):
            print('Publishing error: {e}. Will retry in {interval} seconds', e, interval)

        print("Connect to broker {amqpuri}".format(amqpuri=self._amqp_uri))

        with connections[self._cnx].acquire(block=True) as conn:
            with conn.Producer() as producer:
                ensure_kwargs = self.RETRY_OPTIONS.copy()
                ensure_kwargs['errback'] = _error_callback
                safe_publish = conn.ensure(producer, _publish, **ensure_kwargs)
                safe_publish(producer, routing_key)






