"""
File             : command_processor_tests.py
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
        handler = MyCommandHandler()
        request = MyCommand()
        self._subscriber_registry.register(MyCommand, lambda: handler)
        self._commandProcessor.send(request)

        self.assertTrue(handler.called, "Expected the handle method on the handler to be called with the message")

    def test_handle_event(self):
        """ Given that we have many handlers registered of an event, when we raise an event, it should call all the handlers"""

        handler = MyEventHandler()
        other_handler = MyEventHandler()
        request = MyEvent()
        self._subscriber_registry.register(MyEvent, lambda: handler)
        self._subscriber_registry.register(MyEvent, lambda: other_handler)
        self._commandProcessor.publish(request)

        self.assertTrue(handler.called, "The first handler should be called with the message")
        self.assertTrue((other_handler, "The second handler should also be called with the message"))

    def test_missing_command_handler_registration(self):
        """Given that we are missing a handler for a command, when we send a command, it should throw an exception"""

        handler = MyCommandHandler()
        request = MyCommand()

        exception_thrown = False
        try:
            self._commandProcessor.send(request)
        except:
            exception_thrown = True

        self.assertTrue(exception_thrown, "Expected an exception to be thrown when no handler is registered for a command")

    def test_missing_event_handler_registration(self):
        """Given that we have no handlers register for an event, when we raise an event, it should not error """

        handler = MyEventHandler()
        other_handler = MyEventHandler()
        request = MyEvent()

        exception_thrown = False
        try:
            self._commandProcessor.publish(request)
        except:
            exception_thrown = True

        self.assertFalse(exception_thrown, "Did not expect an exception to be thrown where there are no handlers for an event")







