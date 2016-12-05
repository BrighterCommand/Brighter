""""
File         : kombu_messaging.py
Author           : ian
Created          : 09-28-2016

Last Modified By : ian
Last Modified On : 09-28-2016
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
**********************************************************************i*
"""
import codecs
import json
import re
from typing import Dict
from uuid import UUID, uuid4

from kombu.message import Message as Message

from core.handler import Request
from core.messaging import BrightsideMessage, BrightsideMessageHeader, BrightsideMessageBody, BrightsideMessageType
from core.exceptions import MessagingException

message_type_header = "MessageType"
message_id_header = "MessageId"
message_correlation_id_header = "CorrelationId"
message_topic_name_header = "Topic"
message_handled_count_header = "HandledCount"
message_delay_milliseconds_header = "x-delay"
message_delayed_milliseconds_header = "x-delay"
message_original_message_id_header = "x-original-message-id"
message_delivery_tag_header = "DeliveryTag"

# See http://stackoverflow.com/questions/4020539/process-escape-sequences-in-a-string-in-python
ESCAPE_SEQUENCE_RE = re.compile(r'''
    ( \\U........      # 8-digit hex escapes
    | \\u....          # 4-digit hex escapes
    | \\x..            # 2-digit hex escapes
    | \\[0-7]{1,3}     # Octal escapes
    | \\N\{[^}]+\}     # Unicode characters by name
    | \\[\\'"abfnrtv]  # Single-character escapes
    )''', re.UNICODE | re.VERBOSE)


def decode_escapes(s):
    def decode_match(match):
        return codecs.decode(match.group(0), 'unicode-escape')

    return ESCAPE_SEQUENCE_RE.sub(decode_match, s)


class ReadError:
    def __init__(self, error_message: str) -> None:
        self.error_message = error_message

    def __str__(self) -> str:
        return self.error_message


class ArameMessageFactory:
    """
    The message factory turn an 'on-the-wire' message into our internal representation. We try to be as
    tolerant as possible (following Postel's Law: https://en.wikipedia.org/wiki/Robustness_principle) Be conservative
    in what you do, be liberal in what you accept
    """

    def __init__(self):
        self._has_read_errors = False

    def create_message(self, message: Message) -> BrightsideMessage:

        self._has_read_errors = False

        def _get_correlation_id() -> UUID:
            header, err = self._read_header(message_correlation_id_header, message)
            if err is None:
                return UUID(header)
            else:
                self._has_read_errors = True
                return ""

        def _get_message_id() -> UUID:
            header, err = self._read_header(message_id_header, message)
            if err is None:
                return UUID(header)
            else:
                self._has_read_errors = True
                return uuid4()

        def _get_message_type() -> BrightsideMessageType:
            header, err = self._read_header(message_type_header, message)
            if err is None:
                return BrightsideMessageType(header)
            else:
                self._has_read_errors = True
                return BrightsideMessageType.unacceptable

        def _get_payload() -> str:
            body, err = self._read_payload(message)
            if err is None:
                return body
            else:
                self._has_read_errors = True
                return ""

        def _get_payload_type() -> str:
            payload_type, err = self._read_payload_type(message)
            if err is None:
                return payload_type
            else:
                self._has_read_errors = True
                return ""

        def _get_topic() -> str:
            header, err = self._read_header(message_topic_name_header, message)
            if err is None:
                return header
            else:
                self._has_read_errors = True
                return ""

        message_id = _get_message_id()
        topic = _get_topic()
        message_type = _get_message_type() if not message.errors or self._has_read_errors else BrightsideMessageType.unacceptable
        correlation_id = _get_correlation_id()
        payload = _get_payload()
        payload_type = _get_payload_type()

        message_header = BrightsideMessageHeader(identity=message_id, topic=topic, message_type=message_type,
                                                 correlation_id=correlation_id, content_type="json")

        message_body = BrightsideMessageBody(body=payload, body_type=payload_type)

        return BrightsideMessage(message_header, message_body)

    # All of these methods are warned as static, implies they should be helper classes that take state in constructor

    def _read_header(self, header_key: str, message: Message) -> (str, ReadError):
        if header_key not in message.headers.keys():
            return "", ReadError("Could not read header with key: {}".format(header_key))
        else:
            return message.headers.get(header_key), None

    def _read_payload(self, message: Message) -> (str, ReadError):
        if not message.errors:
            body_text = decode_escapes(message.body)
            return body_text[1:-1], None
        else:
            errors = ", ".join(message.errors)
            return "", ReadError("Could not parse message. Errors: {}".format(errors))

    def _read_payload_type(self, message: Message) -> (str, ReadError):
        if not message.errors:
            return message.content_type, None
        else:
            errors = ", ".join(message.errors)
            return "", ReadError("Could not read payload type. Errors: {}".format(errors))


class KombuMessageFactory:
    def __init__(self, message: BrightsideMessage) -> None:
        self._message = message

    def create_message_header(self) -> Dict:

        def _add_correlation_id(brightstide_message_header: Dict, correlation_id: UUID) -> None:
            if correlation_id is not None:
                brightstide_message_header[message_correlation_id_header] = correlation_id

        def _add_message_id(brightside_message_header: Dict, identity: UUID) -> None:
            if identity is None:
                raise MessagingException("Missing id on message, this is a required field")
            brightside_message_header[message_id_header] = identity

        def _add_message_type(brightside_message_header: Dict, brightside_message_type: BrightsideMessageType) -> None:
            if brightside_message_type is None:
                raise MessagingException("Missing type on message, this is a required field")
            brightside_message_header[message_type_header] = brightside_message_type

        header = {}
        _add_message_id(header, self._message.header.id)
        _add_message_type(header, self._message.header.message_type.value)
        _add_correlation_id(header, self._message.header.correlation_id)

        return header


class JsonRequestSerializer:
    def __init__(self, request: Request=None, serialized_request: str=None):
        if request is None and serialized_request is None:
            raise MessagingException("You must provide either an object to serialize, or a dictionary of object properties and an object to hydrate")

        self._request = request
        self._serialized_request = serialized_request

    def serialize_to_json(self):
        def _serialize_instance(obj: object) -> Dict:
            d = {}
            d.update(vars(obj))
            for key, value in d.items():
                if isinstance(value, UUID): # json does not know how to serliaze a UUID, so convince it is a string instead
                    d[key] = str(value)
            return d

        if self._request is None:
            raise MessagingException("You must provide a request to serialize")
        return json.dumps(self._request, default=_serialize_instance)

    def deserialize_from_json(self):
        def _unserialize_instance(d: Dict) -> object:
            for key, value in d.items():
                if isinstance(value, str):  # We need to check if the string on the wire is actually a UUID, by conversion
                    try:
                        guid = UUID(value)
                        value = guid
                    except ValueError:
                        pass
                setattr(self._request, key, value)
            return self._request

        return json.loads(self._serialized_request, object_hook=_unserialize_instance)
