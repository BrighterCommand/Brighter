-- Script Date: 03/07/2015 12:15  - ErikEJ.SqlCeScripting version 3.5.2.49
-- Database information:
-- Locale Identifier: 2057
-- Encryption Mode: 
-- Case Sensitive: False
-- Database: C:\Users\SUNDANCE\iancooper\Paramore\Brighter\Examples\Tasks\App_Data\CommandStore.sdf
-- ServerVersion: 4.0.8876.1
-- DatabaseSize: 64 KB
-- Created: 03/07/2015 12:10

-- User Table information:
-- Number of tables: 1
-- Commands: 0 row(s)

SELECT 1;
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;
CREATE TABLE [Commands] (
  [CommandId] uniqueidentifier NOT NULL
, [CommandType] nvarchar(256) NULL
, [CommandBody] ntext NULL
, [Timestamp] datetime NULL
, CONSTRAINT [PK_MessageId] PRIMARY KEY ([CommandId])
);
COMMIT;

