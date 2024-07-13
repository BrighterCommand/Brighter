# WebAPI Common Files

This folder contains common files for the WebAPI samples.

## DbMaker

Contains library code that enables the samples to build the Inbox, Outbox, and two domain databases: Greetings and Salutations. In your own code you do not need to put this in a separate assembly, but it is here to making switching the samples between Brighter's supported Outbox and Inbox types easy.

## Greetings_Migrations

Used by DbMaker to create the Greetings database.

## Salutations_Migrations

Used by DbMaker to create the Salutations database.

## TransportMaker

Contains library code that enables the sample code to switch between messaging transports. In your own projects you do not need to put this code in a separate assembly, but it is here to make it easy to switch the samples between Brighter's supported middleware transports.