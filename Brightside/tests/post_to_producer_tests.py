#!/usr/bin/env python
""""
File         : post_to_producer_tests.py
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
from core.registry import MessageMapperRegistry
from tests.messaging_testdoubles import FakeMessageStore, FakeProducer
from core.command_processor import CommandProcessor
from tests.handlers_testdoubles import MyCommand


class PostTests(unittest.TestCase):
    def setUp(self):
        self._messageMapperRegistry = MessageMapperRegistry()
        self._message_store = FakeMessageStore()
        self._producer = FakeProducer()
        self._commandProcessor = CommandProcessor(message_mapper_registry=self._messageMapperRegistry, message_store=self._message_store, producer=self._producer)

    def test_handle_command(self):
        """ given that we have a message mapper and producer registered for a commnd, when we post a command, it should send via the producer"""
        self._request = MyCommand()
        self._commandProcessor.post(self._request)

        self.assertTrue(self._message_store.message_was_added, "Expected a message to be added")
        self.assertTrue(self._message_store.get_message(self._request.id), "Exepcted the command to be converted into a message")
        self.assertTrue(self._producer.was_sent_message, "Expected a message to be sent via the producer")







