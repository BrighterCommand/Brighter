#!/usr/bin/env python
""""
File             : arame_gateway_tests.py
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
**********************************************************************i*s
"""

import unittest
from uuid import uuid4

from tests.messaging_testdoubles import TestMessage
from arame.gateway import ArameConsumer, ArameConnection, ArameProducer
from arame.messaging import JsonRequestSerializer
from core.messaging import BrightsideMessage, BrightsideMessageBody, BrightsideMessageBodyType, BrightsideMessageHeader, BrightsideMessageType


class ArameGatewayTests(unittest.TestCase):

    test_topic = "kombu_gateway_tests"

    def setUp(self):
        self._connection = ArameConnection("amqp://guest:guest@localhost:5672//", "paramore.brighter.exchange", is_durable=True)
        self._producer = ArameProducer(self._connection)
        self._consumer = ArameConsumer(self._connection, "brightside_tests", self.test_topic)

    def test_posting_a_message(self):
        """Given that I have an RMQ message producer
            when I send that message via the producer
            then I should be able to read that message via the consumer
        """
        header = BrightsideMessageHeader(uuid4(), self.test_topic, BrightsideMessageType.command)
        body = BrightsideMessageBody("test content")
        message = BrightsideMessage(header, body)

        self._consumer.purge()

        self._producer.send(message)

        read_message = self._consumer.receive(3)
        self._consumer.acknowledge(read_message)

        self.assertEqual(message.id, read_message.id)
        self.assertEqual(message.body.value, read_message.body.value)
        self.assertTrue(self._consumer.has_acknowledged(read_message))

    def test_posting_object_state(self):
        """Given that I have an RMQ producer
            when I deserialize an object via the producer
            then I should be able to re-hydrate it via the consumer
        """
        request = TestMessage()
        header = BrightsideMessageHeader(uuid4(), self.test_topic, BrightsideMessageType.command)
        body = BrightsideMessageBody(JsonRequestSerializer(request=request).serialize_to_json(), BrightsideMessageBodyType.application_json)
        message = BrightsideMessage(header, body)

        self._consumer.purge()

        self._producer.send(message)

        read_message = self._consumer.receive(3)
        self._consumer.acknowledge(read_message)

        deserialized_request = JsonRequestSerializer(request=TestMessage(), serialized_request=read_message.body.value)\
            .deserialize_from_json()

        self.assertIsNotNone(deserialized_request)
        self.assertEqual(request.bool_value, deserialized_request.bool_value)
        self.assertEqual(request.float_value, deserialized_request.float_value)
        self.assertEqual(request.integer_value, deserialized_request.integer_value)
        self.assertEqual(request.id, deserialized_request.id)


