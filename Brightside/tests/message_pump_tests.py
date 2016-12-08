""""
File             : message_pump.py
Author           : ian
Created          : 11-23-2016

Last Modified By : ian
Last Modified On : 11-23-2016
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
***********************************************************************
"""

import unittest

from tests.handlers_testdoubles import MyCommandHandler, MyCommand
from core.command_processor import CommandProcessor
from core.registry import Registry
from serviceactivator.message_pump import MessagePump


class MessagePumpFixture(unittest.TestCase):
    def setUp(self):
        self._subscriber_registry = Registry()
        self._commandProcessor = CommandProcessor(registry=self._subscriber_registry)

    def test_the_pump_should_dispatch_to_a_handler(self):
        """
            Given that I have a message pump for a channel
             When I read a message from that channel
             Then the message should be dispatched to a handler
        """
        self._handler = MyCommandHandler()
        self._request = MyCommand()
        self._subscriber_registry.register(MyCommand, lambda: self._handler)

        message_pump = MessagePump(self._commandProcessor)
        message_pump.run()

