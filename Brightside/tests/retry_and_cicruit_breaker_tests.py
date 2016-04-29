#!/usr/bin/env python
""""
File         : retry_and_cicruit_breaker_tests.py
Author           : ian
Created          : 04-26-2016

Last Modified By : ian
Last Modified On : 04-26-2016
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
from tests.handlers_testdoubles import MyHandlerSupportingRetry, MyCommand
from core.command_processor import CommandProcessor
from core.registry import Registry

class PipelineTests(unittest.TestCase):

    """Tests of decorators on handlers.
        It is worth noting that whereas Brighter has to create a mechanism to allow decorators on the target handler, Python
        already has a decorator syntax. As this maps well to the attributes used by Brighter to signal the decorators
        used to meet orthogonal concerns it is natural to just use Python's own decorator syntax in Brightside, over
        rolling up a bespoke mechanism. Indeed, the use of attributes and decorator classes in Brighter mirrors functionality avaailable
        to Pythoh via its decorator syntax
        This test suite is really about proving we can provide equivalent functionality
        We use Benjamin Hodgoson's Poll library, which is itself inspired by Polly, the library used by Brighter
    """

    def setUp(self):
        self._subscriber_registry = Registry()
        self._commandProcessor = CommandProcessor(self._subscriber_registry)

    def test_handle_retry_on_command(self):
        """ given that we have a retry plocy for a command, when we raise an exception, then we should retry n times or until succeeds """
        self._handler = MyHandlerSupportingRetry()
        self._request = MyCommand()
        self._subscriber_registry.register(MyCommand, lambda: self._handler)
        self._commandProcessor.send(self._request)

        self.assertTrue(self._handler.called, "Expected the handle method on the handler to be called with the message")
        self.assertTrue(self._handler.callCount == 3, "Expected two retries of the pipeline")

    def test_exceed_retry_on_command(self):
        """ given that we have a retry policy for a command, when we raise an exception, then we should bubble the exception out after n retries"""
        pass

    def test_handle_circuit_breaker_on_command(selfself):
        """ given that we have a handler registered for a command         """
        pass
