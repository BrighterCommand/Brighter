Brightside
===========================
Brightside is a command dispatcher and processor for Python. It also provides a work queue for asynchronous dispatch over a broker such as RabbitMQ.
It is intended to work with Brighter, a C# command dispatcher and processor and allow polyglot solutions i.e. a Flask endpoint that sends work over a queue to a C# consumer or an ASP.NET WebAPI endpoint that sends a request over a work queue to a Python application.
Brightside can also be used stand-alone as an opinionated alternative to libraries like Celery, which use RPC and not messaging.

