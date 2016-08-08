#!/usr/bin/env python
""""
File             : messaging.py
Author           : ian
Created          : 07-08-2016

Last Modified By : ian
Last Modified On : 07-08-2016
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

from uuid import UUID
from abc import ABCMeta, abstractmethod


class MessageBody:
    """The body of our message. Note that this must use the same binary payload approach as Paramore Brighter to
        ensure that payload is binary compatible. plain/text should be encoded as a UTF8 byte array for example
    """
    pass


class MessageHeader:
    """The header for our message. Note that this should agree with the Paramore.Brighter definition to ensure that
        different language implementations are compatible
    """
    def __init__(self, identity: UUID) -> None:
        self._id = identity  # type: UUID

    @property
    def id (self):
        return self._id


class Message:
    def __init__(self, message_header: MessageHeader, message_body: MessageBody) -> None:
        self._message_header = message_header
        self._message_body = message_body

    @property
    def header(self) -> MessageHeader:
        return self._message_header

    @property
    def body(self) -> MessageBody:
        return self._message_body

    @property
    def id(self) -> UUID:
        return self._message_header.id


class MessageStore(metaclass=ABCMeta):
    @abstractmethod
    def add(self, message: Message):
        pass

    @abstractmethod
    def get_message(self, key: UUID):
        pass


class Producer(metaclass=ABCMeta):
    @abstractmethod
    def send(self, message: Message):
        pass

