"""
File             : channels.py
Author           : ian
Created          : 11-21-2016

Last Modified By : ian
Last Modified On : 11-21-2016
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
from enum import Enum
from queue import Queue

from core.messaging import BrightsideConsumer, BrightsideMessage, BrightsideMessageFactory


class ChannelState(Enum):
    initialized = 0
    started = 1
    stopping = 2


class ChannelName:
    def __init__(self, name: str) -> None:
        self._name = name

    @property
    def value(self) -> str:
        return self._name

    def __str__(self) -> str:
        return self._name


class Channel:
    def __init__(self, name: str, consumer: BrightsideConsumer) -> None:
        self._consumer = consumer
        self._name = ChannelName(name)
        self._queue = Queue()
        self._state = ChannelState.initialized

    def acknowledge(self, message: BrightsideMessage):
        self._consumer.acknowledge(message)

    @property
    def length(self) -> int:
        return self._queue.qsize()

    def name(self) -> ChannelName:
        return self._name

    def receive(self, timeout: int) -> BrightsideMessage:
        if self._state is ChannelState.initialized:
            self._state = ChannelState.started

        if not self._queue.empty():
            return self._queue.get(block=True, timeout=timeout)

        return self._consumer.receive(timeout=timeout)

    @property
    def state(self) -> ChannelState:
        return self._state

    def stop(self):
        self._queue.put(BrightsideMessageFactory.create_quit_message())
        self._state = ChannelState.stopping





