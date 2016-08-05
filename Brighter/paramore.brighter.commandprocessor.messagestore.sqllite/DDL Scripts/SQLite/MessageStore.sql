-- Script Date: 03/07/2015 12:03  - ErikEJ.SqlCeScripting version 3.5.2.49
-- Database information:
-- Locale Identifier: 1033
-- Encryption Mode: 
-- Case Sensitive: False
-- ServerVersion: 4.0.8876.1
-- DatabaseSize: 84 KB
-- Created: 30/06/2015 23:53

-- User Table information:
-- Number of tables: 1
-- Messages: 0 row(s)

SELECT 1;
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;
CREATE TABLE [Messages] (
  [MessageId] uniqueidentifier NOT NULL
, [Topic] nvarchar(255) NULL
, [MessageType] nvarchar(32) NULL
, [Timestamp] datetime NULL
, [HeaderBag] ntext NULL
, [Body] ntext NULL
, CONSTRAINT [PK_MessageId] PRIMARY KEY ([MessageId])
);
COMMIT;

