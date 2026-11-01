# Brighter Factories

This Factory folder was created to work with agents by providing documentation of how we implement Messaging Gateways (called Transports) and Boxes (Inboxes/Outboxes) in Brighter. Messaging Gateways and Boxes are narrow and deep modules. They are **narrow** because they have a low number of interfaces that Brighter requires implementors to fulfil; they are **deep** because they deal with the complicated sub-system, message-oriented middleware or database, that a user of Brighter has chosen to fulfil the role of Box or Transport.

Brighter does not enforce how a Box or Transport is implemented - this gives maxium freedom of movement to implementors to succeed in fitting the paradigms of that database or message-oriented middleware to Brighter's needs.

Brighter does require that we verify the module implement the required interfaces successfully. For this it maintains a suite of generated tests that we can use to ensure conformance with the behaviors required by Brighter. Implementers of a new Transport or Box MUST use these tests to ensure that their implementation meets the requirements of Brighter. Documentation on how to use these suites can be found [here](tests/README.md).

Brighter does have a standard pattern for implementing a Box or Transport. Whilst it is not required that you follow this, we recommend that you do, adjusting if necessary for the database or message-oriented middleware that you are implementing. This pattern is designed to ensure that your implementation is consistent with the rest of Brighter and to make it easier for others to understand and use your implementation. Documentation these pattern can be found in [transports](transports/transports.md) and [boxes]().
