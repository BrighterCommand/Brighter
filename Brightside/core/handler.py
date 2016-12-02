""""
File         : handler.py
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
from uuid import UUID, uuid4
from abc import ABCMeta, abstractmethod


class Request(metaclass=ABCMeta):
    """Someting we wish to route over a Command Processor"""
    def __init__(self) -> None:
        self._id = uuid4()

    @property
    def id(self) -> UUID:
        return self._id

    @staticmethod
    @abstractmethod
    def is_command() -> bool:
        return False

    @staticmethod
    @abstractmethod
    def is_event() -> bool:
        return False

    def __str__(self):
        """override in subclasses to provide details of parameters"""
        return "Request: id: %s, is_command: %s is_event: %s" % (self.id, self.is_command(), self.is_event())


class Command(Request):
    """ A command is a task to be done, it has affinity with a transaction, it encapsulates the arguments of the call
        to a handler
    """
    def __init__(self) -> None:
        super().__init__()

    @staticmethod
    def is_command() -> bool:
        return True

    @staticmethod
    def is_event() -> bool:
        return False


class Event(Request):
    """ An event is a notification that something ha happened, it has affinity with a transaction, it encapsulates the
    call to a handler
    """
    def __init__(self) -> None:
        super().__init__()

    @staticmethod
    def is_command() -> bool:
        return False

    @staticmethod
    def is_event() ->bool:
        return True


class Handler(metaclass=ABCMeta):
    """ Receives a message from the command dispatcher, and processes it. Forms part of a pipeline of handlers
        A handler calls handlers that succeed it through the base class method
    """

    @abstractmethod
    def handle(self, request: Request) -> Request:
        pass





