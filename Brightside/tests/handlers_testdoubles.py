"""
File         : my_handler.py
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

from core.handler import Handler, Command, Event
from poll import retry, circuitbreaker


class MyCommand(Command):
    pass


class MyCommandHandler(Handler):
    def __init__(self):
        self._called = False

    def handle(self, request):
        self._called = True

    @property
    def called(self):
        return self._called

    @called.setter
    def called(self, value):
        self._called = value


class MyEvent(Event):
    pass


class MyEventHandler(Handler):
    def __init__(self):
        self._called = False

    def handle(self, request):
        self._called = True

    @property
    def called(self):
        return self._called

    @called.setter
    def called(self, value):
        self._called = value

class MyHandlerSupportingRetry(Handler):
    def __init__(self):
        self._called = False
        self._callCount = 0

    @retry(RuntimeError, times=3, interval=1)
    def handle(self, request):
        self._callCount += 1
        if self._callCount <= 2:
            raise RuntimeError("Fake error to check for retry")
        else:
            self._called = True

    @property
    def called(self):
        return self._called

    @called.setter
    def called(self, value):
        self._called = value

    @property
    def callCount(self):
        return self._callCount

    @callCount.setter
    def callCount(self, value):
        self._callCount = value

class MyHandlerBreakingAfterRetry(Handler):
    def __init__(self):
        self._called = False
        self._callCount = 0

    @retry(RuntimeError, times=3, interval=1)
    def handle(self, request):
        self._callCount += 1
        if self._callCount <= 3:
            raise RuntimeError("Fake error to check for retry")
        else:
            #We should not get here, as we will run out of retries
            self._called = True

    @property
    def called(self):
        return self._called

    @called.setter
    def called(self, value):
        self._called = value

    @property
    def callCount(self):
        return self._callCount

    @callCount.setter
    def callCount(self, value):
        self._callCount = value


class MyHandlerBreakingCircuitAfterThreeFailures(Handler):
    def __init__(self):
        self._called = False
        self._callCount = 0

    @retry(RuntimeError)
    @circuitbreaker(RuntimeError, 3, 60)
    def handle(self, request):
        self._callCount += 1
        if self._callCount <= 3:
            raise RuntimeError("Fake error to check for circuit broken")
        else:
            #We should not get here, as we will run out of retries
            self._called = True

    @property
    def called(self):
        return self._called

    @called.setter
    def called(self, value):
        self._called = value

    @property
    def callCount(self):
        return self._callCount

    @callCount.setter
    def callCount(self, value):
        self._callCount = value
