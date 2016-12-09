#!/usr/bin/env python
""""
File             : logging_and_monitoring_tests.py
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
import logging
from mock import call, patch

from core.command_processor import CommandProcessor, Registry

from tests.handlers_testdoubles import MyCommand, MyCommandHandler


class LoggingAndMonitoringFixture(unittest.TestCase):

    def setUp(self):
        self._subscriber_registry = Registry()
        self._commandProcessor = CommandProcessor(registry=self._subscriber_registry)

    def test_logging_a_handler(self):
        """s
        Given that I have a handler decorated for logging
        When I call that handler
        Then I should receive logs indicating the call and return of the handler
        * N.b. This is an example of using decorators to extend the Brightside pipeline
        """
        handler = MyCommandHandler()
        request = MyCommand()
        self._subscriber_registry.register(MyCommand, lambda: handler)
        logger = logging.getLogger("tests.handlers_testdoubles")
        with patch.object(logger, 'log') as mock_log:
            self._commandProcessor.send(request)

        mock_log.assert_has_calls([call(logging.DEBUG, "Entering handle " + str(request)),
                                   call(logging.DEBUG, "Exiting handle " + str(request))])
