# Brighter Factories

This Factory folder was created to work with agents by providing documentation of how we implement Messaging Gateways (called Transports) and Boxes (Inboxes/Outboxes) in Brighter. Messaging Gateways and Boxes are narrow and deep modules. They are **narrow** because they have a low number of interfaces that Brighter requires implementors to fulfil; they are **deep** because they deal with the complicated sub-system, message-oriented middleware or database, that a user of Brighter has chosen to fulfil the role of Box or Transport.

Brighter does not enforce how a Box or Transport is implemented - this gives maxium freedom of movement to implementors to succeed in fitting the paradigms of that database or message-oriented middleware to Brighter's needs.

Brighter does require that we verify the module implement the required interfaces successfully. For this it maintains a suite of generated tests that we can use 



allowing for the creation of skills to add new ones, based on gateway or Db documentation.
