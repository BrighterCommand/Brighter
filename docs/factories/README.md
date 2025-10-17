# Brighter Factories

Brighter is a CQRS and Messaging Framework for .NET. It is designed to allow message passing both internally, using a [Command Oriented Interface](https://martinfowler.com/bliki/CommandOrientedInterface.html), and externally using a range of messaging middleware. We refer to the middleware colloquially as a transport.

This Factory folder was originally created to work with GenAI agents by creating descriptions of how we create new 
Messaging Gateways (called Transports) in Brighter.

## Factory Documents

### Transports

[specs/transports](./transports/transports.md) common information for developing any transport.