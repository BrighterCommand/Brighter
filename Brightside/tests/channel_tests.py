"""
File             : channel_tests.py
Author           : ian
Created          : 02-15-2016

Last Modified By : ian
Last Modified On : 02-15-2016
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
***********************************************************************
"""


import unittest
from uuid import uuid4

from core.messaging import BrightsideMessage, BrightsideMessageBody, BrightsideMessageHeader, BrightsideMessageType
from core.channels import Channel, ChannelState
from tests.channels_testdoubles import FakeConsumer


class ChannelFixture(unittest.TestCase):
    def test_handle_stop(self):
        """
        Given that I have a channel
        When I receive a stop on that channel
        Then I should not process any further messages on that channel
        :return:
        """

        body = BrightsideMessageBody("test message")
        header = BrightsideMessageHeader(uuid4(), "test topic", BrightsideMessageType.command)
        message = BrightsideMessage(header, body)

        consumer = FakeConsumer()
        consumer.queue.put(message)

        channel = Channel("test", consumer)

        channel.stop()

        channel.receive(1)

        self.assertFalse(consumer.queue.empty())  # Consumer is not empty as we have not read the queue
        self.assertTrue(channel.state == ChannelState.stopping)






