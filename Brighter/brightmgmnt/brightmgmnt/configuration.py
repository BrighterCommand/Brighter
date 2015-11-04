"""
File         : configuration.py
Author           : ian
Created          : 02-16-2015

Last Modified By : ian
Last Modified On : 02-16-2015
***********************************************************************
The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

import configparser
from kombu import Exchange


def configure():
    config = configparser.ConfigParser(interpolation=None)
    config.read('cfg/brightmgmnt.ini')

    exchange_name = config['Broker']['exchangename']
    exchange_type = config['Broker']['exchangetype']
    exchange_durability = config.getboolean('Broker', 'durableexchange')

    exchange = Exchange(exchange_name, exchange_type, durable=exchange_durability)
    amqp_uri = config['Broker']['amqpuri']

    return exchange, amqp_uri


def parse_arguments(arguments):
    routing_key = arguments['<machineName>'] + "." + arguments['<serviceName>'] + "." + "configuration"
    command = 'stop' if arguments['stop'] == True else 'start'
    channel = arguments['--channelName'] if arguments['--channelName'] else None

    return routing_key, command, channel



