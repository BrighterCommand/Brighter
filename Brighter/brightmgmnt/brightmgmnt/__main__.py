"""
File         : __main__.py
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

Usage:
  brightermgmnt.py stop <machineName> <serviceName>
  brightermgmnt.py start <machineName> <serviceName>

Options:
  -h --help     Show this screen.
"""
from .messaging import build_message
from docopt import docopt
from .controlbus import send
from .configuration import configure


def run(amqp_uri, exchange, cmdlne_arguments):
    routing_key = cmdlne_arguments['<machineName>'] + "." + cmdlne_arguments['<serviceName>'] + "." + "configuration"
    send(amqp_uri, exchange, build_message("stop"), routing_key)

if __name__ == '__main__':
    arguments = docopt(__doc__, version='Brighter Management v0.0')
    exchange, amqp_uri = configure()
    run(amqp_uri, exchange, arguments)







