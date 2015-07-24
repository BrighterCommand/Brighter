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

from threading import Thread
from kombu import BrokerConnection, Queue
from kombu.pools import producers


class Worker(Thread):

    def __init__(self, exchange, amqp_uri):
        self.exchange = exchange, self.amqp_uri = amqp_uri

    @staticmethod
    def _read_monitoring_messages(self):

        # read the next batch num0ber of monitoring messages from the control bus
        # evaluate for color coding (error is red)
        # print to stdout

        monitoring_queue = Queue('paramore.brighter.controlbus', exchange=exchange, routing_key=routing_key)

        connection = BrokerConnection(hostname=destination)

        with producers[connection].acquire(block=True) as producer:
            print("Send message to broker {amqpuri} with routing key {routing_key}".format(amqpuri=destination, routing_key=routing_key))

    def run(self):
        self._read_monitoring_messages(self)
        return
