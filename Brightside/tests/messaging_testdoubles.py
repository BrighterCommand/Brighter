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

import uuid

from core.handler import Command
from core.messaging import BrightsideMessage, BrightsideMessageStore, BrightsideProducer


class FakeMessageStore(BrightsideMessageStore):
    def __init__(self):
        self._message_was_added = None
        self._messages = []  # type: List[BrightsideMessage]

    @property
    def message_was_added(self):
        return self._message_was_added

    def add(self, message: BrightsideMessage):
        self._messages.append(message)
        self._message_was_added = True

    def get_message(self, key: uuid) -> BrightsideMessage:
        for msg in self._messages:
            if msg.id == key:
                return msg
        return None


class FakeProducer(BrightsideProducer):
    def __init__(self):
        self._was_sent_message = False

    def send(self, message: BrightsideMessage):
        self._was_sent_message = True

    @property
    def was_sent_message(self):
        return self._was_sent_message


class TestMessage(Command):
    def __init__(self):
        self._integer_value = 99
        self._float_value = 3.14
        self._string_value = "fubar"
        self._bool_value = True
        super().__init__()

    @property
    def integer_value(self):
        return self._integer_value

    @property
    def float_value(self):
        return self._float_value

    @property
    def string_value(self):
        return self._string_value

    @property
    def bool_value(self):
        return self._bool_value