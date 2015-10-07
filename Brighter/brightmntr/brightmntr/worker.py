"""
File         : worker.py
Author           : ian
Created          : 06-20-2015

Last Modified By : ian
Last Modified On : 07-24-2015
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

from threading import Thread, Event
from kombu import BrokerConnection, Consumer, Queue
from kombu.pools import connections
from kombu import exceptions as kombu_exceptions
from datetime import datetime
from time import sleep

DRAIN_EVENTS_TIMEOUT = 1  #How long before we timeout Kombu trying to read events from the queue

class Worker(Thread):

    RETRY_OPTIONS = {
        'interval_start': 1,
        'interval_step': 1,
        'interval_max': 1,
        'max_retries': 3,
    }

    def __init__(self, amqp_uri, exchange, routing_key):
        self._exchange = exchange
        self._routing_key = routing_key
        self._amqp_uri = amqp_uri
        self.delay_between_refreshes = 5
        self.limit = -1  # configure the worker for times to run
        self.page_size = 5
        self._monitoring_queue = Queue('paramore.brighter.controlbus', exchange=self._exchange, routing_key=self._routing_key)
        self._running = Event()  # control access to te queue
        self._ensure_options = self.RETRY_OPTIONS.copy()

    def _read_monitoring_messages(self):

        def _drain(cnx, timeout):
            try:
                cnx.drain_events(timeout=timeout)
            except kombu_exceptions.TimeoutError:
                pass

        def _drain_errors(exc, interval):
            print('Draining error: %s', exc)
            print('Retry triggering in %s seconds', interval)

        def _read_message(body, message):
            print("Monitoring event received at: ", datetime.utcnow().isoformat(), " Event:  ", body)
            message.ack()

        # read the next batch number of monitoring messages from the control bus
        # evaluate for color coding (error is red)
        # print to stdout

        connection = BrokerConnection(hostname=self._amqp_uri)
        with connections[connection].acquire(block=True) as conn:
            print('Got connection: %r' % (conn.as_uri(), ))
            with Consumer(conn, [self._monitoring_queue], callbacks=[_read_message], accept=['json', 'text/plain']) as consumer:
                self._running.set()
                ensure_kwargs = self._ensure_options.copy()
                ensure_kwargs['errback'] = _drain_errors
                lines = 0
                while self._running.is_set():
                    # page size number before we sleep
                    safe_drain = conn.ensure(consumer, _drain, **ensure_kwargs)
                    safe_drain(conn, DRAIN_EVENTS_TIMEOUT)
                    lines += 1
                    if lines == self.page_size:
                        sleep(self.delay_between_refreshes)
                        lines = 0

    def run(self):

        self._read_monitoring_messages()

    def stop(self):

        self._running.clear()
