"""
File         : test_request_handler.py
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

from tests.handlers_testdoubles import MyCommandHandler, MyCommand, MyEventHandler, MyEvent
from core.command_processor import CommandProcessor
from core.registry import Registry


class CommandProcessorFixture(unittest.TestCase):

    """ Command Processor tests """

    def setUp(self):
        self._subscriber_registry = Registry()
        self._commandProcessor = CommandProcessor(registry=self._subscriber_registry)

    def test_handle_command(self):
        """ given that we have a handler registered for a command, when we send a command, it should call the handler"""
        self._handler = MyCommandHandler()
        self._request = MyCommand()
        self._subscriber_registry.register(MyCommand, lambda: self._handler)
        self._commandProcessor.send(self._request)

        self.assertTrue(self._handler.called, "Expected the handle method on the handler to be called with the message")

    def test_handle_event(self):
        """ Given that we have many handlers registered of an event, when we raise an event, it should call all the handlers"""

        self._handler = MyEventHandler()
        self._other_handler = MyEventHandler()
        self._request = MyEvent()
        self._subscriber_registry.register(MyEvent, lambda: self._handler)
        self._subscriber_registry.register(MyEvent, lambda: self._other_handler)
        self._commandProcessor.publish(self._request)

        self.assertTrue(self._handler.called, "The first handler should be called with the message")
        self.assertTrue((self._other_handler, "The second handler should also be called with the message"))

    def test_missing_command_handler_registration(self):
        """Given that we are missing a handler for a command, when we send a command, it should throw an exception"""

        self._handler = MyCommandHandler()
        self._request = MyCommand()

        exception_thrown = False
        try:
            self._commandProcessor.send(self._request)
        except:
            exception_thrown = True

        self.assertTrue(exception_thrown, "Expected an exception to be thrown when no handler is registered for a command")

    def test_missing_event_handler_registration(self):
        """Given that we have no handlers register for an event, when we raise an event, it should not error """

        self._handler = MyEventHandler()
        self._other_handler = MyEventHandler()
        self._request = MyEvent()

        exception_thrown = False
        try:
            self._commandProcessor.publish(self._request)
        except:
            exception_thrown = True

        self.assertFalse(exception_thrown, "Did not expect an exception to be thrown where there are no handlers for an event")







